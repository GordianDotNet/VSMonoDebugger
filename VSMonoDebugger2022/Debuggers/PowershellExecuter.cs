﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading;
using VSMonoDebugger.Settings;

namespace VSMonoDebugger.Debuggers
{
    public class PowershellExecuter
    {
        public static void RunScript(string scriptText, string directory, Action<string> writeLineOutput, RedirectOutputOptions redirectOutputOption)
        {
            using (Runspace runspace = RunspaceFactory.CreateRunspace())
            {
                runspace.Open();
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    runspace.SessionStateProxy.Path.SetLocation(directory);
                }

                using (Pipeline pipeline = runspace.CreatePipeline())
                {
                    pipeline.Commands.AddScript(scriptText);

                    pipeline.InvokeAsync();

                    var handles = new WaitHandle[2];
                    handles[0] = pipeline.Output.WaitHandle;
                    handles[1] = pipeline.Error.WaitHandle;
                    pipeline.Input.Close();

                    while (pipeline.PipelineStateInfo.State == PipelineState.Running)
                    {
                        switch (WaitHandle.WaitAny(handles))
                        {
                            case 0:
                                while (pipeline.Output.Count > 0)
                                {
                                    foreach (PSObject result in pipeline.Output.NonBlockingRead())
                                    {
                                        if (redirectOutputOption.HasFlag(RedirectOutputOptions.RedirectStandardOutput))
                                        {
                                            writeLineOutput(result.ToString());
                                        }
                                    }
                                }
                                break;
                            case 1:
                                while (pipeline.Error.Count > 0)
                                {
                                    foreach (PSObject result in pipeline.Error.NonBlockingRead())
                                    {
                                        if (redirectOutputOption.HasFlag(RedirectOutputOptions.RedirectErrorOutput))
                                        {
                                            writeLineOutput(result.ToString());
                                        }
                                    }
                                }
                                break;
                            default:

                                break;
                        }
                    }

                    if (pipeline.PipelineStateInfo.State == PipelineState.Failed)
                    {
                        writeLineOutput("### ERROR: Powershell script failed!");
                        writeLineOutput(pipeline.PipelineStateInfo.Reason.ToString());
                    }

                    WaitHandle.WaitAll(handles);

                    while (pipeline.Output.Count > 0)
                    {
                        foreach (PSObject result in pipeline.Output.NonBlockingRead())
                        {
                            if (redirectOutputOption.HasFlag(RedirectOutputOptions.RedirectStandardOutput))
                            {
                                writeLineOutput(result.ToString());
                            }
                        }
                    }
                    while (pipeline.Error.Count > 0)
                    {
                        foreach (PSObject result in pipeline.Error.NonBlockingRead())
                        {
                            if (redirectOutputOption.HasFlag(RedirectOutputOptions.RedirectErrorOutput))
                            {
                                writeLineOutput(result.ToString());
                            }
                        }
                    }

                    runspace.Close();
                }
            }
        }
    }
}
