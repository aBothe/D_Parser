using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using D_Parser;
using D_Parser.Completion;
using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Misc;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.TypeResolution;
using NUnit.Framework;
using Tests.Resolution;

namespace Tests.Completion
{
	/// <summary>
	/// Description of CompletionTests.
	/// </summary>
	[TestFixture]
	public class CompletionTests
	{
		[Test]
		public void ForStatement()
		{
			var code = @"module A; // 1
void main(){
	for(
		int
		i= // 5
		0;
		i<100;
		i++)
		
	{ // 10
	}";
			var mod = DParser.ParseString(code);
			var main = mod["main"].First() as DMethod;
			
			var l = new CodeLocation(1,4);
			var off = DocumentHelper.LocationToOffset(code,l);
			CodeLocation caretLoc;
			//var parsedBlock = AbstractCompletionProvider.FindCurrentCaretContext(code,main, off,l,out ptr, out caretLoc);
			
			/*
			 * a) Test if completion popup would open in each code situation
			 * b) Test in which case iteration variables can be found.
			 */
			
			//TODO
		}

		[Test]
		public void ModuleCompletion()
		{
			var code = @"module §
";

			var ed = GenEditorData (code);
			ed.SyntaxTree.ModuleName = "asdf";
			TestCompletionListContents (ed, new[]{ ed.SyntaxTree }, new INode[0]);
		}

		[Test]
		public void MemberCompletion()
		{
			var code = @"module A;
int foo;
class K { int member; }
void main() { 
}";

			var ed = GenEditorData(5, 9, code);

			ed.ModuleCode = @"module A;
int foo;
class K { int member; }
void main() {
new K(). 
}";
			ed.CaretOffset = DocumentHelper.LocationToOffset(ed.ModuleCode, ed.CaretLocation);

			var gen = TestCompletionListContents(ed, null, null);

			var foo = ed.SyntaxTree["foo"].First() as DVariable;
			Assert.IsFalse(gen.AddedItems.Contains(foo));
			var K = (DClassLike)ed.SyntaxTree["K"].First();
			Assert.IsFalse(gen.AddedItems.Contains(K));
			var member = (DVariable)K["member"].First();
			Assert.IsTrue(gen.AddedItems.Contains(member));
		}

		[Test]
		public void ForeachCompletion()
		{
			var code =@"module A;
void main() {
foreach(§ 
}";
			var ed = GenEditorData (code);
			var g = new TestCompletionDataGen (null, null);
			Assert.IsTrue(CodeCompletion.GenerateCompletionData (ed, g, 'a', true));
		}

		[Test]
		public void ForeachIteratorVarArg()
		{
			var code = @"module A;
template T(){ struct S {   int a, b, c; }
static void print(Templ=S)(Templ[] p_args...) {
int S; 
foreach(cur; p_args)
 {
 cur;
}}}
";
			var ed = GenEditorData(6, 5, code);
			var S = ResolutionTests.N<DClassLike> (ed.SyntaxTree, "T.S");
			var print = ResolutionTests.N<DMethod> (ed.SyntaxTree, "T.print");
			var cur = (ResolutionTests.S (print, 1, 0, 0) as IExpressionContainingStatement).SubExpressions[0];

			var ctxt = ResolutionTests.CreateDefCtxt (ed.ParseCache, print, cur.EndLocation);
			AbstractType t;
			MemberSymbol ms;

			t = ExpressionTypeEvaluation.EvaluateType (cur, ctxt);
			Assert.IsInstanceOf<MemberSymbol>(t);
			ms = (MemberSymbol)t;

			Assert.IsInstanceOf<TemplateParameterSymbol>(ms.Base);
			t = (ms.Base as TemplateParameterSymbol).Base;

			Assert.IsInstanceOf<StructType>(t);
		}

		[Test]
		public void ForeachIteratorCompletion()
		{
			var code = @"module A;
void main() {Cl** ii;
foreach(i;ii)
i.§
}

struct Cl { int a; }
";
			var ed = GenEditorData(code);

			var a = (ed.MainPackage["A"]["Cl"].First() as DClassLike)["a"].First() as DVariable;

			var g = new TestCompletionDataGen(new[]{ a }, null);
			Assert.IsTrue(CodeCompletion.GenerateCompletionData(ed, g, 'a', true));
		}

		[Test]
		public void NonStaticCompletion1()
		{
			var code = @"module A;
struct SomeStruct {ubyte a; static void read() {
§
}}
";
			var ed = GenEditorData(code);

			var SomeStruct = (ed.MainPackage ["A"] ["SomeStruct"].First () as DClassLike);
			var a = SomeStruct["a"].First() as DVariable;
			var read = SomeStruct ["read"].First () as DMethod;
			var g = new TestCompletionDataGen(new[]{ read },new[]{ a });
			Assert.IsTrue(CodeCompletion.GenerateCompletionData(ed, g, '\0', true));
		}

		[Test]
		public void AutoCompletion()
		{
			var code = @"module A;
void main() {
auto§ 
}";
			var ed = GenEditorData(code);
			var g = new TestCompletionDataGen(null, null);
			Assert.IsFalse(CodeCompletion.GenerateCompletionData(ed, g, 'a', true));
		}

		[Test]
		public void IncrementalParsing_()
		{
			var code=@"module A;
int foo;
void main() {}";

			var m = DParser.ParseString (code);
			var main = m ["main"].First () as DMethod;
			var foo = m ["foo"].First () as DVariable;

			var b = ASTSearchHelper.SearchBlockAt (m, new CodeLocation (11, 3));
			Assert.AreSame(main, b);

			code = @"module A;
int foo;
void main(
string ) {

}";
			var caret = new CodeLocation (8, 4);
			bool _u;
			var newMod = IncrementalParsing.UpdateBlockPartly (m, code, DocumentHelper.LocationToOffset (code, caret), caret, out _u);

			var main2 = newMod ["main"].First () as DMethod;
			Assert.AreNotSame(main2, main);
			Assert.AreEqual(1, main2.Parameters.Count);
			Assert.IsNotNull(main2.Parameters [0].Type);
			Assert.AreEqual(DTokens.IncompleteIdHash, main2.Parameters [0].NameHash);
		}

		[Test]
		public void IncrementalParsing_Lambdas()
		{
			var code=@"module A;
int foo;
void main() {
	
}";

			var m = DParser.ParseString (code);
			var main = m ["main"].First () as DMethod;
			var foo = m ["foo"].First () as DVariable;

			var b = ASTSearchHelper.SearchBlockAt (m, new CodeLocation (1, 4));
			Assert.AreSame(main, b);
			var stmt = main.Body;

			code = @"module A;
int foo;
void main() {
int a;
(string b) =>  ;
}";
			var caret = new CodeLocation (15, 5);
			bool _u;
			var newMain = IncrementalParsing.UpdateBlockPartly (stmt, code, DocumentHelper.LocationToOffset (code, caret), caret, out _u) as DMethod;

			var lambda = newMain.Children [0] as DMethod;
			Assert.IsNotNull(lambda);
			Assert.AreEqual(1, lambda.Parameters.Count);
			Assert.IsNotNull(lambda.Parameters [0].Type);
			Assert.AreEqual("b", lambda.Parameters [0].Name);
		}

		[Test]
		public void IncrementalParsing_LambdaParameters()
		{
			var code=@"module A;
int foo;
void main() {
	
}";

			var m = DParser.ParseString (code);
			var main = m ["main"].First () as DMethod;
			var foo = m ["foo"].First () as DVariable;

			var b = ASTSearchHelper.SearchBlockAt (m, new CodeLocation (1, 4));
			Assert.AreSame(main, b);

			code = @"module A;
int foo;
void main() {
int a;
(string ;
}";
			var caret = new CodeLocation (9, 5);
			bool _u;
			var newMod = IncrementalParsing.UpdateBlockPartly (main.Body, code, DocumentHelper.LocationToOffset (code, caret), caret, out _u) as DMethod;

			var lambda = newMod.Children [0] as DMethod;
			Assert.IsNotNull(lambda);
			Assert.AreEqual(1, lambda.Parameters.Count);
			Assert.IsNotNull(lambda.Parameters [0].Type);
			Assert.AreEqual(DTokens.IncompleteIdHash, lambda.Parameters [0].NameHash);
		}

		[Test]
		public void CompletionTrigger()
		{
			Assert.IsTrue (@"module ".DoesTriggerCompletion());
			Assert.IsTrue (@"import ".DoesTriggerCompletion());
			Assert.IsTrue (@"import std.stdio : ".DoesTriggerCompletion());

			Assert.IsTrue (@"alias ".DoesTriggerCompletion());
			Assert.IsFalse(@"immutable ".DoesTriggerCompletion());
			Assert.IsFalse (@"scope ".DoesTriggerCompletion());
			Assert.IsFalse (@"auto ".DoesTriggerCompletion());
			Assert.IsTrue (@"const( ".DoesTriggerCompletion());

			Assert.IsTrue(@"class A : ".DoesTriggerCompletion());
			Assert.IsTrue(@"class A(T) : ".DoesTriggerCompletion());
			Assert.IsTrue (@"class A(T) if(is( ".DoesTriggerCompletion());
			Assert.IsFalse (@"class A(T) if(is(int  ".DoesTriggerCompletion());
			Assert.IsFalse (@"class A(".DoesTriggerCompletion());
			Assert.IsFalse (@"class A(string ".DoesTriggerCompletion());
			Assert.IsFalse (@"class A(alias ".DoesTriggerCompletion());

			Assert.IsTrue(@"void main(".DoesTriggerCompletion());
			Assert.IsTrue(@"void main(string[".DoesTriggerCompletion());

			Assert.IsTrue(@"class A {int b;}
void main(){
	Class cl;
	if(cl.".DoesTriggerCompletion());
		}

		[TestCase("alias string §")]
		[TestCase("int §")]
		[TestCase("class §")]
		[TestCase("enum §")]
		[TestCase("enum { §")]
		[TestCase("void main(string* §")]
		public void DeclarationCompletion_TriggersButShowsNoEntries(string code)
		{
			var ed = GenEditorData(code);
			
			var gen = new TestCompletionDataGen (null, null);
			Assert.IsTrue(CodeCompletion.GenerateCompletionData (ed, gen, 'a'));
			Assert.IsTrue(gen.IsEmpty);
		}

		[Test]
		public void ArrayAccessCompletion()
		{
			INode[] wl;
			
			var ed = GenEditorData (@"module A;
class C { int f; }
void main() { C[][] o;
o[0][0].
§
}");

			wl = new[]{ (ed.MainPackage["A"]["C"].First() as DClassLike).Children["f"].First() };

			var src = new CancellationTokenSource();
			src.CancelAfter(500);
			ed.CancelToken = src.Token;
			TestCompletionListContents (ed, wl, null);
		}

		[Test]
		public void MissingClassMembersOnParameter ()
		{
			TestsEditorData ed;
			INode [] wl;

			var s = @"module A;
import ArrayMod;
void foo(Array!byte array) {
array.
§
}";
			var arrayDefModule = @"module ArrayMod; class Array(T) { void dooMagic() {} }";

			ed = GenEditorData (s, arrayDefModule);

			wl = new [] { ResolutionTests.GetChildNode (ed.MainPackage.GetModule ("ArrayMod"), "Array.dooMagic") };

			TestCompletionListContents (ed, wl, null);
		}

		[Test]
		public void StaticMemberCompletion()
		{
			TestsEditorData ed;
			ResolutionContext ctxt = null;
			INode[] wl;
			INode[] bl;

			var s = @"module A;
class Class { static int statInt; int normal; }
void main() { Class.§ }";

			ed = GenEditorData (s);

			wl = new[]{ GetNode(ed, "A.Class.statInt", ref ctxt) };
			bl = new[]{ GetNode(ed, "A.Class.normal", ref ctxt) };

			TestCompletionListContents (ed, wl, bl);
		}
		
		[Test]
		public void CompletionSuggestion_WithBasicMethodParameter_SuggestsEnumTypeName()
		{
			var s = @"module A; enum myE { } void foo(myE e);
void main() {
foo(§
}";
			var ed = GenEditorData (s);

			var con = TestCompletionListContents (ed, null, null);
			Assert.AreEqual("myE", con.suggestedItem);
		}

		[Test]
		public void CompletionSuggestion_WithTemplateParameter_SuggestsEnumTypeName()
		{
			var s = @"module A; enum myE { } void foo(T)(T e);
void main() {
foo!myE(§
}";
			var ed = GenEditorData (s);

			var con = TestCompletionListContents (ed, null, null);
			Assert.AreEqual("myE", con.suggestedItem);
		}
		
		[Test]
		[Ignore("not implemented yet")]
		public void MethodParameterCompletion_SuggestsFirstAvailableThingOfParameterMatchingType()
		{
			var s = @"module A; 
class AClass {}
AClass someInstance;
 void foo(T)(T e);
void main() {
foo!AClass(§
}";
			var ed = GenEditorData (s);

			var con = TestCompletionListContents (ed, null, null);
			Assert.AreEqual("someInstance", con.suggestedItem);
		}
		
		[Test]
		public void Completion_OnMethodParameterDeclarations_ShowCtrlSpaceCompletion()
		{
			var s = @"module A; enum myE { } void foo(T)(T e);
void main() {
void subFoo(§) {}
}";
			var ed = GenEditorData (s);
			ResolutionContext ctxt = null;
			var con = TestCompletionListContents (ed, new[]{ GetNode(ed, "A.myE", ref ctxt) }, null);
		}

		[Test]
		public void TriggerOnBegunMemberName_OnMethodCall_SuggestsEnumTypeName()
		{
			var s = @"module A; enum myE { } void foo(T)(T e);
void main() {
foo!myE(m§
}";
			var ed = GenEditorData (s);

			var con = TestCompletionListContents (ed, null, null);
			Assert.AreEqual("m", con.suggestedItem);
		}

		[Test]
		public void TriggerOnBegunMemberName_ReturnsListOfMembersWithPreselectionSuggestion()
		{
			var ed = GenEditorData (@"module A;
class AClass {int propertyA;}
void foo(AClass a) {
a.prop§
}");

			var modA = ed.MainPackage.GetModule("A");
			var wl = new []
			{
				ResolutionTestHelper.GetChildNode (modA, "AClass.propertyA"),
				ResolutionTestHelper.GetChildNode (modA, "foo")
			};

			var cdg = TestCompletionListContents (ed, wl, null);
			Assert.AreEqual("prop", cdg.suggestedItem);
			Assert.AreEqual("a.prop", cdg.TriggerSyntaxRegion.ToString());
		}

		[Test]
		public void TriggerOnBegunMemberName2_ReturnsListOfMembersWithPreselectionSuggestion()
		{
			var ed = GenEditorData (@"module A;
class AClass {class BType{}}
AClass.B§ b;
");

			var modA = ed.MainPackage.GetModule("A");
			var wl = new []
			{
				ResolutionTestHelper.GetChildNode (modA, "AClass.BType")
			};

			var cdg = TestCompletionListContents (ed, wl, null);
			Assert.AreEqual("B", cdg.suggestedItem);
			Assert.AreEqual("AClass.B", cdg.TriggerSyntaxRegion.ToString());
		}

		[Test]
		public void TriggerOnVariableName_SuggestsNewVariableName()
		{
			var ed = GenEditorData(@"module A;
class AClass {}
AClass §
");
			
			var gen = new TestCompletionDataGen (null, null);
			Assert.IsTrue(CodeCompletion.GenerateCompletionData (ed, gen, '\0'));
			
			Assert.IsTrue(gen.AddedItems.Count == 0);
			Assert.AreEqual("aClass", gen.suggestedItem);
			Assert.AreEqual("aClass", string.Join(",",gen.AddedTextItems));
		}

		#region Test lowlevel
		public static INode GetNode(EditorData ed, string id, ref ResolutionContext ctxt)
		{
			if (ctxt == null)
				ctxt = ResolutionContext.Create (ed, true);

			DToken tk;
			var bt = DParser.ParseBasicType (id, out tk);
			var t = TypeDeclarationResolver.ResolveSingle(bt, ctxt);

			var n = (t as DSymbol).Definition;
			Assert.IsNotNull(n);
			return n;
		}

		private class TestsEditorData : EditorData
		{
			public MutableRootPackage MainPackage => (ParseCache as LegacyParseCacheView).FirstPackage();
		}
		
		/// <summary>
		/// Use § as caret indicator!
		/// </summary>
		private static TestsEditorData GenEditorData(string focusedModuleCode, params string[] otherModuleCodes)
		{
			int caretOffset = focusedModuleCode.IndexOf('§');
			Assert.IsTrue(caretOffset != -1);
			focusedModuleCode = focusedModuleCode.Substring(0, caretOffset) +
			                    focusedModuleCode.Substring(caretOffset + 1);
			var caret = DocumentHelper.OffsetToLocation(focusedModuleCode, caretOffset);

			return GenEditorData(caret.Line, caret.Column, focusedModuleCode, otherModuleCodes);
		}

		private static TestsEditorData GenEditorData(int caretLine, int caretPos,string focusedModuleCode,params string[] otherModuleCodes)
		{
			var cache = ResolutionTestHelper.CreateCache (out _, otherModuleCodes);
			var ed = new TestsEditorData { ParseCache = cache };
			ed.CancelToken = CancellationToken.None;

			UpdateEditorData (ed, caretLine, caretPos, focusedModuleCode);

			return ed;
		}

		private static void UpdateEditorData(TestsEditorData ed,int caretLine, int caretPos, string focusedModuleCode)
		{
			var mod = DParser.ParseString (focusedModuleCode);

			ed.MainPackage.AddModule (mod);

			ed.ModuleCode = focusedModuleCode;
			ed.SyntaxTree = mod;
			ed.CaretLocation = new CodeLocation (caretPos, caretLine);
			ed.CaretOffset = DocumentHelper.LocationToOffset (focusedModuleCode, caretLine, caretPos);
		}

		private static TestCompletionDataGen TestCompletionListContents(IEditorData ed, INode[] itemWhiteList, INode[] itemBlackList, char trigger = '\0')
		{
			var gen = new TestCompletionDataGen (itemWhiteList, itemBlackList);
			Assert.IsTrue(CodeCompletion.GenerateCompletionData (ed, gen, trigger));

			Assert.IsFalse(gen.HasRemainingItems, "Some items were not enlisted!");
			return gen;
		}
		#endregion
	}
}
