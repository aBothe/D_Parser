using System.Collections.Generic;
using System.Linq;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.TypeResolution;
using NUnit.Framework;

namespace Tests.Resolution
{
	[TestFixture]
	public class TemplateParameterDeductionTests : ResolutionTestHelper
	{
		[Test]
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
			Assert.IsInstanceOf<StructType>(t);

			ex = DParser.ParseExpression("Thing!int");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOf<StructType>(t);

			ex = DParser.ParseExpression("IntThing");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOf<StructType>(t);
		}

		[Test]
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
			Assert.IsInstanceOf<MemberSymbol>(t); // Returns struct ctor
			var ctor = t as MemberSymbol;
			Assert.AreEqual(DMethod.ConstructorIdentifier, ctor.Name);
			Assert.IsInstanceOf<DMethod>(ctor.Definition);
			var ctorDefinition = ctor.Definition as DMethod;
			Assert.AreEqual(1, ctorDefinition.Parameters.Count);

			ex = DParser.ParseExpression("new IntThing");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOf<StructType>(t);
		}

		[Test]
		public void BasicResolution4()
		{
			var ctxt = CreateDefCtxt(@"module A;
class Blupp : Blah!(Blupp) {}
class Blah(T){ T b; }");

			var ex = DParser.ParseExpression("Blah!Blupp");
			var t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOf<ClassType>(t);
		}

		[Test]
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

			Assert.IsInstanceOf<PostfixExpression_Access>(instanceExpr);

			var res = ExpressionTypeEvaluation.EvaluateType(instanceExpr, ctxt);

			Assert.IsInstanceOf<MemberSymbol>(res);
			var mr = (MemberSymbol)res;

			Assert.IsInstanceOf<TemplateParameterSymbol>(mr.Base);
			var tps = (TemplateParameterSymbol)mr.Base;
			Assert.IsInstanceOf<PrimitiveType>(tps.Base);
			var sr = (PrimitiveType)tps.Base;

			Assert.AreEqual(sr.TypeToken, DTokens.Int);
		}

		[Test]
		public void TestParamDeduction2()
		{
			var ctxt = CreateDefCtxt(@"
module modA;
T foo(T)() {}
");

			var call = DParser.ParseExpression("foo!int()");
			var bt = ExpressionTypeEvaluation.EvaluateType(call, ctxt);

			Assert.IsInstanceOf<TemplateParameterSymbol>(bt);
			var tps = (TemplateParameterSymbol)bt;
			Assert.IsInstanceOf<PrimitiveType>(tps.Base, "Resolution returned empty result instead of 'int'");
			var st = (PrimitiveType)tps.Base;
			Assert.IsNotNull(st, "Result must be Static type int");
			Assert.AreEqual(st.TypeToken, DTokens.Int, "Static type must be int");
		}

		[Test]
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

		[Test]
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
			Assert.IsInstanceOf<MemberSymbol>(r);

			x = DParser.ParseExpression("f!(char[5])");
			r = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			mr = r as MemberSymbol;
			Assert.IsInstanceOf<MemberSymbol>(r);

			var v = mr.DeducedTypes[2].ParameterValue;
			Assert.IsInstanceOf<PrimitiveValue>(v);
			Assert.AreEqual(5M, ((PrimitiveValue)v).Value);



			x = DParser.ParseExpression("fo!(immutable(char)[])");
			r = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			mr = r as MemberSymbol;
			Assert.IsInstanceOf<MemberSymbol>(r);

			x = DParser.ParseExpression("myDeleg");
			r = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			mr = r as MemberSymbol;
			Assert.IsNotNull(mr);
			Assert.IsInstanceOf<DelegateType>(mr.Base);

			x = DParser.ParseExpression("myDeleg(123)");
			r = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.IsInstanceOf<DelegateCallSymbol>(r);
			Assert.IsInstanceOf<PrimitiveType>((r as DerivedDataType).Base);

			x = DParser.ParseExpression("foo(myDeleg(123))");
			r = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			mr = r as MemberSymbol;
			Assert.IsNotNull(mr);
			Assert.IsInstanceOf<PrimitiveType>(mr.Base);
		}

		[Test]
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
			Assert.IsInstanceOf<ClassType>(res);
			var ct = (ClassType)res;

			Assert.IsInstanceOf<ClassType>(ct.Base);
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

			Assert.IsInstanceOf<ClassType>(res);
		}

		[Test]
		public void TestParamDeduction6()
		{
			var pcl = CreateCache(out DModule mod, @"module modA;
class A(T) {}
class B : A!int{}
class C(U: A!int){}
class D : C!B {}");

			var ctxt = CreateDefCtxt(pcl, mod);

			var res = TypeDeclarationResolver.HandleNodeMatch(mod["D"].First(), ctxt);
			Assert.IsInstanceOf<ClassType>(res);
			var ct = (ClassType)res;

			Assert.IsInstanceOf<ClassType>(ct.Base);
			ct = (ClassType)ct.Base;

			Assert.AreEqual(1, ct.DeducedTypes.Count);
		}

		[Test]
		public void TestParamDeduction7()
		{
			var pcl = CreateCache(out DModule A, @"module A;
U genA(U)();
T delegate(T dgParam) genDelegate(T)();");

			var ctxt = CreateDefCtxt(pcl, A);

			var ex = DParser.ParseExpression("genDelegate!int()");
			var t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOf<DelegateType>(t);
			var dt = (DelegateType)t;
			Assert.IsNotNull(dt.Base);
			Assert.IsNotNull(dt.Parameters);

			ex = DParser.ParseExpression("genA!int()");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOf<TemplateParameterSymbol>(t);
		}

		[Test]
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
			Assert.IsInstanceOf<MemberSymbol>(t); // ctor
			Assert.IsInstanceOf<StructType>((t as MemberSymbol).Base);

			ex = DParser.ParseExpression("appender!(double[])()");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOf<StructType>(t);
			var ss = t as StructType;
			Assert.IsTrue(ss.DeducedTypes.Count > 0);
		}

		[Test]
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
			Assert.IsInstanceOf<ArrayValue>(val);
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
				Assert.IsInstanceOf<PrimitiveValue>(val);
				Assert.AreEqual(2m, (val as PrimitiveValue).Value);

				ex = DParser.ParseExpression("mxTemp!(-i)");
				val = Evaluation.EvaluateValue(ex, ctxt);
				Assert.IsInstanceOf<ArrayValue>(val);
				Assert.IsTrue((val as ArrayValue).IsString);
				Assert.AreEqual("int", (val as ArrayValue).StringValue);

				ex = DParser.ParseExpression("mxTemp!(-i) ~ \" \"~name~\";\"");
				val = Evaluation.EvaluateValue(ex, ctxt);
				Assert.IsInstanceOf<ArrayValue>(val);
				Assert.IsTrue((val as ArrayValue).IsString);
				Assert.AreEqual("int someVar;", (val as ArrayValue).StringValue);
			}


			ex = DParser.ParseExpression("def2!5");
			val = Evaluation.EvaluateValue(ex, ctxt);
			Assert.IsInstanceOf<PrimitiveValue>(val);
			Assert.AreEqual(5m, (val as PrimitiveValue).Value);

			ex = DParser.ParseExpression("-k");
			val = Evaluation.EvaluateValue(ex, ctxt);
			Assert.IsInstanceOf<PrimitiveValue>(val);
			Assert.AreEqual(-4m, (val as PrimitiveValue).Value);

			ex = DParser.ParseExpression("mxTemp!(-k)");
			val = Evaluation.EvaluateValue(ex, ctxt);
			Assert.IsInstanceOf<ArrayValue>(val);
			Assert.IsTrue((val as ArrayValue).IsString);
			Assert.AreEqual("int", (val as ArrayValue).StringValue);




			ex = DParser.ParseExpression(@"def!(-5,""foolish"")");
			val = Evaluation.EvaluateValue(ex, ctxt);
			Assert.IsInstanceOf<ArrayValue>(val);
			Assert.IsTrue((val as ArrayValue).IsString);
			Assert.AreEqual("bool foolish;", (val as ArrayValue).StringValue);

			ex = DParser.ParseExpression("bar");
			var t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOf<MemberSymbol>(t);
			Assert.IsInstanceOf<PrimitiveType>((t as MemberSymbol).Base);
			Assert.AreEqual(DTokens.Bool, ((t as MemberSymbol).Base as PrimitiveType).TypeToken);
		}

		[Test]
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
			Assert.IsInstanceOf<MemberSymbol>(t);
			ms = t as MemberSymbol;
			var tps = ms.Base as TemplateParameterSymbol;
			Assert.IsNotNull(tps);
			Assert.IsInstanceOf<ArrayType>(tps.Base);

			x = DParser.ParseExpression("foo(123)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.IsNull(t);

			x = DParser.ParseExpression("foo2(true)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.IsInstanceOf<MemberSymbol>(t);
			ms = t as MemberSymbol;
			Assert.IsNotNull(ms.DeducedTypes);
			Assert.IsInstanceOf<PrimitiveType>(ms.DeducedTypes[0].Base);
		}

		[Test]
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
			Assert.IsInstanceOf<StructType>(t);

			ex = DParser.ParseExpression("appender!string()");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOf<StructType>(t);
		}

		[Test]
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
			Assert.IsInstanceOf<PrimitiveValue>(t);
			Assert.AreEqual(1m, (t as PrimitiveValue).Value);

			ex = ex = DParser.ParseExpression("Tmpl!int");
			t = Evaluation.EvaluateValue(ex, ctxt);
			Assert.IsInstanceOf<PrimitiveValue>(t);
			Assert.AreEqual(0m, (t as PrimitiveValue).Value);

			ctxt.CurrentContext.Set(modA["tt"].First() as IBlockNode);
			ex = DParser.ParseExpression("Tmpl!U");
			t = Evaluation.EvaluateValue(ex, ctxt);
			Assert.IsInstanceOf<PrimitiveValue>(t);
			Assert.AreEqual(1m, (t as PrimitiveValue).Value);
		}

		[Test]
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

			Assert.IsInstanceOf<ClassType>(t);
			ct = t as ClassType;
			Assert.AreEqual(2, ct.DeducedTypes.Count);
			Assert.AreEqual("U", ct.DeducedTypes[0].Name);
			Assert.AreEqual("W", ct.DeducedTypes[1].Name);
			Assert.IsInstanceOf<PrimitiveType>(ct.DeducedTypes[1].Base);
		}

		[Test]
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

			Assert.IsInstanceOf<ClassType>(t);
			ct = t as ClassType;
			Assert.AreEqual(2, ct.DeducedTypes.Count);
		}

		[Test]
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
			Assert.IsInstanceOf<TemplateParameterSymbol>(t);
			Assert.IsInstanceOf<ArrayType>((t as TemplateParameterSymbol).Base);
		}

		[Test]
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

			Assert.IsInstanceOf<MemberSymbol>(t);
			t = (t as DerivedDataType).Base;
			Assert.IsInstanceOf<TemplateParameterSymbol>(t);
			t = (t as DerivedDataType).Base;
			Assert.IsInstanceOf<PrimitiveType>(t);
		}

		[Test]
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
			Assert.IsInstanceOf<TemplateType>(t);
			var tps = (t as TemplateType).DeducedTypes[0] as TemplateParameterSymbol;
			Assert.IsNotNull(tps);
			Assert.IsInstanceOf<DTuple>(tps.Base);
			var tt = tps.Base as DTuple;
			Assert.AreEqual(3, tt.Items.Length);
			Assert.IsTrue(tt.IsExpressionTuple);

			ctxt.ContextIndependentOptions |= ResolutionOptions.ReturnMethodReferencesOnly;

			x = DParser.ParseExpression("Write!(int, char, double).write(1, 'a', 6.8)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.IsInstanceOf<MemberSymbol>(t);

			x = DParser.ParseExpression("tplWrite!(int, char, double)(1, 'a', 6.8)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.IsInstanceOf<MemberSymbol>(t);

			x = DParser.ParseExpression("tplWrite(1, 'a', 6.8)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.IsInstanceOf<MemberSymbol>(t);
			tps = (t as MemberSymbol).DeducedTypes[0] as TemplateParameterSymbol;
			Assert.IsNotNull(tps);
			Assert.IsInstanceOf<DTuple>(tps.Base);
			tt = tps.Base as DTuple;
			Assert.AreEqual(3, tt.Items.Length);
			Assert.IsTrue(tt.IsTypeTuple);

			x = DParser.ParseExpression("tplWrite2(\"asdf\", 'a', 6.8)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.IsInstanceOf<MemberSymbol>(t);
			tps = (t as MemberSymbol).DeducedTypes[0] as TemplateParameterSymbol;
			Assert.IsNotNull(tps);
			Assert.IsInstanceOf<DTuple>(tps.Base);
			tt = tps.Base as DTuple;
			Assert.AreEqual(2, tt.Items.Length);
			Assert.IsInstanceOf<ArrayType>(tt.Items[0]);
		}

		[Test]
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
			Assert.IsInstanceOf<PrimitiveType>(x);

			ex = DParser.ParseExpression("writeln()");
			x = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOf<PrimitiveType>(x);

			ex = DParser.ParseExpression("writeln(E.A)");
			x = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOf<PrimitiveType>(x);
		}

		[Test]
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
			Assert.IsInstanceOf<MemberSymbol>(t);
			var ms = t as MemberSymbol;
			Assert.IsInstanceOf<ArrayType>(ms.Base);

			ex = DParser.ParseExpression("bar");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			ms = t as MemberSymbol;
			Assert.IsInstanceOf<PrimitiveType>(ms.Base);
			Assert.AreEqual(DTokens.Uint, (ms.Base as PrimitiveType).TypeToken);
		}

		[Test]
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
			Assert.IsInstanceOf<ClassType>(t);

			DToken tk;
			var ty = DParser.ParseBasicType("Too", out tk);
			t = RS(ty, ctxt);
			Assert.IsNull(t);

			ty = DParser.ParseBasicType("Too!int", out tk);
			t = RS(ty, ctxt);
			Assert.IsInstanceOf<ClassType>(t);
		}

		[Test]
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
			Assert.IsInstanceOf<MemberSymbol>(t_);
			Assert.IsInstanceOf<ArrayType>((t_ as MemberSymbol).Base);

			ex = (subSt[1] as ExpressionStatement).Expression;
			t = ExpressionTypeEvaluation.GetOverloads((ex as PostfixExpression_MethodCall).PostfixForeExpression as TemplateInstanceExpression, ctxt, null, true);
			Assert.IsNotNull(t);
			Assert.AreEqual(1, t.Count);
			Assert.IsInstanceOf<MemberSymbol>(t[0]);

			t_ = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOf<PointerType>(t_);

			ex = (subSt[2] as ExpressionStatement).Expression;
			t_ = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOf<ArrayType>(t_);

			ex = (subSt[3] as ExpressionStatement).Expression;
			t_ = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOf<ArrayType>(t_);
		}

		[Test]
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
			Assert.IsInstanceOf<PrimitiveType>(t_);
		}

		[Test]
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

			Assert.IsInstanceOf<MemberSymbol>(s);

			var ms = (MemberSymbol)s;
			Assert.IsInstanceOf<DVariable>(ms.Definition);
			Assert.IsInstanceOf<TemplateParameterSymbol>(ms.Base);
			var tps = (TemplateParameterSymbol)ms.Base;
			Assert.IsInstanceOf<PrimitiveType>(tps.Base);

			var pt = (PrimitiveType)tps.Base;
			Assert.AreEqual(DTokens.Int, pt.TypeToken);

			s = RS(DParser.ParseBasicType("Bar!int", out tk), ctxt);

			Assert.IsInstanceOf<PointerType>(((DSymbol)s).Base);

			s = RS(DParser.ParseBasicType("Baz!int", out tk), ctxt);

			Assert.IsInstanceOf<PointerType>(((DSymbol)s).Base);
		}

		[Test]
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

			Assert.IsInstanceOf<MemberSymbol>(t);
			t = (t as MemberSymbol).Base;
			Assert.IsInstanceOf<ArrayType>(t);

			x = DParser.ParseExpression("Traits!double");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.IsInstanceOf<MemberSymbol>(t);
			t = (t as MemberSymbol).Base;
			Assert.IsInstanceOf<PrimitiveType>(t);

			x = DParser.ParseExpression("Traits!int");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.IsNull(t);

			x = DParser.ParseExpression("func!string(1)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.IsInstanceOf<ArrayType>(t);

			x = DParser.ParseExpression("func!double(1)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.IsInstanceOf<PrimitiveType>(t);

			x = DParser.ParseExpression("func!int(1)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.IsNull(t);

		}

		[Test]
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

			Assert.IsInstanceOf<StructType>(t);
		}
	}
}
