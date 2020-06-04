using System.Threading;
using D_Parser;
using D_Parser.Dom;
using D_Parser.Parser;
using NUnit.Framework;

namespace Tests.Completion
{
    public static class TestUtil
    {
        /// <summary>
        /// Use § as caret indicator!
        /// </summary>
        public static TestsEditorData GenEditorData(string focusedModuleCode, params string[] otherModuleCodes)
        {
            int caretOffset = focusedModuleCode.IndexOf('§');
            Assert.IsTrue(caretOffset != -1);
            focusedModuleCode = focusedModuleCode.Substring(0, caretOffset) +
                                focusedModuleCode.Substring(caretOffset + 1);
            var caret = DocumentHelper.OffsetToLocation(focusedModuleCode, caretOffset);

            return GenEditorData(caret.Line, caret.Column, focusedModuleCode, otherModuleCodes);
        }

        public static TestsEditorData GenEditorData(int caretLine, int caretPos,string focusedModuleCode,params string[] otherModuleCodes)
        {
            var cache = ResolutionTestHelper.CreateCache (out _, otherModuleCodes);
            var ed = new TestsEditorData { ParseCache = cache };
            ed.CancelToken = CancellationToken.None;

            UpdateEditorData (ed, caretLine, caretPos, focusedModuleCode);

            return ed;
        }
        
        public static void UpdateEditorData(TestsEditorData ed,int caretLine, int caretPos, string focusedModuleCode)
        {
            var mod = DParser.ParseString (focusedModuleCode);

            ed.MainPackage.AddModule (mod);

            ed.ModuleCode = focusedModuleCode;
            ed.SyntaxTree = mod;
            ed.CaretLocation = new CodeLocation (caretPos, caretLine);
            ed.CaretOffset = DocumentHelper.LocationToOffset (focusedModuleCode, caretLine, caretPos);
        }
    }
}