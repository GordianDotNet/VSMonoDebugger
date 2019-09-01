using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.Debugger.Interop;
using VSMonoDebugger;

namespace Mono.Debugging.VisualStudio
{
    #region Event base classes

    class XamarinAsynchronousEvent : IDebugEvent2
    {
        public const uint Attributes = (uint)enum_EVENTATTRIBUTES.EVENT_ASYNCHRONOUS;

        int IDebugEvent2.GetAttributes(out uint eventAttributes)
        {
            eventAttributes = Attributes;
            return VisualStudioExtensionConstants.S_OK;
        }
    }
    
    #endregion
    
    sealed class XamarinExpressionEvaluationCompleteEvent : XamarinAsynchronousEvent, IDebugExpressionEvaluationCompleteEvent2
    {
        public const string IID = "C0E13A85-238A-4800-8315-D947C960A843";

        XamarinExpression _expression;
        XamarinProperty _property;
        public XamarinExpressionEvaluationCompleteEvent(XamarinExpression expression, XamarinProperty property)
        {
            _expression = expression;
            _property = property;
        }

        public int GetExpression(out IDebugExpression2 ppExpr)
        {
            ppExpr = _expression;
            return VisualStudioExtensionConstants.S_OK;
        }

        public int GetResult(out IDebugProperty2 ppResult)
        {
            ppResult = _property;
            return VisualStudioExtensionConstants.S_OK;
        }
    }

    // This interface is sent by the debug engine (DE) to the session debug manager (SDM) when a thread is created in a program being debugged.
    sealed class XamarinThreadCreateEvent : XamarinAsynchronousEvent, IDebugThreadCreateEvent2
    {
        public const string IID = "2090CCFC-70C5-491D-A5E8-BAD2DD9EE3EA";
    }

    // This interface is sent by the debug engine (DE) to the session debug manager (SDM) when a thread has exited.
    sealed class XamarinThreadDestroyEvent : XamarinAsynchronousEvent, IDebugThreadDestroyEvent2
    {
        public const string IID = "2C3B7532-A36F-4A6E-9072-49BE649B8541";

        readonly uint m_exitCode;
        public XamarinThreadDestroyEvent(uint exitCode)
        {
            m_exitCode = exitCode;
        }

        #region IDebugThreadDestroyEvent2 Members

        int IDebugThreadDestroyEvent2.GetExitCode(out uint exitCode)
        {
            exitCode = m_exitCode;
            
            return VisualStudioExtensionConstants.S_OK;
        }

        #endregion
    }
}