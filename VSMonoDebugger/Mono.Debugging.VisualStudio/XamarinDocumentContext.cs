using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.Debugger.Interop;
using System.Diagnostics;
using VSMonoDebugger;
using VSMonoDebugger.Services;

namespace Mono.Debugging.VisualStudio
{
    // This class represents a document context to the debugger. A document context represents a location within a source file. 
    class XamarinDocumentContext : IDebugDocumentContext2
    {
        string _fileName;
        TEXT_POSITION _begPos;
        TEXT_POSITION _endPos;
        XamarinMemoryAddress _codeContext;


        public XamarinDocumentContext(string fileName, TEXT_POSITION begPos, TEXT_POSITION endPos, XamarinMemoryAddress codeContext)
        {
            _fileName = fileName;
            _begPos = begPos;
            _endPos = endPos;
            _codeContext = codeContext;
        }


        #region IDebugDocumentContext2 Members

        // Compares this document context to a given array of document contexts.
        int IDebugDocumentContext2.Compare(enum_DOCCONTEXT_COMPARE Compare, IDebugDocumentContext2[] rgpDocContextSet, uint dwDocContextSetLen, out uint pdwDocContext)
        {
            dwDocContextSetLen = 0;
            pdwDocContext = 0;

            return VisualStudioExtensionConstants.E_NOTIMPL;
        }

        // Retrieves a list of all code contexts associated with this document context.
        int IDebugDocumentContext2.EnumCodeContexts(out IEnumDebugCodeContexts2 ppEnumCodeCxts)
        {
            ppEnumCodeCxts = null;
            try
            {
                XamarinMemoryAddress[] codeContexts = new XamarinMemoryAddress[1];
                codeContexts[0] = _codeContext;
                ppEnumCodeCxts = new XamarinCodeContextEnum(codeContexts);
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

        // Gets the document that contains this document context.
        // This method is for those debug engines that supply documents directly to the IDE.
        int IDebugDocumentContext2.GetDocument(out IDebugDocument2 ppDocument)
        {           
            ppDocument = null;
            return VisualStudioExtensionConstants.E_FAIL;
        }

        // Gets the language associated with this document context. We assume C#
        int IDebugDocumentContext2.GetLanguageInfo(ref string pbstrLanguage, ref Guid pguidLanguage)
        {
            pbstrLanguage = "C#";
            pguidLanguage = DebugEngineGuids.guidLanguageCs;
            return VisualStudioExtensionConstants.S_OK;
        }

        // Gets the displayable name of the document that contains this document context.
        int IDebugDocumentContext2.GetName(enum_GETNAME_TYPE gnType, out string pbstrFileName)
        {
            pbstrFileName = _fileName;
            return VisualStudioExtensionConstants.S_OK;
        }

        // Gets the source code range of this document context.
        // A source range is the entire range of source code, from the current statement back to just after the previous s
        // statement that contributed code. The source range is typically used for mixing source statements, including 
        // comments, with code in the disassembly window.
        // Sincethis engine does not support the disassembly window, this is not implemented.
        int IDebugDocumentContext2.GetSourceRange(TEXT_POSITION[] pBegPosition, TEXT_POSITION[] pEndPosition)
        {
            throw new NotImplementedException("This method is not implemented");
        }

        // Gets the file statement range of the document context.
        // A statement range is the range of the lines that contributed the code to which this document context refers.
        int IDebugDocumentContext2.GetStatementRange(TEXT_POSITION[] pBegPosition, TEXT_POSITION[] pEndPosition)
        {
            try
            {
                pBegPosition[0].dwColumn = _begPos.dwColumn;
                pBegPosition[0].dwLine = _begPos.dwLine;

                pEndPosition[0].dwColumn = _endPos.dwColumn;
                pEndPosition[0].dwLine = _endPos.dwLine;
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

            return VisualStudioExtensionConstants.S_OK;
        }

        // Moves the document context by a given number of statements or lines.
        // This is used primarily to support the Autos window in discovering the proximity statements around 
        // this document context. 
        int IDebugDocumentContext2.Seek(int nCount, out IDebugDocumentContext2 ppDocContext)
        {
            ppDocContext = null;
            return VisualStudioExtensionConstants.E_NOTIMPL;
        }

        #endregion
    }
}
