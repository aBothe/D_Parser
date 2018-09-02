using System.Linq;
using D_Parser.Completion;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Resolver;
using NUnit.Framework;

namespace Tests.Resolution
{
	[TestFixture]
	public class LooseResolutionTests : ResolutionTestHelper
	{
		[Test]
		public void LooseResolution2()
		{
			var pcw = CreateCache(@"module A;
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

			var A = pcw.FirstPackage()["A"];
			var x = A["x"].First() as DMethod;

			IEditorData ed;
			ISyntaxRegion sr;
			DSymbol t;

			sr = (S(x, 2) as ExpressionStatement).Expression;
			ed = new EditorData { SyntaxTree = A, ParseCache = pcw, CaretLocation = sr.EndLocation };
			t = LooseResolution.SearchNodesByName(ref sr, ed) as DSymbol;

			Assert.That(t, Is.TypeOf(typeof(ClassType)));
			Assert.That(sr, Is.TypeOf(typeof(IdentifierExpression)));

			sr = (S(x, 0) as ExpressionStatement).Expression;
			ed = new EditorData { SyntaxTree = A, ParseCache = pcw, CaretLocation = sr.EndLocation };
			t = LooseResolution.SearchNodesByName(ref sr, ed) as DSymbol;

			Assert.That(t, Is.TypeOf(typeof(ClassType)));

			sr = (S(x, 1) as ExpressionStatement).Expression;
			ed = new EditorData { SyntaxTree = A, ParseCache = pcw, CaretLocation = sr.EndLocation };
			t = LooseResolution.SearchNodesByName(ref sr, ed) as DSymbol;

			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
		}

		[Test]
		public void LooseNodeResolution()
		{
			var pcw = CreateCache(@"module A;
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

			var A = pcw.FirstPackage()["A"];
			var x = A["x"].First() as DMethod;

			IEditorData ed;
			ISyntaxRegion sr;
			LooseResolution.NodeResolutionAttempt attempt;
			DSymbol t;


			ed = new EditorData { SyntaxTree = A, ParseCache = pcw, CaretLocation = (S(x, 3) as ExpressionStatement).Expression.EndLocation };
			t = LooseResolution.ResolveTypeLoosely(ed, out attempt, out sr) as DSymbol;

			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That(t.Definition, Is.TypeOf(typeof(DVariable)));
			Assert.That(attempt, Is.EqualTo(LooseResolution.NodeResolutionAttempt.RawSymbolLookup));


			ed = new EditorData { SyntaxTree = A, ParseCache = pcw, CaretLocation = (S(x, 2) as ExpressionStatement).Expression.EndLocation };
			t = LooseResolution.ResolveTypeLoosely(ed, out attempt, out sr) as DSymbol;

			Assert.That(t, Is.TypeOf(typeof(ClassType)));
			Assert.That(attempt, Is.EqualTo(LooseResolution.NodeResolutionAttempt.RawSymbolLookup));


			ed = new EditorData { SyntaxTree = A, ParseCache = pcw, CaretLocation = (S(x, 1) as ExpressionStatement).Expression.EndLocation };
			t = LooseResolution.ResolveTypeLoosely(ed, out attempt, out sr) as DSymbol;

			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That(t.Definition, Is.TypeOf(typeof(DMethod)));
			Assert.That(attempt, Is.EqualTo(LooseResolution.NodeResolutionAttempt.RawSymbolLookup));


			ed = new EditorData { SyntaxTree = A, ParseCache = pcw, CaretLocation = (S(x, 0) as ExpressionStatement).Expression.EndLocation };
			t = LooseResolution.ResolveTypeLoosely(ed, out attempt, out sr) as DSymbol;

			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That(t.Definition, Is.TypeOf(typeof(DMethod)));
			Assert.That(attempt, Is.EqualTo(LooseResolution.NodeResolutionAttempt.RawSymbolLookup));
		}
	}
}
