using System;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System.Collections;
using ThinkEngine.it.unical.mat.objectsMapper.BrainsScripts;
using UnityEngine.Events;

namespace ThinkEngine
{
    /// <summary>
    /// Represents a mapping between an LTLf action name and corresponding Unity Events.
    /// </summary>
    [System.Serializable]
    public class ActionMapping
    {
        [Tooltip("The name of the LTLf action.")]
        public string actionName;
        
        [Tooltip("Event triggered when the action is evaluated to true.")]
        public UnityEvent onTrue;
        
        [Tooltip("Event triggered when the action is evaluated to false.")]
        public UnityEvent onFalse;
    }

    /// <summary>
    /// Specialized Brain component that reasons over temporal specifications (LTLf).
    /// </summary>
    [ExecuteAlways]
    public class TemporalBrain : Brain
    {
        [Tooltip("The name of the LTLf behavior/automaton config.")]
        public string behaviourName;
        
        [Tooltip("Optional custom ASP template name.")]
        public string aspTemplate;
        
        [Tooltip("Mappings from LTLf action outputs to Unity events.")]
        public List<ActionMapping> actionMappings = new List<ActionMapping>();
        
        [Header("Temporal Trigger Settings")]
        [Tooltip("If enabled, triggers the reasoning cycle periodically.")]
        public bool useInterval = false;
        
        [Tooltip("Interval (in seconds) between periodic reasoning cycles.")]
        public float executionInterval = 1f;
        
        /// <summary>
        /// Struct to hold action notification parameters, avoiding delegate allocation on the heap.
        /// </summary>
        private struct ActionNotification
        {
            public string actionName;
            public bool value;
        }
        
        // Queue to dispatch action triggers from the background thread to the Unity main thread.
        private Queue<ActionNotification> mainThreadActions = new Queue<ActionNotification>();
        private readonly object queueLock = new object();

        [HideInInspector]
        public List<string> availableActions = new List<string>();

        protected override HashSet<string> SupportedFileExtensions
        {
            get
            {
                return new HashSet<string> { "asp" };
            }
        }

        protected override string SpecificFileParts()
        {
            return "";
        }

        internal override string ActualSensorEncoding(string sensorsAsASP)
        {
            return sensorsAsASP;
        }
        
        protected override void Start()
        {
            // Configure AIFilesPath to point to the Temporal/ASP directory
            AIFilesPath = System.IO.Path.Combine(Utility.ThinkEngineBaseFolder, "Temporal", "ASP");
            
            // Automatically initialize and populate AIFilesPrefix with behaviourName
            if (ChosenSensorConfigurations == null)
            {
                // Simple check just to be sure we have everything initialized
            }
            if (_chosenSensorConfigurations == null)
            {
                _chosenSensorConfigurations = new List<string>();
            }
            if (AIFilesPrefix == null)
            {
                AIFilesPrefix = new List<string>();
            }
            if (!string.IsNullOrEmpty(behaviourName) && !AIFilesPrefix.Contains(behaviourName))
            {
                AIFilesPrefix.Add(behaviourName);
            }

            if (string.IsNullOrEmpty(behaviourName))
            {
                Debug.LogError($"TemporalBrain on {gameObject.name} is disabled because Behaviour Name is empty.");
                enableBrain = false;
            }
            else if (Application.isPlaying)
            {
                // Verify if the LTLf automaton file exists, if not, compile it.
                string automataPath = System.IO.Path.Combine(Utility.ThinkEngineBaseFolder, "Temporal", "Automata", behaviourName + ".ltlf");
                if (!System.IO.File.Exists(automataPath))
                {
                    Debug.LogWarning($"LTLf Automata for '{behaviourName}' not found at {automataPath}. Attempting to build from Config...");
                    string message;
                    bool success = ThinkEngine.LTLF.LTLFControllerHandler.Generate(behaviourName, out message);
                    if (!success)
                    {
                        Debug.LogError($"Failed to auto-build LTLf Automata for '{behaviourName}': {message}");
                        enableBrain = false;
                    }
                    else
                    {
                        Debug.Log($"Successfully auto-built LTLf Automata for '{behaviourName}'.");
                    }
                }
            }
            base.Start();
        }

        /// <summary>
        /// Generates the initial ASP template mapping file containing sensor schemas formatted for LTLf mapping.
        /// </summary>
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
                fs.WriteLine("% ASP-LTLf Mapper");
                fs.WriteLine("%");
                fs.WriteLine("% INPUT:");
                fs.WriteLine("% Sensor Fact:");
                
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
                        fs.Write("% " + ActualSensorEncoding(sensorsAsASP));
                    }
                }

                fs.WriteLine("%");
                fs.WriteLine("% Self Index Fact: ");
                fs.WriteLine("% currentBrainID(ID).");
                fs.WriteLine("%");
                fs.WriteLine("% OUTPUT:");
                fs.WriteLine("% ltlf_sensor(Output Variable)");
                fs.WriteLine("%");
                fs.WriteLine("% Output variable:");
                
                string helpString = ThinkEngine.LTLF.LTLFControllerHandler.GetHelp(behaviourName);
                if (!string.IsNullOrEmpty(helpString))
                {
                    string cleanedVars = helpString.Replace("% Sensor Variables:", "").Trim();
                    fs.WriteLine("% " + cleanedVars + ".");
                }
                else
                {
                    fs.WriteLine("% N/A");
                }
            }
        }

        /// <summary>
        /// Dispatches actions queued by the reasoning thread on Unity's main thread.
        /// </summary>
        protected override void Update()
        {
            base.Update();
            
            lock (queueLock)
            {
                while (mainThreadActions.Count > 0)
                {
                    var notification = mainThreadActions.Dequeue();
                    foreach (var mapping in actionMappings)
                    {
                        if (mapping.actionName == notification.actionName)
                        {
                            if (debug)
                            {
                                Debug.Log($"{executorName} - Dispatching event for action '{notification.actionName}' = {notification.value}");
                            }
                            if (notification.value)
                            {
                                mapping.onTrue?.Invoke();
                            }
                            else
                            {
                                mapping.onFalse?.Invoke();
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Initializes the temporal executor thread and configures trigger mechanisms.
        /// </summary>
        protected override IEnumerator Init()
        {
            // Temporarily set trigger to wait state to prevent base.Init from launching automatic triggers.
            string originalExecuteReasonerOn = ExecuteReasonerOn;
            ExecuteReasonerOn = "When Sensors are ready";
            
            yield return StartCoroutine(base.Init());
            
            ExecuteReasonerOn = originalExecuteReasonerOn;
            
            // Set dummy MethodInfo so the executor blocks on Monitor.Wait instead of looping continuously.
            Func<bool> dummyDelegate = DummyTriggerMethod;
            reasonerMethod = dummyDelegate.Method;
            
            executor = new TemporalExecutor(this);
            string GOname = gameObject.name;
            executorName = "executor " + GOname;
            
            // Spin off background reasoning thread.
            executionThread = new Thread(() =>
            {
                Thread.CurrentThread.Name = executorName;
                Thread.CurrentThread.IsBackground = true;
                executor.Run();
            });
            executionThread.Start();
            
            if (useInterval && Application.isPlaying)
            {
                StartCoroutine(TimerTriggerCoroutine());
            }
        }

        /// <summary>
        /// Fallback dummy method to satisfy the reasoning block conditions.
        /// </summary>
        private bool DummyTriggerMethod()
        {
            return false;
        }

        /// <summary>
        /// Periodically calls TriggerExecution if interval mode is enabled.
        /// </summary>
        private IEnumerator TimerTriggerCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(executionInterval);
                if (useInterval)
                {
                    TriggerExecution();
                }
            }
        }

        /// <summary>
        /// Triggers a single execution cycle of the reasoning and action workflow.
        /// </summary>
        public void TriggerExecution()
        {
            if (enableBrain && Application.isPlaying && executor != null)
            {
                lock (toLock)
                {
                    // Reset lastSensorIteration to -1 to bypass the iteration check in Executor.Run()
                    executor.lastSensorIteration = -1;
                    
                    // Resume the waiting executor thread
                    if (solverWaiting)
                    {
                        solverWaiting = false;
                        Monitor.Pulse(toLock);
                    }
                }
            }
        }

        /// <summary>
        /// Called by the background Executor to schedule action execution on the main thread.
        /// </summary>
        internal void NotifyAction(string actionName, bool value)
        {
            lock(queueLock)
            {
                mainThreadActions.Enqueue(new ActionNotification { actionName = actionName, value = value });
            }
        }
        
        /// <summary>
        /// Cleans up thread handles and notifies the waiting executor thread to prevent leak.
        /// </summary>
        void OnDestroy()
        {
            if (executor != null)
            {
                executor.reason = false;
                lock (toLock)
                {
                    Monitor.Pulse(toLock);
                }
            }
        }
    }
}
