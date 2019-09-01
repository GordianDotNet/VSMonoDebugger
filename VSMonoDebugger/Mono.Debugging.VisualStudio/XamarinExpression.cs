using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.Debugger.Interop;
using VSMonoDebugger;

namespace Mono.Debugging.VisualStudio
{
    // This class represents a succesfully parsed expression to the debugger. 
    // It is returned as a result of a successful call to IDebugExpressionContext2.ParseText
    // It allows the debugger to obtain the values of an expression in the debuggee. 
    class XamarinExpression : IDebugExpression2
    {
        readonly XamarinEngine _engine;
        readonly XamarinThread _thread;

        Mono.Debugging.Client.ObjectValue _var;
        System.Threading.Thread _asyncEval;

        public XamarinExpression(XamarinEngine engine, XamarinThread thread, Mono.Debugging.Client.ObjectValue var)
        {
            _var = var;
            _asyncEval = null;
            _engine = engine;
            _thread = thread;
        }

        #region IDebugExpression2 Members

        // This method cancels asynchronous expression evaluation as started by a call to the IDebugExpression2::EvaluateAsync method.
        int IDebugExpression2.Abort()
        {
            _asyncEval.Abort();
            _asyncEval = null;
            return VisualStudioExtensionConstants.S_OK;
        }

        // This method evaluates the expression asynchronously.
        // This method should return immediately after it has started the expression evaluation. 
        // When the expression is successfully evaluated, an IDebugExpressionEvaluationCompleteEvent2 
        // must be sent to the IDebugEventCallback2 event callback
        int IDebugExpression2.EvaluateAsync(enum_EVALFLAGS dwFlags, IDebugEventCallback2 pExprCallback)
        {
            if(pExprCallback == null)
                return VisualStudioExtensionConstants.S_FALSE;

            if (_asyncEval == null || _asyncEval.ThreadState != System.Threading.ThreadState.Running)
            {
                _asyncEval = new System.Threading.Thread(() =>
                {
                    uint attributes;
                    Guid riidEvent = new Guid(XamarinExpressionEvaluationCompleteEvent.IID);
                    IDebugExpressionEvaluationCompleteEvent2 evnt = new XamarinExpressionEvaluationCompleteEvent(this, new XamarinProperty(_var));
                    IDebugEvent2 eventObject = evnt as IDebugEvent2;
                    if (eventObject.GetAttributes(out attributes) != VisualStudioExtensionConstants.S_OK)
                        throw new InvalidOperationException("Failed to create and register a thread. The event object failed to get its attributes");
                    if (pExprCallback.Event(_engine.MonoEngine, null, _engine.ActiveProgram, _thread, eventObject, ref riidEvent, attributes) != VisualStudioExtensionConstants.S_OK)
                        throw new InvalidOperationException("Failed to create and register a thread. The event has not been sent succesfully");
                });
                _asyncEval.Start();
                return VisualStudioExtensionConstants.S_OK;
            }
            return VisualStudioExtensionConstants.S_FALSE;
        }

        // This method evaluates the expression synchronously.
        int IDebugExpression2.EvaluateSync(enum_EVALFLAGS dwFlags, uint dwTimeout, IDebugEventCallback2 pExprCallback, out IDebugProperty2 ppResult)
        {
            ppResult = new XamarinProperty(_var);
            return VisualStudioExtensionConstants.S_OK;
        }

        #endregion
    }
}