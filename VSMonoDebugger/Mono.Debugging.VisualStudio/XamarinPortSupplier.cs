using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using VSMonoDebugger.Services;
using VSMonoDebugger.Settings;

namespace Mono.Debugging.VisualStudio
{
    /// <summary>
    /// see https://github.com/microsoft/PTVS/blob/master/Python/Product/Debugger/Debugger/DebugEngine/Remote/PythonRemoteDebugPortSupplier.cs
    /// see https://github.com/microsoft/MIEngine/blob/master/src/SSHDebugPS/SSH/SSHPortSupplier.cs
    /// </summary>
    [Guid(DebugEngineGuids.XamarinProgramProviderString)]
    public class XamarinPortSupplier : IDebugPortSupplier2
    {
        public const string PortSupplierName = "SSH via VSMonoDebugger Extension";
        protected PortSupplier _portSupplier;
        private readonly List<IDebugPort2> _ports = new List<IDebugPort2>();

        public XamarinPortSupplier()
        {
            _portSupplier = new PortSupplier();
        }

        public int GetPortSupplierName(out string pbstrName)
        {
            NLogService.TraceEnteringMethod();
            pbstrName = PortSupplierName;
            return VSConstants.S_OK;
            //return ((IDebugPortSupplier2)_portSupplier).GetPortSupplierName(out pbstrName);
        }

        public int GetPortSupplierId(out Guid pguidPortSupplier)
        {
            NLogService.TraceEnteringMethod();
            //var result = ((IDebugPortSupplier2)_portSupplier).GetPortSupplierId(out pguidPortSupplier);
            pguidPortSupplier = new Guid(DebugEngineGuids.XamarinProgramProviderString);
            return VSConstants.S_OK;
            //return result;
        }

        public int GetPort(ref Guid guidPort, out IDebugPort2 ppPort)
        {
            NLogService.TraceEnteringMethod();
            // Never called, so this code has not been verified
            foreach (var port in _ports)
            {
                Guid currentGuid;
                if (port.GetPortId(out currentGuid) == VSConstants.S_OK && currentGuid == guidPort)
                {
                    ppPort = port;
                    return VSConstants.S_OK;
                }
            }
            ppPort = null;
            return VSConstants.S_FALSE;
            //return ((IDebugPortSupplier2)_portSupplier).GetPort(ref guidPort, out ppPort);
        }

        public int EnumPorts(out IEnumDebugPorts2 ppEnum)
        {
            NLogService.TraceEnteringMethod();
            ppEnum = new AD7DebugPortsEnum(_ports.ToArray());
            return VSConstants.S_OK;
            //return ((IDebugPortSupplier2)_portSupplier).EnumPorts(out ppEnum);
        }

        public int CanAddPort()
        {
            NLogService.TraceEnteringMethod();
            return VSConstants.S_OK;
            //return ((IDebugPortSupplier2)_portSupplier).CanAddPort();
        }

        public int AddPort(IDebugPortRequest2 pRequest, out IDebugPort2 ppPort)
        {
            NLogService.TraceEnteringMethod();
            string name;
            pRequest.GetPortName(out name);

            if (name == "Mono")
            {
                ppPort = PortSupplier.MainPort;
                return VSConstants.S_OK;
            }

            var userSettings = UserSettingsManager.Instance.Load();

            var setting = userSettings.DeviceConnections.Where(x => x.SSHFullUrl.Contains(name)).FirstOrDefault();

            var newPort = new VSMonoDebuggerSSHPort(this, pRequest, setting);

            //if (newPort.IsConnected)
            {
                ppPort = newPort;
                return VSConstants.S_OK;
            }

            //ppPort = null;
            //return VSConstants.S_FALSE;
            //return ((IDebugPortSupplier2)_portSupplier).AddPort(pRequest, out ppPort);
        }

        public int RemovePort(IDebugPort2 pPort)
        {
            NLogService.TraceEnteringMethod();
            // Never called, so this code has not been verified
            bool removed = _ports.Remove(pPort);
            return removed ? VSConstants.S_OK : VSConstants.S_FALSE;
            //return ((IDebugPortSupplier2)_portSupplier).RemovePort(pPort);
        }
    }

    public class AD7DebugPortsEnum : AD7Enum<IDebugPort2, IEnumDebugPorts2>, IEnumDebugPorts2
    {
        public AD7DebugPortsEnum(IDebugPort2[] ports)
            : base(ports)
        {

        }

        public int Next(uint celt, IDebugPort2[] rgelt, ref uint celtFetched)
        {
            return Next(celt, rgelt, out celtFetched);
        }
    }

    public class AD7ProcessEnum : AD7Enum<IDebugProcess2, IEnumDebugProcesses2>, IEnumDebugProcesses2
    {
        public AD7ProcessEnum(IDebugProcess2[] data) : base(data)
        {
        }

        public int Next(uint celt, IDebugProcess2[] elements, ref uint cFetched)
        {
            return base.Next(celt, elements, out cFetched);
        }
    }

    public class VSMonoDebuggerSSHPort : IDebugPort2
    {
        private readonly XamarinPortSupplier _supplier;
        private readonly IDebugPortRequest2 _request;
        private readonly Guid _guid = Guid.NewGuid();
        private readonly UserSettings _userSettings;
        private readonly List<VSMonoDebuggerProcess> _processes = new List<VSMonoDebuggerProcess>();

        public VSMonoDebuggerSSHPort(XamarinPortSupplier supplier, IDebugPortRequest2 request, UserSettings userSettings)
        {
            _supplier = supplier;
            _request = request;
            _userSettings = userSettings;
            _processes.Add(new VSMonoDebuggerProcess(this));
        }

        public UserSettings UserSetting
        {
            get { return _userSettings; }
        }

        public int EnumProcesses(out IEnumDebugProcesses2 ppEnum)
        {
            NLogService.TraceEnteringMethod();
            ppEnum = new AD7ProcessEnum(_processes.ToArray());
            return VSConstants.S_OK;
        }

        public int GetPortId(out Guid pguidPort)
        {
            NLogService.TraceEnteringMethod();
            pguidPort = _guid;
            return 0;
        }

        public int GetPortName(out string pbstrName)
        {
            NLogService.TraceEnteringMethod();
            pbstrName = _userSettings.SSHFullUrl;
            return VSConstants.S_OK;
        }

        public int GetPortRequest(out IDebugPortRequest2 ppRequest)
        {
            NLogService.TraceEnteringMethod();
            ppRequest = _request;
            return VSConstants.S_OK;
        }

        public int GetPortSupplier(out IDebugPortSupplier2 ppSupplier)
        {
            NLogService.TraceEnteringMethod();
            ppSupplier = _supplier;
            return VSConstants.S_OK;
        }

        public int GetProcess(AD_PROCESS_ID ProcessId, out IDebugProcess2 ppProcess)
        {
            NLogService.TraceEnteringMethod();
            throw new NotImplementedException();
        }
    }

    public class VSMonoDebuggerProcess : IDebugProcess2, IDebugProgram2
    {
        private VSMonoDebuggerSSHPort _port;
        private readonly Guid _guidProcessId = Guid.NewGuid();
        private readonly uint _processId;

        public VSMonoDebuggerSSHPort Port
        {
            get { return _port; }
        }

        public VSMonoDebuggerProcess(VSMonoDebuggerSSHPort port)
        {
            _port = port;
            _processId = 156;
        }

        public int GetInfo(enum_PROCESS_INFO_FIELDS fields, PROCESS_INFO[] pProcessInfo)
        {
            NLogService.TraceEnteringMethod();

            if ((fields & enum_PROCESS_INFO_FIELDS.PIF_FILE_NAME) != 0)
            {
                pProcessInfo[0].bstrFileName = GetFileName();
                pProcessInfo[0].Fields |= enum_PROCESS_INFO_FIELDS.PIF_FILE_NAME;
            }

            if ((fields & enum_PROCESS_INFO_FIELDS.PIF_BASE_NAME) != 0)
            {
                pProcessInfo[0].bstrBaseName = GetBaseName();
                pProcessInfo[0].Fields |= enum_PROCESS_INFO_FIELDS.PIF_BASE_NAME;
            }

            if ((fields & enum_PROCESS_INFO_FIELDS.PIF_TITLE) != 0)
            {
                string title = GetTitle();
                if (title != null)
                {
                    pProcessInfo[0].bstrTitle = title;
                    pProcessInfo[0].Fields |= enum_PROCESS_INFO_FIELDS.PIF_TITLE;
                }
            }

            if ((fields & enum_PROCESS_INFO_FIELDS.PIF_PROCESS_ID) != 0)
            {
                GetADProcessId(out pProcessInfo[0].ProcessId);
                pProcessInfo[0].Fields |= enum_PROCESS_INFO_FIELDS.PIF_PROCESS_ID;
            }

            if ((fields & enum_PROCESS_INFO_FIELDS.PIF_FLAGS) != 0)
            {
                pProcessInfo[0].Flags = 0;

                //if (!_isSameUser || !this.HasRealCommandLine)
                //{
                //    pProcessInfo[0].Flags |= enum_PROCESS_INFO_FLAGS.PIFLAG_SYSTEM_PROCESS;
                //}

                pProcessInfo[0].Fields |= enum_PROCESS_INFO_FIELDS.PIF_FLAGS;
            }

            return VSConstants.S_OK;
        }

        private string GetTitle()
        {
            return "monoTitle";
        }

        private string GetBaseName()
        {
            return "monoBaseName";
        }

        private string GetFileName()
        {
            return "mono process name";
        }

        public int EnumPrograms(out IEnumDebugPrograms2 ppEnum)
        {
            NLogService.TraceEnteringMethod();
            ppEnum = new AD7ProgramEnum(new VSMonoDebuggerProcess[] { this });
            return VSConstants.S_OK;
        }

        public int GetName(enum_GETNAME_TYPE gnType, out string pbstrName)
        {
            NLogService.TraceEnteringMethod();
            pbstrName = _port.ToString();
            return VSConstants.S_OK;
        }

        public int GetServer(out IDebugCoreServer2 ppServer)
        {
            NLogService.TraceEnteringMethod();
            throw new NotImplementedException();
        }

        public int Terminate()
        {
            NLogService.TraceEnteringMethod();
            throw new NotImplementedException();
        }

        public int Attach(IDebugEventCallback2 pCallback, Guid[] rgguidSpecificEngines, uint celtSpecificEngines, int[] rghrEngineAttach)
        {
            NLogService.TraceEnteringMethod();
            throw new NotImplementedException();
        }

        public int CanDetach()
        {
            NLogService.TraceEnteringMethod();
            throw new NotImplementedException();
        }

        public int Detach()
        {
            NLogService.TraceEnteringMethod();
            throw new NotImplementedException();
        }

        public int GetPhysicalProcessId(AD_PROCESS_ID[] pProcessId)
        {
            NLogService.TraceEnteringMethod();
            GetADProcessId(out pProcessId[0]);
            return VSConstants.S_OK;
        }

        private void GetADProcessId(out AD_PROCESS_ID processId)
        {
            NLogService.TraceEnteringMethod();
            processId = new AD_PROCESS_ID();
            processId.ProcessIdType = (uint)enum_AD_PROCESS_ID.AD_PROCESS_ID_SYSTEM;
            processId.dwProcessId = _processId;
            processId.guidProcessId = _guidProcessId;
        }

        public int GetProcessId(out Guid pguidProcessId)
        {
            NLogService.TraceEnteringMethod();
            pguidProcessId = _guidProcessId;
            return VSConstants.S_OK;
        }

        #region IDebugProgram2

        public int GetAttachedSessionName(out string pbstrSessionName)
        {
            NLogService.TraceEnteringMethod();
            throw new NotImplementedException();
        }

        public int EnumThreads(out IEnumDebugThreads2 ppEnum)
        {
            NLogService.TraceEnteringMethod();
            throw new NotImplementedException();
        }

        public int CauseBreak()
        {
            NLogService.TraceEnteringMethod();
            throw new NotImplementedException();
        }

        public int GetPort(out IDebugPort2 ppPort)
        {
            NLogService.TraceEnteringMethod();
            ppPort = _port;
            return VSConstants.S_OK;
        }

        public int GetName(out string pbstrName)
        {
            NLogService.TraceEnteringMethod();
            throw new NotImplementedException();
        }

        public int GetProcess(out IDebugProcess2 ppProcess)
        {
            NLogService.TraceEnteringMethod();
            throw new NotImplementedException();
        }

        public int Attach(IDebugEventCallback2 pCallback)
        {
            NLogService.TraceEnteringMethod();
            throw new NotImplementedException();
        }

        public int GetProgramId(out Guid pguidProgramId)
        {
            NLogService.TraceEnteringMethod();
            pguidProgramId = _guidProcessId;
            return VSConstants.S_OK;
            //throw new NotImplementedException();
        }

        public int GetDebugProperty(out IDebugProperty2 ppProperty)
        {
            NLogService.TraceEnteringMethod();
            throw new NotImplementedException();
        }

        public int Execute()
        {
            NLogService.TraceEnteringMethod();
            throw new NotImplementedException();
        }

        public int Continue(IDebugThread2 pThread)
        {
            NLogService.TraceEnteringMethod();
            throw new NotImplementedException();
        }

        public int Step(IDebugThread2 pThread, enum_STEPKIND sk, enum_STEPUNIT Step)
        {
            NLogService.TraceEnteringMethod();
            throw new NotImplementedException();
        }

        public int GetEngineInfo(out string pbstrEngine, out Guid pguidEngine)
        {
            NLogService.TraceEnteringMethod();
            pbstrEngine = "VSMonoDebugger mono debugger engine";
            pguidEngine = new Guid(DebugEngineGuids.XamarinEngineString);
            return VSConstants.S_OK;
            //throw new NotImplementedException();
        }

        public int EnumCodeContexts(IDebugDocumentPosition2 pDocPos, out IEnumDebugCodeContexts2 ppEnum)
        {
            NLogService.TraceEnteringMethod();
            throw new NotImplementedException();
        }

        public int GetMemoryBytes(out IDebugMemoryBytes2 ppMemoryBytes)
        {
            NLogService.TraceEnteringMethod();
            throw new NotImplementedException();
        }

        public int GetDisassemblyStream(enum_DISASSEMBLY_STREAM_SCOPE dwScope, IDebugCodeContext2 pCodeContext, out IDebugDisassemblyStream2 ppDisassemblyStream)
        {
            NLogService.TraceEnteringMethod();
            throw new NotImplementedException();
        }

        public int EnumModules(out IEnumDebugModules2 ppEnum)
        {
            NLogService.TraceEnteringMethod();
            throw new NotImplementedException();
        }

        public int GetENCUpdate(out object ppUpdate)
        {
            NLogService.TraceEnteringMethod();
            throw new NotImplementedException();
        }

        public int EnumCodePaths(string pszHint, IDebugCodeContext2 pStart, IDebugStackFrame2 pFrame, int fSource, out IEnumCodePaths2 ppEnum, out IDebugCodeContext2 ppSafety)
        {
            NLogService.TraceEnteringMethod();
            throw new NotImplementedException();
        }

        public int WriteDump(enum_DUMPTYPE DUMPTYPE, string pszDumpUrl)
        {
            NLogService.TraceEnteringMethod();
            throw new NotImplementedException();
        }

        #endregion
    }
}
