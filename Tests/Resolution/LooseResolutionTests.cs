using System.Linq;
using D_Parser.Completion;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Resolver;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Resolution
{
	[TestClass]
	public class LooseResolutionTests : ResolutionTestHelper
	{
		[TestMethod]
		public void LooseResolution2()
		{
			var pcw = CreateCache(out DModule A, @"module A;
import pack.B;

void x(){
	Nested;
	someDeepVariable;
	Nested.someDeepVariable;
}",
				@"module pack.B;
				
private void privFoo() {}
package class packClass() {
private int privInt;
}
class C { class Nested { int someDeepVariable; } }");

			var x = A["x"].First() as DMethod;

			IEditorData ed;
			ISyntaxRegion sr;
			DSymbol t;

			sr = (S(x, 2) as ExpressionStatement).Expression;
			ed = new EditorData { SyntaxTree = A, ParseCache = pcw, CaretLocation = sr.EndLocation };
			t = LooseResolution.SearchNodesByName(ref sr, ed) as DSymbol;

			Assert.IsInstanceOfType(t, typeof(ClassType));
			Assert.IsInstanceOfType(sr, typeof(IdentifierExpression));

			sr = (S(x, 0) as ExpressionStatement).Expression;
			ed = new EditorData { SyntaxTree = A, ParseCache = pcw, CaretLocation = sr.EndLocation };
			t = LooseResolution.SearchNodesByName(ref sr, ed) as DSymbol;

			Assert.IsInstanceOfType(t, typeof(ClassType));

			sr = (S(x, 1) as ExpressionStatement).Expression;
			ed = new EditorData { SyntaxTree = A, ParseCache = pcw, CaretLocation = sr.EndLocation };
			t = LooseResolution.SearchNodesByName(ref sr, ed) as DSymbol;

			Assert.IsInstanceOfType(t, typeof(MemberSymbol));
		}

		[TestMethod]
		public void LooseNodeResolution()
		{
			var pcw = CreateCache(out DModule A, @"module A;
import pack.B;

void x(){
	pack.B.privFoo;
	privFoo;
	packClass;
	packClass.privInt;
}",
				@"module pack.B;
				
private void privFoo() {}
package class packClass {
private int privInt;
}
");

			var x = A["x"].First() as DMethod;

			IEditorData ed;
			ISyntaxRegion sr;
			LooseResolution.NodeResolutionAttempt attempt;
			DSymbol t;


			ed = new EditorData { SyntaxTree = A, ParseCache = pcw, CaretLocation = (S(x, 3) as ExpressionStatement).Expression.EndLocation };
			t = LooseResolution.ResolveTypeLoosely(ed, out attempt, out sr) as DSymbol;

			Assert.IsInstanceOfType(t, typeof(MemberSymbol));
			Assert.IsInstanceOfType(t.Definition, typeof(DVariable));
			Assert.AreEqual(LooseResolution.NodeResolutionAttempt.RawSymbolLookup, attempt);


			ed = new EditorData { SyntaxTree = A, ParseCache = pcw, CaretLocation = (S(x, 2) as ExpressionStatement).Expression.EndLocation };
			t = LooseResolution.ResolveTypeLoosely(ed, out attempt, out sr) as DSymbol;

			Assert.IsInstanceOfType(t, typeof(ClassType));
			Assert.AreEqual(LooseResolution.NodeResolutionAttempt.RawSymbolLookup, attempt);


			ed = new EditorData { SyntaxTree = A, ParseCache = pcw, CaretLocation = (S(x, 1) as ExpressionStatement).Expression.EndLocation };
			t = LooseResolution.ResolveTypeLoosely(ed, out attempt, out sr) as DSymbol;

			Assert.IsInstanceOfType(t, typeof(MemberSymbol));
			Assert.IsInstanceOfType(t.Definition, typeof(DMethod));
			Assert.AreEqual(LooseResolution.NodeResolutionAttempt.RawSymbolLookup, attempt);


			ed = new EditorData { SyntaxTree = A, ParseCache = pcw, CaretLocation = (S(x, 0) as ExpressionStatement).Expression.EndLocation };
			t = LooseResolution.ResolveTypeLoosely(ed, out attempt, out sr) as DSymbol;

			Assert.IsInstanceOfType(t, typeof(MemberSymbol));
			Assert.IsInstanceOfType(t.Definition, typeof(DMethod));
			Assert.AreEqual(LooseResolution.NodeResolutionAttempt.RawSymbolLookup, attempt);
		}
	}
}
