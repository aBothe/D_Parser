using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using D_Parser;
using D_Parser.Completion;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Misc;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.Templates;
using D_Parser.Resolver.TypeResolution;
using NUnit.Framework;
using System.IO;
using NUnit.Framework.Constraints;

namespace Tests
{
	/// <summary>
	/// Description of CompletionTests.
	/// </summary>
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
			var code = @"module 
";

			var ed = GenEditorData (1, 8, code);
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
			Assert.That (gen.addedItems, Has.No.Member (foo));
			var K = (DClassLike)ed.SyntaxTree["K"].First();
			Assert.That (gen.addedItems, Has.No.Member (K));
			var member = (DVariable)K["member"].First();
			Assert.That (gen.addedItems, Has.Member (member));
		}

		[Test]
		public void ForeachCompletion()
		{
			var code =@"module A;
void main() {
foreach( 
}";
			var ed = GenEditorData (3, 9, code);
			var g = new TestCompletionDataGen (null, null);
			Assert.That(CodeCompletion.GenerateCompletionData (ed, g, 'a', true), Is.True);
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
			Assert.That (t, Is.TypeOf(typeof(MemberSymbol)));
			ms = (MemberSymbol)t;

			Assert.That (ms.Base, Is.TypeOf(typeof(TemplateParameterSymbol)));
			t = (ms.Base as TemplateParameterSymbol).Base;

			Assert.That (t, Is.TypeOf(typeof(StructType)));
		}

		[Test]
		public void ForeachIteratorCompletion()
		{
			var code = @"module A;
void main() {Cl** ii;
foreach(i;ii)
i.
}

struct Cl { int a; }
";
			var ed = GenEditorData(4, 3, code);

			var a = (ed.MainPackage["A"]["Cl"].First() as DClassLike)["a"].First() as DVariable;

			var g = new TestCompletionDataGen(new[]{ a }, null);
			Assert.That(CodeCompletion.GenerateCompletionData(ed, g, 'a', true), Is.True);
		}

		[Test]
		public void NonStaticCompletion1()
		{
			var code = @"module A;
struct SomeStruct {ubyte a; static void read() {

}}
";
			var ed = GenEditorData(3, 1, code);

			var SomeStruct = (ed.MainPackage ["A"] ["SomeStruct"].First () as DClassLike);
			var a = SomeStruct["a"].First() as DVariable;
			var read = SomeStruct ["read"].First () as DMethod;
			var g = new TestCompletionDataGen(new[]{ read },new[]{ a });
			Assert.That(CodeCompletion.GenerateCompletionData(ed, g, '\0', true), Is.True);
		}

		[Test]
		public void AutoCompletion()
		{
			var code = @"module A;
void main() {
auto 
}";
			var ed = GenEditorData(3, 5, code);
			var g = new TestCompletionDataGen(null, null);
			Assert.That(CodeCompletion.GenerateCompletionData(ed, g, 'a', true), Is.False);
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

			var b = DResolver.SearchBlockAt (m, new CodeLocation (11, 3));
			Assert.That (b, Is.SameAs (main));

			code = @"module A;
int foo;
void main(
string ) {

}";
			var caret = new CodeLocation (8, 4);
			bool _u;
			var newMod = IncrementalParsing.UpdateBlockPartly (m, code, DocumentHelper.LocationToOffset (code, caret), caret, out _u);

			var main2 = newMod ["main"].First () as DMethod;
			Assert.That (main, Is.Not.SameAs (main2));
			Assert.That (main2.Parameters.Count, Is.EqualTo (1));
			Assert.That (main2.Parameters [0].Type, Is.Not.Null);
			Assert.That (main2.Parameters [0].NameHash, Is.EqualTo(DTokens.IncompleteIdHash));
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

			var b = DResolver.SearchBlockAt (m, new CodeLocation (1, 4));
			Assert.That (b, Is.SameAs (main));
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
			Assert.That (lambda, Is.Not.Null);
			Assert.That (lambda.Parameters.Count, Is.EqualTo (1));
			Assert.That (lambda.Parameters [0].Type, Is.Not.Null);
			Assert.That (lambda.Parameters [0].Name, Is.EqualTo("b"));
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

			var b = DResolver.SearchBlockAt (m, new CodeLocation (1, 4));
			Assert.That (b, Is.SameAs (main));

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
			Assert.That (lambda, Is.Not.Null);
			Assert.That (lambda.Parameters.Count, Is.EqualTo (1));
			Assert.That (lambda.Parameters [0].Type, Is.Not.Null);
			Assert.That (lambda.Parameters [0].NameHash, Is.EqualTo(DTokens.IncompleteIdHash));
		}

		[Test]
		public void CompletionTrigger()
		{
			Assert.That (@"module ", Does.Trigger);
			Assert.That (@"import ", Does.Trigger);
			Assert.That (@"import std.stdio : ", Does.Trigger);

			Assert.That (@"alias ", Does.Trigger);
			Assert.That (@"alias string ", Does.Not.Trigger);
			Assert.That (@"int ", Does.Not.Trigger);
			Assert.That (@"immutable ",Does.Not.Trigger);
			Assert.That (@"scope ",Does.Not.Trigger);
			Assert.That (@"auto ", Does.Not.Trigger);
			Assert.That (@"const( ", Does.Trigger);

			Assert.That (@"class ", Does.Not.Trigger);
			Assert.That(@"class A : ", Does.Trigger);
			Assert.That(@"class A(T) : ", Does.Trigger);
			Assert.That (@"class A(T) if(is( ", Does.Trigger);
			Assert.That (@"class A(T) if(is(int  ", Does.Not.Trigger);
			//Assert.That (@"class A(", Does.Not.Trigger);
			Assert.That (@"class A(string ", Does.Not.Trigger);
			Assert.That (@"class A(alias ", Does.Not.Trigger);

			Assert.That (@"enum ", Does.Not.Trigger);
			Assert.That (@"enum { ", Does.Not.Trigger);

			Assert.That(@"void main(", Does.Trigger);
			Assert.That(@"void main(string[", Does.Trigger);
			Assert.That(@"void main(string* ", Does.Not.Trigger);

			Assert.That(@"class A {int b;}
void main(){
	Class cl;
	if(cl.", Does.Trigger);
		}

		[Test]
		public void ArrayAccessCompletion()
		{
			TestsEditorData ed;
			INode[] wl;

			var s = @"module A;
class C { int f; }
void main() { C[][] o;
o[0][0].

}";

			ed = GenEditorData (5, 1, s);

			wl = new[]{ (ed.MainPackage["A"]["C"].First() as DClassLike).Children["f"].First() };

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
void main() { Class. }";

			ed = GenEditorData (3, 21, s);

			wl = new[]{ GetNode(ed, "A.Class.statInt", ref ctxt) };
			bl = new[]{ GetNode(ed, "A.Class.normal", ref ctxt) };

			TestCompletionListContents (ed, wl, bl);
		}

		[Test]
		[Ignore]
		public void CompletionSuggestion()
		{
			TestsEditorData ed;

			var s = @"module A; enum myE { } void foo(myE e);
void main() {
foo(
}";
			ed = GenEditorData (3, 5, s);

			var con = TestCompletionListContents (ed);
			Assert.That (con.suggestedItem, Is.EqualTo ("myE"));
		}

		#region Test lowlevel
		public static class Does
		{
			public class TriggerConstraint : Constraint
			{
				readonly bool neg;
				public TriggerConstraint(bool neg = false)
				{
					this.neg = neg;
				}

				public override bool Matches (object actual)
				{
					var code = actual as string;
					if (code == null)
						return false;

					code += "\n";

					var m = DParser.ParseString (code);
					var cache = ResolutionTests.CreateCache ();
					cache.FirstPackage().AddModule (m);

					var ed = new EditorData{ 
						ModuleCode = code, 
						CaretOffset = code.Length-1, 
						CaretLocation = DocumentHelper.OffsetToLocation(code,code.Length-1),
						SyntaxTree = m,
						ParseCache = cache
					};

					var gen = new TestCompletionDataGen (null, null);
					var res = CodeCompletion.GenerateCompletionData (ed, gen, 'a');

					return neg ? !res : res;
				}

				public override void WriteDescriptionTo (MessageWriter writer)
				{
					writer.WriteLine ();
				}
			}

			public readonly static TriggerConstraint Trigger = new TriggerConstraint();

			public static class Not
			{
				public readonly static TriggerConstraint Trigger = new TriggerConstraint (true);
			}
		}

		public class TestCompletionDataGen : ICompletionDataGenerator
		{
			public TestCompletionDataGen(INode[] whiteList, INode[] blackList)
			{
				if(whiteList != null){
					remainingWhiteList= new List<INode>(whiteList);
					this.whiteList = new List<INode>(whiteList);
				}
				if(blackList != null)
					this.blackList = new List<INode>(blackList);
			}

			public List<INode> remainingWhiteList;
			public List<INode> whiteList;
			public List<INode> blackList;
			public string suggestedItem;

			#region ICompletionDataGenerator implementation

			public void SetSuggestedItem (string item)
			{
				suggestedItem = item;
			}

			public void AddCodeGeneratingNodeItem (INode node, string codeToGenerate)
			{

			}

			public List<byte> Tokens = new List<byte> ();
			public void Add (byte Token)
			{
				Tokens.Add (Token);
			}

			public List<string> Attributes = new List<string> ();
			public void AddPropertyAttribute (string AttributeText)
			{
				Attributes.Add (AttributeText);
			}

			public void AddTextItem (string Text, string Description)
			{

			}

			public void AddIconItem (string iconName, string text, string description)
			{

			}

			public List<INode> addedItems = new List<INode> ();
			public void Add (INode n)
			{
				if (blackList != null)
					Assert.That (blackList.Contains (n), Is.False);

				if (whiteList != null && whiteList.Contains(n))
					Assert.That (remainingWhiteList.Remove (n), Is.True, n+" occurred at least twice!");

				addedItems.Add (n);
			}

			public void AddModule (DModule module, string nameOverride = null)
			{
				this.Add (module);
			}

			public List<string> Packages = new List<string> ();
			public void AddPackage (string packageName)
			{
				Packages.Add (packageName);
			}

			#endregion

			public bool HasRemainingItems { get{ return remainingWhiteList != null && remainingWhiteList.Count > 0; } }


			public void NotifyTimeout()
			{
				throw new OperationCanceledException();
			}
		}

		public static INode GetNode(EditorData ed, string id, ref ResolutionContext ctxt)
		{
			if (ctxt == null)
				ctxt = ResolutionContext.Create (ed, true);

			DToken tk;
			var bt = DParser.ParseBasicType (id, out tk);
			var t = TypeDeclarationResolver.ResolveSingle(bt, ctxt);

			var n = (t as DSymbol).Definition;
			Assert.That (n, Is.Not.Null);
			return n;
		}

		public class TestsEditorData : EditorData
		{
			public MutableRootPackage MainPackage { get{ return (ParseCache as LegacyParseCacheView).FirstPackage(); } }
		}

		public static TestsEditorData GenEditorData(int caretLine, int caretPos,string focusedModuleCode,params string[] otherModuleCodes)
		{
			var cache = ResolutionTests.CreateCache (otherModuleCodes);
			var ed = new TestsEditorData { ParseCache = cache };

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

		public static TestCompletionDataGen TestCompletionListContents(IEditorData ed, INode[] itemWhiteList = null, INode[] itemBlackList = null, char trigger = '\0')
		{
			var gen = new TestCompletionDataGen (itemWhiteList, itemBlackList);
			Assert.That (CodeCompletion.GenerateCompletionData (ed, gen, trigger), Is.True);

			Assert.That (gen.HasRemainingItems, Is.False, "Some items were not enlisted!");
			return gen;
		}
		#endregion
	}
}
