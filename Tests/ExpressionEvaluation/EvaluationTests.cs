﻿using System.Linq;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Misc;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.TypeResolution;
using NUnit.Framework;
using Tests.Resolution;

namespace Tests.ExpressionEvaluation
{
	[TestFixture]
	public class EvaluationTests
	{
		static ISymbolValue E(string expression, ResolutionContext ctxt = null)
		{
			return E(expression, ctxt, out _);
		}

		static ISymbolValue E(string expression, ResolutionContext ctxt, out VariableValue variableValue)
		{
			return Evaluation.EvaluateValue(DParser.ParseExpression(expression),
				ctxt ?? ResolutionTestHelper.CreateDefCtxt(new LegacyParseCacheView(new string[] { }), null),
				out variableValue);
		}

		static ISymbolValue E(string expression, StatefulEvaluationContext vp)
		{
			return Evaluation.EvaluateValue(DParser.ParseExpression(expression), vp);
		}

		static PrimitiveValue GetPrimitiveValue(string literalCode,StatefulEvaluationContext vp=null)
		{
			var v = E(literalCode);

			Assert.IsInstanceOf<PrimitiveValue>(v);
			return (PrimitiveValue)v;
		}

		 static void TestPrimitive(string literal, int btToken, object val)
		{
			var pv = GetPrimitiveValue(literal);

			Assert.AreEqual(btToken, pv.BaseTypeToken);
			Assert.AreEqual(val, pv.Value);
		}

		static void TestString(string literal, string content, bool ProvideObjModule = true)
		{
			ResolutionContext ctxt;

			var block = new DBlockNode();
			if (ProvideObjModule)
				ctxt = ResolutionTestHelper.CreateDefCtxt(ResolutionTestHelper.CreateCache(out _), block);
			else
				ctxt = ResolutionTestHelper.CreateDefCtxt(new LegacyParseCacheView(new string[] { }), block);

			var x = DParser.ParseExpression(literal);

			Assert.IsInstanceOf<StringLiteralExpression>(x);
			var id = (StringLiteralExpression)x;

			var v = Evaluation.EvaluateValue(x, ctxt);

			Assert.IsInstanceOf<ArrayValue>(v);
			var av = (ArrayValue)v;
			Assert.IsTrue(av.IsString);

			Assert.AreEqual(av.StringValue, content);

			Assert.IsInstanceOf<ArrayType>(av.RepresentedType);
			var ar = (ArrayType)av.RepresentedType;

			Assert.IsInstanceOf<PrimitiveType>(ar.ValueType);
			var pt = ar.ValueType as PrimitiveType;
			Assert.IsTrue (pt.HasModifier (DTokens.Immutable));

			switch (id.Subformat)
			{
				case LiteralSubformat.Utf8:
					Assert.AreEqual(DTokens.Char, pt.TypeToken);
					break;
				case LiteralSubformat.Utf16:
					Assert.AreEqual(DTokens.Wchar, pt.TypeToken);
					break;
				case LiteralSubformat.Utf32:
					Assert.AreEqual(DTokens.Dchar, pt.TypeToken);
					break;
				default:
					Assert.Fail();
					return;
			}
		}

		static void TestBool(string literal, bool v = true)
		{
			var pv = GetPrimitiveValue(literal);

			Assert.AreEqual(DTokens.Bool, pv.BaseTypeToken);

			if (v)
				Assert.AreEqual(1M, pv.Value,  literal +" must be true");
			else
				Assert.AreEqual(0M, pv.Value, literal + " must be false");
		}

		[Test]
		public void Test2_066UCSnytax()
		{
			var x = DParser.ParseExpression("short(3)");
			var v = Evaluation.EvaluateValue(x, ResolutionTestHelper.CreateDefCtxt(""));

			Assert.IsInstanceOf<PrimitiveValue>(v);
			Assert.AreEqual(DTokens.Short, ((PrimitiveValue) v).BaseTypeToken);
			Assert.AreEqual(3m, ((PrimitiveValue) v).Value);
		}

		[Test]
		public void TestMathOperations()
		{
			TestPrimitive("0", DTokens.Int, 0M);
			TestPrimitive("-1", DTokens.Int, -1M);
			TestPrimitive("-(1+4)", DTokens.Int, -5M);

			TestPrimitive("1+2", DTokens.Int, 3M);
			TestPrimitive("1-2", DTokens.Int, -1M);
			TestPrimitive("3*4", DTokens.Int, 12M);
			TestPrimitive("3/4", DTokens.Int, 0.75M);
			TestPrimitive("35 % 2", DTokens.Int, 1M);

			TestPrimitive("3*4 + 5", DTokens.Int, 17M);
			TestPrimitive("3+5*4", DTokens.Int, 23M);
		}

		[Test]
		public void TestBooleanOps()
		{
			TestBool("true");
			TestBool("false || false", false);
			TestBool("true || false");
			TestBool("false || true");
			TestBool("true || true");
			TestBool("false && false", false);
			TestBool("false && true", false);
			TestBool("true && false", false);
			TestBool("true && true");

			TestBool("1==1");
			TestBool("0==0");
			TestBool("1!=1", false);
			TestBool("1!=0");

			TestBool("3 > 1");
			TestBool("1 > 2", false);
			TestBool("1 >= 0");
			TestBool("1 >= 1");
			TestBool("1 >= 2", false);
			TestBool("1 < 10");
			TestBool("3 < 1", false);
			TestBool("1 <= 2");
			TestBool("1 <= 1");
			TestBool("1 <= 0", false);

			//TestBool("float.nan !<>= 2");
			TestBool("2.3 <> 2.911");
			TestBool("2.1 <> 2.2");
			TestBool("2.3 <> 2.3", false);
			TestBool("1.4 <>= 1.3");
			TestBool("1.4 <>= 1.4");
			TestBool("1.4 <>= 1.5");
			//TestBool("float.nan <>= 3", false);

			TestBool("3 !> 1", false);
			TestBool("1 !> 2");
			TestBool("1 !>= 0", false);
			TestBool("1 !>= 1", false);
			TestBool("1 !>= 2");
			TestBool("1 !< 10", false);
			TestBool("3 !< 1");
			TestBool("3 !< 3");
			TestBool("1 !<= 2", false);
			TestBool("1 !<= 1", false);
			TestBool("1 !<= 0");
			//TestBool("float.nan !<= 3");
			TestBool("1.4 !<> 1.4");
			TestBool("1.4 !<> 1.5", false);

			TestBool("true ? true : false");
			TestBool("false ? true : false", false);
			TestBool("1 == 1 ? true : 2 == 1");
			TestBool("false && true ? false : true");
			TestBool("false && (true ? false: true)", false);
			
			TestBool("\"def\" == \"def\"");
			TestBool("\"def\" != \"abc\"");
		}

		[Test]
		public void TestPrimitives()
		{
			TestPrimitive("1", DTokens.Int, 1M);
			TestPrimitive("1.0", DTokens.Double, 1.0M);
			TestPrimitive("1f",DTokens.Float, 1M);
			TestPrimitive("1e+3", DTokens.Int, 1000M);
			TestPrimitive("1.0e+2", DTokens.Double, 100M);
			TestPrimitive("'c'",DTokens.Char, (decimal)(int)'c');

			TestString("\"asdf\"", "asdf", true);
			TestString("\"asdf\"c", "asdf", true);
			TestString("\"asdf\"w", "asdf", true);
			TestString("\"asdf\"d", "asdf", true);

			TestString("\"asdf\"", "asdf", false);
			TestString("\"asdf\"c", "asdf", false);
			TestString("\"asdf\"w", "asdf", false);
			TestString("\"asdf\"d", "asdf", false);

			var ctxt = ResolutionContext.Create(new LegacyParseCacheView(new string[]{}), null, new DBlockNode());

			var ex = DParser.ParseExpression("['a','s','d','f']");
			var v = Evaluation.EvaluateValue(ex, ctxt);

			Assert.IsInstanceOf<ArrayValue>(v);
			var ar = (ArrayValue)v;
			Assert.AreEqual(ar.Elements.Length, 4);

			foreach (var ev in ar.Elements)
				Assert.IsInstanceOf<PrimitiveValue>(ev);


			ex = DParser.ParseExpression("[\"KeyA\":12, \"KeyB\":33, \"KeyC\":44]");
			v = Evaluation.EvaluateValue(ex, ctxt);

			Assert.IsInstanceOf<AssociativeArrayValue>(v);
			var aa = (AssociativeArrayValue)v;
			Assert.AreEqual(aa.Elements.Count, 3);

			ex = DParser.ParseExpression("(a,b) => a+b");
			v = Evaluation.EvaluateValue(ex, ctxt);
			Assert.IsInstanceOf<DelegateValue>(v);
		}

		[Test]
		public void TestConstEval()
		{
			var ctxt = ResolutionTestHelper.CreateDefCtxt(@"module modA;
const a= 234;
enum b=123;
const int c=125;
enum int d=126;
");

			CheckPrimitiveVariableValue("a", ctxt, 234M);
			CheckPrimitiveVariableValue("b", ctxt, 123M);
			CheckPrimitiveVariableValue("c", ctxt, 125M);
			CheckPrimitiveVariableValue("d", ctxt, 126M);
			CheckPrimitiveValue("d + 4", ctxt, 130M);
			CheckPrimitiveValue("d + a", ctxt, 360M);
		}

		private static void CheckPrimitiveValue(string expression, ResolutionContext ctxt, decimal targetValue)
		{
			var v = E(expression, ctxt);

			Assert.IsInstanceOf<PrimitiveValue>(v);
			var pv = (PrimitiveValue)v;
			Assert.AreEqual(pv.Value, targetValue);
		}

		private static void CheckPrimitiveVariableValue(string expression, ResolutionContext ctxt, decimal targetValue)
		{
			var v = E(expression, ctxt, out var variableValue);

			Assert.IsNotNull(variableValue);
			Assert.IsInstanceOf<PrimitiveValue>(v);
			var pv = (PrimitiveValue)v;
			Assert.AreEqual(pv.Value, targetValue);
		}

		[Test]
		public void TestArrays()
		{
			var v = E("[11,22,33,43][1]");
			Assert.IsInstanceOf<PrimitiveValue>(v);
			Assert.AreEqual(((PrimitiveValue)v).Value, 22);

			v = E("[11,22,33,44,55,66,77,88,99,100][1..3]");

			Assert.IsInstanceOf<ArrayValue>(v);
			var av=(ArrayValue)v;
			Assert.AreEqual(av.Elements.Length,2);
			Assert.AreEqual((av.Elements[0] as PrimitiveValue).Value,22);
		}

		[Test]
		public void TestStringAccess()
		{
			var v = E("\"asdf\"[1]");
			Assert.IsInstanceOf<PrimitiveValue>(v);
			Assert.AreEqual(((PrimitiveValue)v).Value, (decimal)'s');
		}

		[Test]
		public void TestStringAccess2()
		{
			var ctxt = ResolutionTestHelper.CreateDefCtxt("module A; enum stringConstant = \"asdf\";");

			var v = E("stringConstant[1]", ctxt);
			Assert.IsInstanceOf<PrimitiveValue>(v, v?.ToCode());
			Assert.AreEqual(((PrimitiveValue)v).Value, (decimal)'s');
		}

		[Test]
		public void TestAccessExpression()
		{
			var ctxt = ResolutionTests.CreateDefCtxt(@"module modA;

class A
{
	const int someProp=3;
}

A a;");
			/*
			var v = E("a.someProp", vp);
			Assert.IsInstanceOf<PrimitiveValue>(v);
			Assert.AreEqual(((PrimitiveValue)v).Value,3);
			*/
			var v = E("A.someProp", ctxt, out var variableValue);
			Assert.IsNotNull(variableValue);
			Assert.IsInstanceOf<PrimitiveValue>(v);
			Assert.AreEqual(((PrimitiveValue)v).Value, 3M);
		}

		[Test]
		public void TestCastExpression1()
		{
			var v = E ("cast(ubyte) 20");
			Assert.IsInstanceOf<PrimitiveValue>(v);
			Assert.AreEqual(DTokens.Ubyte, (v as PrimitiveValue).BaseTypeToken);
			Assert.AreEqual(20M, (v as PrimitiveValue).Value);
		}

		static bool EvalIsExpression(string IsExpressionCode, ResolutionContext ctxt)
		{
			var v = E("is("+IsExpressionCode+")", ctxt);

			Assert.IsInstanceOf<PrimitiveValue>(v);
			var pv = (PrimitiveValue)v;
			Assert.AreEqual(pv.BaseTypeToken, DTokens.Bool, "Type of 'is(" + IsExpressionCode + ")' result must be bool");
			return pv.Value == 1M;
		}

		[Test]
		public void TestIsExpression()
		{
			var ctxt = ResolutionTestHelper.CreateDefCtxt(@"module modA;
class A {}
class B : A {}
class C : A {}

struct DynArg(int i) {
        static assert (i >= 0);

        alias i argNr;
}

template isDynArg(T) {
        static if (is(typeof(T.argNr))) {                               // must have the argNr field
                static if(is(T : DynArg!(T.argNr))) {           // now check the exact type
                        static const bool isDynArg = true;
                } else static const bool isDynArg = false;
        } else static const bool isDynArg = false;
}
");
			Assert.IsTrue(EvalIsExpression("char*[] T : U[], U : V*, V", ctxt));
			Assert.IsTrue(EvalIsExpression("string T : U[], U : immutable(V), V : char", ctxt));
			Assert.IsFalse(EvalIsExpression("int[10] X : X[Y], int Y : 5", ctxt));

			Assert.IsTrue(EvalIsExpression("bool : bool", ctxt));
			Assert.IsTrue(EvalIsExpression("bool == bool", ctxt));
			Assert.IsTrue(EvalIsExpression("C : A", ctxt));
			Assert.IsTrue(EvalIsExpression("C : C", ctxt));
			Assert.IsFalse(EvalIsExpression("C == A", ctxt));
			Assert.IsTrue(EvalIsExpression("immutable(char) == immutable", ctxt));
			Assert.IsFalse(EvalIsExpression("string == immutable", ctxt));
			Assert.IsTrue(EvalIsExpression("A == class", ctxt));
			Assert.IsTrue(EvalIsExpression("typeof(A)", ctxt));
			Assert.IsFalse(EvalIsExpression("typeof(D)", ctxt));
		}

		[Test]
		public void IsExpressionAlias()
		{
			var ctxt = ResolutionTests.CreateCtxt("A", @"module A;
static if(is(const(int)* U == const(U)*))
U var;

U derp;
");

			IExpression x;
			AbstractType t;
			DSymbol ds;

			x = DParser.ParseExpression("var");
			(x as IdentifierExpression).Location = new CodeLocation(2, 3);
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			ds = t as DSymbol;

			Assert.IsInstanceOf<MemberSymbol>(t);
			Assert.IsInstanceOf<TemplateParameterSymbol>(ds.Base);
			ds = ds.Base as DSymbol;
			Assert.IsInstanceOf<PrimitiveType>(ds.Base);
			Assert.IsTrue(!(ds.Base as PrimitiveType).HasModifiers);

			ctxt.CurrentContext.DeducedTemplateParameters.Clear();

			var dv = ctxt.MainPackage()["A"]["derp"].First() as DVariable;
			t = TypeDeclarationResolver.HandleNodeMatch(dv, ctxt);
			Assert.IsInstanceOf<MemberSymbol>(t);
			Assert.IsNull((t as MemberSymbol).Base);
		}

		[Test]
		public void IsExpressionAlias_InMethod()
		{
			var ctxt = ResolutionTests.CreateCtxt("A", @"module A;
void main(){
pre;

static if(is(const(int)* U == const(U)*))
{
U var;
}

post;
}
");
			IExpression x;
			AbstractType t;
			DSymbol ds;

			var main = ctxt.MainPackage()["A"]["main"].First() as DMethod;

			using(ctxt.Push(main, main.Body.Location))
				t = TypeDeclarationResolver.ResolveSingle(new IdentifierDeclaration("U") { Location = main.Body.SubStatements.First().Location }, ctxt);

			Assert.IsNull(t);

			using (ctxt.Push(main, main.Body.Location))
				t = TypeDeclarationResolver.ResolveSingle(new IdentifierDeclaration("U") { Location = main.Body.SubStatements.ElementAt(2).Location }, ctxt);

			Assert.IsNull(t);

			x = DParser.ParseExpression("var");

			(x as IdentifierExpression).Location = new CodeLocation(3, 7);
			
			using (ctxt.Push(main, x.Location))
				t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			ds = t as DSymbol;

			Assert.IsInstanceOf<MemberSymbol>(t);
			Assert.IsInstanceOf<TemplateParameterSymbol>(ds.Base);
			ds = ds.Base as DSymbol;
			Assert.IsInstanceOf<PrimitiveType>(ds.Base);
			Assert.IsTrue(!(ds.Base as PrimitiveType).HasModifiers);
		}

		[Test]
		public void IsExpressionVector()
		{
			var ctxt = ResolutionTests.CreateCtxt("A", @"module A;
alias int4 = int[4];
alias float4 = __vector(float[4]);
static if(is(float4 == __vector) { enum float4_isvector = true; }
static if(is(int4 == __vector) {} else { enum int4_isvector = false; }
");

			IExpression x;
			AbstractType t;
			DSymbol ds;
			ISymbolValue v;

			x = DParser.ParseExpression("float4_isvector");
			(x as IdentifierExpression).Location = new CodeLocation(2, 3);
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.IsInstanceOf<MemberSymbol>(t);
			v = Evaluation.EvaluateValue(x, ctxt);
			Assert.IsInstanceOf<PrimitiveValue>(v);
			Assert.AreEqual(1m, (v as PrimitiveValue).Value);

			x = DParser.ParseExpression("int4_isvector");
			(x as IdentifierExpression).Location = new CodeLocation(2, 3);
			v = Evaluation.EvaluateValue(x, ctxt);
			Assert.IsInstanceOf<PrimitiveValue>(v);
			Assert.AreEqual(0m, (v as PrimitiveValue).Value);
		}

		[Test]
		public void HashingTests()
		{
			testHash ("2","-2",true);
			testHash("is(typeof(TTT))");
			testHash("['a':123, 'b':456]","['a':123,'b':456]"); //TODO: Is it acceptable to build dictionaries' hashes by ignoring the order?
		}
		
		void testHash(string expressionCode, string eqExpressionCode = null, bool notEq = false)
		{
			var x = DParser.ParseExpression(expressionCode);
			var x2 = eqExpressionCode == null ? x : DParser.ParseExpression(eqExpressionCode);
			var hashVis = D_Parser.Dom.Visitors.AstElementHashingVisitor.Instance;

			var h1 = x.Accept(hashVis);
			var h2 = x2.Accept(hashVis);

			if (notEq)
			{
				Assert.AreNotEqual(h2, h1);
			}
			else
			{
				Assert.AreEqual(h2, h1);
			}
		}

		[Test]
		public void HashingTest1()
		{
			var hashVis = D_Parser.Dom.Visitors.AstElementHashingVisitor.Instance;
			var v1 = E ("123");
			var v2 = E ("-123");

			Assert.AreNotEqual(v2.Accept(hashVis), v1.Accept(hashVis));
		}

		[Test]
		public void AliasedTypeTuple()
		{
			var ctxt = ResolutionTests.CreateDefCtxt (@"module A;
template Tuple(T...) { alias Tuple = T; }
alias Tup = Tuple!(int, float, string);

template isIntOrFloat(T)
{
    static if (is(T == int) || is(T == float))
        enum isIntOrFloat = true;
    else
        enum isIntOrFloat = false;
}
");
			IExpression x;
			ISymbolValue v;
			AbstractType t;

			x = DParser.ParseExpression ("Tup[2]");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
			Assert.IsInstanceOf<ArrayType>(t);

			x = DParser.ParseExpression ("isIntOrFloat!(Tup[2])");
			v = Evaluation.EvaluateValue (x, ctxt);
			Assert.IsInstanceOf<PrimitiveValue>(v);
			Assert.AreEqual(0m, (v as PrimitiveValue).Value);

			x = DParser.ParseExpression ("Tup[0]");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
			Assert.IsInstanceOf<PrimitiveType>(t);

			x = DParser.ParseExpression ("Tup[1]");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
			Assert.IsInstanceOf<PrimitiveType>(t);

			x = DParser.ParseExpression ("isIntOrFloat!(Tup[0])");
			v = Evaluation.EvaluateValue (x, ctxt);
			Assert.IsInstanceOf<PrimitiveValue>(v);
			Assert.AreNotEqual(0m, (v as PrimitiveValue).Value);
		}

		[Test] 
		public void EponymousTemplates()
		{
			var ctxt = ResolutionTests.CreateDefCtxt (@"module B;
alias Tuple(T...) = T;
alias Tup = Tuple!(int, float, string);

enum isIntOrFloat(F) = is(F == int) || is(F == float);
");
			IExpression x;
			ISymbolValue v;
			AbstractType t;

			x = DParser.ParseExpression ("isIntOrFloat!(Tup[0])");
			v = Evaluation.EvaluateValue (x, ctxt);
			Assert.IsInstanceOf<PrimitiveValue>(v);
			Assert.AreNotEqual(0m, (v as PrimitiveValue).Value);

			DToken tk;
			var td = DParser.ParseBasicType ("Tuple!(int, float, string)", out tk);
			//t = TypeDeclarationResolver.ResolveSingle (td, ctxt);
			//Assert.IsInstanceOf<MemberSymbol>(t);
			//Assert.IsInstanceOf<DTuple>((t as MemberSymbol).Base);

			x = DParser.ParseExpression ("Tup[0]");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
			Assert.IsInstanceOf<PrimitiveType>(t);

			x = DParser.ParseExpression ("Tup[1]");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
			Assert.IsInstanceOf<PrimitiveType>(t);

			x = DParser.ParseExpression ("Tup[2]");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
			Assert.IsInstanceOf<ArrayType>(t);


			x = DParser.ParseExpression ("isIntOrFloat!int");
			v = Evaluation.EvaluateValue (x, ctxt);
			Assert.IsInstanceOf<PrimitiveValue>(v);
			Assert.AreNotEqual(0m, (v as PrimitiveValue).Value);

			x = DParser.ParseExpression ("isIntOrFloat!float");
			v = Evaluation.EvaluateValue (x, ctxt);
			Assert.IsInstanceOf<PrimitiveValue>(v);
			Assert.AreNotEqual(0m, (v as PrimitiveValue).Value);

			x = DParser.ParseExpression ("isIntOrFloat!string");
			v = Evaluation.EvaluateValue (x, ctxt);
			Assert.IsInstanceOf<PrimitiveValue>(v);
			Assert.AreEqual(0m, (v as PrimitiveValue).Value);
		}

		[Test]
		public void PtrStaticProp()
		{
			var ctxt = ResolutionTestHelper.CreateCtxt("A", @"module A; ubyte[] arr;");

			AbstractType t;
			IExpression x;

			x = DParser.ParseExpression("arr.ptr");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.IsInstanceOf<StaticProperty>(t);
			t = (t as StaticProperty).Base;
			Assert.IsInstanceOf<PointerType>(t);
			Assert.IsInstanceOf<PrimitiveType>((t as PointerType).Base);
		}

		[Test]
		public void ResolveStringAndToString()
		{
			var pcl = ResolutionTestHelper.CreateCache(out DModule modA, @"module modA;");
			var ctxt = ResolutionTestHelper.CreateDefCtxt(pcl, modA);

			var ts = ResolutionTestHelper.RS("string", ctxt);
			Assert.IsInstanceOf<ArrayType>(ts);

			var x = DParser.ParseExpression(@"(new Object).toString()");
			var t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.IsInstanceOf<ArrayType>(t);
		}

		[Test]
		public void ImplicitIntToCharConversion()
		{
			var x = DParser.ParseExpression("`a` ~ 97");
			var v = Evaluation.EvaluateValue(x, ResolutionTestHelper.CreateDefCtxt("module A;"));

			Assert.IsInstanceOf<ArrayValue>(v);
			var av = v as ArrayValue;
			Assert.IsTrue(av.IsString);
			Assert.AreEqual("aa", av.StringValue);
		}

		[Test]
		public void StaticProperty_TupleofStringof()
		{
			var ctxt = ResolutionTestHelper.CreateDefCtxt(@"module A;
struct S1 {int a; bool b;}
struct C1 {string s;}
struct T1(A) {A x; }
alias AL1 = T1!int;
alias AL2 = T1!AL1;");

			{
				var v = E("S1.tupleof.stringof", ctxt);
				Assert.IsInstanceOf<ArrayValue>(v);
				var arrayValue = v as ArrayValue;
				Assert.IsTrue(arrayValue.IsString);
				Assert.AreEqual("tuple(a, b)", arrayValue.StringValue);
			}

			{
				var v = E("C1.tupleof.stringof", ctxt);
				Assert.IsInstanceOf<ArrayValue>(v);
				var arrayValue = v as ArrayValue;
				Assert.IsTrue(arrayValue.IsString);
				Assert.AreEqual("tuple(s)", arrayValue.StringValue);
			}

			{
				var v = E("AL1.stringof", ctxt);
				Assert.IsInstanceOf<ArrayValue>(v);
				var arrayValue = v as ArrayValue;
				Assert.IsTrue(arrayValue.IsString);
				Assert.AreEqual("T1!(int)", arrayValue.StringValue);
			}

			{
				var v = E("AL2.stringof", ctxt);
				Assert.IsInstanceOf<ArrayValue>(v);
				var arrayValue = v as ArrayValue;
				Assert.IsTrue(arrayValue.IsString);
				Assert.AreEqual("T1!(T1!(int))", arrayValue.StringValue);
			}
		}

		[Test]
		[Ignore("TODO")]
		public void StaticProperty_AlignOf()
		{
			var ctxt = ResolutionTestHelper.CreateDefCtxt(@"module A;
class B(TemplateParam){
enum align_of = TemplateParam.alignof;
}
B!int b;");
			var x = DParser.ParseExpression("b.align_of");
			var v = Evaluation.EvaluateValue(x, ctxt);
			Assert.IsInstanceOf<PrimitiveValue>(v);
			var pv = v as PrimitiveValue;
			Assert.AreEqual(4m, pv.Value);
		}

		/// <summary>
		/// https://dlang.org/spec/enum.html#named_enums
		/// </summary>
		[TestFixture]
		public class EnumValueInitializers
		{
			private const string enumSampleCode = @"module A;
enum
{
    E0,
    E7 = 7,
    E8, // 8
	E8_5, // 9
    E9 = 9,
    E11 = 11,
}";

			[Test]
			public void EnumValueInitializerDefaults_AssumeFirstValue()
			{
				var ctxt = ResolutionTestHelper.CreateDefCtxt(enumSampleCode);

				var x = DParser.ParseExpression("E0");
				var v = Evaluation.EvaluateValue(x, ctxt);

				Assert.IsInstanceOf<PrimitiveValue>(v, v?.ToString());
				var pv = v as PrimitiveValue;
				Assert.AreEqual(DTokens.Int, pv.BaseTypeToken);
				Assert.AreEqual(0m, pv.Value);
			}

			[Test]
			public void EnumValueInitializerDefaults_AssumeMissingInBetweenValue()
			{
				var ctxt = ResolutionTestHelper.CreateDefCtxt(enumSampleCode);

				var x = DParser.ParseExpression("E8");
				var v = Evaluation.EvaluateValue(x, ctxt);

				Assert.IsInstanceOf<PrimitiveValue>(v, v?.ToString());
				var pv = v as PrimitiveValue;
				Assert.AreEqual(DTokens.Int, pv.BaseTypeToken);
				Assert.AreEqual(8m, pv.Value);
			}

			[Test]
			public void EnumValueInitializerDefaults_AssumeMissingInBetweenValue2()
			{
				var ctxt = ResolutionTestHelper.CreateDefCtxt(enumSampleCode);

				var x = DParser.ParseExpression("E8_5");
				var v = Evaluation.EvaluateValue(x, ctxt);

				Assert.IsInstanceOf<PrimitiveValue>(v, v?.ToString());
				var pv = v as PrimitiveValue;
				Assert.AreEqual(DTokens.Int, pv.BaseTypeToken);
				Assert.AreEqual(9m, pv.Value);
			}

			[Test]
			public void EnumValueInitializerDefaults_RegularInitializer()
			{
				var ctxt = ResolutionTestHelper.CreateDefCtxt(enumSampleCode);

				var x = DParser.ParseExpression("E9");
				var v = Evaluation.EvaluateValue(x, ctxt);

				Assert.IsInstanceOf<PrimitiveValue>(v, v?.ToString());
				var pv = v as PrimitiveValue;
				Assert.AreEqual(DTokens.Int, pv.BaseTypeToken);
				Assert.AreEqual(9m, pv.Value);
			}

			[Test]
			public void EnumValueInitializerDefaults_RegularInitializer2()
			{
				var ctxt = ResolutionTestHelper.CreateDefCtxt(@"module A;
enum {
E0,
E1,
E2
}");

				var x = DParser.ParseExpression("E2");
				var v = Evaluation.EvaluateValue(x, ctxt);

				Assert.IsInstanceOf<PrimitiveValue>(v, v?.ToString());
				var pv = v as PrimitiveValue;
				Assert.AreEqual(DTokens.Int, pv.BaseTypeToken);
				Assert.AreEqual(2m, pv.Value);
			}
		}
	}
}
