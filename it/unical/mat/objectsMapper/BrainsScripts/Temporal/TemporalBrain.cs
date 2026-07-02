using System;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System.Collections;
using ThinkEngine.it.unical.mat.objectsMapper.BrainsScripts;
using UnityEngine.Events;

namespace ThinkEngine
{
    [System.Serializable]
    public class ActionMapping
    {
        public string actionName;
        public UnityEvent onTrue;
        public UnityEvent onFalse;
    }

    [ExecuteAlways]
    public class TemporalBrain : Brain
    {
        public string behaviourName;
        public string aspTemplate;
        public List<ActionMapping> actionMappings = new List<ActionMapping>();
        
        private Queue<System.Action> mainThreadActions = new Queue<System.Action>();
        private readonly object queueLock = new object();

        protected override HashSet<string> SupportedFileExtensions
        {
            get
            {
                return new HashSet<string> { "asp" };
            }
        }

        protected override string SpecificFileParts()
        {
            string comments = "\n% --- LTLF Sensors ---\n";
            comments += "% Output format: ltlf_sensor(\"sensor_name\").\n";
            comments += "% Self index fact: brain(Index).\n";
            comments += "% Expected by '" + behaviourName + "':\n";
            
            comments += ThinkEngine.LTLF.LTLFControllerHandler.GetHelp(behaviourName);
            
            comments += "% ------------------------\n";
            return comments; 
        }
        
        internal override string ActualSensorEncoding(string sensorsAsASP)
        {
            return sensorsAsASP;
        }
        
        protected override void Start()
        {
            if (string.IsNullOrEmpty(behaviourName))
            {
                Debug.LogError($"TemporalBrain on {gameObject.name} is disabled because Behaviour Name is empty.");
                enableBrain = false;
            }
            else if (Application.isPlaying)
            {
                string automataPath = System.IO.Path.Combine(Utility.ThinkEngineBaseFolder, "Temporal", "Automata", behaviourName + ".ltlf");
                if (!System.IO.File.Exists(automataPath))
                {
                    Debug.LogWarning($"LTLf Automata for '{behaviourName}' not found at {automataPath}. Attempting to build from Config...");
                    string message;
                    bool success = ThinkEngine.LTLF.LTLFControllerHandler.Generate(behaviourName, out message);
                    if (!success)
                    {
                        Debug.LogError($"Failed to auto-build LTLf Automata for '{behaviourName}': {message}");
                    }
                    else
                    {
                        Debug.Log($"Successfully auto-built LTLf Automata for '{behaviourName}'.");
                    }
                }
            }
            base.Start();
        }

        internal override void GenerateFile()
        {
            string templateName = string.IsNullOrEmpty(aspTemplate) ? behaviourName : aspTemplate;
            if (string.IsNullOrEmpty(templateName))
            {
                templateName = gameObject.name + "TemporalTemplate";
            }
            
            string temporalFolder = System.IO.Path.Combine(Utility.ThinkEngineBaseFolder, "Temporal");
            string aspFolder = System.IO.Path.Combine(temporalFolder, "ASP");
            
            if (!System.IO.Directory.Exists(aspFolder))
            {
                System.IO.Directory.CreateDirectory(aspFolder);
            }
            
            string aiFileTemplatePath = System.IO.Path.Combine(aspFolder, templateName + ".asp");

            using (System.IO.StreamWriter fs = new System.IO.StreamWriter(aiFileTemplatePath))
            {
                fs.Write("%For runtime instantiated GameObject, only the prefab mapping is provided. Use that one substituting the gameobject name accordingly.\n %Sensors.\n");
                HashSet<string> seenSensorConfNames = new HashSet<string>();

                foreach (SensorConfiguration sensorConf in Utility.SensorsManager.GetConfigurations(ChosenSensorConfigurations))
                {
                    if (seenSensorConfNames.Contains(sensorConf.ConfigurationName))
                    {
                        continue;
                    }
                    seenSensorConfNames.Add(sensorConf.ConfigurationName);
                    foreach (PropertyFeatures features in sensorConf.PropertyFeaturesList)
                    {
                        string sensorsAsASP = MapperManager.GetASPTemplate(features.PropertyAlias, sensorConf.gameObject, features.property, true);
                        fs.Write(ActualSensorEncoding(sensorsAsASP));
                    }
                }
                fs.Write(SpecificFileParts());
            }
        }

        protected override void Update()
        {
            base.Update();
            
            lock (queueLock)
            {
                while (mainThreadActions.Count > 0)
                {
                    mainThreadActions.Dequeue().Invoke();
                }
            }
        }

        protected override IEnumerator Init()
        {
            yield return StartCoroutine(base.Init());
            executor = new TemporalExecutor(this);
            string GOname = gameObject.name;
            executorName = "executor " + GOname;
            executionThread = new Thread(() =>
            {
                Thread.CurrentThread.Name = executorName;
                Thread.CurrentThread.IsBackground = true;
                executor.Run();
            });
            executionThread.Start();
        }

        internal void NotifyAction(string actionName, bool value)
        {
            lock(queueLock)
            {
                mainThreadActions.Enqueue(() => {
                    foreach (var mapping in actionMappings)
                    {
                        if (mapping.actionName == actionName)
                        {
                            if (value)
                            {
                                mapping.onTrue?.Invoke();
                            }
                            else
                            {
                                mapping.onFalse?.Invoke();
                            }
                        }
                    }
                });
            }
        }
        
        void OnDestroy()
        {
            if (executor != null)
            {
                executor.reason = false;
            }
        }
    }
}
