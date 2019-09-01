using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.Debugger.Interop;
using VSMonoDebugger;
using VSMonoDebugger.Services;

namespace Mono.Debugging.VisualStudio
{
    class XamarinMemoryAddress : IDebugCodeContext2
    {
        readonly XamarinEngine _engine;
        readonly uint _address;
        IDebugDocumentContext2 _documentContext;
        
        public XamarinMemoryAddress(XamarinEngine engine, uint address)
        {
            _engine = engine;
            _address = address;
        }

        public void SetDocumentContext(IDebugDocumentContext2 docContext)
        {
            _documentContext = docContext;
        }

        #region IDebugCodeContext2 Members

        // Gets the document context for this code-context
        public int GetDocumentContext(out IDebugDocumentContext2 ppSrcCxt)
        {
            ppSrcCxt = _documentContext;
            return VisualStudioExtensionConstants.S_OK;
        }

        // Gets the language information for this code context.
        public int GetLanguageInfo(ref string pbstrLanguage, ref Guid pguidLanguage)
        {
            if (_documentContext != null)
            {
                _documentContext.GetLanguageInfo(ref pbstrLanguage, ref pguidLanguage);
                return VisualStudioExtensionConstants.S_OK;
            }
            else
            {
                return VisualStudioExtensionConstants.S_FALSE;
            }
        }

        // Adds a specified value to the current context's address to create a new context.
        public int Add(ulong dwCount, out IDebugMemoryContext2 newAddress)
        {
            newAddress = new XamarinMemoryAddress(_engine, (uint)dwCount + _address);
            return VisualStudioExtensionConstants.S_OK;
        }

        // Compares the memory context to each context in the given array in the manner indicated by compare flags, 
        // returning an index of the first context that matches.
        public int Compare(enum_CONTEXT_COMPARE uContextCompare, IDebugMemoryContext2[] compareToItems, uint compareToLength, out uint foundIndex)
        {
            foundIndex = uint.MaxValue;

            try
            {
                enum_CONTEXT_COMPARE contextCompare = (enum_CONTEXT_COMPARE)uContextCompare;

                for (uint c = 0; c < compareToLength; c++)
                {
                    XamarinMemoryAddress compareTo = compareToItems[c] as XamarinMemoryAddress;
                    if (compareTo == null)
                    {
                        continue;
                    }

                    if (!XamarinEngine.ReferenceEquals(this._engine, compareTo._engine))
                    {
                        continue;
                    }

                    bool result;

                    switch (contextCompare)
                    {
                        case enum_CONTEXT_COMPARE.CONTEXT_EQUAL:
                            result = (this._address == compareTo._address);
                            break;

                        case enum_CONTEXT_COMPARE.CONTEXT_LESS_THAN:
                            result = (this._address < compareTo._address);
                            break;

                        case enum_CONTEXT_COMPARE.CONTEXT_GREATER_THAN:
                            result = (this._address > compareTo._address);
                            break;

                        case enum_CONTEXT_COMPARE.CONTEXT_LESS_THAN_OR_EQUAL:
                            result = (this._address <= compareTo._address);
                            break;

                        case enum_CONTEXT_COMPARE.CONTEXT_GREATER_THAN_OR_EQUAL:
                            result = (this._address >= compareTo._address);
                            break;

                        // The sample debug engine doesn't understand scopes or functions
                        case enum_CONTEXT_COMPARE.CONTEXT_SAME_SCOPE:
                        case enum_CONTEXT_COMPARE.CONTEXT_SAME_FUNCTION:
                            result = (this._address == compareTo._address);
                            break;

                        case enum_CONTEXT_COMPARE.CONTEXT_SAME_MODULE:
                            result = (this._address == compareTo._address);
                            /*if (result == false)
                            {
                                DebuggedModule module = m_engine.DebuggedProcess.ResolveAddress(m_address);

                                if (module != null)
                                {
                                    result = (compareTo.m_address >= module.BaseAddress) &&
                                        (compareTo.m_address < module.BaseAddress + module.Size);
                                }
                            }*/
                            break;

                        case enum_CONTEXT_COMPARE.CONTEXT_SAME_PROCESS:
                            result = true;
                            break;

                        default:
                            // A new comparison was invented that we don't support
                            return VisualStudioExtensionConstants.E_NOTIMPL;
                    }

                    if (result)
                    {
                        foundIndex = c;
                        return VisualStudioExtensionConstants.S_OK;
                    }
                }

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

        // Gets information that describes this context.
        public int GetInfo(enum_CONTEXT_INFO_FIELDS dwFields, CONTEXT_INFO[] pinfo)
        {
            try
            {
                pinfo[0].dwFields = 0;

                if ((dwFields & enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS) != 0)
                {
                    pinfo[0].bstrAddress = _address.ToString();
                    pinfo[0].dwFields |= enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS;
                }

                // Fields not supported by the sample
                if ((dwFields & enum_CONTEXT_INFO_FIELDS.CIF_ADDRESSOFFSET) != 0) { }
                if ((dwFields & enum_CONTEXT_INFO_FIELDS.CIF_ADDRESSABSOLUTE) != 0) { }
                if ((dwFields & enum_CONTEXT_INFO_FIELDS.CIF_MODULEURL) != 0) { }
                if ((dwFields & enum_CONTEXT_INFO_FIELDS.CIF_FUNCTION) != 0) { }
                if ((dwFields & enum_CONTEXT_INFO_FIELDS.CIF_FUNCTIONOFFSET) != 0) { }

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

        // Gets the user-displayable name for this context
        // This is not supported by the sample engine.
        public int GetName(out string pbstrName)
        {
            throw new NotImplementedException();
        }

        // Subtracts a specified value from the current context's address to create a new context.
        public int Subtract(ulong dwCount, out IDebugMemoryContext2 ppMemCxt)
        {
            ppMemCxt = new XamarinMemoryAddress(_engine, (uint)dwCount - _address);
            return VisualStudioExtensionConstants.S_OK;
        }

        #endregion
    }
}
