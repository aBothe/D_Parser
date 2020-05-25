﻿using System.Linq;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.TypeResolution;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Resolution
{
	[TestClass]
	public class ImplicitConversionTests
	{
		public static AbstractType GetType(string name, ResolutionContext ctxt)
		{
			return ExpressionTypeEvaluation.GetOverloads(new IdentifierExpression(name), ctxt, null, false)[0];
		}

		[TestMethod]
		public void ClassInheritanceTest()
		{
			var pcl = ResolutionTests.CreateCache(out DModule m, @"module modA;
				class A{}
				class B:A {}
				class C:A {}
				class D:C {}");
			var ctxt = ResolutionContext.Create(pcl, null, m);

			var A = GetType("A",ctxt);
			var B = GetType("B", ctxt);
			var C = GetType("C", ctxt);
			var D = GetType("D", ctxt);

			Assert.IsTrue(ResultComparer.IsEqual(A, A));
			Assert.IsTrue(ResultComparer.IsEqual(B, B));
			Assert.IsTrue(ResultComparer.IsEqual(C, C));
			Assert.IsTrue(ResultComparer.IsEqual(D, D));

			Assert.IsFalse(ResultComparer.IsImplicitlyConvertible(A, B));
			Assert.IsFalse(ResultComparer.IsImplicitlyConvertible(A, C));
			Assert.IsFalse(ResultComparer.IsImplicitlyConvertible(A, D));

			Assert.IsFalse(ResultComparer.IsImplicitlyConvertible(B,C));
			Assert.IsFalse(ResultComparer.IsImplicitlyConvertible(C,B));

			Assert.IsTrue(ResultComparer.IsImplicitlyConvertible(A, A));
			Assert.IsTrue(ResultComparer.IsImplicitlyConvertible(B, A));
			Assert.IsTrue(ResultComparer.IsImplicitlyConvertible(C, A));
			Assert.IsTrue(ResultComparer.IsImplicitlyConvertible(D, C));
			Assert.IsTrue(ResultComparer.IsImplicitlyConvertible(D, A));
		}

		[TestMethod]
		public void InterfaceInheritanceTest()
		{
			var pcl = ResolutionTestHelper.CreateCache(out DModule m, @"module modA;
				class A {}
				class B {}

				interface IA {}
				interface IB {}

				interface IC : IA {}
				interface ID : IC {}

				class E : A, IA {}
				class F : B, IA {}

				class G : A, IC {}
				class H : B, ID {}");
			var ctxt = ResolutionContext.Create(pcl, null, m);

			var A = GetType("A", ctxt);
			var B = GetType("B", ctxt);
			var IA = GetType("IA", ctxt);
			var IB = GetType("IB", ctxt);
			var IC = GetType("IC", ctxt);
			var ID = GetType("ID", ctxt);
			var E = GetType("E", ctxt);
			var F = GetType("F", ctxt);
			var G = GetType("G", ctxt);
			var H = GetType("H", ctxt);

			Assert.IsTrue(ResultComparer.IsImplicitlyConvertible(IC, IA));
			Assert.IsTrue(ResultComparer.IsImplicitlyConvertible(ID, IC));
			Assert.IsTrue(ResultComparer.IsImplicitlyConvertible(ID, IA));

			Assert.IsFalse(ResultComparer.IsImplicitlyConvertible(IA, IC));
			Assert.IsFalse(ResultComparer.IsImplicitlyConvertible(IA, ID));
			Assert.IsFalse(ResultComparer.IsImplicitlyConvertible(IC, IB));

			Assert.IsTrue(ResultComparer.IsImplicitlyConvertible(E, A));
			Assert.IsTrue(ResultComparer.IsImplicitlyConvertible(E, IA));

			Assert.IsFalse(ResultComparer.IsImplicitlyConvertible(E, F));
			Assert.IsFalse(ResultComparer.IsImplicitlyConvertible(F, E));
			
			Assert.IsTrue(ResultComparer.IsImplicitlyConvertible(F, B));
			Assert.IsTrue(ResultComparer.IsImplicitlyConvertible(F, IA));

			Assert.IsTrue(ResultComparer.IsImplicitlyConvertible(G, A));
			Assert.IsTrue(ResultComparer.IsImplicitlyConvertible(G, IC));
			Assert.IsTrue(ResultComparer.IsImplicitlyConvertible(G, IA));

			Assert.IsTrue(ResultComparer.IsImplicitlyConvertible(H, B));
			Assert.IsTrue(ResultComparer.IsImplicitlyConvertible(H, ID));
			Assert.IsTrue(ResultComparer.IsImplicitlyConvertible(H, IC));
			Assert.IsTrue(ResultComparer.IsImplicitlyConvertible(H, IA));
			
		}

		[TestMethod]
		public void TestTemplateDeductionAsConversion()
		{
			var pcl = ResolutionTestHelper.CreateCache(out DModule m, @"module modA;
void foo(T:T)(T[] t) {}

int[] p=[1,2,3,4,5];
");
			var ctxt = ResolutionContext.Create(pcl, null, m);

			var foo = pcl.FirstPackage()["modA"]["foo"].First() as DMethod;
			using (ctxt.Push(foo, foo.Body.Location))
			{
				var foo_firstArg = TypeDeclarationResolver.ResolveSingle(foo.Parameters[0].Type, ctxt);

				var p = GetType("p", ctxt) as MemberSymbol;

				Assert.IsTrue(ResultComparer.IsImplicitlyConvertible(p, foo_firstArg, ctxt));
			}
		}
	}
}
