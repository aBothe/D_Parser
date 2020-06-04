using System.Linq;
using D_Parser.Completion;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Resolver;
using NUnit.Framework;
using Tests.Completion;

namespace Tests.Resolution
{
	[TestFixture]
	public class LooseResolutionTests : ResolutionTestHelper
	{
		[Test]
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

			Assert.IsInstanceOf<ClassType>(t);
			Assert.IsInstanceOf<IdentifierExpression>(sr);

			sr = (S(x, 0) as ExpressionStatement).Expression;
			ed = new EditorData { SyntaxTree = A, ParseCache = pcw, CaretLocation = sr.EndLocation };
			t = LooseResolution.SearchNodesByName(ref sr, ed) as DSymbol;

			Assert.IsInstanceOf<ClassType>(t);

			sr = (S(x, 1) as ExpressionStatement).Expression;
			ed = new EditorData { SyntaxTree = A, ParseCache = pcw, CaretLocation = sr.EndLocation };
			t = LooseResolution.SearchNodesByName(ref sr, ed) as DSymbol;

			Assert.IsInstanceOf<MemberSymbol>(t);
		}

		[Test]
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

			Assert.IsInstanceOf<MemberSymbol>(t);
			Assert.IsInstanceOf<DVariable>(t.Definition);
			Assert.AreEqual(LooseResolution.NodeResolutionAttempt.RawSymbolLookup, attempt);


			ed = new EditorData { SyntaxTree = A, ParseCache = pcw, CaretLocation = (S(x, 2) as ExpressionStatement).Expression.EndLocation };
			t = LooseResolution.ResolveTypeLoosely(ed, out attempt, out sr) as DSymbol;

			Assert.IsInstanceOf<ClassType>(t);
			Assert.AreEqual(LooseResolution.NodeResolutionAttempt.RawSymbolLookup, attempt);


			ed = new EditorData { SyntaxTree = A, ParseCache = pcw, CaretLocation = (S(x, 1) as ExpressionStatement).Expression.EndLocation };
			t = LooseResolution.ResolveTypeLoosely(ed, out attempt, out sr) as DSymbol;

			Assert.IsInstanceOf<MemberSymbol>(t);
			Assert.IsInstanceOf<DMethod>(t.Definition);
			Assert.AreEqual(LooseResolution.NodeResolutionAttempt.RawSymbolLookup, attempt);


			ed = new EditorData { SyntaxTree = A, ParseCache = pcw, CaretLocation = (S(x, 0) as ExpressionStatement).Expression.EndLocation };
			t = LooseResolution.ResolveTypeLoosely(ed, out attempt, out sr) as DSymbol;

			Assert.IsInstanceOf<MemberSymbol>(t);
			Assert.IsInstanceOf<DMethod>(t.Definition);
			Assert.AreEqual(LooseResolution.NodeResolutionAttempt.RawSymbolLookup, attempt);
		}

		[Test]
		public void NewExpression_ReturnsConstructor()
		{
			var ed = TestUtil.GenEditorData(@"module A;
class MyClass(T) {this() {}}
auto a = new MyCla§ss!int();
");

			var t = LooseResolution.ResolveTypeLoosely(ed, out LooseResolution.NodeResolutionAttempt attempt,
				out ISyntaxRegion sr) as DSymbol;

			Assert.IsInstanceOf<MemberSymbol>(t);
			Assert.IsInstanceOf<DMethod>(t.Definition);
			Assert.IsInstanceOf<NewExpression>(sr);
			Assert.AreEqual(LooseResolution.NodeResolutionAttempt.Normal, attempt);
		}

		[Test]
		public void NewExpression_OnlyImplicitCtor_ReturnsConstructor()
		{
			var ed = TestUtil.GenEditorData(@"module A;
class MyClass(T) {}
auto a = new MyCla§ss!int();
");

			var t = LooseResolution.ResolveTypeLoosely(ed, out LooseResolution.NodeResolutionAttempt attempt,
				out ISyntaxRegion sr) as DSymbol;

			Assert.IsInstanceOf<MemberSymbol>(t);
			Assert.IsInstanceOf<DMethod>(t.Definition);
			Assert.IsInstanceOf<NewExpression>(sr);
			Assert.AreEqual(LooseResolution.NodeResolutionAttempt.Normal, attempt);
		}
	}
}
