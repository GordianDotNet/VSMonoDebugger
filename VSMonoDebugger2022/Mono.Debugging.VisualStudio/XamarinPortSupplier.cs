using Microsoft.VisualStudio.Debugger.Interop;
using System;
using System.Runtime.InteropServices;
using VSMonoDebugger.Services;

namespace Mono.Debugging.VisualStudio
{
    [Guid(DebugEngineGuids.XamarinProgramProviderString)]
    public class XamarinPortSupplier : IDebugPortSupplier2
    {
        protected IDebugPortSupplier2 _portSupplier;
        public XamarinPortSupplier()
        {
            //_portSupplier = new PortSupplier();

            _portSupplier = XamarinAssemblyFacade.CreatePortSupplier();
        }

        public int GetPortSupplierName(out string pbstrName)
        {
            return _portSupplier.GetPortSupplierName(out pbstrName);
        }

        public int GetPortSupplierId(out Guid pguidPortSupplier)
        {
            var result = _portSupplier.GetPortSupplierId(out pguidPortSupplier);
            pguidPortSupplier = new Guid(DebugEngineGuids.XamarinProgramProviderString);
            return result;
        }

        public int GetPort(ref Guid guidPort, out IDebugPort2 ppPort)
        {
            return _portSupplier.GetPort(ref guidPort, out ppPort);
        }

        public int EnumPorts(out IEnumDebugPorts2 ppEnum)
        {
            return _portSupplier.EnumPorts(out ppEnum);
        }

        public int CanAddPort()
        {
            return _portSupplier.CanAddPort();
        }

        public int AddPort(IDebugPortRequest2 pRequest, out IDebugPort2 ppPort)
        {
            return _portSupplier.AddPort(pRequest, out ppPort);
        }

        public int RemovePort(IDebugPort2 pPort)
        {
            return _portSupplier.RemovePort(pPort);
        }
    }
}
