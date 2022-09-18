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

            var clsid = "{E8978DA6-047F-4E3D-9C78-CDBE46041603}";
            var type = Type.GetTypeFromCLSID(new Guid(clsid));
            var portSupplier = Activator.CreateInstance(type, true);
            _portSupplier = portSupplier as IDebugPortSupplier2;
        }

        public int GetPortSupplierName(out string pbstrName)
        {
            return ((IDebugPortSupplier2)_portSupplier).GetPortSupplierName(out pbstrName);
        }

        public int GetPortSupplierId(out Guid pguidPortSupplier)
        {
            var result = ((IDebugPortSupplier2)_portSupplier).GetPortSupplierId(out pguidPortSupplier);
            pguidPortSupplier = new Guid(DebugEngineGuids.XamarinProgramProviderString);
            return result;
        }

        public int GetPort(ref Guid guidPort, out IDebugPort2 ppPort)
        {
            return ((IDebugPortSupplier2)_portSupplier).GetPort(ref guidPort, out ppPort);
        }

        public int EnumPorts(out IEnumDebugPorts2 ppEnum)
        {
            return ((IDebugPortSupplier2)_portSupplier).EnumPorts(out ppEnum);
        }

        public int CanAddPort()
        {
            return ((IDebugPortSupplier2)_portSupplier).CanAddPort();
        }

        public int AddPort(IDebugPortRequest2 pRequest, out IDebugPort2 ppPort)
        {
            return ((IDebugPortSupplier2)_portSupplier).AddPort(pRequest, out ppPort);
        }

        public int RemovePort(IDebugPort2 pPort)
        {
            return ((IDebugPortSupplier2)_portSupplier).RemovePort(pPort);
        }
    }
}
