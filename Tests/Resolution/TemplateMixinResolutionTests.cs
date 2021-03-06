﻿using System.Collections.Generic;
using System.Linq;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.ExpressionSemantics;
using NUnit.Framework;

namespace Tests.Resolution
{
	[TestFixture]
	public class TemplateMixinResolutionTests : ResolutionTestHelper
	{
		[Test]
		public void TemplateMixins1()
		{
			var ctxt = CreateDefCtxt(@"module A;
mixin template Mx(T)
{
	T myFoo;
}

mixin template Mx1()
{
	int someProp;
}
mixin Mx1;
mixin Mx!int;

mixin Mx1 myMx;
mixin Mx!float myTempMx;");

			var ex = DParser.ParseExpression("someProp");
			var x = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOf<MemberSymbol>(x);
			Assert.IsInstanceOf<PrimitiveType>((x as MemberSymbol).Base);

			ex = DParser.ParseExpression("myFoo");
			x = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOf<MemberSymbol>(x);
			var ms = x as MemberSymbol;
			Assert.IsInstanceOf<TemplateParameterSymbol>(ms.Base);
			Assert.IsInstanceOf<PrimitiveType>((ms.Base as TemplateParameterSymbol).Base);

			ex = DParser.ParseExpression("myMx.someProp;");
			x = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOf<MemberSymbol>(x);
			Assert.IsInstanceOf<PrimitiveType>((x as MemberSymbol).Base);

			ex = DParser.ParseExpression("myTempMx.myFoo");
			x = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOf<MemberSymbol>(x);
			ms = x as MemberSymbol;
			Assert.IsInstanceOf<TemplateParameterSymbol>(ms.Base);
			Assert.IsInstanceOf<PrimitiveType>((ms.Base as TemplateParameterSymbol).Base);
		}

		[Test]
		public void TemplateMixins2()
		{
			var ctxt = CreateDefCtxt(@"module A;
mixin template Foo() {
  int[] func() { writefln(""Foo.func()""); }
}

class Bar {
  mixin Foo;
}

class Code : Bar {
  float func() { writefln(""Code.func()""); }
}

void test() {
  Bar b = new Bar();
  b.func();      // calls Foo.func()

  b = new Code();
  b.func();      // calls Code.func()
}");

			var ex = DParser.ParseExpression("(new Code()).func");
			var x = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOf<PrimitiveType>(x);

			ex = DParser.ParseExpression("(new Bar()).func");
			x = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOf<ArrayType>(x);
		}

		[Test]
		public void TemplateMixins3()
		{
			var pcl = CreateCache(out DModule A, @"module A;
mixin template Singleton(I) {
	static I getInstance() {}
	
	void singletonBar() {}
}

mixin template Foo(T) {
  int localDerp;
  T[] arrayTest;
}

class clA
{
	mixin Singleton!clA;
	
	void clFoo() {}
}

void foo() {
	localDerp;
	mixin Foo!int;
	localDerp;
	arrayTest[0];
}");
			var foo = A["foo"].First() as DMethod;
			var ctxt = CreateDefCtxt(pcl, foo, foo.Body);
			var subSt = foo.Body.SubStatements as List<IStatement>;

			var t = ExpressionTypeEvaluation.EvaluateType((subSt[0] as ExpressionStatement).Expression, ctxt);
			Assert.IsNull(t);

			t = ExpressionTypeEvaluation.EvaluateType((subSt[2] as ExpressionStatement).Expression, ctxt);
			Assert.IsInstanceOf<MemberSymbol>(t);
			var ms = t as MemberSymbol;
			Assert.IsInstanceOf<PrimitiveType>(ms.Base);

			t = ExpressionTypeEvaluation.EvaluateType((subSt[3] as ExpressionStatement).Expression, ctxt);
			Assert.IsInstanceOf<ArrayAccessSymbol>(t);
			t = (t as ArrayAccessSymbol).Base;
			Assert.IsInstanceOf<TemplateParameterSymbol>(t);
			Assert.IsInstanceOf<PrimitiveType>((t as TemplateParameterSymbol).Base);

			var ex = DParser.ParseExpression("clA.getInstance");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt, false);
			Assert.IsInstanceOf<MemberSymbol>(t);

			foo = (A["Singleton"].First() as DClassLike)["singletonBar"].First() as DMethod;
			ctxt.CurrentContext.Set(foo, foo.Body.Location);
			t = RS("I", ctxt);
			Assert.IsInstanceOf<TemplateParameterSymbol>(t);

			foo = (A["clA"].First() as DClassLike)["clFoo"].First() as DMethod;
			ctxt.CurrentContext.Set(foo, foo.Body.Location);
			t = RS("I", ctxt);
			Assert.IsNull(t);
		}

		[Test]
		public void TemplateMixins4()
		{
			var pcl = CreateCache(out DModule B,
				@"module B; import A; mixin mixedInImports; class cl{ void bar(){  } }",
				@"module A;
mixin template mixedInImports()
{
	import C;
}", @"module C;
void CFoo() {}");
			var ctxt = CreateDefCtxt(pcl, B);

			var t = RS("CFoo", ctxt);
			Assert.IsInstanceOf<MemberSymbol>(t);
			Assert.IsInstanceOf<DMethod>((t as MemberSymbol).Definition);

			var bar = (B["cl"].First() as DClassLike)["bar"].First() as DMethod;
			ctxt.CurrentContext.Set(bar, bar.Body.Location);

			t = RS("CFoo", ctxt);
			Assert.IsInstanceOf<MemberSymbol>(t);
			Assert.IsInstanceOf<DMethod>((t as MemberSymbol).Definition);
		}

		readonly string[] autoImplementHook = new [] { @"module A;
import std.typecons;
struct Parms;
interface TestAPI
{
        string foo(string name);
        string bar(string lol, int lal, Parms parms);
}

AutoImplement!(TestAPI, generateEmptyFunction) derp;
BlackHole!TestAPI yorp;
		", @"module std.typecons;

template generateEmptyFunction(C, func.../+[BUG 4217]+/)
{
    static if (is(ReturnType!(func) == void))
        enum string generateEmptyFunction = q{
        };
    else static if (functionAttributes!(func) & FunctionAttribute.ref_)
        enum string generateEmptyFunction = q{
            static typeof(return) dummy;
            return dummy;
        };
    else
        enum string generateEmptyFunction = q{
            return typeof(return).init;
        };
}

template isAbstractFunction() {}

class AutoImplement(Base, alias how, alias what = isAbstractFunction) : Base
{
    private alias AutoImplement_Helper!(
            ""autoImplement_helper_"", ""Base"", Base, how, what )
             autoImplement_helper_;
    mixin(autoImplement_helper_.code);
}

template BlackHole(Base)
{
    alias AutoImplement!(Base, generateEmptyFunction, isAbstractFunction)
            BlackHole;
}
" };

		[Test]
		public void AutoImplementHook1()
		{
			var ctxt = CreateCtxt("A", autoImplementHook);

			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("BlackHole!TestAPI");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt, false);

			Assert.IsInstanceOf<AliasedType>(t);
			var aliasedType = t as AliasedType;

			Assert.IsInstanceOf<ClassType>(aliasedType.Base);
			var classType = aliasedType.Base as ClassType;
			Assert.IsInstanceOf<InterfaceType>(classType.BaseInterfaces[0]);
		}

		[Test]
		public void AutoImplementHook2()
		{
			var ctxt = CreateCtxt("A", autoImplementHook);

			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("yorp.foo");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt, false);

			Assert.IsInstanceOf<MemberSymbol>(t);
		}

		[Test]
		public void AutoImplementHook3()
		{
			var ctxt = CreateCtxt("A", autoImplementHook);

			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("AutoImplement!(TestAPI, generateEmptyFunction)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt, false);

			Assert.IsInstanceOf<ClassType>(t);

			x = DParser.ParseExpression("derp.foo");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt, false);

			Assert.IsInstanceOf<MemberSymbol>(t);
		}

		[Test]
		public void BitfieldsHook()
		{
			var ctxt = CreateCtxt("A", @"module A;
import std.bitmanip;

struct S {
    int a;
    mixin(bitfields!(
        uint, ""x"",    2,
        int*,  ""y"",    3,
        uint[], ""z"",    2,
        bool, ""flag"", 1));
}

S s;
		", @"module std.bitmanip;

template bitfields(T...)
{
    enum { bitfields = createFields!(createStoreName!(T), 0, T).result }
}
");

			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("s.x");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.IsInstanceOf<TemplateParameterSymbol>(t);
			Assert.IsInstanceOf<PrimitiveType>((t as TemplateParameterSymbol).Base);
		}

		const string templateAliasParamsCode = @"module A;
struct TestField(T)
{
        T t;
        alias t this; // doesn't matter
}

mixin template MyTemplate(alias T)
{
        auto Field1 = T!(ulong)();
        auto Field2 = T!(string)();
}

class TestClass
{
        mixin MyTemplate!(TestField);
}

TestClass c;
";

		[Test]
		public void TemplateAliasParams()
		{
			var ctxt = CreateDefCtxt(templateAliasParamsCode);
			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("MyTemplate!(TestField)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.IsInstanceOf<MixinTemplateType>(t);
			var MyTemplate = t as MixinTemplateType;
			var MyTemplateDef = MyTemplate.Definition as DClassLike;
			var firstDeducedParam = MyTemplate.DeducedTypes[0];
			Assert.AreSame(MyTemplateDef.TemplateParameters[0], (firstDeducedParam.Definition as TemplateParameter.Node).TemplateParameter);
			Assert.IsInstanceOf<StructType>(firstDeducedParam.Base);

			ctxt.CurrentContext.Set(MyTemplateDef);
			ctxt.CurrentContext.IntroduceTemplateParameterTypes(MyTemplate);

			x = DParser.ParseExpression("T!ulong");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.IsInstanceOf<StructType>(t);
		}

		[Test]
		public void TemplateAliasParams2_AccessMixinTemplateAliasedStruct()
		{
			var ctxt = CreateDefCtxt(templateAliasParamsCode);
			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("c.Field1");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.IsInstanceOf<MemberSymbol>(t);
			var @base = (t as MemberSymbol).Base;
			Assert.IsInstanceOf<StructType>(@base);
		}

		[Test]
		public void TemplateAliasParams3_AccessMixinTemplateAliasedStructProperties()
		{
			var ctxt = CreateDefCtxt(templateAliasParamsCode);
			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("c.Field2.t");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.IsInstanceOf<MemberSymbol>(t);
			var ms = t as MemberSymbol;
			Assert.IsInstanceOf<TemplateParameterSymbol>(ms.Base);
			var tps = ms.Base as TemplateParameterSymbol;
			Assert.IsInstanceOf<ArrayType>(tps.Base);
			var at = tps.Base as ArrayType;
			Assert.IsInstanceOf<PrimitiveType>(at.ValueType);
		}

		[Test]
		public void TemplateAliasParams4_CachingIssues()
		{
			var ctxt = CreateDefCtxt(templateAliasParamsCode);
			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("c.Field1");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.IsInstanceOf<MemberSymbol>(t);
			var @base = (t as MemberSymbol).Base;
			Assert.IsInstanceOf<StructType>(@base);

			x = DParser.ParseExpression("c.Field2.t");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.IsInstanceOf<MemberSymbol>(t);
			var ms = t as MemberSymbol;
			Assert.IsInstanceOf<TemplateParameterSymbol>(ms.Base);
			var tps = ms.Base as TemplateParameterSymbol;
			Assert.IsInstanceOf<ArrayType>(tps.Base);
			var at = tps.Base as ArrayType;
			Assert.IsInstanceOf<PrimitiveType>(at.ValueType);
		}

		const string templateAliasParamsCode5 = @"module A;
struct TestField(TFValueType)
{
	TFValueType t;
}

class TestClass(alias T)
{
	auto Field1 = T!(ulong)();
	auto Field2 = T!(string)();
}

TestClass!TestField c;
";

		[Test]
		public void TemplateAliasParams5()
		{
			var ctxt = CreateDefCtxt(templateAliasParamsCode5);
			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("c.Field1");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.IsInstanceOf<MemberSymbol>(t);
			var @base = (t as MemberSymbol).Base;
			Assert.IsInstanceOf<StructType>(@base);

			x = DParser.ParseExpression("c.Field2.t");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.IsInstanceOf<MemberSymbol>(t);
			var ms = t as MemberSymbol;
			Assert.IsInstanceOf<TemplateParameterSymbol>(ms.Base);
			var tps = ms.Base as TemplateParameterSymbol;
			Assert.IsInstanceOf<ArrayType>(tps.Base);
			var at = tps.Base as ArrayType;
			Assert.IsInstanceOf<PrimitiveType>(at.ValueType);
		}

		const string templateAliasParamsCode6 = @"module A;
struct TestField(TFValueType)
{
	TFValueType t;
}

class TestClass(alias T)
{
	auto Field1 = T!(ulong)();
	T!(string) Field2;
}

TestClass!(TestField!int) c;
";

		[Test]
		public void TemplateAliasParams6_AlreadyResolvableTestFieldStruct()
		{
			TemplateAliasParams6_7(templateAliasParamsCode6);
		}

		const string templateAliasParamsCode7 = @"module A;
struct TestField(TFValueType)
{
	TFValueType t;
}

class TestClass(T) // no alias here
{
	T!(ulong) Field1;
	auto Field2 = T!(string)();
}

TestClass!(TestField!int) c;
";

		[Test]
		public void TemplateAliasParams7_AlreadyResolvableTestFieldStruct_NoAliasParam()
		{
			TemplateAliasParams6_7(templateAliasParamsCode7);
		}

		void TemplateAliasParams6_7(string code)
		{
			var ctxt = CreateDefCtxt(code);
			IExpression x;
			AbstractType t;

			{
				x = DParser.ParseExpression("c.Field1.t");
				t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

				Assert.IsInstanceOf<MemberSymbol>(t);
				var @base = (t as MemberSymbol).Base;
				Assert.IsInstanceOf<TemplateParameterSymbol>(@base);
				var tps = @base as TemplateParameterSymbol;
				var firstParamType = tps.Base;
				Assert.IsInstanceOf<PrimitiveType>(firstParamType);
				var primitiveFirstParamType = firstParamType as PrimitiveType;
				Assert.AreEqual(DTokens.Ulong, primitiveFirstParamType.TypeToken);
			}

			{
				x = DParser.ParseExpression("c.Field2.t");
				t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

				Assert.IsInstanceOf<MemberSymbol>(t);
				var ms = t as MemberSymbol;
				Assert.IsInstanceOf<TemplateParameterSymbol>(ms.Base);
				var tps = ms.Base as TemplateParameterSymbol;
				Assert.IsInstanceOf<ArrayType>(tps.Base);
				var at = tps.Base as ArrayType;
				Assert.IsInstanceOf<PrimitiveType>(at.ValueType);
			}
		}

		[Test]
		public void StdSignals()
		{
			var ctxt = CreateCtxt("A", @"module A;
mixin template Signal(T1 ...)
{
	final int emit( T1 i ) {}
}

class D
{
	mixin Signal!int sig;
	mixin Signal!int;
}

D d;
");

			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("d.emit(123)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.IsInstanceOf<PrimitiveType>(t);

			x = DParser.ParseExpression("d.sig.emit(123)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.IsInstanceOf<PrimitiveType>(t);
		}

		[Test]
		public void SustainingDeducedTypesInImplicitTemplateProperties()
		{
			var ctxt = CreateCtxt("A", @"module A;
template baz(string s) { enum baz = ""int ""~s~"";""; }
");

			var x = DParser.ParseExpression("baz!\"w\"");
			var v = Evaluation.EvaluateValue(x, ctxt);

			Assert.IsInstanceOf<ArrayValue>(v);
			var av = v as ArrayValue;
			Assert.IsTrue(av.IsString);
			Assert.AreEqual("int w;", av.StringValue);
		}
	}
}
