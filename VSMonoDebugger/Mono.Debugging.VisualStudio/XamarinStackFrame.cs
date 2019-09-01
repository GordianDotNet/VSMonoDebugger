using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.Debugger.Interop;
using System.Diagnostics;
using VSMonoDebugger;
using VSMonoDebugger.Services;

namespace Mono.Debugging.VisualStudio
{
    enum EnumDebugPropertyInfoContents
    {
        eNone = 0x0,
        eLocals = 0x1,
        eProperties = 0x2,
        eLocalsAndProperties = 0x3,
    }

    // Represents a logical stack frame on the thread stack. 
    // Also implements the IDebugExpressionContext interface, which allows expression evaluation and watch windows.
    class XamarinStackFrame : IDebugStackFrame2, IDebugExpressionContext2
    {
        readonly XamarinEngine _engine;
        readonly XamarinThread _thread;
        Mono.Debugging.Client.StackFrame _stackFrame;

        Mono.Debugging.Client.ObjectValue[] _parameters;
        Mono.Debugging.Client.ObjectValue[] _locals;     
        Mono.Debugging.Client.ObjectValue _thisObject;

        public XamarinStackFrame(XamarinEngine engine, XamarinThread thread, Mono.Debugging.Client.StackFrame stackFrame)
        {
            _engine = engine;
            _thread = thread;
            _stackFrame = stackFrame;

            _parameters = _stackFrame.GetParameters();
            _locals = _stackFrame.GetLocalVariables();
            _thisObject = _stackFrame.GetThisReference();
        }

        #region Non-interface methods

        // Construct a FRAMEINFO for this stack frame with the requested information.
        public FRAMEINFO CreateFrameInfo(enum_FRAMEINFO_FLAGS dwFieldSpec)
        {
            FRAMEINFO frameInfo = new FRAMEINFO();

            // The debugger is asking for the formatted name of the function which is displayed in the callstack window.
            // There are several optional parts to this name including the module, argument types and values, and line numbers.
            // The optional information is requested by setting flags in the dwFieldSpec parameter.
            if ((dwFieldSpec & enum_FRAMEINFO_FLAGS.FIF_FUNCNAME) != 0)
            {
                // If there is source information, construct a string that contains the module name, function name, and optionally argument names and values.
                frameInfo.m_bstrFuncName = "";

                if ((dwFieldSpec & enum_FRAMEINFO_FLAGS.FIF_FUNCNAME_MODULE) != 0)
                {
                    frameInfo.m_bstrFuncName = _stackFrame.FullModuleName + "!";
                }

                frameInfo.m_bstrFuncName += _stackFrame.SourceLocation.MethodName;

                if ((dwFieldSpec & enum_FRAMEINFO_FLAGS.FIF_FUNCNAME_ARGS) != 0 && _parameters.Length > 0)
                {
                    frameInfo.m_bstrFuncName += "(";
                    for (int i = 0; i < _parameters.Length; i++)
                    {
                        if ((dwFieldSpec & enum_FRAMEINFO_FLAGS.FIF_FUNCNAME_ARGS_TYPES) != 0)
                        {
                            frameInfo.m_bstrFuncName += _parameters[i].TypeName + " ";
                        }

                        if ((dwFieldSpec & enum_FRAMEINFO_FLAGS.FIF_FUNCNAME_ARGS_NAMES) != 0)
                        {
                            frameInfo.m_bstrFuncName += _parameters[i].Name;
                        }

                        if ((dwFieldSpec & enum_FRAMEINFO_FLAGS.FIF_FUNCNAME_ARGS_VALUES) != 0)
                        {
                            frameInfo.m_bstrFuncName += "=" + _parameters[i].Value;
                        }

                        if (i < _parameters.Length - 1)
                        {
                            frameInfo.m_bstrFuncName += ", ";
                        }
                    }
                    frameInfo.m_bstrFuncName += ")";
                }

                if ((dwFieldSpec & enum_FRAMEINFO_FLAGS.FIF_FUNCNAME_LINES) != 0)
                {
                    frameInfo.m_bstrFuncName += " Line:" + _stackFrame.SourceLocation.Line.ToString();
                }

                frameInfo.m_dwValidFields |= enum_FRAMEINFO_FLAGS.FIF_FUNCNAME;
            }

            // The debugger is requesting the name of the module for this stack frame.
            if ((dwFieldSpec & enum_FRAMEINFO_FLAGS.FIF_MODULE) != 0)
            {
                frameInfo.m_bstrModule = _stackFrame.FullModuleName;
                frameInfo.m_dwValidFields |=  enum_FRAMEINFO_FLAGS.FIF_MODULE;
            }

            // The debugger is requesting the range of memory addresses for this frame.
            // For the sample engine, this is the contents of the frame pointer.
            if ((dwFieldSpec &  enum_FRAMEINFO_FLAGS.FIF_STACKRANGE) != 0)
            {
                /*frameInfo.m_addrMin = m_threadContext.ebp;
                frameInfo.m_addrMax = m_threadContext.ebp;
                frameInfo.m_dwValidFields |= enum_FRAMEINFO_FLAGS.FIF_STACKRANGE;*/
            }

            // The debugger is requesting the IDebugStackFrame2 value for this frame info.
            if ((dwFieldSpec & enum_FRAMEINFO_FLAGS.FIF_FRAME) != 0)
            {
                frameInfo.m_pFrame = this;
                frameInfo.m_dwValidFields |= enum_FRAMEINFO_FLAGS.FIF_FRAME;
            }
            
            // Does this stack frame of symbols loaded?
            if ((dwFieldSpec & enum_FRAMEINFO_FLAGS.FIF_DEBUGINFO) != 0)
            {
                frameInfo.m_fHasDebugInfo = _stackFrame.HasDebugInfo ? 1 : 0;
                frameInfo.m_dwValidFields |= enum_FRAMEINFO_FLAGS.FIF_DEBUGINFO;
            }

            // Is this frame stale?
            if ((dwFieldSpec & enum_FRAMEINFO_FLAGS.FIF_STALECODE) != 0)
            {
                frameInfo.m_fStaleCode = 0;
                frameInfo.m_dwValidFields |= enum_FRAMEINFO_FLAGS.FIF_STALECODE;
            }

            // The debugger would like a pointer to the IDebugModule2 that contains this stack frame.
            /*if ((dwFieldSpec & enum_FRAMEINFO_FLAGS.FIF_DEBUG_MODULEP) != 0)
            {
                if (module != null)
                {
                    XamarinModule XamarinModule = (XamarinModule)module.Client;
                    Debug.Assert(XamarinModule != null);
                    frameInfo.m_pModule = XamarinModule;
                    frameInfo.m_dwValidFields |= enum_FRAMEINFO_FLAGS.FIF_DEBUG_MODULEP;
                }
            }*/

            return frameInfo;
        }

        // Construct an instance of IEnumDebugPropertyInfo2 for the combined locals and parameters.
        private void CreateLocalsPlusArgsProperties(EnumDebugPropertyInfoContents contents, out uint elementsReturned, out IEnumDebugPropertyInfo2 enumObject)
        {
            elementsReturned = 0;
            if ((contents & EnumDebugPropertyInfoContents.eLocals) != 0)
            {
                if (_thisObject != null)
                {
                    elementsReturned += elementsReturned + 1;
                }
                elementsReturned += (uint)_locals.Length;
            }
            if ((contents & EnumDebugPropertyInfoContents.eProperties) != 0)
            {
                elementsReturned += (uint)_parameters.Length;
            }

            DEBUG_PROPERTY_INFO[] propInfo = new DEBUG_PROPERTY_INFO[elementsReturned];
            int propInfoIdx = 0;

            if ((contents & EnumDebugPropertyInfoContents.eLocals) != 0)
            {
                if (_thisObject != null)
                {
                    propInfo[propInfoIdx++] = new XamarinProperty(_thisObject).ConstructDebugPropertyInfo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_STANDARD);
                }

                for (int i = 0; i < _locals.Length; i++)
                {
                    propInfo[propInfoIdx++] = new XamarinProperty(_locals[i]).ConstructDebugPropertyInfo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_STANDARD);
                }
            }

            if ((contents & EnumDebugPropertyInfoContents.eProperties) != 0)
            {
                for (int i = 0; i < _parameters.Length; i++)
                {
                    propInfo[propInfoIdx++] = new XamarinProperty(_parameters[i]).ConstructDebugPropertyInfo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_STANDARD);
                }
            }

            enumObject = new XamarinPropertyInfoEnum(propInfo);
        }

        #endregion

        #region IDebugStackFrame2 Members

        // Creates an enumerator for properties associated with the stack frame, such as local variables.
        // The sample engine only supports returning locals and parameters. Other possible values include
        // class fields (this pointer), registers, exceptions...
        int IDebugStackFrame2.EnumProperties(enum_DEBUGPROP_INFO_FLAGS dwFields, uint nRadix, ref Guid guidFilter, uint dwTimeout, out uint elementsReturned, out IEnumDebugPropertyInfo2 enumObject)
        {
            int hr;

            elementsReturned = 0;
            enumObject = null;
            
            try
            {
                if (guidFilter == DebugEngineGuids.guidFilterLocalsPlusArgs ||
                    guidFilter == DebugEngineGuids.guidFilterAllLocalsPlusArgs ||
                    guidFilter == DebugEngineGuids.guidFilterAllLocals)        
                {
                    CreateLocalsPlusArgsProperties(EnumDebugPropertyInfoContents.eLocalsAndProperties, out elementsReturned, out enumObject);
                    hr = VisualStudioExtensionConstants.S_OK;
                }
                else if (guidFilter == DebugEngineGuids.guidFilterLocals)
                {
                    CreateLocalsPlusArgsProperties(EnumDebugPropertyInfoContents.eLocals, out elementsReturned, out enumObject);
                    hr = VisualStudioExtensionConstants.S_OK;
                }
                else if (guidFilter == DebugEngineGuids.guidFilterArgs)
                {
                    CreateLocalsPlusArgsProperties(EnumDebugPropertyInfoContents.eLocalsAndProperties, out elementsReturned, out enumObject);
                    hr = VisualStudioExtensionConstants.S_OK;
                }
                else
                {
                    hr = VisualStudioExtensionConstants.E_NOTIMPL;
                }
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
            
            return hr;
        }

        // Gets the code context for this stack frame. The code context represents the current instruction pointer in this stack frame.
        int IDebugStackFrame2.GetCodeContext(out IDebugCodeContext2 memoryAddress)
        {
            memoryAddress = null;

            try
            {
                memoryAddress = new XamarinMemoryAddress(_engine, 0);
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

        // Gets a description of the properties of a stack frame.
        // Calling the IDebugProperty2::EnumChildren method with appropriate filters can retrieve the local variables, method parameters, registers, and "this" 
        // pointer associated with the stack frame. The debugger calls EnumProperties to obtain these values in the sample.
        int IDebugStackFrame2.GetDebugProperty(out IDebugProperty2 property)
        {
            throw new NotImplementedException();
        }

        // Gets the document context for this stack frame. The debugger will call this when the current stack frame is changed
        // and will use it to open the correct source document for this stack frame.
        int IDebugStackFrame2.GetDocumentContext(out IDebugDocumentContext2 docContext)
        {
            docContext = null;
            try
            {
                if (_stackFrame.HasDebugInfo)
                {
                    // Assume all lines begin and end at the beginning of the line.
                    TEXT_POSITION begTp = new TEXT_POSITION();
                    begTp.dwColumn = 0;
                    begTp.dwLine = (uint)(_stackFrame.SourceLocation.Line > 0 ? _stackFrame.SourceLocation.Line - 1 : 0);
                    TEXT_POSITION endTp = new TEXT_POSITION();
                    endTp.dwColumn = 0;
                    endTp.dwLine = (uint)(_stackFrame.SourceLocation.Line > 0 ? _stackFrame.SourceLocation.Line - 1 : 0);
                    
                    docContext = new XamarinDocumentContext(_stackFrame.SourceLocation.FileName, begTp, endTp, null);
                    return VisualStudioExtensionConstants.S_OK;
                }
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

            return VisualStudioExtensionConstants.S_FALSE;
        }

        // Gets an evaluation context for expression evaluation within the current context of a stack frame and thread.
        // Generally, an expression evaluation context can be thought of as a scope for performing expression evaluation. 
        // Call the IDebugExpressionContext2::ParseText method to parse an expression and then call the resulting IDebugExpression2::EvaluateSync 
        // or IDebugExpression2::EvaluateAsync methods to evaluate the parsed expression.
        int IDebugStackFrame2.GetExpressionContext(out IDebugExpressionContext2 ppExprCxt)
        {
            ppExprCxt = (IDebugExpressionContext2)this;
            return VisualStudioExtensionConstants.S_OK;
        }

        // Gets a description of the stack frame.
        int IDebugStackFrame2.GetInfo(enum_FRAMEINFO_FLAGS dwFieldSpec, uint nRadix, FRAMEINFO[] pFrameInfo)
        {
            try
            {
                pFrameInfo[0] = CreateFrameInfo(dwFieldSpec);

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

        // Gets the language associated with this stack frame. 
        // In this sample, all the supported stack frames are C++
        int IDebugStackFrame2.GetLanguageInfo(ref string pbstrLanguage, ref Guid pguidLanguage)
        {
            pbstrLanguage = "C#";
            pguidLanguage = DebugEngineGuids.guidLanguageCs;
            return VisualStudioExtensionConstants.S_OK;
        }

        // Gets the name of the stack frame.
        // The name of a stack frame is typically the name of the method being executed.
        int IDebugStackFrame2.GetName(out string name)
        {name = null;

            try
            {
                name = _stackFrame.SourceLocation.MethodName;

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

        // Gets a machine-dependent representation of the range of physical addresses associated with a stack frame.
        int IDebugStackFrame2.GetPhysicalStackRange(out ulong addrMin, out ulong addrMax)
        {
            addrMin = 0;
            addrMax = 1;
            return VisualStudioExtensionConstants.S_OK;
            /*
            addrMin = m_threadContext.ebp;
            addrMax = m_threadContext.ebp;

            return VisualStudioExtensionConstants.S_OK;*/
        }

        // Gets the thread associated with a stack frame.
        int IDebugStackFrame2.GetThread(out IDebugThread2 thread)
        {
            thread = _thread;
            return VisualStudioExtensionConstants.S_OK;
        }

        #endregion

        #region IDebugExpressionContext2 Members

        // Retrieves the name of the evaluation context. 
        // The name is the description of this evaluation context. It is typically something that can be parsed by an expression evaluator 
        // that refers to this exact evaluation context. For example, in C++ the name is as follows: 
        // "{ function-name, source-file-name, module-file-name }"
        int IDebugExpressionContext2.GetName(out string pbstrName)
        {
            throw new NotImplementedException();
        }

        private XamarinExpression ParseThisInternals(Mono.Debugging.Client.ObjectValue parent, string pszCode)
        {
            foreach (var currVariable in parent.GetAllChildren())
            {
                if (String.CompareOrdinal(currVariable.Name, "base") == 0 ||
                    String.CompareOrdinal(currVariable.Name, "Non-public members") == 0)
                {
                    var result = ParseThisInternals(currVariable, pszCode);
                    if (result != null)
                        return result;
                }
                if (String.CompareOrdinal(currVariable.Name, pszCode) == 0)
                {
                    return new XamarinExpression(_engine, _thread, currVariable);
                }
            }
            return null;
        }

        // Parses a text-based expression for evaluation.
        int IDebugExpressionContext2.ParseText(string pszCode,
                                                enum_PARSEFLAGS dwFlags, 
                                                uint nRadix, 
                                                out IDebugExpression2 ppExpr, 
                                                out string pbstrError, 
                                                out uint pichError)
        {
            pbstrError = "";
            pichError = 0;
            ppExpr = null;

            try
            {               
                // Check if the expression belongs to the parameters
                foreach (var currVariable in _parameters)
                {
                    if (String.CompareOrdinal(currVariable.Name, pszCode) == 0)
                    {
                        ppExpr = new XamarinExpression(_engine, _thread, currVariable);
                        return VisualStudioExtensionConstants.S_OK;
                    }
                }

                // Check if the expression belongs to the locals
                foreach (var currVariable in _locals)
                {
                    if (String.CompareOrdinal(currVariable.Name, pszCode) == 0)
                    {
                        ppExpr = new XamarinExpression(_engine, _thread, currVariable);
                        return VisualStudioExtensionConstants.S_OK;
                    }
                }

                if(_thisObject != null)
                {
                    // Are we looking for "this"?
                    if (String.CompareOrdinal("this", pszCode) == 0)
                    {
                        ppExpr = new XamarinExpression(_engine, _thread, _thisObject);
                        return VisualStudioExtensionConstants.S_OK;
                    }

                    // Lastly, check if it's a member of this 
                    var parsedExpression = ParseThisInternals(_thisObject, pszCode);
                    if(parsedExpression != null)
                    {
                        ppExpr = parsedExpression;
                        return VisualStudioExtensionConstants.S_OK;
                    }
                }

                pbstrError = "Invalid Expression";
                pichError = (uint)pbstrError.Length;
                return VisualStudioExtensionConstants.S_FALSE;
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

        #endregion
    }
}

