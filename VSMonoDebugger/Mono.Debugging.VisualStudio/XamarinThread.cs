using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.Debugger.Interop;
using System.Diagnostics;
using VSMonoDebugger;
using VSMonoDebugger.Services;
using Mono.Debugging.Soft;

namespace Mono.Debugging.VisualStudio
{
    class XamarinThread : IDebugThread2
    {
        readonly XamarinEngine _engine;
        readonly long _id;
        readonly string _name;
        readonly string _location;
        readonly SoftDebuggerSession _session;

        public XamarinThread(XamarinEngine engine, long id, string name, string location, SoftDebuggerSession session)
        {
            _engine = engine;
            _id = id;
            _name = name;
            _location = location;
            _session = session;
        }      

        #region IDebugThread2 Members

        // Determines whether the next statement can be set to the given stack frame and code context.
        int IDebugThread2.CanSetNextStatement(IDebugStackFrame2 stackFrame, IDebugCodeContext2 codeContext)
        {
            return VisualStudioExtensionConstants.S_FALSE;
        }

        // Retrieves a list of the stack frames for this thread.
        // For the sample engine, enumerating the stack frames requires walking the callstack in the debuggee for this thread
        // and coverting that to an implementation of IEnumDebugFrameInfo2. 
        // Real engines will most likely want to cache this information to avoid recomputing it each time it is asked for,
        // and or construct it on demand instead of walking the entire stack.
        int IDebugThread2.EnumFrameInfo(enum_FRAMEINFO_FLAGS dwFieldSpec, uint nRadix, out IEnumDebugFrameInfo2 enumObject)
        {
            enumObject = null;

            if(_session.ActiveThread.Id != _id)
            {
                return VisualStudioExtensionConstants.S_FALSE;
            }

            if (_session.ActiveThread.Backtrace.FrameCount > 0)
            {
                try
                {
                    FRAMEINFO[] frameInfoArray;

                    if (_session.ActiveThread.Backtrace.FrameCount == 1)
                    {
                        // failed to walk any frames. Only return the top frame.
                        frameInfoArray = new FRAMEINFO[1];
                        XamarinStackFrame frame = new XamarinStackFrame(_engine, this, _session.ActiveThread.Backtrace.GetFrame(0));
                        frameInfoArray[0] = frame.CreateFrameInfo(dwFieldSpec);
                    }
                    else
                    {
                        frameInfoArray = new FRAMEINFO[_session.ActiveThread.Backtrace.FrameCount];

                        for (int i = 0; i < _session.ActiveThread.Backtrace.FrameCount; i++)
                        {
                            XamarinStackFrame frame = new XamarinStackFrame(_engine, this, _session.ActiveThread.Backtrace.GetFrame(i));
                            frameInfoArray[i] = frame.CreateFrameInfo(dwFieldSpec);
                        }
                    }

                    enumObject = new XamarinFrameInfoEnum(frameInfoArray);
                    return VisualStudioExtensionConstants.S_OK;
                }
                catch (ComponentException e)
                {
                    return e.HResult;
                }
                catch (Exception e)
                {
                    NLogService.Logger.Error(e);
                    return VisualStudioExtensionConstants.S_FALSE;
                }
            }

            return VisualStudioExtensionConstants.S_FALSE;
        }

        // Get the name of the thread. For the sample engine, the name of the thread is always "Sample Engine Thread"
        int IDebugThread2.GetName(out string threadName)
        {
            threadName = _name;
            return VisualStudioExtensionConstants.S_OK;
        }

        // Return the program that this thread belongs to.
        int IDebugThread2.GetProgram(out IDebugProgram2 program)
        {
            program = _engine.ActiveProgram;
            return VisualStudioExtensionConstants.S_OK;
        }

        // Gets the system thread identifier.
        int IDebugThread2.GetThreadId(out uint threadId)
        {
            threadId = (uint)_id;
            return VisualStudioExtensionConstants.S_OK;
        }

        // Gets properties that describe a thread.
        int IDebugThread2.GetThreadProperties(enum_THREADPROPERTY_FIELDS dwFields, THREADPROPERTIES[] propertiesArray)
        {
            try
            {
                THREADPROPERTIES props = new THREADPROPERTIES();

                if ((dwFields & enum_THREADPROPERTY_FIELDS.TPF_ID) != 0)
                {
                    props.dwThreadId = (uint)_id;
                    props.dwFields |= enum_THREADPROPERTY_FIELDS.TPF_ID;
                }
                if ((dwFields & enum_THREADPROPERTY_FIELDS.TPF_SUSPENDCOUNT) != 0) 
                {
                    // sample debug engine doesn't support suspending threads
                    props.dwFields |= enum_THREADPROPERTY_FIELDS.TPF_SUSPENDCOUNT;
                }
                if ((dwFields & enum_THREADPROPERTY_FIELDS.TPF_STATE) != 0) 
                {
                    props.dwThreadState = (uint)enum_THREADSTATE.THREADSTATE_RUNNING;
                    props.dwFields |= enum_THREADPROPERTY_FIELDS.TPF_STATE;
                }
                if ((dwFields & enum_THREADPROPERTY_FIELDS.TPF_PRIORITY) != 0) 
                {
                    props.bstrPriority = "Normal";
                    props.dwFields |= enum_THREADPROPERTY_FIELDS.TPF_PRIORITY;
                }
                if ((dwFields & enum_THREADPROPERTY_FIELDS.TPF_NAME) != 0)
                {
                    props.bstrName = _name;
                    props.dwFields |= enum_THREADPROPERTY_FIELDS.TPF_NAME;
                }
                if ((dwFields & enum_THREADPROPERTY_FIELDS.TPF_LOCATION) != 0)
                {
                    props.bstrLocation = _location;
                    props.dwFields |= enum_THREADPROPERTY_FIELDS.TPF_LOCATION;
                }

                return VisualStudioExtensionConstants.S_OK;
            }
            catch (ComponentException e)
            {
                return e.HResult;
            }
            catch (Exception e)
            {
                NLogService.Logger.Error(e);
                return VisualStudioExtensionConstants.S_FALSE;
            }
        }

        // Resume a thread.
        // This is called when the user chooses "Unfreeze" from the threads window when a thread has previously been frozen.
        int IDebugThread2.Resume(out uint suspendCount)
        {
            suspendCount = 0;
            return VisualStudioExtensionConstants.E_NOTIMPL;
        }

        // Sets the next statement to the given stack frame and code context.
        int IDebugThread2.SetNextStatement(IDebugStackFrame2 stackFrame, IDebugCodeContext2 codeContext)
        {
            return VisualStudioExtensionConstants.E_NOTIMPL;
        }

        // suspend a thread.
        // This is called when the user chooses "Freeze" from the threads window
        int IDebugThread2.Suspend(out uint suspendCount)
        {
            suspendCount = 0;
            return VisualStudioExtensionConstants.E_NOTIMPL;
        }

        #endregion

        #region Uncalled interface methods
        // These methods are not currently called by the Visual Studio debugger, so they don't need to be implemented

        int IDebugThread2.GetLogicalThread(IDebugStackFrame2 stackFrame, out IDebugLogicalThread2 logicalThread)
        {
            Debug.Fail("This function is not called by the debugger");

            logicalThread = null;
            return VisualStudioExtensionConstants.E_NOTIMPL;
        }

        int IDebugThread2.SetThreadName(string name)
        {
            Debug.Fail("This function is not called by the debugger");
            
            return VisualStudioExtensionConstants.E_NOTIMPL;
        }

        #endregion
    }
}
