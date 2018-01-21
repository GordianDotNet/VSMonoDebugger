using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NLog;
using IServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
using Task = System.Threading.Tasks.Task;
//using Microsoft.MIDebugEngine;
using System.Collections.Generic;
using System.Security.Cryptography;
//using Mono.Debugging.Soft;
//using Mono.Debugging.VisualStudio;
using System.Runtime.Remoting;
using System.Runtime.Serialization.Formatters.Binary;
using System.Diagnostics;
using VSMonoDebugger.Services;
using VSMonoDebugger.Settings;

namespace VSMonoDebugger
{
    internal class MonoVisualStudioExtension
    {
        /// <summary>
        /// see https://www.codeproject.com/Reference/720512/List-of-Visual-Studio-Project-Type-GUIDs
        /// </summary>
        public readonly static string VS_PROJECTKIND_SOLUTION_FOLDER = "{66A26720-8FB5-11D2-AA7E-00C04F688DDE}";

        private readonly DTE _dte;
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public MonoVisualStudioExtension(DTE dTE)
        {
            _dte = dTE;
        }

        internal async Task BuildSolutionAsync()
        {
            await System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                var failedBuilds = BuildSolution();
                if (failedBuilds > 0)
                {
                    throw new Exception($"Build failed! Project failed to build: {failedBuilds}.");
                }
            });
        }

        internal int BuildSolution()
        {
            var sb = (SolutionBuild2) _dte.Solution.SolutionBuild;
            sb.Build(true);
            return sb.LastBuildInfo;
        }

        internal string GetStartupAssemblyPath()
        {
            Project startupProject = GetStartupProject();
            return GetAssemblyPath(startupProject);
        }

        private Project GetStartupProject()
        {
            var sb = (SolutionBuild2) _dte.Solution.SolutionBuild;
            string project = ((Array) sb.StartupProjects).Cast<string>().First();

            try
            {
                var projects = Projects(_dte.Solution);
                foreach (var p in projects)
                {
                    if (p.UniqueName == project)
                        return p;
                }
                //startupProject = _dte.Solution.Item(project);
            }
            catch (ArgumentException aex)
            {
                throw new ArgumentException($"The parameter '{project}' is incorrect.", aex);
            }

            throw new ArgumentException($"The parameter '{project}' is incorrect.");
        }

        public static IList<Project> Projects(Solution solution)
        {
            Projects projects = solution.Projects;
            List<Project> list = new List<Project>();
            var item = projects.GetEnumerator();
            while (item.MoveNext())
            {
                var project = item.Current as Project;
                if (project == null)
                {
                    continue;
                }

                if (project.Kind == VS_PROJECTKIND_SOLUTION_FOLDER)
                {
                    list.AddRange(GetSolutionFolderProjects(project));
                }
                else
                {
                    list.Add(project);
                }
            }

            return list;
        }

        private static IEnumerable<Project> GetSolutionFolderProjects(Project solutionFolder)
        {
            List<Project> list = new List<Project>();
            for (var i = 1; i <= solutionFolder.ProjectItems.Count; i++)
            {
                var subProject = solutionFolder.ProjectItems.Item(i).SubProject;
                if (subProject == null)
                {
                    continue;
                }

                // If this is another solution folder, do a recursive call, otherwise add
                if (subProject.Kind == VS_PROJECTKIND_SOLUTION_FOLDER)
                {
                    list.AddRange(GetSolutionFolderProjects(subProject));
                }
                else
                {
                    list.Add(subProject);
                }
            }
            return list;
        }

        internal string GetAssemblyPath(Project vsProject)
        {
            string fullPath = vsProject.Properties.Item("FullPath").Value.ToString();
            string outputPath =
                vsProject.ConfigurationManager.ActiveConfiguration.Properties.Item("OutputPath").Value.ToString();
            string outputDir = Path.Combine(fullPath, outputPath);
            string outputFileName = vsProject.Properties.Item("OutputFileName").Value.ToString();
            if (string.IsNullOrEmpty(outputFileName))
            {
                outputFileName = $"{vsProject.Name}.exe";
                Debug.WriteLine($"OutputFileName for project {vsProject.Name} is empty! Using fallback: {outputFileName}");
            }
            string assemblyPath = Path.Combine(outputDir, outputFileName);
            return assemblyPath;
        }

        internal string GetStartArguments()
        {
            try
            {
                Project startupProject = GetStartupProject();
                Configuration configuration = startupProject.ConfigurationManager.ActiveConfiguration;
                return configuration.Properties.Item("StartArguments").Value?.ToString() ?? string.Empty;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{nameof(GetStartArguments)}: {ex.Message} {ex.StackTrace}");
                return string.Empty;
            }
        }
        
        internal void AttachDebuggerToRunningProcess(DebugOptions debugOptions)
        {
            if (DebugEngineGuids.UseAD7Engine == EngineType.XamarinEngine)
            {
                // Workaround to get StartProject
                XamarinEngine.StartupProject = GetStartupProject();
            }

            IntPtr pInfo = GetDebugInfo(debugOptions);
            var sp = new ServiceProvider((IServiceProvider)_dte);
            try
            {
                var dbg = (IVsDebugger)sp.GetService(typeof(SVsShellDebugger));
                int hr = dbg.LaunchDebugTargets(1, pInfo);
                Marshal.ThrowExceptionForHR(hr);                
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                string msg;
                var sh = (IVsUIShell)sp.GetService(typeof(SVsUIShell));
                sh.GetErrorInfo(out msg);

                if (!string.IsNullOrWhiteSpace(msg))
                {
                    logger.Error(msg);
                }
                throw;
            }
            finally
            {
                if (pInfo != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(pInfo);
            }
        }
        
        public static string ComputeHash(string file)
        {
            using (FileStream stream = File.OpenRead(file))
            {
                var sha = new SHA256Managed();
                byte[] checksum = sha.ComputeHash(stream);
                return BitConverter.ToString(checksum).Replace("-", string.Empty);
            }
        }

        private IntPtr GetDebugInfo(DebugOptions debugOptions)//string args, int debugPort, string targetExe, string outputDirectory)
        {
            var info = new VsDebugTargetInfo()
            {
                //cbSize = (uint)Marshal.SizeOf(info),
                dlo = DEBUG_LAUNCH_OPERATION.DLO_CreateProcess,
                bstrExe = debugOptions.StartupAssemblyPath,
                bstrCurDir = debugOptions.OutputDirectory,
                bstrArg = debugOptions.StartArguments,
                bstrRemoteMachine = null, // debug locally                
                grfLaunch = (uint)__VSDBGLAUNCHFLAGS.DBGLAUNCH_StopDebuggingOnEnd, // When this process ends, debugging is stopped.
                //grfLaunch = (uint)__VSDBGLAUNCHFLAGS.DBGLAUNCH_DetachOnStop, // Detaches instead of terminating when debugging stopped.
                fSendStdoutToOutputWindow = 0,
                clsidCustom = DebugEngineGuids.EngineGuid,
                //bstrEnv = "",
                bstrOptions = debugOptions.SerializeToJson() // add debug engine options
            };

            if (DebugEngineGuids.UseAD7Engine == EngineType.XamarinEngine)
            {
                info.bstrPortName = "Mono";
                info.clsidPortSupplier = DebugEngineGuids.ProgramProviderGuid;
            }

            info.cbSize = (uint)Marshal.SizeOf(info);

            IntPtr pInfo = Marshal.AllocCoTaskMem((int) info.cbSize);
            Marshal.StructureToPtr(info, pInfo, false);
            return pInfo;
        }
        
        public DebugOptions CreateDebugOptions(UserSettings settings, bool useSSH = false)
        {
            var startupAssemblyPath = GetStartupAssemblyPath();
            var targetExeFileName = Path.GetFileName(startupAssemblyPath);
            var outputDirectory = Path.GetDirectoryName(startupAssemblyPath);
            var startArguments = GetStartArguments();

            var debugOptions = new DebugOptions()
            {
                UseSSH = useSSH,
                StartupAssemblyPath = startupAssemblyPath,
                UserSettings = settings,
                OutputDirectory = outputDirectory,
                TargetExeFileName = targetExeFileName,
                StartArguments = startArguments
            };

            return debugOptions;
        }

        public Task ConvertPdb2Mdb(string outputDirectory, Action<string> msgOutput)
        {
            return System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                msgOutput?.Invoke($"Start ConvertPdb2Mdb: {outputDirectory} ...");

                var assemblyFiles = Directory.EnumerateFiles(outputDirectory, "*.exe", SearchOption.AllDirectories)
                    .Union(Directory.EnumerateFiles(outputDirectory, "*.dll", SearchOption.AllDirectories));

                foreach (string file in assemblyFiles)
                {
                    var pdbFile = Path.ChangeExtension(file, "pdb");
                    if (!File.Exists(pdbFile))
                    {
                        // No *.pdb file found for file
                        continue;
                    }

                    var mdbFile = file + ".mdb";
                    if (File.GetLastWriteTime(pdbFile) <= File.GetLastWriteTime(mdbFile))
                    {
                        // No newer *.pdb file found
                        msgOutput?.Invoke($"No mdb file creation needed for {file}. (*.pdb file write time <= *.mdb file write time)");
                        continue;
                    }

                    msgOutput?.Invoke($"Creating mdb file for {file}");
                    try
                    {
                        Pdb2Mdb.Converter.Convert(file);
                    }
                    catch (Exception ex)
                    {
                        msgOutput?.Invoke($"Error while creating mdb file for {file}. {ex.Message}");
                    }                    
                }

                msgOutput?.Invoke($"End ConvertPdb2Mdb.");
            });
        }
    }
}