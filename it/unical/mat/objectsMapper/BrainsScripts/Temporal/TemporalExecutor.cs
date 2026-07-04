using it.unical.mat.embasp.@base;
using it.unical.mat.embasp.languages.asp;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ThinkEngine.LTLF;
using System;

namespace ThinkEngine.it.unical.mat.objectsMapper.BrainsScripts
{
    internal class TemporalExecutor : ASPExecutor
    {
        public TemporalExecutor(TemporalBrain b)
        {
            brain = b;
        }

        protected override void SpecificAnswerSetOperations(AnswerSet answer)
        {
            TemporalBrain tBrain = (TemporalBrain)brain;
            string behaviour = tBrain.behaviourName;
            string uuid = tBrain.brainID.ToString();
            
            List<string> activeSensors = new List<string>();
            
            foreach (string atom in answer.GetAnswerSet())
            {
                if (atom.StartsWith("ltlf_sensor("))
                {
                    // ltlf_sensor("sensorName") or ltlf_sensor(sensorName)
                    int startIndex = "ltlf_sensor(".Length;
                    string inner = atom.Substring(startIndex, atom.Length - startIndex - 1);
                    string sens = inner.Trim().Trim('"');
                    activeSensors.Add(sens);
                }
            }
            
            string sensorsString = string.Join(" ", activeSensors);
            
            if (tBrain.debug)
            {
                Debug.Log($"{tBrain.executorName} - Active LTLf Sensors: [{sensorsString}]");
            }
            
            string ltlfExePath = LTLFControllerHandler.ExecutablePath;
            if (!File.Exists(ltlfExePath))
            {
                Debug.LogError($"LTLF Controller not found at {ltlfExePath}");
                return;
            }
            
            LTLFControllerService.Instance.StartService();
            
            string output = LTLFControllerService.Instance.Evaluate(behaviour, uuid, sensorsString);
            
            if (tBrain.debug)
            {
                Debug.Log($"{tBrain.executorName} - LTLf Evaluation Output: {output}");
            }
            
            if (!string.IsNullOrEmpty(output))
            {
                string[] outParts = output.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (outParts.Length > 0 && outParts[0] == uuid)
                {
                    HashSet<string> trueActions = new HashSet<string>();
                    
                    // Parse actions returned by the LTLf controller
                    for (int i = 1; i < outParts.Length; i++)
                    {
                        string actionToken = outParts[i];
                        if (!actionToken.StartsWith("!"))
                        {
                            trueActions.Add(actionToken);
                        }
                    }
                    
                    // Notify all mapped actions based on whether they are in the trueActions set
                    foreach (var mapping in tBrain.actionMappings)
                    {
                        string actionName = mapping.actionName;
                        bool value = trueActions.Contains(actionName);
                        
                        if (tBrain.debug)
                        {
                            Debug.Log($"{tBrain.executorName} - Enqueuing action notification: '{actionName}' = {value}");
                        }
                        
                        tBrain.NotifyAction(actionName, value);
                    }
                }
            }
        }

        protected override bool SpecificFactsRetrieving(Brain brain)
        {
            return reason;
        }

        protected override void SpecificFactsWriting(Brain brain, StreamWriter fs)
        {
            // The base executor already writes currentBrainID(ID).
        }

        protected override List<OptionDescriptor> SpecificOptions()
        {
            List<OptionDescriptor> options = new List<OptionDescriptor>();
            options.Add(new OptionDescriptor("--filter=ltlf_sensor/1 "));
            return options;
        }
    }
}
