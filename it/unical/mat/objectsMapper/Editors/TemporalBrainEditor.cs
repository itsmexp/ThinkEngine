#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.IO;

namespace ThinkEngine.Editors
{
    [CustomEditor(typeof(TemporalBrain))]
    public class TemporalBrainEditor : BrainEditor
    {
        private TemporalBrain myScript;
        
        protected override Brain Target
        {
            get
            {
                return myScript;
            }
        }
        
        protected override void OnEnable()
        {
            myScript = target as TemporalBrain;
            base.OnEnable();
            if (!ExcludedProperties.Contains("behaviourName")) ExcludedProperties.Add("behaviourName");
            if (!ExcludedProperties.Contains("aspTemplate")) ExcludedProperties.Add("aspTemplate");
            if (!ExcludedProperties.Contains("actionMappings")) ExcludedProperties.Add("actionMappings");
            if (!ExcludedProperties.Contains("debug")) ExcludedProperties.Add("debug");
            if (!ExcludedProperties.Contains("useInterval")) ExcludedProperties.Add("useInterval");
            if (!ExcludedProperties.Contains("executionInterval")) ExcludedProperties.Add("executionInterval");
            if (!ExcludedProperties.Contains("availableActions")) ExcludedProperties.Add("availableActions");
        }
        
        private void LoadBehavior()
        {
            if (myScript == null || string.IsNullOrEmpty(myScript.behaviourName))
            {
                EditorUtility.DisplayDialog("Error", "Please specify a Behaviour Name.", "OK");
                return;
            }

            string behaviourName = myScript.behaviourName;
            string temporalFolder = Path.Combine(Utility.StreamingAssetsContent, "Temporal");
            string configPath = Path.Combine(temporalFolder, "Config", behaviourName + ".txt");
            string automataPath = Path.Combine(temporalFolder, "Automata", behaviourName + ".ltlf");

            if (File.Exists(automataPath))
            {
                myScript.availableActions = ThinkEngine.LTLF.LTLFControllerHandler.GetActions(behaviourName);
                EditorUtility.SetDirty(myScript);
                serializedObject.Update();
                EditorUtility.DisplayDialog("Success", "Behavior loaded successfully.", "OK");
            }
            else if (File.Exists(configPath))
            {
                EditorUtility.DisplayDialog("Warning", "L'automa LTLf sottostante non è stato ancora generato. Clicca su 'Generate LTLf Automata'.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "La behaviour specificata non esiste.", "OK");
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            GUI.enabled = false;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            GUI.enabled = true;

            EditorGUILayout.PropertyField(serializedObject.FindProperty("enableBrain"));

            EditorGUILayout.Space();
            
            SerializedProperty debugProp = serializedObject.FindProperty("debug");
            SerializedProperty behaviourNameProp = serializedObject.FindProperty("behaviourName");
            SerializedProperty aspTemplateProp = serializedObject.FindProperty("aspTemplate");

            EditorGUILayout.PropertyField(debugProp);
            EditorGUILayout.PropertyField(behaviourNameProp, new GUIContent("Behaviour Name"));
            EditorGUILayout.PropertyField(aspTemplateProp, new GUIContent("ASP Template"));
            
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Load Behavior", GUILayout.Width(150), GUILayout.Height(25)))
            {
                LoadBehavior();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField("Temporal Trigger Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("useInterval"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("executionInterval"));
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Action Mappings", EditorStyles.boldLabel);
            
            var actions = new List<string>(myScript.availableActions);
            if (actions.Count == 0) actions.Add("N/A (Load behavior)");

            SerializedProperty mappingsProp = serializedObject.FindProperty("actionMappings");
            
            for (int i = 0; i < mappingsProp.arraySize; i++)
            {
                SerializedProperty mappingProp = mappingsProp.GetArrayElementAtIndex(i);
                SerializedProperty actionNameProp = mappingProp.FindPropertyRelative("actionName");
                SerializedProperty onTrueProp = mappingProp.FindPropertyRelative("onTrue");
                SerializedProperty onFalseProp = mappingProp.FindPropertyRelative("onFalse");

                EditorGUILayout.BeginVertical("box");
                
                int selectedIndex = Mathf.Max(0, actions.IndexOf(actionNameProp.stringValue));
                selectedIndex = EditorGUILayout.Popup("Action", selectedIndex, actions.ToArray());
                if (actions.Count > 0 && selectedIndex >= 0 && selectedIndex < actions.Count && actions[selectedIndex] != "N/A (Load behavior)")
                {
                    actionNameProp.stringValue = actions[selectedIndex];
                }

                EditorGUILayout.PropertyField(onTrueProp);
                EditorGUILayout.PropertyField(onFalseProp);
                
                if (GUILayout.Button("Remove Mapping"))
                {
                    mappingsProp.DeleteArrayElementAtIndex(i);
                    i--;
                }
                EditorGUILayout.EndVertical();
            }
            if (GUILayout.Button("Add Action Mapping"))
            {
                mappingsProp.arraySize++;
            }

            EditorGUILayout.Space();
            
            EditorGUILayout.BeginVertical("box");
            IfConfigurationChanged();
            base.ListAvailableConfigurations();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Generate LTLf Automata"))
            {
                BuildLTLFController();
            }
            if (GUILayout.Button("Show in Explorer"))
            {
                string automataDir = Path.Combine(Utility.StreamingAssetsContent, "Temporal", "Automata");
                if (!Directory.Exists(automataDir)) Directory.CreateDirectory(automataDir);
                EditorUtility.OpenWithDefaultApp(automataDir);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Generate ASP Template"))
            {
                myScript.GenerateFile();
            }
            if (GUILayout.Button("Show in Explorer"))
            {
                string aspDir = Path.Combine(Utility.StreamingAssetsContent, "Temporal", "ASP");
                if (!Directory.Exists(aspDir)) Directory.CreateDirectory(aspDir);
                EditorUtility.OpenWithDefaultApp(aspDir);
            }
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
            SavingInBrain();

            if (GUI.changed)
            {
                EditorUtility.SetDirty(Target);
            }
        }
        
        private void BuildLTLFController()
        {
            if (string.IsNullOrEmpty(myScript.behaviourName))
            {
                EditorUtility.DisplayDialog("Error", "Please specify a Behaviour Name before building.", "OK");
                return;
            }
            
            string message;
            bool success = ThinkEngine.LTLF.LTLFControllerHandler.Generate(myScript.behaviourName, out message);
            
            if (success)
            {
                EditorUtility.DisplayDialog("Success", message, "OK");
                AssetDatabase.Refresh();
            }
            else
            {
                EditorUtility.DisplayDialog("Error", message, "OK");
            }
        }
    }
}
#endif
