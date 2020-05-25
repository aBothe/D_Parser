using System.Linq;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.ExpressionSemantics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tests.Completion;

namespace Tests.Resolution
{
	[TestClass]
	public class ResolutionTests : ResolutionTestHelper
	{
		[TestMethod]
		public void Test2_066UCSnytax()
		{
			var x = DParser.ParseExpression("creal(3)");
			var t = ExpressionTypeEvaluation.EvaluateType(x, CreateDefCtxt(""));

			Assert.IsInstanceOfType(t, typeof(PrimitiveType));
			Assert.AreEqual(DTokens.Creal, (t as PrimitiveType).TypeToken);
		}

		[TestMethod]
		public void TypePointerInstanceAccessing()
		{
			var ctxt = CreateCtxt("A", @"module A;
class Cl{ int a; }
Cl* cl;
");
			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("cl.a");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.IsInstanceOfType(t, typeof(MemberSymbol));
		}

		[TestMethod]
		public void AnonymousClasses()
		{
			var ctxt = CreateCtxt("A",@"module A;

class BaseClass
{
	int a;
}

auto n = new class BaseClass {};
");
			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression ("n.a");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);
			Assert.IsInstanceOfType(t, typeof(MemberSymbol));
			Assert.IsInstanceOfType((t as MemberSymbol).Base, typeof(PrimitiveType));
		}

		[TestMethod]
		public void StaticProperties_TupleOf()
		{
			var ctxt = CreateCtxt("A", @"module A;
enum mstr = ""int* a; string b;"";

struct S
{
	int c;
	mixin(mstr);
}

S s;
");
			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("s.tupleof");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			Assert.IsInstanceOfType(t, typeof(StaticProperty));
			Assert.IsInstanceOfType((t as StaticProperty).Base, typeof(DTuple));
			var dtuple = (t as StaticProperty).Base as DTuple;
			Assert.AreEqual(3, dtuple.Items.Length);
			Assert.IsInstanceOfType(dtuple.Items[0], typeof(PrimitiveType));
			Assert.IsInstanceOfType(dtuple.Items[1], typeof(PointerType));
			Assert.IsInstanceOfType(dtuple.Items[2], typeof(ArrayType));
		}

		[TestMethod]
		public void ArrayTypes()
		{
			var ctxt = CreateCtxt("A", @"module A;");

			ITypeDeclaration td;
			AssocArrayType aa;
			ArrayType at;

			td = DParser.ParseBasicType("int[int]");
			aa = RS(td, ctxt) as AssocArrayType;
			Assert.IsNotInstanceOfType(aa, typeof(ArrayType));
			Assert.IsInstanceOfType(aa.KeyType, typeof(PrimitiveType));
			Assert.AreEqual(DTokens.Int, (aa.KeyType as PrimitiveType).TypeToken);
			Assert.IsInstanceOfType(aa.ValueType, typeof(PrimitiveType));

			td = DParser.ParseBasicType("int[short]");
			aa = RS(td, ctxt) as AssocArrayType;
			Assert.IsNotInstanceOfType(aa, typeof(ArrayType));
			Assert.IsInstanceOfType(aa.KeyType, typeof(PrimitiveType));
			Assert.AreEqual(DTokens.Short, (aa.KeyType as PrimitiveType).TypeToken);
			Assert.IsInstanceOfType(aa.ValueType, typeof(PrimitiveType));

			td = DParser.ParseBasicType("int[string]");
			aa = RS(td, ctxt) as AssocArrayType;
			Assert.IsNotInstanceOfType(aa, typeof(ArrayType));
			Assert.IsInstanceOfType(aa.KeyType, typeof(ArrayType));
			Assert.IsTrue((aa.KeyType as ArrayType).IsString);
			Assert.IsInstanceOfType(aa.ValueType, typeof(PrimitiveType));
			aa = null;

			td = DParser.ParseBasicType("byte[3]");
			at = RS(td, ctxt) as ArrayType;
			Assert.AreEqual(3, at.FixedLength);
			Assert.IsNull(at.KeyType);
			Assert.IsInstanceOfType(at.ValueType, typeof(PrimitiveType));

			td = DParser.ParseBasicType("byte[6L]");
			at = RS(td, ctxt) as ArrayType;
			Assert.AreEqual(6, at.FixedLength);
			Assert.IsNull(at.KeyType);
			Assert.IsInstanceOfType(at.ValueType, typeof(PrimitiveType));
		}

		[TestMethod]
		public void ArrayIndexer()
		{
			var ctxt = CreateDefCtxt(@"module A;
class Obj
{
	int myProp;
}

auto arr = new Obj[];
auto o = new Obj();
Obj[][] oo;
");
			
			var myProp = CompletionTests.GetNode (null, "Obj.myProp", ref ctxt);

			IExpression ex;
			AbstractType t;

			ex = DParser.ParseExpression("oo[0][0]");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);

			Assert.IsInstanceOfType(t, typeof(ArrayAccessSymbol));

			ex = DParser.ParseExpression("oo[0][0].myProp");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);

			Assert.IsInstanceOfType(t, typeof(MemberSymbol));
			Assert.IsTrue(myProp.IsDefinedIn(t));
			Assert.IsInstanceOfType((t as MemberSymbol).Base, typeof(PrimitiveType));

			ex = DParser.ParseExpression("arr[0]");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);

			Assert.IsInstanceOfType(t, typeof(ArrayAccessSymbol));
			
			ex = DParser.ParseExpression("arr[0].myProp");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			
			Assert.IsInstanceOfType(t, typeof(MemberSymbol));
			Assert.IsTrue(myProp.IsDefinedIn(t));
			Assert.IsInstanceOfType((t as MemberSymbol).Base, typeof(PrimitiveType));
			
			ex = DParser.ParseExpression("o.myProp");
			t = ExpressionTypeEvaluation.EvaluateType(ex, ctxt);
			
			Assert.IsInstanceOfType(t, typeof(MemberSymbol));
			Assert.IsTrue(myProp.IsDefinedIn(t));
			Assert.IsInstanceOfType((t as MemberSymbol).Base, typeof(PrimitiveType));
		}

		/// <summary>
		/// Array slices are now r-values
		/// </summary>
		[TestMethod]
		public void ArraySlicesNoRValues()
		{
			var ctxt = CreateCtxt("modA", @"module modA;

int takeRef(ref int[] arr) { }
int take(int[] arr) { }
int takeAutoRef(T)(auto ref T[] arr) { }

int[] arr = [1, 2, 3, 4];
int[] arr2 = arr[1 .. 2];");

			IExpression x;
			AbstractType t;

			// error, cannot pass r-value by reference
			x = DParser.ParseExpression("takeRef(arr[1 .. 2])");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);
			//Assert.IsNull(t);

			// ok
			x = DParser.ParseExpression ("take(arr)");
			t = ExpressionTypeEvaluation.EvaluateType (x,ctxt);
			Assert.IsInstanceOfType(t, typeof(PrimitiveType));

			x = DParser.ParseExpression ("takeRef(arr)");
			t = ExpressionTypeEvaluation.EvaluateType (x,ctxt);
			Assert.IsInstanceOfType(t, typeof(PrimitiveType));

			x = DParser.ParseExpression ("takeAutoRef(arr)");
			t = ExpressionTypeEvaluation.EvaluateType (x,ctxt);
			Assert.IsInstanceOfType(t, typeof(PrimitiveType));

			// ok, arr2 is a variable
			x = DParser.ParseExpression ("take(arr2)");
			t = ExpressionTypeEvaluation.EvaluateType (x,ctxt);
			Assert.IsInstanceOfType(t, typeof(PrimitiveType));

			x = DParser.ParseExpression ("takeRef(arr2)");
			t = ExpressionTypeEvaluation.EvaluateType (x,ctxt);
			Assert.IsInstanceOfType(t, typeof(PrimitiveType));

			x = DParser.ParseExpression ("takeAutoRef(arr2)");
			t = ExpressionTypeEvaluation.EvaluateType (x,ctxt);
			Assert.IsInstanceOfType(t, typeof(PrimitiveType));


			x = DParser.ParseExpression ("take(arr[1 .. 2])");
			t = ExpressionTypeEvaluation.EvaluateType (x,ctxt);
			Assert.IsInstanceOfType(t, typeof(PrimitiveType));

			

			x = DParser.ParseExpression ("takeAutoRef(arr[1 .. 2])");
			t = ExpressionTypeEvaluation.EvaluateType (x,ctxt);
			Assert.IsInstanceOfType(t, typeof(PrimitiveType));
		}

		[TestMethod]
		public void Ctors()
		{
			var pcl = CreateCache(out DModule m, @"module modA;

class A {}
class B : A{
	this() {
		super();
	}
}");

			var B = m["B"].First() as DClassLike;
			var this_ = (DMethod)B[DMethod.ConstructorIdentifier].First();
			var ctxt = CreateDefCtxt(pcl, this_);

			var super = (this_.Body.SubStatements.First() as IExpressionContainingStatement).SubExpressions[0] as PostfixExpression_MethodCall;

			var t = ExpressionTypeEvaluation.EvaluateType(super.PostfixForeExpression, ctxt);
			Assert.IsInstanceOfType(t, typeof(ClassType));
			
			t = ExpressionTypeEvaluation.EvaluateType(super, ctxt);
			Assert.IsNull(t);
		}

		/// <summary>
		/// Constructor qualifiers are taken into account when constructing objects
		/// </summary>
		[TestMethod]
		public void QualifiedConstructors()
		{
			var ctxt = CreateCtxt("modA", @"module modA;
class C
{
    this() immutable { }
    this() shared    { }
	this()           { }
    this() const     { }
}

class D
{
    this() immutable { }
	this() const { }
}

class P
{
    this() pure { }
}
");

			IExpression x;
			MemberSymbol ctor;
			ClassType ct;

			x = DParser.ParseExpression ("new C");
			ctor = ExpressionTypeEvaluation.EvaluateType (x, ctxt, false) as MemberSymbol;

			Assert.IsNotNull(ctor);
			Assert.IsInstanceOfType(ctor.Base, typeof(ClassType));
			ct = ctor.Base as ClassType;
			Assert.IsFalse(ct.HasModifiers);

			x = DParser.ParseExpression ("new const C");
			ctor = ExpressionTypeEvaluation.EvaluateType(x, ctxt, false) as MemberSymbol;

			Assert.IsNotNull(ctor);
			Assert.IsInstanceOfType(ctor.Base, typeof(ClassType));
			ct = ctor.Base as ClassType;
			Assert.IsTrue (ct.HasModifier (DTokens.Const));

			x = DParser.ParseExpression ("new immutable C");
			ctor = ExpressionTypeEvaluation.EvaluateType(x, ctxt, false) as MemberSymbol;

			Assert.IsNotNull(ctor);
			Assert.IsInstanceOfType(ctor.Base, typeof(ClassType));
			ct = ctor.Base as ClassType;
			Assert.IsTrue (ct.HasModifier (DTokens.Immutable));

			x = DParser.ParseExpression ("new shared C");
			ctor = ExpressionTypeEvaluation.EvaluateType(x, ctxt, false) as MemberSymbol;

			Assert.IsNotNull(ctor);
			Assert.IsInstanceOfType(ctor.Base, typeof(ClassType));
			ct = ctor.Base as ClassType;
			Assert.IsTrue (ct.HasModifier(DTokens.Shared));



			x = DParser.ParseExpression ("new P");
			ctor = ExpressionTypeEvaluation.EvaluateType(x, ctxt, false) as MemberSymbol;

			Assert.IsNotNull(ctor);
			Assert.IsInstanceOfType(ctor.Base, typeof(ClassType));
			ct = ctor.Base as ClassType;
			Assert.IsFalse(ct.HasModifiers);

			x = DParser.ParseExpression ("new const P");
			ctor = ExpressionTypeEvaluation.EvaluateType(x, ctxt, false) as MemberSymbol;

			Assert.IsNotNull(ctor);
			Assert.IsInstanceOfType(ctor.Base, typeof(ClassType));
			ct = ctor.Base as ClassType;
			Assert.IsTrue (ct.HasModifier (DTokens.Const));

			x = DParser.ParseExpression ("new immutable P");
			ctor = ExpressionTypeEvaluation.EvaluateType(x, ctxt, false) as MemberSymbol;

			Assert.IsNotNull(ctor);
			Assert.IsInstanceOfType(ctor.Base, typeof(ClassType));
			ct = ctor.Base as ClassType;
			Assert.IsTrue (ct.HasModifier (DTokens.Immutable));



			x = DParser.ParseExpression ("new const D");
			ctor = ExpressionTypeEvaluation.EvaluateType(x, ctxt, false) as MemberSymbol;

			Assert.IsNotNull(ctor);
			Assert.IsInstanceOfType(ctor.Base, typeof(ClassType));
			ct = ctor.Base as ClassType;
			Assert.IsTrue (ct.HasModifier (DTokens.Const));

			x = DParser.ParseExpression ("new D");
			ctor = ExpressionTypeEvaluation.EvaluateType(x, ctxt, false) as MemberSymbol;

			Assert.IsNull(ctor);
		}

		const string iftiSampleCode = @"module modA;
struct A{    struct Foo { } }
struct B{    struct Foo { } }

int call(T)(T t, T.Foo foo) { }

auto a = A();
auto a_f = A.Foo();

auto b = B();
auto b_f = B.Foo();
";

		/// <summary>
		/// Implicit Function Template Instantiation now supports enclosing type/scope deduction.
		/// </summary>
		[TestMethod]
		public void ImprovedIFTI()
		{
			var ctxt = CreateCtxt("modA", iftiSampleCode);
			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression ("call(a, a_f)");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);

			Assert.IsInstanceOfType(t, typeof(PrimitiveType));

			x = DParser.ParseExpression ("call(b, b_f)");
			t = ExpressionTypeEvaluation.EvaluateType (x, ctxt);

			Assert.IsInstanceOfType(t, typeof(PrimitiveType));
		}

		[TestMethod]
		public void ImprovedIFTI2()
		{
			var ctxt = CreateCtxt("modA", iftiSampleCode);
			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("call(a, b_f)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.IsNull(t);
		}

		[TestMethod]
		public void ImprovedIFTI3()
		{
			var ctxt = CreateCtxt("modA", iftiSampleCode);
			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("call(b, a_f)");
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.IsNull(t);
		}
	}
}
