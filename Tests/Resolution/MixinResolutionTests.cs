using System.Linq;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.ExpressionSemantics;
using NUnit.Framework;

namespace Tests.Resolution
{
	[TestFixture]
	public class MixinResolutionTests : ResolutionTestHelper
	{
		[Test]
		public void MixinCache()
		{
			var ctxt = CreateCtxt("A", @"module A;

mixin(""int intA;"");

class ClassA
{
	mixin(""int intB;"");
}

class ClassB(T)
{
	mixin(""int intC;"");
}

ClassA ca;
ClassB!int cb;
ClassB!bool cc;
");

			IExpression x, x2;
			MemberSymbol t, t2;

			x = DParser.ParseExpression("intA");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt) as MemberSymbol;
			t2 = ExpressionTypeEvaluation.EvaluateType(x, ctxt) as MemberSymbol;

			Assert.AreSame(t2.Definition, t.Definition);

			x = DParser.ParseExpression("ca.intB");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt) as MemberSymbol;
			t2 = ExpressionTypeEvaluation.EvaluateType(x, ctxt) as MemberSymbol;

			Assert.AreSame(t2.Definition, t.Definition);

			x = DParser.ParseExpression("cb.intC");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt) as MemberSymbol;
			t2 = ExpressionTypeEvaluation.EvaluateType(x, ctxt) as MemberSymbol;

			Assert.AreSame(t2.Definition, t.Definition);

			x2 = DParser.ParseExpression("cc.intC");
			t2 = ExpressionTypeEvaluation.EvaluateType(x2, ctxt) as MemberSymbol;

			Assert.AreNotSame(t2.Definition, t.Definition);
		}

		[Test]
		public void Mixins1()
		{
			var pcl = CreateCache(out DModule A, @"module A;
private mixin(""int privA;"");
package mixin(""int packA;"");
private int privAA;
package int packAA;

mixin(""int x; int ""~""y""~"";"");",

												  @"module pack.B;
import A;",
												 @"module C; import A;");

			var ctxt = CreateDefCtxt(pcl, A);

			var x = R("x", ctxt);
			Assert.AreEqual(1, x.Count);

			x = R("y", ctxt);
			Assert.AreEqual(1, x.Count);

			ctxt.CurrentContext.Set(pcl.FirstPackage()["pack.B"]);

			x = R("x", ctxt);
			Assert.AreEqual(1, x.Count);

			x = R("privAA", ctxt);
			Assert.AreEqual(0, x.Count);

			x = R("privA", ctxt);
			Assert.AreEqual(0, x.Count);

			x = R("packAA", ctxt);
			Assert.AreEqual(0, x.Count);

			x = R("packA", ctxt);
			Assert.AreEqual(0, x.Count);

			ctxt.CurrentContext.Set(pcl.FirstPackage()["C"]);

			x = R("privA", ctxt);
			Assert.AreEqual(0, x.Count);

			x = R("packAA", ctxt);
			Assert.AreEqual(1, x.Count);

			x = R("packA", ctxt);
			Assert.AreEqual(1, x.Count);
		}

		[Test]
		public void Mixins2()
		{
			var pcl = CreateCache(out DModule A, @"module A; 

void main()
{
	mixin(""int x;"");
	
	derp;
	
	mixin(""int y;"");
}
");

			var main = A["main"].First() as DMethod;
			var stmt = main.Body.SubStatements.ElementAt(1);
			var ctxt = ResolutionTests.CreateDefCtxt(pcl, main, stmt);

			var t = RS((ITypeDeclaration)new IdentifierDeclaration("x") { Location = stmt.Location }, ctxt);
			Assert.IsInstanceOf<MemberSymbol>(t);

			t = RS((ITypeDeclaration)new IdentifierDeclaration("y") { Location = stmt.Location }, ctxt);
			Assert.IsNull(t);
		}

		[Test]
		public void Mixins3()
		{
			var ctxt = CreateDefCtxt(@"module A;
template Temp(string v)
{
	mixin(v);
}

class cl
{
	mixin(""int someInt=345;"");
}");
			IExpression ex;
			AbstractType t;

			ex = DParser.ParseExpression("(new cl()).someInt");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOf<MemberSymbol>(t);

			ex = DParser.ParseExpression("Temp!\"int Temp;\"");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			Assert.IsInstanceOf<MemberSymbol>(t);
		}

		[Test]
		public void Mixins4()
		{
			var pcl = CreateCache(out DModule B,
				@"module B; import A; mixin(mixinStuff); class cl{ void bar(){  } }",
				@"module A; enum mixinStuff = q{import C;};",
				@"module C; void CFoo() {}");
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

		[Test]
		public void Mixins5()
		{
			var pcl = CreateCache(out DModule A, @"module A;
mixin(""template mxT(string n) { enum mxT = n; }"");
mixin(""class ""~mxT!(""myClass"")~"" {}"");
", @"module B;
mixin(""class ""~mxT!(""myClass"")~"" {}"");
mixin(""template mxT(string n) { enum mxT = n; }"");
");

			var ctxt = CreateDefCtxt(pcl, A);

			var t = RS("myClass", ctxt);
			Assert.IsInstanceOf<ClassType>(t);

			ctxt.CurrentContext.Set(pcl.FirstPackage()["B"]);

			t = RS("myClass", ctxt);
			Assert.IsNull(t);
		}

		[Test]
		public void StaticProperty_Stringof()
		{
			var ctxt = CreateDefCtxt(@"module A;
interface IUnknown {}

public template uuid(T, immutable char[] g) {
	const char [] uuid =
		""const IID IID_""~T.stringof~""={ 0x"" ~ g[0..8] ~ "",0x"" ~ g[9..13] ~ "",0x"" ~ g[14..18] ~ "",[0x"" ~ g[19..21] ~ "",0x"" ~ g[21..23] ~ "",0x"" ~ g[24..26] ~ "",0x"" ~ g[26..28] ~ "",0x"" ~ g[28..30] ~ "",0x"" ~ g[30..32] ~ "",0x"" ~ g[32..34] ~ "",0x"" ~ g[34..36] ~ ""]};""
		""template uuidof(T:""~T.stringof~""){""
		""    const IID uuidof ={ 0x"" ~ g[0..8] ~ "",0x"" ~ g[9..13] ~ "",0x"" ~ g[14..18] ~ "",[0x"" ~ g[19..21] ~ "",0x"" ~ g[21..23] ~ "",0x"" ~ g[24..26] ~ "",0x"" ~ g[26..28] ~ "",0x"" ~ g[28..30] ~ "",0x"" ~ g[30..32] ~ "",0x"" ~ g[32..34] ~ "",0x"" ~ g[34..36] ~ ""]};""
		""}"";
}
");

			IExpression x;
			ISymbolValue v;

			x = DParser.ParseExpression(@"uuid!(IUnknown, ""00000000-0000-0000-C000-000000000046"")");
			(x as TemplateInstanceExpression).Location = new CodeLocation(1, 3);
			v = D_Parser.Resolver.ExpressionSemantics.Evaluation.EvaluateValue(x, ctxt);

			var av = v as ArrayValue;
			Assert.IsInstanceOf<ArrayValue>(v);
			Assert.IsTrue(av.IsString);
			Assert.AreEqual(@"const IID IID_IUnknown={ 0x00000000,0x0000,0x0000,[0xC0,0x00,0x00,0x00,0x00,0x00,0x00,0x46]};"
			                + "template uuidof(T:IUnknown){"
			                + "    const IID uuidof ={ 0x00000000,0x0000,0x0000,[0xC0,0x00,0x00,0x00,0x00,0x00,0x00,0x46]};"
			                + "}", av.StringValue);
		}

		[Test]
		public void Mixins7()
		{
			var ctxt = CreateDefCtxt(@"module A;
mixin template mix_test() {int a;}

class C {
enum mix = ""test"";
mixin( ""mixin mix_"" ~ mix ~ "";"" );
}

C c;
");
			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("c.a");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.IsInstanceOf<MemberSymbol>(t);
			Assert.IsInstanceOf<PrimitiveType>((t as MemberSymbol).Base);
		}

		[Test]
		public void NestedMixins()
		{
			var ctxt = CreateDefCtxt(@"module A;
mixin(""template mxT1(string n) { enum mxT1 = n; }"");
mixin(mxT1!(""template"")~"" mxT2(string n) { enum mxT2 = n; }"");
mixin(""template mxT3(string n) { ""~mxT2!(""enum"")~"" mxT3 = n; }"");

mixin(""template mxT4(""~mxT3!(""string"")~"" n) { enum mxT4 = n; }"");
mixin(""class ""~mxT4!(""myClass"")~"" {}"");"");");

			var t = RS("mxT1", ctxt);
			Assert.IsInstanceOf<TemplateType>(t);

			t = RS("mxT2", ctxt);
			Assert.IsInstanceOf<TemplateType>(t);

			t = RS("mxT3", ctxt);
			Assert.IsInstanceOf<TemplateType>(t);

			t = RS("mxT4", ctxt);
			Assert.IsInstanceOf<TemplateType>(t);

			t = RS("myClass", ctxt);
			Assert.IsInstanceOf<ClassType>(t);
		}
	}
}
