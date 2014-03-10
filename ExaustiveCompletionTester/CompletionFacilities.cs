using D_Parser;
using D_Parser.Completion;
using D_Parser.Dom;
using D_Parser.Misc;
using D_Parser.Parser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExaustiveCompletionTester
{
	public class CompletionFacilities
	{
		public static DModule objMod = DParser.ParseString(@"module object;
						alias immutable(char)[] string;
						alias immutable(wchar)[] wstring;
						alias immutable(dchar)[] dstring;
						class Object { string toString(); }
						alias int size_t;");

		public static ParseCacheView CreateCache(params string[] moduleCodes)
		{
			var r = new MutableRootPackage(objMod);

			foreach (var code in moduleCodes)
				r.AddModule(DParser.ParseString(code));

			return new ParseCacheView(new[] { r });
		}

		public static EditorData GenEditorData(int caretLine, int caretPos, string focusedModuleCode, params string[] otherModuleCodes)
		{
			var cache = CreateCache(otherModuleCodes);
			var ed = new EditorData { ParseCache = cache };

			UpdateEditorData(ed, caretLine, caretPos, focusedModuleCode);

			return ed;
		}

		public static void UpdateEditorData(EditorData ed, int caretLine, int caretPos, string focusedModuleCode)
		{
			var mod = DParser.ParseString(focusedModuleCode);
			var pack = ed.ParseCache[0] as MutableRootPackage;

			pack.AddModule(mod);

			ed.ModuleCode = focusedModuleCode;
			ed.SyntaxTree = mod;
			ed.CaretLocation = new CodeLocation(caretPos, caretLine);
			ed.CaretOffset = DocumentHelper.LocationToOffset(focusedModuleCode, caretLine, caretPos);
		}
	}
}
