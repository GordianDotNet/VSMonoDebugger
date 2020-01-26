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
using Newtonsoft.Json;

namespace VSMonoDebugger
{
    internal class MonoVisualStudioExtension
    {
        /// <summary>
        /// see https://www.codeproject.com/Reference/720512/List-of-Visual-Studio-Project-Type-GUIDs
        /// </summary>
        public readonly static string VS_PROJECTKIND_SOLUTION_FOLDER = "{66A26720-8FB5-11D2-AA7E-00C04F688DDE}";

        protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private DTE _dte;
        private CommandEvents _startCommandEvents;
        private readonly ErrorListProvider _errorListProvider;

        public MonoVisualStudioExtension(Package package, DTE dte)
        {
            _dte = dte;
            _errorListProvider = new ErrorListProvider(package);
        }

        public static string GetExtensionInstallationDirectoryOrNull()
        {
            try
            {
                var uri = new Uri(typeof(MonoVisualStudioExtension).Assembly.CodeBase, UriKind.Absolute);
                return Path.GetDirectoryName(uri.LocalPath);
            }
            catch
            {
                return null;
            }
        }

        public async Task OverrideRunCommandAsync()
        {
            NLogService.TraceEnteringMethod(Logger);

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // https://stackoverflow.com/questions/15908652/how-to-programmatically-override-the-build-and-launch-actions
            // https://visualstudioextensions.vlasovstudio.com/2017/06/29/changing-visual-studio-2017-private-registry-settings/
            // https://github.com/3F/vsCommandEvent
            var _dteEvents = _dte.Events;
            _startCommandEvents = _dte.Events.CommandEvents["{5EFC7975-14BC-11CF-9B2B-00AA00573819}", 295];
            _startCommandEvents.BeforeExecute += OnBeforeStartCommand;
        }

        private void OnBeforeStartCommand(string guid, int id, object customIn, object customOut, ref bool cancelDefault)
        {
            NLogService.TraceEnteringMethod(Logger);

            //your event handler this command
        }

        public async Task BuildStartupProjectAsync()
        {
            NLogService.TraceEnteringMethod(Logger);

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

        private int BuildStartupProject()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var sb = (SolutionBuild2) _dte.Solution.SolutionBuild;

            try
            {
                var startProject = GetStartupProject();
                var activeConfiguration = _dte.Solution.SolutionBuild.ActiveConfiguration;
                var activeConfigurationName = activeConfiguration.Name;
                var startProjectName = startProject.FullName;
                Logger.Info($"BuildProject {startProject.FullName} {activeConfiguration.Name}");
                sb.BuildProject(activeConfiguration.Name, startProject.FullName, true);                
            }
            catch (Exception ex)
            {
                NLogService.LogError(Logger, ex);
                Logger.Info($"BuildProject failed - Fallback: BuildSolution");
                // Build complete solution (fallback solution)
                return BuildSolution();
            }

            return sb.LastBuildInfo;
        }

        private int BuildSolution()
        {
            NLogService.TraceEnteringMethod(Logger);
            ThreadHelper.ThrowIfNotOnUIThread();

            var sb = (SolutionBuild2)_dte.Solution.SolutionBuild;            
            sb.Build(true);
            return sb.LastBuildInfo;
        }

        private string GetStartupProjectPath()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Project startupProject = GetStartupProject();
            var projectFullName = startupProject.FullName;
            return Path.GetDirectoryName(projectFullName);
        }

        private string GetStartupAssemblyPath()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Project startupProject = GetStartupProject();
            return GetAssemblyPath(startupProject);
        }

        public bool IsStartupProjectAvailable()
        {
            //NLogService.TraceEnteringMethod(Logger);

            ThreadHelper.ThrowIfNotOnUIThread();

            var sb = (SolutionBuild2)_dte.Solution.SolutionBuild;
            return sb.StartupProjects != null && ((Array)sb.StartupProjects).Cast<string>().Count() > 0;
        }

        public VSMonoDebuggerProjectSettings? GetProjectSettingsFromStartupProject()
        {
            NLogService.TraceEnteringMethod(Logger);

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
                        Logger.Info($"Local project config file {projectConfigFile} found.");
                        var projectConfigFileContent = File.ReadAllText(projectConfigFile);
                        return VSMonoDebuggerProjectSettings.DeserializeFromJson(projectConfigFileContent);
                    }
                }
            }
            catch (Exception ex)
            {
                // *.VSMonoDebugger.config can contain illigal escape characters for WindowsPath "C:\Temp" => "C:\\Temp"
                // Don't fix it ... user has to be json conform
                Logger.Info("Please validate that the local project config file (*.VSMonoDebugger.config) conatins no illigal escape character sequences for WindowsDeployPath!");
                NLogService.LogError(Logger, ex);
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
                            Logger.Info($"Only C# projects are supported as startup project! ProjectName = {project.Name} Language = {project.CodeModel.Language}");
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

        private IList<Project> Projects(Solution solution)
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

        private string GetAssemblyPath(Project vsProject)
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
                Logger.Info($"GetFullOutputPath returned null! Using fallback: '{outputDir}'");
            }
            string outputFileName = vsProject.Properties.Item("OutputFileName").Value.ToString();
            if (string.IsNullOrEmpty(outputFileName))
            {
                outputFileName = $"{vsProject.Name}.exe";
                Logger.Info($"OutputFileName for project {vsProject.Name} is empty! Using fallback: {outputFileName}");
            }
            string assemblyPath = Path.Combine(outputDir, outputFileName);
            return assemblyPath;
        }

        private bool IsCSharpProject(Project vsProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                return vsProject.CodeModel.Language == CodeModelLanguageConstants.vsCMLanguageCSharp;
            }
            catch (Exception ex)
            {
                Logger.Info($"Project doesn't support property vsProject.CodeModel.Language! No CSharp project. {ex.Message}");
                return false;
            }
        }

        private string GetStartArguments()
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
                NLogService.LogError(Logger, ex);
                return string.Empty;
            }
        }
        
        public void AttachMonoDebuggerToRunningProcess(DebugOptions debugOptions)
        {
            NLogService.TraceEnteringMethod(Logger);

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
                    Logger.Error($"GetService did not returned SVsShellDebugger");
                }
                int hr = dbg.LaunchDebugTargets(1, pInfo);
                Marshal.ThrowExceptionForHR(hr);                
            }
            catch (Exception ex)
            {
                NLogService.LogError(Logger, ex);
                string msg = null;
                var sh = sp.GetService(typeof(SVsUIShell)) as IVsUIShell;
                if (sh != null)
                {
                    sh.GetErrorInfo(out msg);
                }
                if (!string.IsNullOrWhiteSpace(msg))
                {
                    Logger.Error(msg);
                }
                throw;
            }
            finally
            {
                if (pInfo != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(pInfo);
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

        /// <summary>
        /// We don't want to create a launch.json file
        /// Currently not working!
        /// </summary>
        /// <param name="debugOptions"></param>
        internal void AttachDotnetDebuggerToRunningProcess2(DebugOptions debugOptions)
        {
            NLogService.TraceEnteringMethod(Logger);

            ThreadHelper.ThrowIfNotOnUIThread();

            var extensionInstallationDirectory = GetExtensionInstallationDirectoryOrNull();
            if (extensionInstallationDirectory == null)
            {
                Logger.Error($"{nameof(extensionInstallationDirectory)} is null!");
            }

            var settings = debugOptions.UserSettings;

            var launchJson = settings.LaunchJsonContent;
            var jsonStringPlinkExe = JsonConvert.SerializeObject(Path.Combine(extensionInstallationDirectory, "plink.exe")).Trim('"');
            launchJson = launchJson.Replace(settings.PLINK_EXE_PATH, jsonStringPlinkExe);

            var sshPassword = string.IsNullOrWhiteSpace(settings.SSHPrivateKeyFile) ? $"-pw {settings.SSHPassword} {settings.SSHFullUrl}" : $"-i {settings.SSHPrivateKeyFile} {settings.SSHFullUrl}";
            launchJson = launchJson.Replace(settings.PLINK_SSH_CONNECTION_ARGS, $"{sshPassword} ");

            var jsonStringDeployPath = JsonConvert.SerializeObject(debugOptions.UserSettings.SSHDeployPath).Trim('"');
            launchJson = launchJson.Replace(settings.DEPLOYMENT_PATH, jsonStringDeployPath);
            launchJson = launchJson.Replace(settings.TARGET_EXE_FILENAME, debugOptions.TargetExeFileName);
            var jsonStringStartArguments = JsonConvert.SerializeObject(debugOptions.StartArguments).Trim('"');
            launchJson = launchJson.Replace(settings.START_ARGUMENTS, jsonStringStartArguments);

            var sp = new ServiceProvider((IServiceProvider)_dte);
            try
            {
                var dbg = sp.GetService(typeof(SVsShellDebugger)) as IVsDebugger4;
                if (dbg == null)
                {
                    Logger.Error($"GetService did not returned SVsShellDebugger");
                }
                VsDebugTargetInfo4[] debugTargets = new VsDebugTargetInfo4[1];
                debugTargets[0].dlo = (uint)DEBUG_LAUNCH_OPERATION.DLO_CreateProcess;
                debugTargets[0].bstrExe = debugOptions.StartupAssemblyPath;
                debugTargets[0].bstrCurDir = debugOptions.OutputDirectory;
                debugTargets[0].bstrArg = debugOptions.StartArguments;
                debugTargets[0].bstrRemoteMachine = null; // debug locally                
                //debugTargets[0].grfLaunch = (uint)__VSDBGLAUNCHFLAGS.DBGLAUNCH_StopDebuggingOnEnd; // When this process ends, debugging is stopped.
                //debugTargets[0].grfLaunch = (uint)__VSDBGLAUNCHFLAGS.DBGLAUNCH_DetachOnStop, // Detaches instead of terminating when debugging stopped.
                //debugTargets[0].fSendStdoutToOutputWindow = 0,
                //debugTargets[0].bstrExe = Path.Combine(extensionInstallationDirectory, "plink.exe");
                debugTargets[0].bstrOptions = launchJson;
                debugTargets[0].guidLaunchDebugEngine = new Guid("{541B8A8A-6081-4506-9F0A-1CE771DEBC04}");
                VsDebugTargetProcessInfo[] processInfo = new VsDebugTargetProcessInfo[debugTargets.Length];

                dbg.LaunchDebugTargets4(1, debugTargets, processInfo);
            }
            catch (Exception ex)
            {
                NLogService.LogError(Logger, ex);
                string msg = null;
                var sh = sp.GetService(typeof(SVsUIShell)) as IVsUIShell;
                if (sh != null)
                {
                    sh.GetErrorInfo(out msg);
                }
                if (!string.IsNullOrWhiteSpace(msg))
                {
                    Logger.Error(msg);
                }
                throw;
            }

            //var launchJsonFile = @"C:\TFS\launch.json";
            //File.WriteAllText(launchJsonFile, launchJson);

            //Logger.Info($"Project {debugOptions.StartupAssemblyPath} was built successfully. Invoking remote debug command");
            //var dte2 = _dte as DTE2;
            //dte2.ExecuteCommand("DebugAdapterHost.Launch", $"/LaunchJson:\"{launchJsonFile}\" /EngineGuid:541B8A8A-6081-4506-9F0A-1CE771DEBC04");
        }

        public void AttachDotnetDebuggerToRunningProcess(DebugOptions debugOptions)
        {
            NLogService.TraceEnteringMethod(Logger);            

            var extensionInstallationDirectory = GetExtensionInstallationDirectoryOrNull();
            if (extensionInstallationDirectory == null)
            {
                Logger.Error($"{nameof(extensionInstallationDirectory)} is null!");
            }

            var settings = debugOptions.UserSettings;

            var launchJson = settings.LaunchJsonContent;
            var jsonStringPlinkExe = JsonConvert.SerializeObject(Path.Combine(extensionInstallationDirectory, "plink.exe")).Trim('"');
            launchJson = launchJson.Replace(settings.PLINK_EXE_PATH, jsonStringPlinkExe);

            var sshPassword = string.IsNullOrWhiteSpace(settings.SSHPrivateKeyFile) ? $"-pw {settings.SSHPassword} {settings.SSHFullUrl}" : $"-i {settings.SSHPrivateKeyFile} {settings.SSHFullUrl}";
            launchJson = launchJson.Replace(settings.PLINK_SSH_CONNECTION_ARGS, $"{sshPassword} ");

            var jsonStringDeployPath = JsonConvert.SerializeObject(debugOptions.UserSettings.SSHDeployPath).Trim('"');
            launchJson = launchJson.Replace(settings.DEPLOYMENT_PATH, jsonStringDeployPath);
            launchJson = launchJson.Replace(settings.TARGET_EXE_FILENAME, debugOptions.TargetExeFileName);
            var jsonStringStartArguments = JsonConvert.SerializeObject(debugOptions.StartArguments).Trim('"');
            launchJson = launchJson.Replace(settings.START_ARGUMENTS, jsonStringStartArguments);

            string launchJsonFile = Path.Combine(GetStartupProjectPath(), "launch.json");
            Logger.Info($"Path of launch.json = {launchJsonFile}");
            File.WriteAllText(launchJsonFile, launchJson);

            Logger.Info($"Project {debugOptions.StartupAssemblyPath} was built successfully. Invoking remote debug command");
            var dte2 = _dte as DTE2;
            dte2.ExecuteCommand("DebugAdapterHost.Launch", $"/LaunchJson:\"{launchJsonFile}\" /EngineGuid:541B8A8A-6081-4506-9F0A-1CE771DEBC04");
        }

        public DebugOptions CreateDebugOptions(UserSettings settings)
        {
            NLogService.TraceEnteringMethod(Logger);

            ThreadHelper.ThrowIfNotOnUIThread();

            var startupAssemblyPath = GetStartupAssemblyPath();
            var targetExeFileName = Path.GetFileName(startupAssemblyPath);
            var outputDirectory = Path.GetDirectoryName(startupAssemblyPath);
            var startArguments = GetStartArguments();

            var preDebugScript = settings.DeployAndDebugOnLocalWindowsSystem ? settings.PreDebugScriptWithParametersWindows : settings.PreDebugScriptWithParameters;
            preDebugScript = ReplaceDebugParameters(preDebugScript, settings, targetExeFileName, startArguments, "\n");

            var debugScript = settings.DeployAndDebugOnLocalWindowsSystem ? settings.DebugScriptWithParametersWindows : settings.DebugScriptWithParameters;
            debugScript = ReplaceDebugParameters(debugScript, settings, targetExeFileName, startArguments, "\n");

            var debugOptions = new DebugOptions()
            {
                StartupAssemblyPath = startupAssemblyPath,
                UserSettings = settings,
                OutputDirectory = outputDirectory,
                TargetExeFileName = targetExeFileName,
                StartArguments = startArguments,
                PreDebugScript = preDebugScript,
                DebugScript = debugScript
            };

            return debugOptions;
        }

        private string ReplaceDebugParameters(string scriptWithParameters, UserSettings settings, string targetExeFileName, string startArguments, string endOfLine)
        {
            return (scriptWithParameters ?? string.Empty)
                .Replace(settings.MONO_DEBUG_PORT, settings.SSHMonoDebugPort.ToString())
                .Replace(settings.TARGET_EXE_FILENAME, targetExeFileName)
                .Replace(settings.START_ARGUMENTS, startArguments)
                .Replace("\r\n", endOfLine);
        }

        public async Task CreateMdbForAllDependantProjectsAsync(Action<string> msgOutput)
        {
            NLogService.TraceEnteringMethod(Logger);

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
                        var outputDir = Path.GetFullPath(outputDirectories[projectName]);
                        Logger.Info($"{projectName} - OutputDir: {outputDir}");

                        await ConvertPdb2MdbAsync(outputDir, msgOutput);
                    }
                    else
                    {
                        Logger.Info($"{projectName} - OutputDir: NOT FOUND!");
                    }
                }
                catch (Exception ex)
                {
                    NLogService.LogError(Logger, ex);
                }
            }
        }

        private Dictionary<string, string> CollectOutputDirectories(SolutionBuild2 sb, Action<string> msgOutput)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var outputPaths = new Dictionary<string, string>();
            foreach (BuildDependency dep in sb.BuildDependencies)
            {
                try
                {
                    msgOutput($"### CollectOutputDirectories: Propertes of project '{dep.Project.Name}'");
                    Logger.Info($"### CollectOutputDirectories: Propertes of project '{dep.Project.Name}'");

                    if (!IsCSharpProject(dep.Project))
                    {
                        msgOutput($"Only C# projects are supported project! ProjectName = {dep.Project.Name} Language = {dep.Project.CodeModel.Language}");
                        Logger.Info($"Only C# projects are supported project! ProjectName = {dep.Project.Name} Language = {dep.Project.CodeModel.Language}");
                        continue;
                    }

                    var outputDir = GetFullOutputPath(dep.Project);
                    if (string.IsNullOrEmpty(outputDir))
                    {
                        continue;
                    }
                    msgOutput($"OutputFullPath = {outputDir}");
                    Logger.Info($"OutputFullPath = {outputDir}");
                    outputPaths[dep.Project.FullName] = outputDir;
                }
                catch (Exception ex)
                {
                    msgOutput($"### CollectOutputDirectories: unsupported project - error was: '{ex.Message}'");
                    Logger.Info($"### CollectOutputDirectories: unsupported project - error was: '{ex.Message}'");
                }
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
                Logger.Info($"OutputPath not available!");
                return null;
            }

            var outputDir = Path.Combine(fullPath, outputPath);
            return outputDir;
        }

        private Dictionary<string, Project> CollectAllDependentProjects(Project currentProject, Action<string> msgOutput, Dictionary<string, Project> projects = null)
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

        private Task ConvertPdb2MdbAsync(string outputDirectory, Action<string> msgOutput)
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
                        NLogService.LogError(Logger, ex);
                        msgOutput?.Invoke($"Error while creating mdb file for {file}. {ex.Message}");
                    }                    
                }

                msgOutput?.Invoke($"End ConvertPdb2Mdb.");
            });
        }
    }
}