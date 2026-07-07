using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;

namespace ThinkEngine.LTLF
{
    public class LTLFControllerService : IDisposable
    {
        private static LTLFControllerService _instance;
        public static LTLFControllerService Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new LTLFControllerService();
                }
                return _instance;
            }
        }

        private Process process;
        private StreamWriter writer;
        private StreamReader reader;
        private readonly object processLock = new object();

        private LTLFControllerService()
        {
        }

        public void StartService()
        {
            lock(processLock)
            {
                if (process != null && !process.HasExited)
                    return;

                string exePath = LTLFControllerHandler.ExecutablePath;

                process = new Process();
                process.StartInfo.FileName = exePath;
                process.StartInfo.Arguments = "serve";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                process.StartInfo.RedirectStandardInput = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                
                string temporalFolder = Path.Combine(Utility.StreamingAssetsContent, "Temporal");
                string automataDir = Path.Combine(temporalFolder, "Automata");
                process.StartInfo.EnvironmentVariables["LTLF_STORAGE_DIR"] = automataDir;
                
                try
                {
                    process.Start();
                    writer = process.StandardInput;
                    reader = process.StandardOutput;
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError("Failed to start LTLF Controller: " + e.Message);
                }
            }
        }

        public string Evaluate(string behaviourName, string uuid, string sensors)
        {
            lock(processLock)
            {
                if (process == null || process.HasExited)
                {
                    UnityEngine.Debug.LogError("LTLF Controller process is not running!");
                    return "";
                }
                
                string inputLine = $"{behaviourName} {uuid} {sensors}".Trim();
                try 
                {
                    writer.WriteLine(inputLine);
                    writer.Flush();
                    
                    // Read response
                    string response = reader.ReadLine();
                    return response;
                }
                catch(Exception e)
                {
                    UnityEngine.Debug.LogError("Error communicating with LTLF Controller: " + e.Message);
                    return "";
                }
            }
        }

        public void StopService()
        {
            lock(processLock)
            {
                if (process != null && !process.HasExited)
                {
                    try 
                    {
                        writer.WriteLine("exit");
                        writer.Flush();
                        if (!process.WaitForExit(1000))
                        {
                            process.Kill();
                        }
                    }
                    catch 
                    {
                        try { process.Kill(); } catch { }
                    }
                    finally 
                    {
                        process.Close();
                    }
                }
                process = null;
                writer = null;
                reader = null;
            }
        }

        public void Dispose()
        {
            StopService();
        }
    }
}
