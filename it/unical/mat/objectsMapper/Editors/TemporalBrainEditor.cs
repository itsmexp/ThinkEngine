#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

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
        }
        
        private bool showAdvanced = false;
        private List<string> cachedActions = new List<string>();

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            
            myScript.debug = EditorGUILayout.Toggle("Debug", myScript.debug);
            myScript.behaviourName = EditorGUILayout.TextField("Behaviour Name", myScript.behaviourName);
            myScript.aspTemplate = EditorGUILayout.TextField("ASP Template", myScript.aspTemplate);
            
            EditorGUILayout.Space();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Action Mappings", EditorStyles.boldLabel);
            if (GUILayout.Button("Load Actions", GUILayout.Width(120)))
            {
                cachedActions = string.IsNullOrEmpty(myScript.behaviourName) ? new List<string>() : ThinkEngine.LTLF.LTLFControllerHandler.GetActions(myScript.behaviourName);
            }
            EditorGUILayout.EndHorizontal();
            
            var actions = new List<string>(cachedActions);
            if (actions.Count == 0) actions.Add("N/A (Build or set behaviour)");

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
                if (actions.Count > 0 && selectedIndex >= 0 && selectedIndex < actions.Count && actions[selectedIndex] != "N/A (Build or set behaviour)")
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
                string automataDir = Path.Combine(Utility.ThinkEngineBaseFolder, "Temporal", "Automata");
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
                string aspDir = Path.Combine(Utility.ThinkEngineBaseFolder, "Temporal", "ASP");
                if (!Directory.Exists(aspDir)) Directory.CreateDirectory(aspDir);
                EditorUtility.OpenWithDefaultApp(aspDir);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Advanced Brain Settings", true, EditorStyles.foldoutHeader);
            if (showAdvanced)
            {
                EditorGUILayout.BeginVertical("box");
                
                DrawPropertiesExcluding(serializedObject, ExcludedProperties.ToArray());
                
                EditorGUILayout.BeginHorizontal();
                Target.maintainInputFile = EditorGUILayout.Toggle("Maintain input file", Target.maintainInputFile);
                if (GUILayout.Button("Open input folder"))
                {
                    EditorUtility.OpenWithDefaultApp(Path.Combine(Path.GetTempPath(),"ThinkEngineFacts"));
                }
                EditorGUILayout.EndHorizontal();

                ChooseReasonerTriggerMethod();
                
                EditorGUILayout.EndVertical();
            }

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
