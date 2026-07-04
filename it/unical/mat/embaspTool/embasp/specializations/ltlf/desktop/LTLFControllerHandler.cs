using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace ThinkEngine.LTLF
{
    /// <summary>
    /// Handles the execution of one-shot commands (like config, generate, help) 
    /// for the LTLf Controller executable. It abstracts the process management 
    /// from the Brain and the Editor.
    /// </summary>
    public static class LTLFControllerHandler
    {
        /// <summary>
        /// Gets the path to the ltlf_controller executable based on the current platform.
        /// </summary>
        public static string ExecutablePath
        {
            get
            {
                string dlv2Path = Path.Combine(Utility.StreamingAssetsContent, "lib");
                string ltlfExePath = Path.Combine(dlv2Path, "ltlf_controller.exe");
                
                if (Application.platform == RuntimePlatform.LinuxPlayer || Application.platform == RuntimePlatform.LinuxEditor)
                {
                    ltlfExePath = Path.Combine(dlv2Path, "ltlf_controller");
                }
                
                return ltlfExePath;
            }
        }

        /// <summary>
        /// Retrieves the expected sensors for a given behaviour.
        /// </summary>
        /// <param name="behaviourName">The name of the behaviour to inspect.</param>
        /// <returns>A string of ASP comments containing the sensors, or an error comment.</returns>
        public static string GetHelp(string behaviourName)
        {
            string exePath = ExecutablePath;
            if (!File.Exists(exePath)) 
            {
                return "% (ltlf_controller executable not found)\n";
            }

            try
            {
                using (Process helpProcess = new Process())
                {
                    helpProcess.StartInfo.FileName = exePath;
                    helpProcess.StartInfo.Arguments = $"help \"{behaviourName}\"";
                    helpProcess.StartInfo.UseShellExecute = false;
                    helpProcess.StartInfo.RedirectStandardOutput = true;
                    helpProcess.StartInfo.CreateNoWindow = true;
                    helpProcess.Start();
                    
                    string output = helpProcess.StandardOutput.ReadToEnd();
                    helpProcess.WaitForExit();
                    
                    if (helpProcess.ExitCode == 0)
                    {
                        return ParseHelpOutput(output);
                    }
                    return "% (Build the LTLF controller first to see expected sensors)\n";
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[LTLFControllerHandler] Error executing help: {ex.Message}");
                return "% (Error retrieving sensors)\n";
            }
        }

        /// <summary>
        /// Retrieves the action variables for a given behaviour.
        /// </summary>
        /// <param name="behaviourName">The name of the behaviour to inspect.</param>
        /// <returns>A list of action names.</returns>
        public static System.Collections.Generic.List<string> GetActions(string behaviourName)
        {
            var actions = new System.Collections.Generic.List<string>();
            string exePath = ExecutablePath;
            if (!File.Exists(exePath)) 
            {
                return actions;
            }

            try
            {
                using (Process helpProcess = new Process())
                {
                    helpProcess.StartInfo.FileName = exePath;
                    helpProcess.StartInfo.Arguments = $"help \"{behaviourName}\"";
                    helpProcess.StartInfo.UseShellExecute = false;
                    helpProcess.StartInfo.RedirectStandardOutput = true;
                    helpProcess.StartInfo.CreateNoWindow = true;
                    helpProcess.Start();
                    
                    string output = helpProcess.StandardOutput.ReadToEnd();
                    helpProcess.WaitForExit();
                    
                    if (helpProcess.ExitCode == 0)
                    {
                        string[] lines = output.Split('\n');
                        foreach (string line in lines)
                        {
                            if (line.StartsWith("Action Variables:"))
                            {
                                // Format is usually: Action Variables: action1, action2, action3
                                string vars = line.Substring("Action Variables:".Length).Trim();
                                if (!string.IsNullOrEmpty(vars))
                                {
                                    string[] splits = vars.Split(',');
                                    foreach (string s in splits)
                                    {
                                        if (!string.IsNullOrWhiteSpace(s))
                                            actions.Add(s.Trim());
                                    }
                                }
                            }
                        }
                    }
                    return actions;
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[LTLFControllerHandler] Error executing help: {ex.Message}");
                return actions;
            }
        }

        private static string ParseHelpOutput(string output)
        {
            string comments = "";
            string[] lines = output.Split('\n');
            foreach (string line in lines)
            {
                if (line.StartsWith("Sensor Variables:"))
                {
                    comments += "% " + line.Trim() + "\n";
                }
            }
            return comments;
        }

        /// <summary>
        /// Builds the LTLf controller automata for a specific behaviour using its config file.
        /// </summary>
        /// <param name="behaviourName">The name of the behaviour to generate.</param>
        /// <param name="message">Output message containing success or error details.</param>
        /// <returns>True if generation was successful, false otherwise.</returns>
        public static bool Generate(string behaviourName, out string message)
        {
            string exePath = ExecutablePath;
            if (!File.Exists(exePath))
            {
                message = $"Executable not found at {exePath}.\nPlease make sure it is built or copied to the lib directory.";
                return false;
            }

            string temporalFolder = Path.Combine(Utility.StreamingAssetsContent, "Temporal");
            string configPath = Path.Combine(temporalFolder, "Config", behaviourName + ".txt");
            
            if (!File.Exists(configPath))
            {
                message = $"Config file not found at {configPath}.";
                return false;
            }

            string automataDir = Path.Combine(temporalFolder, "Automata");
            if (!Directory.Exists(automataDir))
            {
                Directory.CreateDirectory(automataDir);
            }

            try
            {
                // First configure the storage directory
                using (Process configProcess = new Process())
                {
                    configProcess.StartInfo.FileName = exePath;
                    configProcess.StartInfo.Arguments = $"config --storage-dir \"{automataDir}\"";
                    configProcess.StartInfo.UseShellExecute = false;
                    configProcess.StartInfo.CreateNoWindow = true;
                    configProcess.Start();
                    configProcess.WaitForExit();
                }
                
                // Then generate the automata
                using (Process genProcess = new Process())
                {
                    genProcess.StartInfo.FileName = exePath;
                    genProcess.StartInfo.Arguments = $"generate \"{configPath}\" --name \"{behaviourName}\"";
                    genProcess.StartInfo.UseShellExecute = false;
                    genProcess.StartInfo.RedirectStandardOutput = true;
                    genProcess.StartInfo.RedirectStandardError = true;
                    genProcess.StartInfo.CreateNoWindow = true;
                    genProcess.Start();
                    
                    string error = genProcess.StandardError.ReadToEnd();
                    genProcess.WaitForExit();
                    
                    if (genProcess.ExitCode == 0)
                    {
                        message = $"Controller '{behaviourName}' built successfully in {automataDir}";
                        return true;
                    }
                    else
                    {
                        message = $"Failed to build controller:\n{error}";
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                message = $"An exception occurred during generation: {e.Message}";
                return false;
            }
        }
    }
}
