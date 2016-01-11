using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.TypeResolution;
using NUnit.Framework;
using D_Parser.Dom;
using D_Parser.Misc;
using D_Parser.Dom.Statements;

namespace Tests
{
	[TestFixture]
	public class EvaluationTests
	{
		public static AbstractSymbolValueProvider GetDefaultSymbolVP()
		{
			return new StandardValueProvider(new ResolutionContext(new LegacyParseCacheView(new string[0]), null));
		}

		public static ISymbolValue E(string expression, AbstractSymbolValueProvider vp=null)
		{
			return Evaluation.EvaluateValue(DParser.ParseExpression(expression), vp ?? GetDefaultSymbolVP());
		}

		public static PrimitiveValue GetPrimitiveValue(string literalCode,AbstractSymbolValueProvider vp=null)
		{
			var v = E(literalCode,vp ?? GetDefaultSymbolVP());

			Assert.That(v,Is.TypeOf(typeof(PrimitiveValue)));
			return (PrimitiveValue)v;
		}

		public static void TestPrimitive(string literal, int btToken, object val, AbstractSymbolValueProvider vp=null)
		{
			var pv = GetPrimitiveValue(literal, vp ?? GetDefaultSymbolVP());

			Assert.That(pv.BaseTypeToken, Is.EqualTo(btToken));
			Assert.That(pv.Value, Is.EqualTo(val));
		}

		public static void TestString(string literal, string content, bool ProvideObjModule = true)
		{
			ResolutionContext ctxt = null;

			var block = new DBlockNode();
			if (ProvideObjModule)
				ctxt = ResolutionTests.CreateDefCtxt(ResolutionTests.CreateCache(), block);
			else
				ctxt = ResolutionTests.CreateDefCtxt(new LegacyParseCacheView(new string[] { }), block);

			var x = DParser.ParseExpression(literal);

			Assert.That(x,Is.TypeOf(typeof(IdentifierExpression)));
			var id = (IdentifierExpression)x;

			var v = Evaluation.EvaluateValue(x, ctxt);

			Assert.That(v,Is.TypeOf(typeof(ArrayValue)));
			var av = (ArrayValue)v;
			Assert.That(av.IsString, Is.True);

			Assert.AreEqual(av.StringValue, content);

			Assert.That(av.RepresentedType, Is.TypeOf(typeof(ArrayType)));
			var ar = (ArrayType)av.RepresentedType;

			Assert.That (ar.ValueType, Is.TypeOf (typeof(PrimitiveType)));
			var pt = ar.ValueType as PrimitiveType;
			Assert.That (pt.Modifier, Is.EqualTo (DTokens.Immutable));

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

		public static void TestBool(string literal, bool v = true, AbstractSymbolValueProvider vp =null)
		{
			var pv = GetPrimitiveValue(literal, vp);

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
			var v = Evaluation.EvaluateValue(x, ResolutionTests.CreateDefCtxt());

			Assert.That(v, Is.TypeOf(typeof(PrimitiveValue)));
			Assert.That((v as PrimitiveValue).BaseTypeToken, Is.EqualTo(DTokens.Short));
			Assert.That((v as PrimitiveValue).Value, Is.EqualTo(3m));
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

			var ctxt = new ResolutionContext(new LegacyParseCacheView(new string[]{}), null, new DBlockNode());

			var ex = DParser.ParseExpression("['a','s','d','f']");
			var v = Evaluation.EvaluateValue(ex, ctxt);

			Assert.That(v,Is.TypeOf(typeof(ArrayValue)));
			var ar = (ArrayValue)v;
			Assert.AreEqual(ar.Elements.Length, 4);

			foreach (var ev in ar.Elements)
				Assert.That(ev, Is.TypeOf(typeof(PrimitiveValue)));


			ex = DParser.ParseExpression("[\"KeyA\":12, \"KeyB\":33, \"KeyC\":44]");
			v = Evaluation.EvaluateValue(ex, ctxt);

			Assert.That(v, Is.TypeOf(typeof(AssociativeArrayValue)));
			var aa = (AssociativeArrayValue)v;
			Assert.AreEqual(aa.Elements.Count, 3);

			ex = DParser.ParseExpression("(a,b) => a+b");
			v = Evaluation.EvaluateValue(ex, ctxt);
			Assert.That(v, Is.TypeOf(typeof(DelegateValue)));
		}

		[Test]
		public void TestConstEval()
		{
			var pcl = ResolutionTests.CreateCache(@"module modA;
const a= 234;
enum b=123;
const int c=125;
enum int d=126;
");
			var vp = new StandardValueProvider(ResolutionContext.Create(pcl, null, pcl.FirstPackage()["modA"]));

			var v = E("a", vp);

			Assert.That(v, Is.TypeOf(typeof(VariableValue)));
			var val = vp[((VariableValue)v).Variable];

			Assert.That(val, Is.TypeOf(typeof(PrimitiveValue)));
			var pv = (PrimitiveValue)val;

			Assert.AreEqual(pv.Value, 234M);

			v = E("b", vp);
			val = vp[((VariableValue)v).Variable];
			pv=(PrimitiveValue)val;
			Assert.AreEqual(pv.Value, 123M);

			v = E("c", vp);
			val = vp[((VariableValue)v).Variable];
			pv = (PrimitiveValue)val;
			Assert.AreEqual(pv.Value, 125M);

			v = E("d", vp);
			val = vp[((VariableValue)v).Variable];
			pv = (PrimitiveValue)val;
			Assert.AreEqual(pv.Value, 126M);

			pv = E("d + 4", vp) as PrimitiveValue;
			Assert.IsNotNull(pv);
			Assert.AreEqual(130M, pv.Value);

			pv = E("d + a", vp) as PrimitiveValue;
			Assert.IsNotNull(pv);
			Assert.AreEqual(360M, pv.Value);
		}

		[Test]
		public void TestArrays()
		{
			var vp = new StandardValueProvider(null);

			var v = E("[11,22,33,43][1]", vp);
			Assert.That(v, Is.TypeOf(typeof(PrimitiveValue)));
			Assert.AreEqual(((PrimitiveValue)v).Value, 22);

			v = E("[11,22,33,44,55,66,77,88,99,100][1..3]", vp);

			Assert.That(v, Is.TypeOf(typeof(ArrayValue)));
			var av=(ArrayValue)v;
			Assert.AreEqual(av.Elements.Length,2);
			Assert.AreEqual((av.Elements[0] as PrimitiveValue).Value,22);
		}

		[Test]
		public void TestAccessExpression()
		{
			var pcl = ResolutionTests.CreateCache(@"module modA;

class A
{
	const int someProp=3;
}

A a;");

			var vp = new StandardValueProvider(ResolutionContext.Create(pcl, null, pcl.FirstPackage()["modA"]));
			/*
			var v = E("a.someProp", vp);
			Assert.IsInstanceOfType(v, typeof(PrimitiveValue));
			Assert.AreEqual(((PrimitiveValue)v).Value,3);
			*/
			var v = E("A.someProp", vp);
			Assert.That(v, Is.TypeOf(typeof(VariableValue)));
			var vv = vp[((VariableValue)v).Variable] as PrimitiveValue;
			Assert.AreEqual(3, vv.Value);
		}

		[Test]
		public void TestCastExpression1()
		{
			var ctxt = ResolutionTests.CreateCtxt("A",@"module A;");
			var vp = new StandardValueProvider (ctxt);

			IExpression x;
			ISymbolValue v;

			x = DParser.ParseExpression ("cast(ubyte)20");
			v = Evaluation.EvaluateValue (x, vp);

			Assert.That (v, Is.TypeOf (typeof(PrimitiveValue)));
			Assert.That ((v as PrimitiveValue).BaseTypeToken, Is.EqualTo (DTokens.Ubyte));
			Assert.That ((v as PrimitiveValue).Value, Is.EqualTo(20M));
		}

		public static bool EvalIsExpression(string IsExpressionCode, AbstractSymbolValueProvider vp)
		{
			var e = DParser.ParseExpression("is("+IsExpressionCode+")");

			var v = Evaluation.EvaluateValue(e, vp);

			Assert.That(v, Is.TypeOf(typeof(PrimitiveValue)));
			var pv = (PrimitiveValue)v;
			Assert.AreEqual(pv.BaseTypeToken, DTokens.Bool, "Type of 'is(" + IsExpressionCode + ")' result must be bool");
			return pv.Value == 1M;
		}

		[Test]
		public void TestIsExpression()
		{
			var pcl = ResolutionTests.CreateCache(@"module modA;
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

			var vp = new StandardValueProvider(ResolutionContext.Create(pcl, null,pcl.FirstPackage()["modA"]));

			Assert.IsTrue(EvalIsExpression("char*[] T : U[], U : V*, V", vp));
			Assert.IsTrue(EvalIsExpression("string T : U[], U : immutable(V), V : char", vp));
			Assert.IsFalse(EvalIsExpression("int[10] X : X[Y], int Y : 5",vp));

			Assert.IsTrue(EvalIsExpression("bool : bool", vp));
			Assert.IsTrue(EvalIsExpression("bool == bool", vp));
			Assert.IsTrue(EvalIsExpression("C : A", vp));
			Assert.IsTrue(EvalIsExpression("C : C", vp));
			Assert.IsFalse(EvalIsExpression("C == A", vp));
			Assert.IsTrue(EvalIsExpression("immutable(char) == immutable", vp));
			Assert.IsFalse(EvalIsExpression("string == immutable", vp));
			Assert.IsTrue(EvalIsExpression("A == class", vp));
			Assert.IsTrue(EvalIsExpression("typeof(A)", vp));
			Assert.IsFalse(EvalIsExpression("typeof(D)", vp));
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

			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That(ds.Base, Is.TypeOf(typeof(TemplateParameterSymbol)));
			ds = ds.Base as DSymbol;
			Assert.That(ds.Base, Is.TypeOf(typeof(PrimitiveType)));
			Assert.That((ds.Base as PrimitiveType).Modifier, Is.EqualTo(0));

			ctxt.CurrentContext.DeducedTemplateParameters.Clear();

			var dv = ctxt.MainPackage()["A"]["derp"].First() as DVariable;
			t = TypeDeclarationResolver.HandleNodeMatch(dv, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((t as MemberSymbol).Base, Is.Null);
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

			Assert.That(t, Is.Null);

			using (ctxt.Push(main, main.Body.Location))
				t = TypeDeclarationResolver.ResolveSingle(new IdentifierDeclaration("U") { Location = main.Body.SubStatements.ElementAt(2).Location }, ctxt);

			Assert.That(t, Is.Null);

			x = DParser.ParseExpression("var");

			(x as IdentifierExpression).Location = new CodeLocation(3, 7);
			
			using (ctxt.Push(main, x.Location))
				t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			ds = t as DSymbol;

			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That(ds.Base, Is.TypeOf(typeof(TemplateParameterSymbol)));
			ds = ds.Base as DSymbol;
			Assert.That(ds.Base, Is.TypeOf(typeof(PrimitiveType)));
			Assert.That((ds.Base as PrimitiveType).Modifier, Is.EqualTo(0));
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
			
			Assert.That(h1, notEq ? Is.Not.EqualTo(h2) : Is.EqualTo(h2));
		}

		[Test]
		public void HashingTest1()
		{
			var hashVis = D_Parser.Dom.Visitors.AstElementHashingVisitor.Instance;
			var vp = new StandardValueProvider (ResolutionTests.CreateCtxt ("A", "module A;"));
			var v1 = Evaluation.EvaluateValue (DParser.ParseExpression ("123"), vp);
			var v2 = Evaluation.EvaluateValue (DParser.ParseExpression ("-123"), vp);

			Assert.That (v1.Accept(hashVis), Is.Not.EqualTo(v2.Accept(hashVis)));
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
			Assert.That (t, Is.TypeOf(typeof(ArrayType)));

			x = DParser.ParseExpression ("isIntOrFloat!(Tup[2])");
			v = Evaluation.EvaluateValue (x, ctxt);
			Assert.That(v, Is.TypeOf(typeof(PrimitiveValue)));
			Assert.That ((v as PrimitiveValue).Value, Is.EqualTo (0m));

			x = DParser.ParseExpression ("Tup[0]");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
			Assert.That (t, Is.TypeOf(typeof(PrimitiveType)));

			x = DParser.ParseExpression ("Tup[1]");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
			Assert.That (t, Is.TypeOf(typeof(PrimitiveType)));

			x = DParser.ParseExpression ("isIntOrFloat!(Tup[0])");
			v = Evaluation.EvaluateValue (x, ctxt);
			Assert.That(v, Is.TypeOf(typeof(PrimitiveValue)));
			Assert.That ((v as PrimitiveValue).Value, Is.Not.EqualTo (0m));
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
			Assert.That(v, Is.TypeOf(typeof(PrimitiveValue)));
			Assert.That ((v as PrimitiveValue).Value, Is.Not.EqualTo (0m));

			DToken tk;
			var td = DParser.ParseBasicType ("Tuple!(int, float, string)", out tk);
			//t = TypeDeclarationResolver.ResolveSingle (td, ctxt);
			//Assert.That (t, Is.TypeOf(typeof(MemberSymbol)));
			//Assert.That ((t as MemberSymbol).Base, Is.TypeOf(typeof(DTuple)));

			x = DParser.ParseExpression ("Tup[0]");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
			Assert.That (t, Is.TypeOf(typeof(PrimitiveType)));

			x = DParser.ParseExpression ("Tup[1]");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
			Assert.That (t, Is.TypeOf(typeof(PrimitiveType)));

			x = DParser.ParseExpression ("Tup[2]");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
			Assert.That (t, Is.TypeOf(typeof(ArrayType)));


			x = DParser.ParseExpression ("isIntOrFloat!int");
			v = Evaluation.EvaluateValue (x, ctxt);
			Assert.That(v, Is.TypeOf(typeof(PrimitiveValue)));
			Assert.That ((v as PrimitiveValue).Value, Is.Not.EqualTo (0m));

			x = DParser.ParseExpression ("isIntOrFloat!float");
			v = Evaluation.EvaluateValue (x, ctxt);
			Assert.That(v, Is.TypeOf(typeof(PrimitiveValue)));
			Assert.That ((v as PrimitiveValue).Value, Is.Not.EqualTo (0m));

			x = DParser.ParseExpression ("isIntOrFloat!string");
			v = Evaluation.EvaluateValue (x, ctxt);
			Assert.That(v, Is.TypeOf(typeof(PrimitiveValue)));
			Assert.That ((v as PrimitiveValue).Value, Is.EqualTo (0m));
		}
		
		#region Traits
		[Test]
		public void Traits()
		{
			var pcl = ResolutionTests.CreateCache(@"module A;
int i;
string s;

abstract class C { int foo(); }
class NC { int foo(); }

C c;
int[] dynArr;
int[5] statArr;
auto assocArr = ['c' : 23, 'b' : 84];

struct S {
  void bar() { }
  void bar(int i) {}
  void bar(string s) {}
  static int statInt;
}

class D {
	void bar() { }
	abstract void absBar();
	static void statFoo() {}
	final void finBar() {};
	private int privInt;
	package int packInt;
}

class E : D{
	final override void absBar()
	{
		
	}
	
	final void bar() {}
}

interface I {
  void bar();
}

template Tmpl(){
	void bar();
}
", @"module std.someStd;");
			var ctxt = ResolutionTests.CreateDefCtxt(pcl, pcl.FirstPackage()["A"], null);
			
			BoolTrait(ctxt, "isArithmetic, int");
			BoolTrait(ctxt, "isArithmetic, i, i+1, int");
			BoolTrait(ctxt, "isArithmetic", false);
			BoolTrait(ctxt, "isArithmetic, int*", false);
			BoolTrait(ctxt, "isArithmetic, s, 123", false);
			BoolTrait(ctxt, "isArithmetic, 123, s", false);
			
			BoolTrait(ctxt, "isAbstractClass, C, c");
			BoolTrait(ctxt, "isAbstractClass, C");
			BoolTrait(ctxt, "isAbstractClass, int", false);
			BoolTrait(ctxt, "isAbstractClass, NC", false);
			BoolTrait(ctxt, "isAbstractClass", false);
			
			BoolTrait(ctxt, "isAssociativeArray, assocArr");
			BoolTrait(ctxt, "isAssociativeArray, dynArr", false);
			BoolTrait(ctxt, "isStaticArray, statArr");
			BoolTrait(ctxt, "isStaticArray, dynArr",false);
			
			BoolTrait(ctxt, "isVirtualMethod, D.bar");
			BoolTrait(ctxt, "isVirtualMethod, D.absBar");
			BoolTrait(ctxt, "isVirtualMethod, I.bar");
			BoolTrait(ctxt, "isVirtualMethod, Tmpl!().bar");
			//BoolTrait(ctxt, "isVirtualMethod, E.bar");
			//BoolTrait(ctxt, "isVirtualMethod, E.absBar");
			BoolTrait(ctxt, "isVirtualMethod, S.bar", false);
			BoolTrait(ctxt, "isVirtualMethod, D.statFoo", false);
			BoolTrait(ctxt, "isVirtualMethod, D.finBar", false);
			
			BoolTrait(ctxt, "isVirtualFunction, D.bar");
			BoolTrait(ctxt, "isVirtualFunction, D.absBar");
			BoolTrait(ctxt, "isVirtualFunction, I.bar");
			BoolTrait(ctxt, "isVirtualFunction, Tmpl!().bar");
			//BoolTrait(ctxt, "isVirtualFunction, E.bar");
			//BoolTrait(ctxt, "isVirtualFunction, E.absBar");
			BoolTrait(ctxt, "isVirtualFunction, S.bar", false);
			BoolTrait(ctxt, "isVirtualFunction, D.statFoo", false);
			BoolTrait(ctxt, "isVirtualFunction, D.finBar");
			
			BoolTrait(ctxt, "hasMember, C, \"foo\"");
			BoolTrait(ctxt, "hasMember, c, \"foo\"");
			BoolTrait(ctxt, "hasMember, C, \"noFoo\"", false);
			BoolTrait(ctxt, "hasMember, int, \"sizeof\"");
			
			var x = DParser.ParseExpression(@"__traits(identifier, C.aso.derp)");
			var v = Evaluation.EvaluateValue(x, ctxt);
			
			Assert.That(v, Is.TypeOf(typeof(ArrayValue)));
			var av = v as ArrayValue;
			Assert.That(av.IsString, Is.True);
			Assert.That(av.StringValue, Is.EqualTo("C.aso.derp"));
			
			x = DParser.ParseExpression("__traits(getMember, c, \"foo\")");
			var t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			
			
			
			x = DParser.ParseExpression("__traits(getOverloads, S, \"bar\")");
			v = Evaluation.EvaluateValue(x, ctxt);
			Assert.That(v, Is.TypeOf(typeof(TypeValue)));
			Assert.That((v as TypeValue).RepresentedType, Is.TypeOf(typeof(DTuple)));
			
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(DTuple)));
			
			
			x = DParser.ParseExpression("__traits(getProtection, D.privInt)");
			v = Evaluation.EvaluateValue(x, ctxt);
			
			Assert.That(v, Is.TypeOf(typeof(ArrayValue)));
			av = v as ArrayValue;
			Assert.That(av.IsString, Is.True);
			Assert.That(av.StringValue, Is.EqualTo("private"));
			
			x = DParser.ParseExpression("__traits(getProtection, D)");
			v = Evaluation.EvaluateValue(x, ctxt);
			
			Assert.That(v, Is.TypeOf(typeof(ArrayValue)));
			av = v as ArrayValue;
			Assert.That(av.IsString, Is.True);
			Assert.That(av.StringValue, Is.EqualTo("public"));
			
			x = DParser.ParseExpression("__traits(getProtection, D.packInt)");
			v = Evaluation.EvaluateValue(x, ctxt);
			
			Assert.That(v, Is.TypeOf(typeof(ArrayValue)));
			av = v as ArrayValue;
			Assert.That(av.IsString, Is.True);
			Assert.That(av.StringValue, Is.EqualTo("package"));
			
			BoolTrait(ctxt, "isSame, int, int");
			BoolTrait(ctxt, "isSame, int, double", false);
			BoolTrait(ctxt, "isSame, C, D", false);
			BoolTrait(ctxt, "isSame, D, D");
			
			BoolTrait(ctxt, "compiles", false);
			BoolTrait(ctxt, "compiles, asd.herp", false);
			BoolTrait(ctxt, "compiles, i");
			BoolTrait(ctxt, "compiles, i + 1");
			//BoolTrait(ctxt, "compiles, &i + 1", false); //TODO: Check if both operand types match..is this still efficient?
			BoolTrait(ctxt, "compiles, typeof(1)");
			BoolTrait(ctxt, "compiles, S.nonExistingItem", false); //TODO: Make the resolver not resolve non-static items implicitly (i.e. without explicit resolution option)
			BoolTrait(ctxt, "compiles, S.statInt");
			BoolTrait(ctxt, "compiles, 1,2,3,int,long,std");
			BoolTrait(ctxt, "compiles, 1,2,3,int,long,3[1]", false);
			BoolTrait(ctxt, "compiles, 3[1]", false);
			BoolTrait(ctxt, "compiles, immutable(S44)(3, &i)", false);
		}
		
		void BoolTrait(ResolutionContext ctxt,string traitCode, bool shallReturnTrue = true)
		{
			var x = DParser.ParseExpression("__traits("+traitCode+")");
			var v = Evaluation.EvaluateValue(x, ctxt);
			
			Assert.That(v, Is.TypeOf(typeof(PrimitiveValue)));
			Assert.That((v as PrimitiveValue).BaseTypeToken, Is.EqualTo(DTokens.Bool));
			Assert.That((v as PrimitiveValue).Value, Is.EqualTo(shallReturnTrue ? 1m : 0m));
		}
		
		#endregion
	}
}
