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
            
            string ltlfExePath = LTLFControllerHandler.ExecutablePath;
            if (!File.Exists(ltlfExePath))
            {
                Debug.LogError($"LTLF Controller not found at {ltlfExePath}");
                return;
            }
            
            LTLFControllerService.Instance.StartService();
            
            string output = LTLFControllerService.Instance.Evaluate(behaviour, uuid, sensorsString);
            
            if (!string.IsNullOrEmpty(output))
            {
                string[] outParts = output.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (outParts.Length > 0 && outParts[0] == uuid)
                {
                    for (int i = 1; i < outParts.Length; i++)
                    {
                        string actionToken = outParts[i];
                        bool value = true;
                        string actionName = actionToken;
                        
                        if (actionToken.StartsWith("!"))
                        {
                            value = false;
                            actionName = actionToken.Substring(1);
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
            fs.WriteLine($"brain({brain.gameObject.GetInstanceID()}).");
        }

        protected override List<OptionDescriptor> SpecificOptions()
        {
            List<OptionDescriptor> options = new List<OptionDescriptor>();
            options.Add(new OptionDescriptor("--filter=ltlf_sensor/1 "));
            return options;
        }
    }
}
