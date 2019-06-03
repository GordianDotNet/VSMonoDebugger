VSMonoDebugger
============

Enables Visual Studio 2017 and 2019 to deploy and debug a .Net application on a remote Linux machine with mono installed over SSH.

# Usage

### Visual Studio 2019 Support
In Visual Studio 2019, extension menus are handled differently. "Mono" can now be found in the "Extensions" menu.

![Visual Studio 2019 Extensions Menu](VSMonoDebugger/Resources/VisualStudio2019ExtensionMenu.png)

### Settings
You have to save a valid SSH connection first!

> Menu "Mono"/"Settings..."

![VSMonoDebugger Settings](VSMonoDebugger/Resources/VSMonoDebugger_Settings1.png)

- [1] Selects the connection to use for deploying and debugging
- [2] You can create a additional SSH connection
- [3] You can remove a SSH connection (The last connection can not be deleted)
- [4] Enter the remote SSH IP or host name
- [5] Enter the SSH port
- [6] Enter the SSH username
- [7] Enter the SSH password or private key file passphrase (if there is no password => leave the field empty)
- [8] Enter/set the filepath to a private key file (if there is no ssh private key authentication => leave the field empty)
- [9] Mono opens a debug session over this port
- [10] This is where the project output is deployed
- [11] If enabled you can create a JSON config file to overwrite the 'Remote Deploy Path'. (see below)
- [12] Timeout to connect to the debug session
- [13] Console outputs can be output in the Visual Studio Output window
- [14] All changes are saved after you click on "Save"
- [15] All changes are discarded if you click on "Cancel"
- [16] Change the default debugging scripts on tab "Debug scripts". (ex.: to add sudo)

![VSMonoDebugger Settings](VSMonoDebugger/Resources/VSMonoDebugger_Settings2.png)
- [1] Change the Pre-Debug script to kill old mono debug sessions
- [2] The default Pre-Debug script is loaded if you click on "Set default Pre-Debug script" or the input is empty
- [3] Change the Debug script to start a mono debug session
- [4] The default Debug script is loaded if you click on "Set default Debug script" or the input is empty
- [5] For further script information click on "Script information"

![VSMonoDebugger Settings](VSMonoDebugger/Resources/VSMonoDebugger_Settings3.png)
- [1] Information about all supported parameters

### Deploy
You can deploy your "Startup project" output to the remote machine via SSH configured under "Settings".

> Menu "Mono"/"Deploy only (SSH)"

###### Notice
To speed up deployment, [SshFileSync](https://github.com/GordianDotNet/SshFileSync) is used.

> To upload only changed files, an additional cache file '.uploadCache.cache' is stored in the destination folder. 
> Don't delete this cache file! 

### Debug
You can start a debug session in Visual Studio 2017 on the remote machine.

> Menu "Mono"/"Debug only (SSH)"

### Deploy and Debug
You can run both commands in one step.

> Menu "Mono"/"Deploy and Debug (SSH)"

### Attach to mono debugger (without SSH)
You can debug a mono process that is already waiting for a debugger. SSH is not necessary. Only one TCP connection is required. The mono process must be started manually with the necessary arguments.

> Menu "Mono"/"Attach to mono debugger (without SSH)"

### Build Startup Project with MDB Files
You can build the startup project and all dependent projects. Additionally the mdb files are created. This is necessary for the support "Attach to mono debugger (without SSH)", because the mdb files must be present in every output directory of dependent projects.

> Menu "Mono"/"Build Startup Project with MDB Files"

### Overwrite the remote deploy path for a startup project
1) You have to enable the option via

> Menu "Mono"/"Settings..."/"Search 'Deploy Path' in 'PROJECTNAME.VSMonoDebugger.config' of the startup project."

2) You have to create a file with name 'PROJECTNAME.VSMonoDebugger.config' in the same directory like your startup project file (PROJECTNAME have to be the name of your project). 

ex.: 'MyNewSampleProject.csproj' results in 'MyNewSampleProject.VSMonoDebugger.config'

3) The file must contain a JSON object with the property "SSHDeployPath":

```
{
	"SSHDeployPath": "./NewDeployPath/",
}
```

# Known Issues

- [ ] Support prerequisite Microsoft.VisualStudio.Component.MonoDebugger without copying the dlls (Makes problems with Xamarin's debugger)
**If you are using Xamarin and debugging fails, please disable VSMonoDebugger!**

- [ ] Support settings in an [Options Page](https://msdn.microsoft.com/en-us/library/bb166195.aspx)
- [ ] Code has to be refactored for better error logging
- [ ] Why can't the Mono.Cecil.Pdb.NativePdbReaderProvider type be found in Visual Studio to support pdb files?

# Solved Issues

- [x] Script to stop running mono debug process has dependencies (workaround depends on installed packages)
- [x] Support break points in referenced projects. *.mdb files are searched locally in each output directory, so we need mdb files in each project output directory (see Mono.Cecil.Cil.DefaultSymbolReaderProvider.GetSymbolReader()).
- [x] Ignore unsupported project types (like C++, VB, F#)
- [x] Under Ubuntu replace the Pre-Debug script with `pkill -f mono` to kill old mono debug sessions.
And give your user root rights for `pkill` and `mono` [see: Run a specific program as root without a password promt](https://unix.stackexchange.com/questions/18830/how-to-run-a-specific-program-as-root-without-a-password-prompt)
```
# User alias specification
yourUserName ALL=(ALL) NOPASSWD: /usr/bin/pkill, /usr/bin/mono
```
- [x] Support [ssh private key authentication](https://www.digitalocean.com/community/tutorials/how-to-set-up-ssh-keys-on-ubuntu-1604) to authenticate
- [x] When using the ProvideAutoLoad attribute, your package (VSMonoDebugger.VSMonoDebuggerPackage) class should derive from AsyncPackage instead of Package to improve performance. Read more about using AsyncPackage here: https://aka.ms/asyncpackage.
**Visual Studio 2019 shows a warning**

# Version History

## 0.9.0
**2019-06-03**

- [x] Feature: Switched to AsyncPackage to avoid warning in Visual Studio 2019
- [x] Feature: Supports a separate deployment path for each startup project via PROJECTNAME.VSMonoDebugger.config in the project folder
- [x] Bugfix: #7 Catch exception of unsupported setup projects .vdproj
- [x] Bugfix: #10 Allow only IPv4 for the result of Dns.GetHostAddresses - IPv6 is not supported by mono softdebugger

## 0.8.0
**2019-05-05**

- [x] Feature: Support Visual Studio 2019
- [x] Bugfix: Include System.Diagnostics.Tracer.dll - is not installed on some systems  

## 0.7.8
**2018-09-24**

- [x] Feature: Support ssh private key authentication

## 0.7.7
**2018-09-13**

- [x] Bugfix: Fixed build error if solution has other project types like C++ (only CSharp projects are built)

## 0.7.6
**2018-08-25**

- [x] Bugfix: Support break points in referenced projects. *.mdb files are searched locally in each output directory, so we need mdb files in each project output directory. 
- [x] Feature: Build only startup project instead of whole solution and show error window automatically
- [x] Feature: Add new menu item "Attach to mono debugger (without SSH)"
- [x] Feature: Add new menu item "Build Startup Project with MDB Files"


## 0.7.5
**2018-07-11**

- [x] Bugfix: NullReference in UserSettingsContainer fixed
- [x] Bugfix: Visual Studio 2017 doesn't include Newtonsoft.Json.dll anymore. See [newtonsoftjsondll-not-included-in-vsix](https://social.msdn.microsoft.com/Forums/sqlserver/en-US/550ddfdc-027c-41ba-9b32-31e6391bc038/newtonsoftjsondll-not-included-in-vsix?forum=vsx)
- [x] Bugfix: Load default UserSettings without username and password instead of NullReferenceException
- [x] Feature: Support custom debug scripts. (ex. to support sudo or additional preparations.)
- [x] Feature: Error logging and handling improved for StartDebuggerAsync if a script has syntax errors.

## 0.7.0
**2018-03-18**

- [x] Support fast deployment with [SshDeltaCopy](https://github.com/GordianDotNet/SshFileSync) 
- [x] Integrate the Xamarin Mono Debugger (Mono.Debugging.VisualStudio.4.9.10-pre)
- [x] Create mdb files before deployment with a integrated pdb2mdb function
- [x] Manage multiple ssh connections (deployment devices)
- [x] Show standard output in the Visual Studio Output window
- [x] Support embedded devices (Testet on a Raspberry 2 with Yocto Linux version 4.9.27 and Mono 5.4)

## 0.1.0
**2018-01-20**

- [x] Start project

# Used resources

- [x] [Visual Studio Image Library 2017](https://www.microsoft.com/en-my/download/details.aspx?id=35825)
- [x] [Mono.Debugging.VisualStudio.4.9.10-pre](Mono.Debugging.VisualStudio.4.9.10-pre/README.txt)
- [x] [pdb2mdb source code](https://github.com/mono/mono/tree/master/mcs/tools/pdb2mdb)
- [x] [Newtonsoft.Json.dll Version 10.0.3.21018](https://www.newtonsoft.com/json)