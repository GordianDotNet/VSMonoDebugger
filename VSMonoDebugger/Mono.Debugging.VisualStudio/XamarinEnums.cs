using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.Debugger.Interop;
using VSMonoDebugger;

namespace Mono.Debugging.VisualStudio
{
    #region Base Class
    class XamarinEnum<T,I> where I: class
    {
        readonly T[] m_data;
        uint m_position;

        public XamarinEnum(T[] data)
        {
            m_data = data;
            m_position = 0;
        }

        public int Clone(out I ppEnum)
        {
            ppEnum = null;
            return VisualStudioExtensionConstants.E_NOTIMPL;
        }

        public int GetCount(out uint pcelt)
        {
            pcelt = (uint)m_data.Length;
            return VisualStudioExtensionConstants.S_OK;
        }

        public int Next(uint celt, T[] rgelt, out uint celtFetched)
        {
            return Move(celt, rgelt, out celtFetched);
        }

        public int Reset()
        {
            lock (this)
            {
                m_position = 0;

                return VisualStudioExtensionConstants.S_OK;
            }
        }

        public int Skip(uint celt)
        {
            uint celtFetched;

            return Move(celt, null, out celtFetched);
        }

        private int Move(uint celt, T[] rgelt, out uint celtFetched)
        {
            lock (this)
            {
                int hr = VisualStudioExtensionConstants.S_OK;
                celtFetched = (uint)m_data.Length - m_position;

                if (celt > celtFetched)
                {
                    hr = VisualStudioExtensionConstants.S_FALSE;
                }
                else if (celt < celtFetched)
                {
                    celtFetched = celt;
                }

                if (rgelt != null)
                {
                    for (int c = 0; c < celtFetched; c++)
                    {
                        rgelt[c] = m_data[m_position + c];
                    }
                }

                m_position += celtFetched;

                return hr;
            }
        }
    }
    #endregion Base Class

    class XamarinProgramEnum : XamarinEnum<IDebugProgram2, IEnumDebugPrograms2>, IEnumDebugPrograms2
    {
        public XamarinProgramEnum(IDebugProgram2[] data) : base(data)
        {
        }

        public int Next(uint celt, IDebugProgram2[] rgelt, ref uint celtFetched)
        {
            return Next(celt, rgelt, out celtFetched);
        }
    }

    class XamarinFrameInfoEnum : XamarinEnum<FRAMEINFO, IEnumDebugFrameInfo2>, IEnumDebugFrameInfo2
    {
        public XamarinFrameInfoEnum(FRAMEINFO[] data)
            : base(data)
        {
        }

        public int Next(uint celt, FRAMEINFO[] rgelt, ref uint celtFetched)
        {
            return Next(celt, rgelt, out celtFetched);
        }
    }

    class XamarinPropertyInfoEnum : XamarinEnum<DEBUG_PROPERTY_INFO, IEnumDebugPropertyInfo2>, IEnumDebugPropertyInfo2
    {
        public XamarinPropertyInfoEnum(DEBUG_PROPERTY_INFO[] data)
            : base(data)
        {
        }
    }

    class XamarinThreadEnum : XamarinEnum<IDebugThread2, IEnumDebugThreads2>, IEnumDebugThreads2
    {
        public XamarinThreadEnum(IDebugThread2[] threads)
            : base(threads)
        {
            
        }

        public int Next(uint celt, IDebugThread2[] rgelt, ref uint celtFetched)
        {
            return Next(celt, rgelt, out celtFetched);
        }
    }

    class XamarinModuleEnum : XamarinEnum<IDebugModule2, IEnumDebugModules2>, IEnumDebugModules2
    {
        public XamarinModuleEnum(IDebugModule2[] modules)
            : base(modules)
        {

        }

        public int Next(uint celt, IDebugModule2[] rgelt, ref uint celtFetched)
        {
            return Next(celt, rgelt, out celtFetched);
        }
    }

    class XamarinPropertyEnum : XamarinEnum<DEBUG_PROPERTY_INFO, IEnumDebugPropertyInfo2>, IEnumDebugPropertyInfo2
    {
        public XamarinPropertyEnum(DEBUG_PROPERTY_INFO[] properties)
            : base(properties)
        {

        }
    }

    class XamarinCodeContextEnum : XamarinEnum<IDebugCodeContext2, IEnumDebugCodeContexts2>, IEnumDebugCodeContexts2
    {
        public XamarinCodeContextEnum(IDebugCodeContext2[] codeContexts)
            : base(codeContexts)
        {

        }

        public int Next(uint celt, IDebugCodeContext2[] rgelt, ref uint celtFetched)
        {
            return Next(celt, rgelt, out celtFetched);
        }
    }

    class XamarinBoundBreakpointsEnum : XamarinEnum<IDebugBoundBreakpoint2, IEnumDebugBoundBreakpoints2>, IEnumDebugBoundBreakpoints2
    {
        public XamarinBoundBreakpointsEnum(IDebugBoundBreakpoint2[] breakpoints)
            : base(breakpoints)
        {

        }

        public int Next(uint celt, IDebugBoundBreakpoint2[] rgelt, ref uint celtFetched)
        {
            return Next(celt, rgelt, out celtFetched);
        }
    }

    class XamarinErrorBreakpointsEnum : XamarinEnum<IDebugErrorBreakpoint2, IEnumDebugErrorBreakpoints2>, IEnumDebugErrorBreakpoints2
    {
        public XamarinErrorBreakpointsEnum(IDebugErrorBreakpoint2[] breakpoints)
            : base(breakpoints)
        {

        }

        public int Next(uint celt, IDebugErrorBreakpoint2[] rgelt, ref uint celtFetched)
        {
            return Next(celt, rgelt, out celtFetched);
        }
    }
}
