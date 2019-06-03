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
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Diagnostics;
using VSMonoDebugger.Services;
using VSMonoDebugger.Settings;
using Mono.Debugging.VisualStudio;

namespace VSMonoDebugger
{
    internal class MonoVisualStudioExtension
    {
        /// <summary>
        /// see https://www.codeproject.com/Reference/720512/List-of-Visual-Studio-Project-Type-GUIDs
        /// </summary>
        public readonly static string VS_PROJECTKIND_SOLUTION_FOLDER = "{66A26720-8FB5-11D2-AA7E-00C04F688DDE}";

        private readonly Package _package;
        private readonly DTE _dte;
        private readonly ErrorListProvider _errorListProvider;
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public MonoVisualStudioExtension(Package package)
        {
            _package = package;
            _dte = package.GetService<DTE>();
            _errorListProvider = new ErrorListProvider(package);
        }

        internal async Task BuildStartupProjectAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            
            var failedBuilds = BuildStartupProject();
            if (failedBuilds > 0)
            {
                Window window = _dte.Windows.Item("{34E76E81-EE4A-11D0-AE2E-00A0C90FFFC3}");//EnvDTE.Constants.vsWindowKindOutput
                OutputWindow outputWindow = (OutputWindow)window.Object;
                outputWindow.ActivePane.Activate();
                outputWindow.ActivePane.OutputString($"{failedBuilds} project(s) failed to build. See error and output window!");

                _errorListProvider.Show();

                throw new Exception($"{failedBuilds} project(s) failed to build. See error and output window!");
            }
        }

        internal int BuildStartupProject()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var sb = (SolutionBuild2) _dte.Solution.SolutionBuild;

            try
            {
                var startProject = GetStartupProject();
                var activeConfiguration = _dte.Solution.SolutionBuild.ActiveConfiguration;
                var activeConfigurationName = activeConfiguration.Name;
                var startProjectName = startProject.FullName;
                LogInfo($"BuildProject {startProject.FullName} {activeConfiguration.Name}");
                sb.BuildProject(activeConfiguration.Name, startProject.FullName, true);                
            }
            catch (Exception ex)
            {
                LogError(ex);
                LogInfo($"BuildProject failed - Fallback: BuildSolution");
                // Build complete solution (fallback solution)
                return BuildSolution();
            }

            return sb.LastBuildInfo;
        }

        internal int BuildSolution()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var sb = (SolutionBuild2)_dte.Solution.SolutionBuild;
            LogInfo($"BuildSolution");
            sb.Build(true);
            return sb.LastBuildInfo;
        }

        internal string GetStartupAssemblyPath()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Project startupProject = GetStartupProject();
            return GetAssemblyPath(startupProject);
        }

        public bool IsStartupProjectAvailable()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var sb = (SolutionBuild2)_dte.Solution.SolutionBuild;
            return sb.StartupProjects != null && ((Array)sb.StartupProjects).Cast<string>().Count() > 0;
        }

        public VSMonoDebuggerProjectSettings? GetProjectSettingsFromStartupProject()
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                var startupProject = GetStartupProject();
                var projectFullName = startupProject.FullName;
                if (File.Exists(projectFullName))
                {
                    var projectConfigFile = Path.ChangeExtension(projectFullName, ".VSMonoDebugger.config");
                    if (File.Exists(projectConfigFile))
                    {
                        LogInfo($"Local project config file {projectConfigFile} found.");
                        var projectConfigFileContent = File.ReadAllText(projectConfigFile);
                        return VSMonoDebuggerProjectSettings.DeserializeFromJson(projectConfigFileContent);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
            }

            return null;
        }

        private Project GetStartupProject()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var sb = (SolutionBuild2) _dte.Solution.SolutionBuild;
            var startupProjects = ((Array) sb.StartupProjects).Cast<string>().ToList();

            try
            {
                var projects = Projects(_dte.Solution);
                foreach (var project in projects)
                {
                    if (startupProjects.Contains(project.UniqueName))
                    {
                        if (IsCSharpProject(project))
                        {
                            // We are only support one C# project at once
                            return project;
                        }
                        else
                        {
                            LogInfo($"Only C# projects are supported as startup project! ProjectName = {project.Name} Language = {project.CodeModel.Language}");
                        }
                    }
                }
            }
            catch (ArgumentException aex)
            {
                throw new ArgumentException($"No startup project extracted! The parameter StartupProjects = '{string.Join(",", startupProjects.ToArray())}' is incorrect.", aex);
            }

            throw new ArgumentException($"No startup project found! Checked projects in StartupProjects = '{string.Join(",", startupProjects.ToArray())}'");
        }

        public static IList<Project> Projects(Solution solution)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

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
            ThreadHelper.ThrowIfNotOnUIThread();

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
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!IsCSharpProject(vsProject))
            {
                throw new ArgumentException($"Only C# projects are supported as startup project! ProjectName = {vsProject.Name} Language = {vsProject.CodeModel.Language}");
            }

            var outputDir = GetFullOutputPath(vsProject);
            if (outputDir == null)
            {
                outputDir = string.Empty;
                LogInfo($"GetFullOutputPath returned null! Using fallback: '{outputDir}'");
            }
            string outputFileName = vsProject.Properties.Item("OutputFileName").Value.ToString();
            if (string.IsNullOrEmpty(outputFileName))
            {
                outputFileName = $"{vsProject.Name}.exe";
                LogInfo($"OutputFileName for project {vsProject.Name} is empty! Using fallback: {outputFileName}");
            }
            string assemblyPath = Path.Combine(outputDir, outputFileName);
            return assemblyPath;
        }

        private static bool IsCSharpProject(Project vsProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            return vsProject.CodeModel.Language == CodeModelLanguageConstants.vsCMLanguageCSharp;
        }

        internal string GetStartArguments()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

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
            ThreadHelper.ThrowIfNotOnUIThread();

            if (DebugEngineGuids.UseAD7Engine == EngineType.XamarinEngine)
            {
                // Workaround to get StartProject
                XamarinEngine.StartupProject = GetStartupProject();
            }

            IntPtr pInfo = GetDebugInfo(debugOptions);
            var sp = new ServiceProvider((IServiceProvider)_dte);
            try
            {
                var dbg = sp.GetService(typeof(SVsShellDebugger)) as IVsDebugger;
                if (dbg == null)
                {
                    logger.Error($"GetService did not returned SVsShellDebugger");
                }
                int hr = dbg.LaunchDebugTargets(1, pInfo);
                Marshal.ThrowExceptionForHR(hr);                
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                string msg = null;
                var sh = sp.GetService(typeof(SVsUIShell)) as IVsUIShell;
                if (sh != null)
                {
                    sh.GetErrorInfo(out msg);
                }
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
            ThreadHelper.ThrowIfNotOnUIThread();

            var startupAssemblyPath = GetStartupAssemblyPath();
            var targetExeFileName = Path.GetFileName(startupAssemblyPath);
            var outputDirectory = Path.GetDirectoryName(startupAssemblyPath);
            var startArguments = GetStartArguments();
            var preDebugScript = settings.PreDebugScriptWithParameters
                .Replace(settings.MONO_DEBUG_PORT, settings.SSHMonoDebugPort.ToString())
                .Replace(settings.TARGET_EXE_FILENAME, targetExeFileName)
                .Replace(settings.START_ARGUMENTS, startArguments)
                .Replace("\r\n", "\n");
            var debugScript = settings.DebugScriptWithParameters
                .Replace(settings.MONO_DEBUG_PORT, settings.SSHMonoDebugPort.ToString())
                .Replace(settings.TARGET_EXE_FILENAME, targetExeFileName)
                .Replace(settings.START_ARGUMENTS, startArguments)
                .Replace("\r\n", "\n");

            var debugOptions = new DebugOptions()
            {
                UseSSH = useSSH,
                StartupAssemblyPath = startupAssemblyPath,
                UserSettings = settings,
                OutputDirectory = outputDirectory,
                TargetExeFileName = targetExeFileName,
                StartArguments = startArguments,
                PreDebugScript = preDebugScript,
                DebugScript = debugScript,
            };

            return debugOptions;
        }

        public async Task CreateMdbForAllDependantProjectsAsync(Action<string> msgOutput)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var sb = (SolutionBuild2)_dte.Solution.SolutionBuild;

            var startProject = GetStartupProject();
            var dependantProjects = CollectAllDependentProjects(startProject, msgOutput);
            var outputDirectories = CollectOutputDirectories(sb, msgOutput);

            foreach (var projectName in dependantProjects.Keys)
            {
                try
                {
                    if (outputDirectories.ContainsKey(projectName))
                    {
                        var outputDir = outputDirectories[projectName];
                        LogInfo($"{projectName} - OutputDir: {outputDir}");

                        await ConvertPdb2MdbAsync(outputDir, msgOutput);
                    }
                    else
                    {
                        LogInfo($"{projectName} - OutputDir: NOT FOUND!");
                    }
                }
                catch (Exception ex)
                {
                    LogError(ex);
                }
            }
        }

        private Dictionary<string, string> CollectOutputDirectories(SolutionBuild2 sb, Action<string> msgOutput)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var outputPaths = new Dictionary<string, string>();
            foreach (BuildDependency dep in sb.BuildDependencies)
            {
                msgOutput($"### CollectOutputDirectories: Propertes of project '{dep.Project.Name}'");
                LogInfo($"### CollectOutputDirectories: Propertes of project '{dep.Project.Name}'");

                if (!IsCSharpProject(dep.Project))
                {
                    msgOutput($"Only C# projects are supported project! ProjectName = {dep.Project.Name} Language = {dep.Project.CodeModel.Language}");
                    LogInfo($"Only C# projects are supported project! ProjectName = {dep.Project.Name} Language = {dep.Project.CodeModel.Language}");
                    continue;
                }

                var outputDir = GetFullOutputPath(dep.Project);
                if (string.IsNullOrEmpty(outputDir))
                {
                    continue;
                }
                msgOutput($"OutputFullPath = {outputDir}");
                LogInfo($"OutputFullPath = {outputDir}");
                outputPaths[dep.Project.FullName] = outputDir;
            }
            return outputPaths;
        }

        private string GetFullOutputPath(Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var fullPath = string.Empty;
            var outputPath = string.Empty;
            foreach (Property property in project.Properties)
            {
                try
                {
                    //LogInfo($"Name: {property.Name} = {property.Value}");
                    if (property.Name == "FullPath" || (property.Name == "" && string.IsNullOrEmpty(fullPath)))
                    {
                        fullPath = property.Value?.ToString();
                    }
                }
                catch
                {
                    //LogInfo($"Name: {property.Name} = --ERROR--");
                }

            }

            try
            {
                outputPath = project.ConfigurationManager.ActiveConfiguration.Properties.Item("OutputPath").Value.ToString();
            }
            catch
            {
                LogInfo($"OutputPath not available!");
                return null;
            }

            var outputDir = Path.Combine(fullPath, outputPath);
            return outputDir;
        }

        public Dictionary<string, Project> CollectAllDependentProjects(Project currentProject, Action<string> msgOutput, Dictionary<string, Project> projects = null)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            projects = projects ?? new Dictionary<string, Project>();

            if (currentProject == null || projects.ContainsKey(currentProject.FullName))
            {
                return projects;
            }

            projects.Add(currentProject.FullName, currentProject);

            var vsproject = currentProject.Object as VSLangProj.VSProject;

            foreach (VSLangProj.Reference reference in vsproject.References)
            {
                if (reference.SourceProject == null)
                {
                    // This is an assembly reference
                }
                else
                {
                    // This is a project reference
                    var dependentProject = reference.SourceProject;
                    CollectAllDependentProjects(dependentProject, msgOutput, projects);
                }
            }

            return projects;
        }

        public Task ConvertPdb2MdbAsync(string outputDirectory, Action<string> msgOutput)
        {
            return Task.Run(() =>
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

        public void LogInfo(string message)
        {
            logger.Log(new LogEventInfo(LogLevel.Info, "MonoVisualStudioExtension", message));

        }

        public void LogError(Exception ex)
        {
            logger.Error(ex);
        }
    }
}