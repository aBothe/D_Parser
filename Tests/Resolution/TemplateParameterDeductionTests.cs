using System.Collections.Generic;
using System.Linq;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.TypeResolution;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Resolution
{
	[TestClass]
	public class TemplateParameterDeductionTests : ResolutionTestHelper
	{
		[TestMethod]
		public void BasicResolution2()
		{
			var ctxt = CreateDefCtxt(@"module A;
struct Thing(T)
{
	public T property;
}

alias Thing!(int) IntThing;
alias Thing SomeThing;
");

			IExpression ex;
			AbstractType t;

			ex = DParser.ParseExpression("SomeThing!int");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOfType(t, typeof(StructType));

			ex = DParser.ParseExpression("Thing!int");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOfType(t, typeof(StructType));

			ex = DParser.ParseExpression("IntThing");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOfType(t, typeof(StructType));
		}

		[TestMethod]
		public void BasicResolution3()
		{
			var ctxt = CreateDefCtxt(@"module A;
struct Thing(T)
{
	public T property;
}

alias Thing!(int) IntThing;
alias Thing SomeThing;
");

			IExpression ex;
			AbstractType t;

			ex = DParser.ParseExpression("new Thing!int(123)");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt, false);
			Assert.IsInstanceOfType(t, typeof(MemberSymbol)); // Returns struct ctor
			var ctor = t as MemberSymbol;
			Assert.AreEqual(DMethod.ConstructorIdentifier, ctor.Name);
			Assert.IsInstanceOfType(ctor.Definition, typeof(DMethod));
			var ctorDefinition = ctor.Definition as DMethod;
			Assert.AreEqual(1, ctorDefinition.Parameters.Count);

			ex = DParser.ParseExpression("new IntThing");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOfType(t, typeof(StructType));
		}

		[TestMethod]
		public void BasicResolution4()
		{
			var ctxt = CreateDefCtxt(@"module A;
class Blupp : Blah!(Blupp) {}
class Blah(T){ T b; }");

			var ex = DParser.ParseExpression("Blah!Blupp");
			var t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOfType(t, typeof(ClassType));
		}

		[TestMethod]
		public void TestParamDeduction1()
		{
			var ctxt = CreateDefCtxt(@"module modA;

//void foo(T:MyClass!E,E)(T t) {}
int foo(Y,T)(Y y, T t) {}
//string[] foo(T)(T t, T u) {}

class A {
	const void aBar(this T)() {}
}
class B:A{}
class C:B{}

class MyClass(T) { T tvar; }
class MyClass(T:A) {}
class MyClass(T:B) {}

class D(int u) {}
class D(int u:1) {}

const int a=3;
int b=4;
");

			var instanceExpr = DParser.ParseExpression("(new MyClass!int).tvar");

			Assert.IsInstanceOfType(instanceExpr, typeof(PostfixExpression_Access));

			var res = ExpressionTypeEvaluation.EvaluateType(instanceExpr, ctxt);

			Assert.IsInstanceOfType(res, typeof(MemberSymbol));
			var mr = (MemberSymbol)res;

			Assert.IsInstanceOfType(mr.Base, typeof(TemplateParameterSymbol));
			var tps = (TemplateParameterSymbol)mr.Base;
			Assert.IsInstanceOfType(tps.Base, typeof(PrimitiveType));
			var sr = (PrimitiveType)tps.Base;

			Assert.AreEqual(sr.TypeToken, DTokens.Int);
		}

		[TestMethod]
		public void TestParamDeduction2()
		{
			var ctxt = CreateDefCtxt(@"
module modA;
T foo(T)() {}
");

			var call = DParser.ParseExpression("foo!int()");
			var bt = ExpressionTypeEvaluation.EvaluateType(call, ctxt);

			Assert.IsInstanceOfType(bt, typeof(TemplateParameterSymbol));
			var tps = (TemplateParameterSymbol)bt;
			Assert.IsInstanceOfType(tps.Base, typeof(PrimitiveType), "Resolution returned empty result instead of 'int'");
			var st = (PrimitiveType)tps.Base;
			Assert.IsNotNull(st, "Result must be Static type int");
			Assert.AreEqual(st.TypeToken, DTokens.Int, "Static type must be int");
		}

		[TestMethod]
		public void TestParamDeduction3()
		{
			var ctxt = CreateDefCtxt(@"module modA;

class A {}
class A2 {}

class B(T){
	class C(T2) : T {} 
}");
			var inst = DParser.ParseExpression("(new B!A).new C!A2"); // TODO
		}

		[TestMethod]
		public void TestParamDeduction4()
		{
			var ctxt = CreateDefCtxt(@"module modA;

void fo(T:U[], U)(T o) {}
void f(T:U[n], U,int n)(T o) {}

char[5] arr;

double foo(T)(T a) {}

int delegate(int b) myDeleg;

");
			ctxt.CurrentContext.ContextDependentOptions |= ResolutionOptions.ReturnMethodReferencesOnly;

			IExpression x;
			AbstractType r;
			MemberSymbol mr;

			x = DParser.ParseExpression("fo!(char[5])");
			r = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			mr = r as MemberSymbol;
			Assert.IsInstanceOfType(r, typeof(MemberSymbol));

			x = DParser.ParseExpression("f!(char[5])");
			r = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			mr = r as MemberSymbol;
			Assert.IsInstanceOfType(r, typeof(MemberSymbol));

			var v = mr.DeducedTypes[2].ParameterValue;
			Assert.IsInstanceOfType(v, typeof(PrimitiveValue));
			Assert.AreEqual(5M, ((PrimitiveValue)v).Value);



			x = DParser.ParseExpression("fo!(immutable(char)[])");
			r = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			mr = r as MemberSymbol;
			Assert.IsInstanceOfType(r, typeof(MemberSymbol));

			x = DParser.ParseExpression("myDeleg");
			r = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			mr = r as MemberSymbol;
			Assert.IsNotNull(mr);
			Assert.IsInstanceOfType(mr.Base, typeof(DelegateType));

			x = DParser.ParseExpression("myDeleg(123)");
			r = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.IsInstanceOfType(r, typeof(DelegateCallSymbol));
			Assert.IsInstanceOfType((r as DerivedDataType).Base, typeof(PrimitiveType));

			x = DParser.ParseExpression("foo(myDeleg(123))");
			r = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			mr = r as MemberSymbol;
			Assert.IsNotNull(mr);
			Assert.IsInstanceOfType(mr.Base, typeof(PrimitiveType));
		}

		[TestMethod]
		public void TestParamDeduction5()
		{
			var pcl = CreateCache(out DModule mod, @"module modA;
struct Params{}
class IConn ( P ){}
class Conn : IConn!(Params){}
class IRegistry ( P ){}
class Registry (C : IConn!(Params) ) : IRegistry!(Params){}
class ConcreteRegistry : Registry!(Conn){}
class IClient ( P, R : IRegistry!(P) ){}
class Client : IClient!(Params, ConcreteRegistry){}");

			var Client = mod["Client"].First() as DClassLike;
			var ctxt = CreateDefCtxt(pcl, mod);

			var res = TypeDeclarationResolver.HandleNodeMatch(Client, ctxt);
			Assert.IsInstanceOfType(res, typeof(ClassType));
			var ct = (ClassType)res;

			Assert.IsInstanceOfType(ct.Base, typeof(ClassType));
			ct = (ClassType)ct.Base;

			Assert.AreEqual(ct.DeducedTypes.Count, 2);
			var dedtype = ct.DeducedTypes[0];
			Assert.AreEqual("P", dedtype.Name);
			Assert.AreEqual(mod["Params"].First(), ((DSymbol)dedtype.Base).Definition);
			dedtype = ct.DeducedTypes[1];
			Assert.AreEqual("R", dedtype.Name);
			Assert.AreEqual(mod["ConcreteRegistry"].First(), ((DSymbol)dedtype.Base).Definition);


			ctxt.CurrentContext.Set(mod);
			DToken opt = null;
			var tix = DParser.ParseBasicType("IClient!(Params,ConcreteRegistry)", out opt);
			res = RS(tix, ctxt);

			Assert.IsInstanceOfType(res, typeof(ClassType));
		}

		[TestMethod]
		public void TestParamDeduction6()
		{
			var pcl = CreateCache(out DModule mod, @"module modA;
class A(T) {}
class B : A!int{}
class C(U: A!int){}
class D : C!B {}");

			var ctxt = CreateDefCtxt(pcl, mod);

			var res = TypeDeclarationResolver.HandleNodeMatch(mod["D"].First(), ctxt);
			Assert.IsInstanceOfType(res, typeof(ClassType));
			var ct = (ClassType)res;

			Assert.IsInstanceOfType(ct.Base, typeof(ClassType));
			ct = (ClassType)ct.Base;

			Assert.AreEqual(1, ct.DeducedTypes.Count);
		}

		[TestMethod]
		public void TestParamDeduction7()
		{
			var pcl = CreateCache(out DModule A, @"module A;
U genA(U)();
T delegate(T dgParam) genDelegate(T)();");

			var ctxt = CreateDefCtxt(pcl, A);

			var ex = DParser.ParseExpression("genDelegate!int()");
			var t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOfType(t, typeof(DelegateType));
			var dt = (DelegateType)t;
			Assert.IsNotNull(dt.Base);
			Assert.IsNotNull(dt.Parameters);

			ex = DParser.ParseExpression("genA!int()");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOfType(t, typeof(TemplateParameterSymbol));
		}

		[TestMethod]
		public void TestParamDeduction8_Appender()
		{
			var pcl = CreateCache(out DModule A, @"module A;
double[] darr;
struct Appender(A:E[],E) { A data; }

Appender!(E[]) appender(A : E[], E)(A array = null)
{
    return Appender!(E[])(array);
}");
			var ctxt = CreateDefCtxt(pcl, A);

			var ex = DParser.ParseExpression("new Appender!(double[])(darr)");
			var t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt, false);
			Assert.IsInstanceOfType(t, typeof(MemberSymbol)); // ctor
			Assert.IsInstanceOfType((t as MemberSymbol).Base, typeof(StructType));

			ex = DParser.ParseExpression("appender!(double[])()");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOfType(t, typeof(StructType));
			var ss = t as StructType;
			Assert.IsTrue(ss.DeducedTypes.Count > 0);
		}

		[TestMethod]
		public void TestParamDeduction9()
		{
			var ctxt = CreateDefCtxt(@"module A;
const int k = 4;
template mxTemp(int i)
{
	static if(i < 0)
		enum mxTemp = ""int"";
	else
		enum mxTemp = ""bool"";
}

template def(int i,string name)
{
	enum def = mxTemp!(-i) ~ "" ""~name~"";"";
}

template def2(int i)
{
	enum def2 = i;
}

mixin(def!(-1,""bar""));
");
			IExpression ex;
			ISymbolValue val;

			ex = DParser.ParseExpression(@"def!(-2,""someVar"")");
			val = Evaluation.EvaluateValue(ex, ctxt);
			Assert.IsInstanceOfType(val, typeof(ArrayValue));
			Assert.IsTrue((val as ArrayValue).IsString);
			Assert.AreEqual("bool someVar;", (val as ArrayValue).StringValue);

			var def = ctxt.MainPackage()["A"]["def"].First() as DClassLike;
			var defS = new TemplateType(def, new[]{
				new TemplateParameterSymbol(def.TemplateParameters[0], new PrimitiveValue(2)),
				new TemplateParameterSymbol(def.TemplateParameters[1], new ArrayValue(Evaluation.GetStringType(ctxt), "someVar"))
			});
			using (ctxt.Push(defS))
			{
				ex = DParser.ParseExpression("i");
				val = Evaluation.EvaluateValue(ex, ctxt);
				Assert.IsInstanceOfType(val, typeof(PrimitiveValue));
				Assert.AreEqual(2m, (val as PrimitiveValue).Value);

				ex = DParser.ParseExpression("mxTemp!(-i)");
				val = Evaluation.EvaluateValue(ex, ctxt);
				Assert.IsInstanceOfType(val, typeof(ArrayValue));
				Assert.IsTrue((val as ArrayValue).IsString);
				Assert.AreEqual("int", (val as ArrayValue).StringValue);

				ex = DParser.ParseExpression("mxTemp!(-i) ~ \" \"~name~\";\"");
				val = Evaluation.EvaluateValue(ex, ctxt);
				Assert.IsInstanceOfType(val, typeof(ArrayValue));
				Assert.IsTrue((val as ArrayValue).IsString);
				Assert.AreEqual("int someVar;", (val as ArrayValue).StringValue);
			}


			ex = DParser.ParseExpression("def2!5");
			val = Evaluation.EvaluateValue(ex, ctxt);
			Assert.IsInstanceOfType(val, typeof(PrimitiveValue));
			Assert.AreEqual(5m, (val as PrimitiveValue).Value);

			ex = DParser.ParseExpression("-k");
			val = Evaluation.EvaluateValue(ex, ctxt);
			Assert.IsInstanceOfType(val, typeof(PrimitiveValue));
			Assert.AreEqual(-4m, (val as PrimitiveValue).Value);

			ex = DParser.ParseExpression("mxTemp!(-k)");
			val = Evaluation.EvaluateValue(ex, ctxt);
			Assert.IsInstanceOfType(val, typeof(ArrayValue));
			Assert.IsTrue((val as ArrayValue).IsString);
			Assert.AreEqual("int", (val as ArrayValue).StringValue);




			ex = DParser.ParseExpression(@"def!(-5,""foolish"")");
			val = Evaluation.EvaluateValue(ex, ctxt);
			Assert.IsInstanceOfType(val, typeof(ArrayValue));
			Assert.IsTrue((val as ArrayValue).IsString);
			Assert.AreEqual("bool foolish;", (val as ArrayValue).StringValue);

			ex = DParser.ParseExpression("bar");
			var t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOfType(t, typeof(MemberSymbol));
			Assert.IsInstanceOfType((t as MemberSymbol).Base, typeof(PrimitiveType));
			Assert.AreEqual(DTokens.Bool, ((t as MemberSymbol).Base as PrimitiveType).TypeToken);
		}

		[TestMethod]
		public void TestParamDeduction10()
		{
			var ctxt = CreateCtxt("A", @"module A;

void foo(T)(int a) {}
void foo2(T=double)(bool b) {}
V foo3(V)(V v) {}");

			ctxt.ContextIndependentOptions |= ResolutionOptions.ReturnMethodReferencesOnly;

			IExpression x;
			AbstractType t;
			MemberSymbol ms;

			x = DParser.ParseExpression("foo3(\"asdf\")");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.IsInstanceOfType(t, typeof(MemberSymbol));
			ms = t as MemberSymbol;
			var tps = ms.Base as TemplateParameterSymbol;
			Assert.IsNotNull(tps);
			Assert.IsInstanceOfType(tps.Base, typeof(ArrayType));

			x = DParser.ParseExpression("foo(123)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.IsNull(t);

			x = DParser.ParseExpression("foo2(true)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.IsInstanceOfType(t, typeof(MemberSymbol));
			ms = t as MemberSymbol;
			Assert.IsNotNull(ms.DeducedTypes);
			Assert.IsInstanceOfType(ms.DeducedTypes[0].Base, typeof(PrimitiveType));
		}

		[TestMethod]
		public void TemplateParamDeduction11()
		{
			var ctxt = CreateDefCtxt(@"module modA;
Appender!(E[]) appender(A : E[], E)(A array = null) { return Appender!(E[])(array); }
struct Appender(A : T[], T) {
	this(T[] arr){}
}
");

			var ex = DParser.ParseExpression("appender!(int[])()");
			var t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOfType(t, typeof(StructType));

			ex = DParser.ParseExpression("appender!string()");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOfType(t, typeof(StructType));
		}

		[TestMethod]
		public void TemplateParamDeduction12()
		{
			var pcl = CreateCache(out DModule modA, @"module modA;
template Tmpl(T)
{
	enum Tmpl = false;
}

template Tmpl(alias T)
{
	enum Tmpl = true;
}

template tt(alias U)
{
}

int sym;
");
			var ctxt = CreateDefCtxt(pcl, modA);

			var ex = DParser.ParseExpression("Tmpl!sym");
			var t = Evaluation.EvaluateValue(ex, ctxt);
			Assert.IsInstanceOfType(t, typeof(PrimitiveValue));
			Assert.AreEqual(1m, (t as PrimitiveValue).Value);

			ex = ex = DParser.ParseExpression("Tmpl!int");
			t = Evaluation.EvaluateValue(ex, ctxt);
			Assert.IsInstanceOfType(t, typeof(PrimitiveValue));
			Assert.AreEqual(0m, (t as PrimitiveValue).Value);

			ctxt.CurrentContext.Set(modA["tt"].First() as IBlockNode);
			ex = DParser.ParseExpression("Tmpl!U");
			t = Evaluation.EvaluateValue(ex, ctxt);
			Assert.IsInstanceOfType(t, typeof(PrimitiveValue));
			Assert.AreEqual(1m, (t as PrimitiveValue).Value);
		}

		[TestMethod]
		public void TemplateParamDeduction13()
		{
			var ctxt = CreateDefCtxt(@"module modA;
class A(S:string) {}
class A(T){}

class C(U: A!W, W){ W item; }
");

			ITypeDeclaration td;
			AbstractType t;
			ClassType ct;

			td = DParser.ParseBasicType("C!(A!int)");
			t = RS(td, ctxt);

			Assert.IsInstanceOfType(t, typeof(ClassType));
			ct = t as ClassType;
			Assert.AreEqual(2, ct.DeducedTypes.Count);
			Assert.AreEqual("U", ct.DeducedTypes[0].Name);
			Assert.AreEqual("W", ct.DeducedTypes[1].Name);
			Assert.IsInstanceOfType(ct.DeducedTypes[1].Base, typeof(PrimitiveType));
		}

		[TestMethod]
		public void TemplateParamDeduction14()
		{
			var ctxt = CreateDefCtxt(@"module modA;
class A(S:string) {}

class C(U: A!W, W){ W item; }
");

			ITypeDeclaration td;
			AbstractType t;
			ClassType ct;

			td = DParser.ParseBasicType("C!(A!string)");
			t = RS(td, ctxt);

			Assert.IsInstanceOfType(t, typeof(ClassType));
			ct = t as ClassType;
			Assert.AreEqual(2, ct.DeducedTypes.Count);
		}

		[TestMethod]
		public void DefaultTemplateParamType()
		{
			var ctxt = CreateDefCtxt(@"module A;
struct StringNumPair(T = string, U = long){
    T m_str;
    U m_num;

    @property size_t len(){
        return cast(size_t) (m_str.length + m_num.length);
    }
}
");
			var A = ctxt.MainPackage()["A"];
			var StringNumPair = A["StringNumPair"].First() as DClassLike;
			var len = StringNumPair["len"].First() as DMethod;
			ctxt.CurrentContext.Set(len, len.Body.First().Location);

			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("T");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.IsInstanceOfType(t, typeof(TemplateParameterSymbol));
			Assert.IsInstanceOfType((t as TemplateParameterSymbol).Base, typeof(ArrayType));
		}

		[TestMethod]
		public void TemplateArgAsBasetype()
		{
			var ctxt = CreateDefCtxt(@"module A;
class A(T) { T t; }
class B(Z) : A!Z {}

B!int b;");

			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("b.t");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.IsInstanceOfType(t, typeof(MemberSymbol));
			t = (t as DerivedDataType).Base;
			Assert.IsInstanceOfType(t, typeof(TemplateParameterSymbol));
			t = (t as DerivedDataType).Base;
			Assert.IsInstanceOfType(t, typeof(PrimitiveType));
		}

		[TestMethod]
		public void TemplateTypeTuple1()
		{
			var ctxt = CreateDefCtxt(@"module modA;
template Print(A ...) { 
	void print() { 
		writefln(""args are "", A); 
	} 
} 

template Write(A ...) {
	void write(A a) { // A is a TypeTuple, a is an ExpressionTuple 
		writefln(""args are "", a); 
	} 
} 

void tplWrite(W...)(W a) { writefln(""args are "", a); } 
void tplWrite2(W...)(W a,double d) { } 

void main() { 
	Print!(1,'a',6.8).print(); // prints: args are 1a6.8 
	Write!(int, char, double).write(1, 'a', 6.8); // prints: args are 1a6.8
}");

			var x = DParser.ParseExpression("Print!(1,'a',6.8)");
			var t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.IsInstanceOfType(t, typeof(TemplateType));
			var tps = (t as TemplateType).DeducedTypes[0] as TemplateParameterSymbol;
			Assert.IsNotNull(tps);
			Assert.IsInstanceOfType(tps.Base, typeof(DTuple));
			var tt = tps.Base as DTuple;
			Assert.AreEqual(3, tt.Items.Length);
			Assert.IsTrue(tt.IsExpressionTuple);

			ctxt.ContextIndependentOptions |= ResolutionOptions.ReturnMethodReferencesOnly;

			x = DParser.ParseExpression("Write!(int, char, double).write(1, 'a', 6.8)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.IsInstanceOfType(t, typeof(MemberSymbol));

			x = DParser.ParseExpression("tplWrite!(int, char, double)(1, 'a', 6.8)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.IsInstanceOfType(t, typeof(MemberSymbol));

			x = DParser.ParseExpression("tplWrite(1, 'a', 6.8)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.IsInstanceOfType(t, typeof(MemberSymbol));
			tps = (t as MemberSymbol).DeducedTypes[0] as TemplateParameterSymbol;
			Assert.IsNotNull(tps);
			Assert.IsInstanceOfType(tps.Base, typeof(DTuple));
			tt = tps.Base as DTuple;
			Assert.AreEqual(3, tt.Items.Length);
			Assert.IsTrue(tt.IsTypeTuple);

			x = DParser.ParseExpression("tplWrite2(\"asdf\", 'a', 6.8)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.IsInstanceOfType(t, typeof(MemberSymbol));
			tps = (t as MemberSymbol).DeducedTypes[0] as TemplateParameterSymbol;
			Assert.IsNotNull(tps);
			Assert.IsInstanceOfType(tps.Base, typeof(DTuple));
			tt = tps.Base as DTuple;
			Assert.AreEqual(2, tt.Items.Length);
			Assert.IsInstanceOfType(tt.Items[0], typeof(ArrayType));
		}

		[TestMethod]
		public void EmptyTypeTuple()
		{
			var ctxt = CreateDefCtxt(@"module A;
enum E {A,B}

int writeln(T...)(T t)
{
}");

			IExpression ex;
			AbstractType x;

			ex = DParser.ParseExpression("\"asdf\".writeln()");
			x = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOfType(x, typeof(PrimitiveType));

			ex = DParser.ParseExpression("writeln()");
			x = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOfType(x, typeof(PrimitiveType));

			ex = DParser.ParseExpression("writeln(E.A)");
			x = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOfType(x, typeof(PrimitiveType));
		}

		[TestMethod]
		public void TypeTupleAsArgument()
		{
			var pcl = CreateCache(out DModule modA, @"module A;
template bar(T...) {
    static if(T.length == 1) {
        enum bar = ['a','g','h'];
    } else {
        enum bar = 0u;
    }
}

auto foo() {
    
}");
			var foo = modA["foo"].First() as DMethod;
			var ctxt = CreateDefCtxt(pcl, foo, foo.Body);

			var ex = DParser.ParseExpression("bar!int");
			var t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOfType(t, typeof(MemberSymbol));
			var ms = t as MemberSymbol;
			Assert.IsInstanceOfType(ms.Base, typeof(ArrayType));

			ex = DParser.ParseExpression("bar");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			ms = t as MemberSymbol;
			Assert.IsInstanceOfType(ms.Base, typeof(PrimitiveType));
			Assert.AreEqual(DTokens.Uint, (ms.Base as PrimitiveType).TypeToken);
		}

		[TestMethod]
		public void IdentifierOnlyTemplateDeduction()
		{
			var ctxt = CreateDefCtxt(@"module A;
class Too(T:int)
{ int foo1;}
class Too(T:float)
{ int foo2;}");

			var ex = DParser.ParseExpression("Too");
			var t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsNull(t);

			ex = DParser.ParseExpression("Too!int");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOfType(t, typeof(ClassType));

			DToken tk;
			var ty = DParser.ParseBasicType("Too", out tk);
			t = RS(ty, ctxt);
			Assert.IsNull(t);

			ty = DParser.ParseBasicType("Too!int", out tk);
			t = RS(ty, ctxt);
			Assert.IsInstanceOfType(t, typeof(ClassType));
		}

		[TestMethod]
		public void TemplateParameterPrototypeRecognition()
		{
			var pcl = CreateCache(out DModule modA, @"module A;
static int tmplFoo(T)() {}
static int[] tmplFoo2(T : U[], U)(int oo) {}
static int* tmplBar(T)(T t) {}

void foo(FU)(FU u)
{
	tmplFoo2!FU;
	tmplBar!FU(u);
	tmplFoo2!(int[])(123);
	tmplFoo2!FU(123);
}");
			IExpression ex;

			var foo = modA["foo"].First() as DMethod;
			var ctxt = CreateDefCtxt(pcl, foo, foo.Body);
			var subSt = foo.Body.SubStatements as List<IStatement>;

			ex = (subSt[0] as ExpressionStatement).Expression;
			var t = ExpressionTypeEvaluation.GetOverloads(ex as TemplateInstanceExpression, ctxt, null, true);
			Assert.IsNotNull(t);
			Assert.AreEqual(1, t.Count);

			var t_ = ExpressionTypeEvaluation.EvaluateType(ex, ctxt, false);
			Assert.IsInstanceOfType(t_, typeof(MemberSymbol));
			Assert.IsInstanceOfType((t_ as MemberSymbol).Base, typeof(ArrayType));

			ex = (subSt[1] as ExpressionStatement).Expression;
			t = ExpressionTypeEvaluation.GetOverloads((ex as PostfixExpression_MethodCall).PostfixForeExpression as TemplateInstanceExpression, ctxt, null, true);
			Assert.IsNotNull(t);
			Assert.AreEqual(1, t.Count);
			Assert.IsInstanceOfType(t[0], typeof(MemberSymbol));

			t_ = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOfType(t_, typeof(PointerType));

			ex = (subSt[2] as ExpressionStatement).Expression;
			t_ = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOfType(t_, typeof(ArrayType));

			ex = (subSt[3] as ExpressionStatement).Expression;
			t_ = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOfType(t_, typeof(ArrayType));
		}

		[TestMethod]
		public void TemplateParameterPrototypeRecognition1()
		{
			var pcl = CreateCache(out DModule modA, @"module A;
static int tmplFoo(T)() {}
static int[] tmplFoo2(T : U[], U)(int oo) {}
static int* tmplBar(T)(T t) {}

void foo(U)(U u)
{
	tmplFoo!U;
}");

			var foo = modA["foo"].First() as DMethod;
			var ctxt = CreateDefCtxt(pcl, foo, foo.Body);
			var subSt = foo.Body.SubStatements as List<IStatement>;

			var ex = (subSt[0] as ExpressionStatement).Expression;
			var t = ExpressionTypeEvaluation.GetOverloads(ex as TemplateInstanceExpression, ctxt, null, true);
			Assert.IsNotNull(t);
			Assert.AreEqual(1, t.Count);

			var t_ = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOfType(t_, typeof(PrimitiveType));
		}

		[TestMethod]
		public void TemplateAliasing()
		{
			var ctxt = CreateDefCtxt(@"module m;
template Foo(A)
{
	A Foo;
}

template Bar(B)
{
	version(X)
		B[] Bar;
	else
		B* Bar;
}

template Baz(B)
{
	debug
		B* Baz;
	else
		B[] Baz;
}");

			DToken tk;
			var td = DParser.ParseBasicType("Foo!int", out tk);

			var s = RS(td, ctxt);

			Assert.IsInstanceOfType(s, typeof(MemberSymbol));

			var ms = (MemberSymbol)s;
			Assert.IsInstanceOfType(ms.Definition, typeof(DVariable));
			Assert.IsInstanceOfType(ms.Base, typeof(TemplateParameterSymbol));
			var tps = (TemplateParameterSymbol)ms.Base;
			Assert.IsInstanceOfType(tps.Base, typeof(PrimitiveType));

			var pt = (PrimitiveType)tps.Base;
			Assert.AreEqual(DTokens.Int, pt.TypeToken);

			s = RS(DParser.ParseBasicType("Bar!int", out tk), ctxt);

			Assert.IsInstanceOfType(((DSymbol)s).Base, typeof(PointerType));

			s = RS(DParser.ParseBasicType("Baz!int", out tk), ctxt);

			Assert.IsInstanceOfType(((DSymbol)s).Base, typeof(PointerType));
		}

		[TestMethod]
		public void CrossModuleTemplateDecl()
		{
			var ctxt = CreateCtxt("c", @"
module a;
template Traits(T) if (is(T == string)){    enum Traits = ""abc"";}
auto func(T, A...)(A args) if (is(T == string)){    return ""abc"";}
", @"
module b;
template Traits(T) if (is(T == double)){ enum Traits = true;}
auto func(T, A...)(A args) if (is(T == double)){    return 2;}
", @"
module c;
import a, b;
");
			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("Traits!string");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.IsInstanceOfType(t, typeof(MemberSymbol));
			t = (t as MemberSymbol).Base;
			Assert.IsInstanceOfType(t, typeof(ArrayType));

			x = DParser.ParseExpression("Traits!double");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.IsInstanceOfType(t, typeof(MemberSymbol));
			t = (t as MemberSymbol).Base;
			Assert.IsInstanceOfType(t, typeof(PrimitiveType));

			x = DParser.ParseExpression("Traits!int");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.IsNull(t);

			x = DParser.ParseExpression("func!string(1)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.IsInstanceOfType(t, typeof(ArrayType));

			x = DParser.ParseExpression("func!double(1)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.IsInstanceOfType(t, typeof(PrimitiveType));

			x = DParser.ParseExpression("func!int(1)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.IsNull(t);

		}

		[TestMethod]
		/// <summary>
		/// https://github.com/aBothe/D_Parser/issues/192
		/// </summary>
		public void TemplateValueParameterDefaultSelfRefSO()
		{
			var code = @"module A;
struct Template( void var = Template ) {}
";
			AbstractType t;
			IExpression x;
			DModule A;
			var ctxt = CreateDefCtxt("A", out A, code);

			x = (N<DClassLike>(A, "Template").TemplateParameters[0] as TemplateValueParameter).DefaultExpression;

			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.IsInstanceOfType(t, typeof(StructType));
		}
	}
}
