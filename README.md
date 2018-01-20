VSMonoDebugger
============

Enables Visual Studio 2017 to deploy and debug mono application on remote linux machines via SSH.

# Usage

### Configuration
You have to save a valid SSH connection first!

> Menu "Mono"/"SSH Configuration ..."

### Deploy
You can deploy your "Startup project" output to the remote machine via SSH configured under "Configuration".

> Menu "Mono"/"Deploy only"

###### Notice
To speed up deployment, SshFileSync is used.
https://github.com/GordianDotNet/SshFileSync

> To upload only changed files, an additional cache file '.uploadCache.cache' is stored in the destination folder. 
> Don't delete this cache file! 

### Debug
You can start a debug session in Visual Studio 2017 on the remote machine.

> Menu "Mono"/"Debug only"

### Deploy and Debug
You can run both commands in one step.

> Menu "Mono"/"Deploy and Debug"

# Known Issue
- [ ] Implementation is missing (initial git commit)

# Version History

## 0.1.0
**2018-01-20**

- [x] Start project

# Used resources

- [x] [Visual Studio Image Library 2017](https://www.microsoft.com/en-my/download/details.aspx?id=35825)