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
			var pcl = CreateCache(@"module A;
struct Thing(T)
{
	public T property;
}

alias Thing!(int) IntThing;
alias Thing SomeThing;
");

			var ctxt = CreateDefCtxt(pcl, pcl.FirstPackage()["A"]);

			IExpression ex;
			AbstractType t;

			ex = DParser.ParseExpression("SomeThing!int");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.TypeOf(typeof(StructType)));

			ex = DParser.ParseExpression("Thing!int");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.TypeOf(typeof(StructType)));

			ex = DParser.ParseExpression("IntThing");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.TypeOf(typeof(StructType)));
		}

		[Test]
		public void BasicResolution3()
		{
			var pcl = CreateCache(@"module A;
struct Thing(T)
{
	public T property;
}

alias Thing!(int) IntThing;
alias Thing SomeThing;
");

			var ctxt = CreateDefCtxt(pcl, pcl.FirstPackage()["A"]);

			IExpression ex;
			AbstractType t;

			ex = DParser.ParseExpression("new Thing!int");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt, false);
			Assert.That(t, Is.TypeOf(typeof(AmbiguousType))); // Returns the empty & struct ctor
			var ctors = AmbiguousType.TryDissolve(t).ToArray();
			Assert.That(ctors.Length, Is.EqualTo(2));
			Assert.That(((DSymbol)ctors[0]).Name, Is.EqualTo(DMethod.ConstructorIdentifier));

			ex = DParser.ParseExpression("new IntThing");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.TypeOf(typeof(StructType)));
		}

		[Test]
		public void BasicResolution4()
		{
			var pcl = CreateCache(@"module A;
class Blupp : Blah!(Blupp) {}
class Blah(T){ T b; }");

			var ctxt = CreateDefCtxt(pcl, pcl.FirstPackage()["A"]);

			var ex = DParser.ParseExpression("Blah!Blupp");
			var t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.TypeOf(typeof(ClassType)));
		}

		[Test]
		public void TestParamDeduction1()
		{

			var pcl = CreateCache(@"module modA;

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

			var ctxt = CreateDefCtxt(pcl, pcl.FirstPackage()["modA"]);

			var instanceExpr = DParser.ParseExpression("(new MyClass!int).tvar");

			Assert.That(instanceExpr, Is.TypeOf(typeof(PostfixExpression_Access)));

			var res = ExpressionTypeEvaluation.EvaluateType(instanceExpr, ctxt);

			Assert.That(res, Is.TypeOf(typeof(MemberSymbol)));
			var mr = (MemberSymbol)res;

			Assert.That(mr.Base, Is.TypeOf(typeof(TemplateParameterSymbol)));
			var tps = (TemplateParameterSymbol)mr.Base;
			Assert.That(tps.Base, Is.TypeOf(typeof(PrimitiveType)));
			var sr = (PrimitiveType)tps.Base;

			Assert.AreEqual(sr.TypeToken, DTokens.Int);
		}

		[Test]
		public void TestParamDeduction2()
		{
			var pcl = CreateCache(@"
module modA;
T foo(T)() {}
");

			var ctxt = CreateDefCtxt(pcl, pcl.FirstPackage()["modA"]);

			var call = DParser.ParseExpression("foo!int()");
			var bt = ExpressionTypeEvaluation.EvaluateType(call, ctxt);

			Assert.That(bt, Is.TypeOf(typeof(TemplateParameterSymbol)));
			var tps = (TemplateParameterSymbol)bt;
			Assert.That(tps.Base, Is.TypeOf(typeof(PrimitiveType)), "Resolution returned empty result instead of 'int'");
			var st = (PrimitiveType)tps.Base;
			Assert.IsNotNull(st, "Result must be Static type int");
			Assert.AreEqual(st.TypeToken, DTokens.Int, "Static type must be int");
		}

		[Test]
		public void TestParamDeduction3()
		{
			var pcl = CreateCache(@"module modA;

class A {}
class A2 {}

class B(T){
	class C(T2) : T {} 
}");

			var ctxt = CreateDefCtxt(pcl, pcl.FirstPackage()["modA"]);

			var inst = DParser.ParseExpression("(new B!A).new C!A2"); // TODO
		}

		[Test]
		public void TestParamDeduction4()
		{
			var ctxt = CreateCtxt("modA", @"module modA;

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
			Assert.That(r, Is.TypeOf(typeof(MemberSymbol)));

			x = DParser.ParseExpression("f!(char[5])");
			r = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			mr = r as MemberSymbol;
			Assert.That(r, Is.TypeOf(typeof(MemberSymbol)));

			var v = mr.DeducedTypes[2].ParameterValue;
			Assert.That(v, Is.TypeOf(typeof(PrimitiveValue)));
			Assert.AreEqual(5M, ((PrimitiveValue)v).Value);



			x = DParser.ParseExpression("fo!(immutable(char)[])");
			r = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			mr = r as MemberSymbol;
			Assert.That(r, Is.TypeOf(typeof(MemberSymbol)));

			x = DParser.ParseExpression("myDeleg");
			r = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			mr = r as MemberSymbol;
			Assert.IsNotNull(mr);
			Assert.That(mr.Base, Is.TypeOf(typeof(DelegateType)));

			x = DParser.ParseExpression("myDeleg(123)");
			r = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(r, Is.TypeOf(typeof(DelegateCallSymbol)));
			Assert.That((r as DerivedDataType).Base, Is.TypeOf(typeof(PrimitiveType)));

			x = DParser.ParseExpression("foo(myDeleg(123))");
			r = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			mr = r as MemberSymbol;
			Assert.IsNotNull(mr);
			Assert.That(mr.Base, Is.TypeOf(typeof(PrimitiveType)));
		}

		[Test]
		public void TestParamDeduction5()
		{
			var pcl = CreateCache(@"module modA;
struct Params{}
class IConn ( P ){}
class Conn : IConn!(Params){}
class IRegistry ( P ){}
class Registry (C : IConn!(Params) ) : IRegistry!(Params){}
class ConcreteRegistry : Registry!(Conn){}
class IClient ( P, R : IRegistry!(P) ){}
class Client : IClient!(Params, ConcreteRegistry){}");

			var mod = pcl.FirstPackage()["modA"];
			var Client = mod["Client"].First() as DClassLike;
			var ctxt = CreateDefCtxt(pcl, mod);

			var res = TypeDeclarationResolver.HandleNodeMatch(Client, ctxt);
			Assert.That(res, Is.TypeOf(typeof(ClassType)));
			var ct = (ClassType)res;

			Assert.That(ct.Base, Is.TypeOf(typeof(ClassType)));
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

			Assert.That(res, Is.TypeOf(typeof(ClassType)));
		}

		[Test]
		public void TestParamDeduction6()
		{
			var pcl = CreateCache(@"module modA;
class A(T) {}
class B : A!int{}
class C(U: A!int){}
class D : C!B {}");

			var mod = pcl.FirstPackage()["modA"];
			var ctxt = CreateDefCtxt(pcl, mod);

			var res = TypeDeclarationResolver.HandleNodeMatch(mod["D"].First(), ctxt);
			Assert.That(res, Is.TypeOf(typeof(ClassType)));
			var ct = (ClassType)res;

			Assert.That(ct.Base, Is.TypeOf(typeof(ClassType)));
			ct = (ClassType)ct.Base;

			Assert.AreEqual(1, ct.DeducedTypes.Count);
		}

		[Test]
		public void TestParamDeduction7()
		{
			var pcl = CreateCache(@"module A;
U genA(U)();
T delegate(T dgParam) genDelegate(T)();");

			var A = pcl.FirstPackage()["A"];
			var ctxt = CreateDefCtxt(pcl, A);

			var ex = DParser.ParseExpression("genDelegate!int()");
			var t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.TypeOf(typeof(DelegateType)));
			var dt = (DelegateType)t;
			Assert.That(dt.Base, Is.Not.Null);
			Assert.That(dt.Parameters, Is.Not.Null);

			ex = DParser.ParseExpression("genA!int()");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.TypeOf(typeof(TemplateParameterSymbol)));
		}

		[Test]
		public void TestParamDeduction8_Appender()
		{
			var pcl = CreateCache(@"module A;
double[] darr;
struct Appender(A:E[],E) { A data; }

Appender!(E[]) appender(A : E[], E)(A array = null)
{
    return Appender!(E[])(array);
}");

			var A = pcl.FirstPackage()["A"];
			var ctxt = CreateDefCtxt(pcl, A);

			var ex = DParser.ParseExpression("new Appender!(double[])(darr)");
			var t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt, false);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol))); // ctor
			Assert.That((t as MemberSymbol).Base, Is.TypeOf(typeof(StructType)));

			ex = DParser.ParseExpression("appender!(double[])()");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.TypeOf(typeof(StructType)));
			var ss = t as StructType;
			Assert.That(ss.DeducedTypes.Count, Is.GreaterThan(0));
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
			Assert.That(val, Is.TypeOf(typeof(ArrayValue)));
			Assert.That((val as ArrayValue).IsString, Is.True);
			Assert.That((val as ArrayValue).StringValue, Is.EqualTo("bool someVar;"));

			var def = ctxt.MainPackage()["A"]["def"].First() as DClassLike;
			var defS = new TemplateType(def, new[]{
				new TemplateParameterSymbol(def.TemplateParameters[0], new PrimitiveValue(2)),
				new TemplateParameterSymbol(def.TemplateParameters[1], new ArrayValue(Evaluation.GetStringType(ctxt), "someVar"))
			});
			using (ctxt.Push(defS))
			{
				ex = DParser.ParseExpression("i");
				val = Evaluation.EvaluateValue(ex, ctxt);
				Assert.That(val, Is.TypeOf(typeof(PrimitiveValue)));
				Assert.That((val as PrimitiveValue).Value, Is.EqualTo(2m));

				ex = DParser.ParseExpression("mxTemp!(-i)");
				val = Evaluation.EvaluateValue(ex, ctxt);
				Assert.That(val, Is.TypeOf(typeof(ArrayValue)));
				Assert.That((val as ArrayValue).IsString, Is.True);
				Assert.That((val as ArrayValue).StringValue, Is.EqualTo("int"));

				ex = DParser.ParseExpression("mxTemp!(-i) ~ \" \"~name~\";\"");
				val = Evaluation.EvaluateValue(ex, ctxt);
				Assert.That(val, Is.TypeOf(typeof(ArrayValue)));
				Assert.That((val as ArrayValue).IsString, Is.True);
				Assert.That((val as ArrayValue).StringValue, Is.EqualTo("int someVar;"));
			}


			ex = DParser.ParseExpression("def2!5");
			val = Evaluation.EvaluateValue(ex, ctxt);
			Assert.That(val, Is.TypeOf(typeof(PrimitiveValue)));
			Assert.That((val as PrimitiveValue).Value, Is.EqualTo(5m));

			ex = DParser.ParseExpression("-k");
			val = Evaluation.EvaluateValue(ex, ctxt);
			Assert.That(val, Is.TypeOf(typeof(PrimitiveValue)));
			Assert.That((val as PrimitiveValue).Value, Is.EqualTo(-4m));

			ex = DParser.ParseExpression("mxTemp!(-k)");
			val = Evaluation.EvaluateValue(ex, ctxt);
			Assert.That(val, Is.TypeOf(typeof(ArrayValue)));
			Assert.That((val as ArrayValue).IsString, Is.True);
			Assert.That((val as ArrayValue).StringValue, Is.EqualTo("int"));




			ex = DParser.ParseExpression(@"def!(-5,""foolish"")");
			val = Evaluation.EvaluateValue(ex, ctxt);
			Assert.That(val, Is.TypeOf(typeof(ArrayValue)));
			Assert.That((val as ArrayValue).IsString, Is.True);
			Assert.That((val as ArrayValue).StringValue, Is.EqualTo("bool foolish;"));

			ex = DParser.ParseExpression("bar");
			var t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((t as MemberSymbol).Base, Is.TypeOf(typeof(PrimitiveType)));
			Assert.That(((t as MemberSymbol).Base as PrimitiveType).TypeToken, Is.EqualTo(DTokens.Bool));
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
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			ms = t as MemberSymbol;
			var tps = ms.Base as TemplateParameterSymbol;
			Assert.That(tps, Is.Not.Null);
			Assert.That(tps.Base, Is.TypeOf(typeof(ArrayType)));

			x = DParser.ParseExpression("foo(123)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.Null);

			x = DParser.ParseExpression("foo2(true)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			ms = t as MemberSymbol;
			Assert.That(ms.DeducedTypes, Is.Not.Null);
			Assert.That(ms.DeducedTypes[0].Base, Is.TypeOf(typeof(PrimitiveType)));
		}

		[Test]
		public void TemplateParamDeduction11()
		{
			var pcl = CreateCache(@"module modA;
Appender!(E[]) appender(A : E[], E)(A array = null) { return Appender!(E[])(array); }
struct Appender(A : T[], T) {
	this(T[] arr){}
}
");
			var ctxt = CreateDefCtxt(pcl, pcl.FirstPackage()["modA"]);

			var ex = DParser.ParseExpression("appender!(int[])()");
			var t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.TypeOf(typeof(StructType)));

			ex = DParser.ParseExpression("appender!string()");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.TypeOf(typeof(StructType)));
		}

		[Test]
		public void TemplateParamDeduction12()
		{
			var pcl = CreateCache(@"module modA;
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
			var modA = pcl.FirstPackage()["modA"];
			var ctxt = CreateDefCtxt(pcl, modA);

			var ex = DParser.ParseExpression("Tmpl!sym");
			var t = Evaluation.EvaluateValue(ex, ctxt);
			Assert.That(t, Is.InstanceOf(typeof(PrimitiveValue)));
			Assert.That((t as PrimitiveValue).Value == 1m);

			ex = ex = DParser.ParseExpression("Tmpl!int");
			t = Evaluation.EvaluateValue(ex, ctxt);
			Assert.That(t, Is.InstanceOf(typeof(PrimitiveValue)));
			Assert.That((t as PrimitiveValue).Value == 0m);

			ctxt.CurrentContext.Set(modA["tt"].First() as IBlockNode);
			ex = DParser.ParseExpression("Tmpl!U");
			t = Evaluation.EvaluateValue(ex, ctxt);
			Assert.That(t, Is.InstanceOf(typeof(PrimitiveValue)));
			Assert.That((t as PrimitiveValue).Value == 1m);
		}

		[Test]
		public void TemplateParamDeduction13()
		{
			var ctxt = CreateCtxt("modA", @"module modA;
class A(S:string) {}
class A(T){}

class C(U: A!W, W){ W item; }
");

			ITypeDeclaration td;
			AbstractType t;
			ClassType ct;

			td = DParser.ParseBasicType("C!(A!int)");
			t = RS(td, ctxt);

			Assert.That(t, Is.TypeOf(typeof(ClassType)));
			ct = t as ClassType;
			Assert.That(ct.DeducedTypes.Count, Is.EqualTo(2));
			Assert.That(ct.DeducedTypes[0].Name, Is.EqualTo("U"));
			Assert.That(ct.DeducedTypes[1].Name, Is.EqualTo("W"));
			Assert.That(ct.DeducedTypes[1].Base, Is.TypeOf(typeof(PrimitiveType)));
		}

		[Test]
		public void TemplateParamDeduction14()
		{
			var ctxt = CreateCtxt("modA", @"module modA;
class A(S:string) {}

class C(U: A!W, W){ W item; }
");

			ITypeDeclaration td;
			AbstractType t;
			ClassType ct;

			td = DParser.ParseBasicType("C!(A!string)");
			t = RS(td, ctxt);

			Assert.That(t, Is.TypeOf(typeof(ClassType)));
			ct = t as ClassType;
			Assert.That(ct.DeducedTypes.Count, Is.EqualTo(2));
		}

		[Test]
		public void DefaultTemplateParamType()
		{
			var ctxt = CreateCtxt("A", @"module A;
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
			Assert.That(t, Is.TypeOf(typeof(TemplateParameterSymbol)));
			Assert.That((t as TemplateParameterSymbol).Base, Is.TypeOf(typeof(ArrayType)));
		}

		[Test]
		public void TemplateArgAsBasetype()
		{
			var ctxt = CreateCtxt("A", @"module A;
class A(T) { T t; }
class B(Z) : A!Z {}

B!int b;");

			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("b.t");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			t = (t as DerivedDataType).Base;
			Assert.That(t, Is.TypeOf(typeof(TemplateParameterSymbol)));
			t = (t as DerivedDataType).Base;
			Assert.That(t, Is.TypeOf(typeof(PrimitiveType)));
		}

		[Test]
		public void TemplateTypeTuple1()
		{
			var pcl = CreateCache(@"module modA;
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

			var modA = pcl.FirstPackage()["modA"];
			var ctxt = CreateDefCtxt(pcl, modA);

			var x = DParser.ParseExpression("Print!(1,'a',6.8)");
			var t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(TemplateType)));
			var tps = (t as TemplateType).DeducedTypes[0] as TemplateParameterSymbol;
			Assert.That(tps, Is.Not.Null);
			Assert.That(tps.Base, Is.TypeOf(typeof(DTuple)));
			var tt = tps.Base as DTuple;
			Assert.That(tt.Items.Length, Is.EqualTo(3));
			Assert.That(tt.IsExpressionTuple);

			ctxt.ContextIndependentOptions |= ResolutionOptions.ReturnMethodReferencesOnly;

			x = DParser.ParseExpression("Write!(int, char, double).write(1, 'a', 6.8)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));

			x = DParser.ParseExpression("tplWrite!(int, char, double)(1, 'a', 6.8)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));

			x = DParser.ParseExpression("tplWrite(1, 'a', 6.8)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			tps = (t as MemberSymbol).DeducedTypes[0] as TemplateParameterSymbol;
			Assert.That(tps, Is.Not.Null);
			Assert.That(tps.Base, Is.TypeOf(typeof(DTuple)));
			tt = tps.Base as DTuple;
			Assert.That(tt.Items.Length, Is.EqualTo(3));
			Assert.That(tt.IsTypeTuple);

			x = DParser.ParseExpression("tplWrite2(\"asdf\", 'a', 6.8)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			tps = (t as MemberSymbol).DeducedTypes[0] as TemplateParameterSymbol;
			Assert.That(tps, Is.Not.Null);
			Assert.That(tps.Base, Is.TypeOf(typeof(DTuple)));
			tt = tps.Base as DTuple;
			Assert.That(tt.Items.Length, Is.EqualTo(2));
			Assert.That(tt.Items[0], Is.TypeOf(typeof(ArrayType)));
		}

		[Test]
		public void EmptyTypeTuple()
		{
			var pcl = CreateCache(@"module A;
enum E {A,B}

int writeln(T...)(T t)
{
}");
			var ctxt = CreateDefCtxt(pcl, pcl.FirstPackage()["A"]);

			IExpression ex;
			AbstractType x;

			ex = DParser.ParseExpression("\"asdf\".writeln()");
			x = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(x, Is.TypeOf(typeof(PrimitiveType)));

			ex = DParser.ParseExpression("writeln()");
			x = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(x, Is.TypeOf(typeof(PrimitiveType)));

			ex = DParser.ParseExpression("writeln(E.A)");
			x = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(x, Is.TypeOf(typeof(PrimitiveType)));
		}

		[Test]
		public void TypeTupleAsArgument()
		{
			var pcl = CreateCache(@"module A;
template bar(T...) {
    static if(T.length == 1) {
        enum bar = ['a','g','h'];
    } else {
        enum bar = 0u;
    }
}

auto foo() {
    
}");
			var foo = pcl.FirstPackage()["A"]["foo"].First() as DMethod;
			var ctxt = CreateDefCtxt(pcl, foo, foo.Body);

			var ex = DParser.ParseExpression("bar!int");
			var t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			var ms = t as MemberSymbol;
			Assert.That(ms.Base, Is.TypeOf(typeof(ArrayType)));

			ex = DParser.ParseExpression("bar");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			ms = t as MemberSymbol;
			Assert.That(ms.Base, Is.TypeOf(typeof(PrimitiveType)));
			Assert.That((ms.Base as PrimitiveType).TypeToken, Is.EqualTo(DTokens.Uint));
		}

		[Test]
		public void IdentifierOnlyTemplateDeduction()
		{
			var pcl = CreateCache(@"module A;
class Too(T:int)
{ int foo1;}
class Too(T:float)
{ int foo2;}");

			var ctxt = CreateDefCtxt(pcl, pcl.FirstPackage()["A"]);

			var ex = DParser.ParseExpression("Too");
			var t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.Null);

			ex = DParser.ParseExpression("Too!int");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t, Is.TypeOf(typeof(ClassType)));

			DToken tk;
			var ty = DParser.ParseBasicType("Too", out tk);
			t = RS(ty, ctxt);
			Assert.That(t, Is.Null);

			ty = DParser.ParseBasicType("Too!int", out tk);
			t = RS(ty, ctxt);
			Assert.That(t, Is.TypeOf(typeof(ClassType)));
		}

		[Test]
		public void TemplateParameterPrototypeRecognition()
		{
			var pcl = CreateCache(@"module A;
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

			var foo = pcl.FirstPackage()["A"]["foo"].First() as DMethod;
			var ctxt = CreateDefCtxt(pcl, foo, foo.Body);
			var subSt = foo.Body.SubStatements as List<IStatement>;

			ex = (subSt[0] as ExpressionStatement).Expression;
			var t = ExpressionTypeEvaluation.GetOverloads(ex as TemplateInstanceExpression, ctxt, null, true);
			Assert.That(t, Is.Not.Null);
			Assert.That(t.Count, Is.EqualTo(1));

			var t_ = ExpressionTypeEvaluation.EvaluateType(ex, ctxt, false);
			Assert.That(t_, Is.TypeOf(typeof(MemberSymbol)));
			Assert.That((t_ as MemberSymbol).Base, Is.TypeOf(typeof(ArrayType)));

			ex = (subSt[1] as ExpressionStatement).Expression;
			t = ExpressionTypeEvaluation.GetOverloads((ex as PostfixExpression_MethodCall).PostfixForeExpression as TemplateInstanceExpression, ctxt, null, true);
			Assert.That(t, Is.Not.Null);
			Assert.That(t.Count, Is.EqualTo(1));
			Assert.That(t[0], Is.TypeOf(typeof(MemberSymbol)));

			t_ = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t_, Is.TypeOf(typeof(PointerType)));

			ex = (subSt[2] as ExpressionStatement).Expression;
			t_ = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t_, Is.TypeOf(typeof(ArrayType)));

			ex = (subSt[3] as ExpressionStatement).Expression;
			t_ = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t_, Is.TypeOf(typeof(ArrayType)));
		}

		[Test]
		public void TemplateParameterPrototypeRecognition1()
		{
			var pcl = CreateCache(@"module A;
static int tmplFoo(T)() {}
static int[] tmplFoo2(T : U[], U)(int oo) {}
static int* tmplBar(T)(T t) {}

void foo(U)(U u)
{
	tmplFoo!U;
}");

			var foo = pcl.FirstPackage()["A"]["foo"].First() as DMethod;
			var ctxt = CreateDefCtxt(pcl, foo, foo.Body);
			var subSt = foo.Body.SubStatements as List<IStatement>;

			var ex = (subSt[0] as ExpressionStatement).Expression;
			var t = ExpressionTypeEvaluation.GetOverloads(ex as TemplateInstanceExpression, ctxt, null, true);
			Assert.That(t, Is.Not.Null);
			Assert.That(t.Count, Is.EqualTo(1));

			var t_ = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.That(t_, Is.TypeOf(typeof(PrimitiveType)));
		}

		[Test]
		public void TemplateAliasing()
		{
			var pcl = CreateCache(@"module m;
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

			var ctxt = CreateDefCtxt(pcl, pcl.FirstPackage()["m"]);

			DToken tk;
			var td = DParser.ParseBasicType("Foo!int", out tk);

			var s = RS(td, ctxt);

			Assert.That(s, Is.InstanceOf(typeof(MemberSymbol)));

			var ms = (MemberSymbol)s;
			Assert.That(ms.Definition, Is.InstanceOf(typeof(DVariable)));
			Assert.That(ms.Base, Is.InstanceOf(typeof(TemplateParameterSymbol)));
			var tps = (TemplateParameterSymbol)ms.Base;
			Assert.That(tps.Base, Is.InstanceOf(typeof(PrimitiveType)));

			var pt = (PrimitiveType)tps.Base;
			Assert.AreEqual(DTokens.Int, pt.TypeToken);

			s = RS(DParser.ParseBasicType("Bar!int", out tk), ctxt);

			Assert.That(((DSymbol)s).Base, Is.TypeOf(typeof(PointerType)));

			s = RS(DParser.ParseBasicType("Baz!int", out tk), ctxt);

			Assert.That(((DSymbol)s).Base, Is.TypeOf(typeof(PointerType)));
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

			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			t = (t as MemberSymbol).Base;
			Assert.That(t, Is.TypeOf(typeof(ArrayType)));

			x = DParser.ParseExpression("Traits!double");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.TypeOf(typeof(MemberSymbol)));
			t = (t as MemberSymbol).Base;
			Assert.That(t, Is.TypeOf(typeof(PrimitiveType)));

			x = DParser.ParseExpression("Traits!int");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.That(t, Is.Null);

			x = DParser.ParseExpression("func!string(1)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(ArrayType)));

			x = DParser.ParseExpression("func!double(1)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.TypeOf(typeof(PrimitiveType)));

			x = DParser.ParseExpression("func!int(1)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.That(t, Is.Null);

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

			Assert.That(t, Is.TypeOf(typeof(StructType)));
		}
	}
}
