using System;
using System.Threading.Tasks;
using VSMonoDebugger.Settings;

namespace VSMonoDebugger.Debuggers
{
    public interface IDebugger
    {
        Task<bool> DeployRunAndDebugAsync(DebugOptions debugOptions, Func<string, Task> writeOutput, RedirectOutputOptions redirectOutputOption = RedirectOutputOptions.None);

        Task<bool> DeployAsync(DebugOptions debugOptions, Func<string, Task> writeOutput, RedirectOutputOptions redirectOutputOption = RedirectOutputOptions.None);

        Task<bool> RunAndDebugAsync(DebugOptions debugOptions, Func<string, Task> writeOutput, RedirectOutputOptions redirectOutputOption = RedirectOutputOptions.None);
    }
}
