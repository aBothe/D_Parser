using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.ExpressionSemantics;
using NUnit.Framework;

namespace Tests
{
	public partial class ResolutionTests
	{
		#region Template Parameter-related
		[Test]
		/// <summary>
		/// https://github.com/aBothe/D_Parser/issues/192
		/// </summary>
		public void TemplateValueParameterDefaultSelfRefSO(){
			var code = @"module A;
struct Template( void var = Template ) {}
";
			AbstractType t;
			IExpression x;
			DModule A;
			var ctxt = CreateDefCtxt("A", out A, code);

			x = (N<DClassLike>(A, "Template").TemplateParameters[0] as TemplateValueParameter).DefaultExpression;

			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.TypeOf(typeof(StructType)));
		}
		#endregion

		[Test]
		public void ConstAttributedSymbolType ()
		{
			AbstractType t;
			IExpression x;
			DModule A;

			var code = "module A; const private Object co;";

			var ctxt = CreateDefCtxt ("A", out A, code);

			x = DParser.ParseExpression ("co");

			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);

			Assert.That (t, Is.TypeOf<MemberSymbol> ());
			var baseType = ((MemberSymbol)t).Base;

			Assert.That (baseType, Is.TypeOf<ClassType> ());
			var objectClass = baseType as ClassType;

			Assert.That (objectClass.HasModifier (DTokens.Const));
		}

		[Test]
		public void ConstAttributedSymbolType_MemberFunctionAttributeDecl(){
			AbstractType t;
			IExpression x;
			DModule A;

			var code = "module A; const(Object) co;";

			var ctxt = CreateDefCtxt ("A", out A, code);

			x = DParser.ParseExpression ("co");

			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);

			Assert.That (t, Is.TypeOf<MemberSymbol>());
			var baseType = ((MemberSymbol)t).Base;

			Assert.That (baseType, Is.TypeOf<ClassType> ());
			var objectClass = baseType as ClassType;

			Assert.That (objectClass.HasModifier (DTokens.Const));
		}

		readonly string constNonConstParamDistinguishingSOcode = @"module A;
class B{
auto opEquals(Object lhs, Object rhs)
{
    return lhs.opEquals(rhs) && rhs.opEquals(lhs);
}
auto opEquals(const Object lhs, const Object rhs)
{
    return opEquals(cast()lhs, cast()rhs);
}
}
B b;
Object o,o2;
const Object co,co2;
";

		[Test]
		public void ConstNonConstParamDistinguishingSO()
		{
			AbstractType t;
			IExpression x;
			DModule A;
			DClassLike B;
			DMethod opEquals1, opEquals2;
			var ctxt = CreateDefCtxt("A", out A, constNonConstParamDistinguishingSOcode);

			B = N<DClassLike>(A, "B");
			opEquals1 = B.Children[0] as DMethod;
			opEquals2 = B.Children[1] as DMethod;
			Assert.That(opEquals1, Is.Not.Null);
			Assert.That(opEquals2, Is.Not.Null);

			x = DParser.ParseExpression("b.opEquals(o,o2)");

			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt, false);
			Assert.That(t, Is.TypeOf<MemberSymbol>());
			Assert.That((t as MemberSymbol).Definition, Is.SameAs(opEquals1));

			Assert.That((t as MemberSymbol).Base, Is.TypeOf<PrimitiveType>());

		}

		[Test]
		public void ConstNonConstParamDistinguishingSO2 ()
		{
			AbstractType t2;
			IExpression x2;
			DModule A;
			DClassLike B;
			DMethod opEquals1, opEquals2;
			var ctxt = CreateDefCtxt ("A", out A, constNonConstParamDistinguishingSOcode);

			B = N<DClassLike> (A, "B");
			opEquals1 = B.Children [0] as DMethod;
			opEquals2 = B.Children [1] as DMethod;
			Assert.That (opEquals1, Is.Not.Null);
			Assert.That (opEquals2, Is.Not.Null);

			x2 = DParser.ParseExpression ("b.opEquals(co,co2)");

			t2 = ExpressionTypeEvaluation.EvaluateType (x2, ctxt, false);
			Assert.That (t2, Is.TypeOf<MemberSymbol> ());
			Assert.That ((t2 as MemberSymbol).Definition, Is.SameAs (opEquals2));

			Assert.That ((t2 as MemberSymbol).Base, Is.TypeOf<PrimitiveType> ());

		}

	}
}

