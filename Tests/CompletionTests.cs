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
			ParserTrackerVariables ptr;
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
		public void ForeachCompletion()
		{
			var code =@"module A;
void main() {
foreach( 
}";
			var ed = GenEditorData (3, 9, code);
			var g = new TestCompletionDataGen (null, null);
			Assert.That(CodeCompletion.GenerateCompletionData (ed, g, '\0', true), Is.False);
		}

		[Test]
		public void MemberCompletion()
		{
			bool expectNodeName;

			object lpo;

			lpo = GetLastParsedObject ("std.templ!(hello).", out expectNodeName);
			Assert.That (lpo, Is.TypeOf (typeof(PostfixExpression_Access)));
			Assert.That ((lpo as PostfixExpression_Access).PostfixForeExpression, Is.TypeOf(typeof(PostfixExpression_Access)));
			Assert.That (expectNodeName, Is.False);

			lpo = GetLastParsedObject ("templ!(hello.", out expectNodeName);
			Assert.That (lpo, Is.TypeOf (typeof(PostfixExpression_Access)));
			Assert.That (((lpo as PostfixExpression_Access).PostfixForeExpression as IdentifierExpression).StringValue, Is.EqualTo ("hello"));
			Assert.That (expectNodeName, Is.False);

			lpo = GetLastParsedObject ("templ!(hello).", out expectNodeName);
			Assert.That (lpo, Is.TypeOf (typeof(PostfixExpression_Access)));
			Assert.That (((lpo as PostfixExpression_Access).PostfixForeExpression as TemplateInstanceExpression).TemplateId, Is.EqualTo ("templ"));
			Assert.That (expectNodeName, Is.False);

			Assert.That (GetLastParsedObject("std.templ!(hello.", out expectNodeName), 
			             Is.TypeOf (typeof(PostfixExpression_Access)));
			Assert.That (expectNodeName, Is.False);

			Assert.That (GetLastParsedObject("b = B.", out expectNodeName), 
			             Is.TypeOf (typeof(PostfixExpression_Access)));
			Assert.That (expectNodeName, Is.False);

			Assert.That (GetLastParsedObject("b = (B.", out expectNodeName), 
			             Is.TypeOf (typeof(PostfixExpression_Access)));
			Assert.That (expectNodeName, Is.False);
		}

		static object GetLastParsedObject(string code, out bool expectNodeId)
		{
			using (var sr = new StringReader(code)) {
				var parser = DParser.Create (sr);
				parser.Step ();
				parser.Statement ();

				expectNodeId = parser.TrackerVariables.ExpectingNodeName;
				return parser.TrackerVariables.LastParsedObject;
			}
		}

		[Test]
		public void StaticMemberCompletion()
		{
			EditorData ed;
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

		#region Test lowlevel
		class TestCompletionDataGen : ICompletionDataGenerator
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

			#region ICompletionDataGenerator implementation

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
		}

		INode GetNode(EditorData ed, string id, ref ResolutionContext ctxt)
		{
			if (ctxt == null)
				ctxt = ResolutionContext.Create (ed);

			DToken tk;
			var bt = DParser.ParseBasicType (id, out tk);
			var t = TypeDeclarationResolver.ResolveSingle(bt, ctxt);

			var n = (t as DSymbol).Definition;
			Assert.That (n, Is.Not.Null);
			return n;
		}

		EditorData GenEditorData(int caretLine, int caretPos,string focusedModuleCode,params string[] otherModuleCodes)
		{
			var cache = ResolutionTests.CreateCache (otherModuleCodes);
			var ed = new EditorData { ParseCache = cache };

			UpdateEditorData (ed, caretLine, caretPos, focusedModuleCode);

			return ed;
		}

		void UpdateEditorData(EditorData ed,int caretLine, int caretPos, string focusedModuleCode)
		{
			var mod = DParser.ParseString (focusedModuleCode);
			var pack = ed.ParseCache [0] as MutableRootPackage;

			pack.AddModule (mod);

			ed.ModuleCode = focusedModuleCode;
			ed.SyntaxTree = mod;
			ed.CaretLocation = new CodeLocation (caretPos, caretLine);
			ed.CaretOffset = DocumentHelper.LocationToOffset (focusedModuleCode, caretLine, caretPos);
		}

		void TestCompletionListContents(IEditorData ed, INode[] itemWhiteList, INode[] itemBlackList = null, char trigger = '\0')
		{
			var gen = new TestCompletionDataGen (itemWhiteList, itemBlackList);
			Assert.That (CodeCompletion.GenerateCompletionData (ed, gen, trigger), Is.True);

			Assert.That (gen.HasRemainingItems, Is.False, "Some items were not enlisted!");
		}
		#endregion
	}
}
