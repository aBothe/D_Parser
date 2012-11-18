using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Parser;
using D_Parser.Dom;

namespace Tests
{
	[TestFixture]
	public class ImplicitConversionTests
	{
		public static AbstractType GetType(string name, ResolutionContext ctxt)
		{
			return TypeDeclarationResolver.ResolveIdentifier(name, ctxt, null)[0] as AbstractType;
		}

		[Test]
		public void ClassInheritanceTest()
		{
			var pcl=ResolutionTests.CreateCache(@"module modA;
				class A{}
				class B:A {}
				class C:A {}
				class D:C {}");
			var ctxt = ResolutionContext.Create(pcl, null, pcl[0]["modA"]);

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

		[Test]
		public void InterfaceInheritanceTest()
		{
			var pcl = ResolutionTests.CreateCache(@"module modA;
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
			var ctxt = ResolutionContext.Create(pcl, null, pcl[0]["modA"]);

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

		[Test]
		public void TestTemplateDeductionAsConversion()
		{
			var pcl = ResolutionTests.CreateCache(@"module modA;
void foo(T:T)(T[] t) {}

int[] p=[1,2,3,4,5];
");
			var ctxt = ResolutionContext.Create(pcl, null, pcl[0]["modA"]);

			var foo = pcl[0]["modA"]["foo"][0] as DMethod;
			ctxt.PushNewScope(foo);
			var foo_firstArg= TypeDeclarationResolver.Resolve(foo.Parameters[0].Type, ctxt);
			
			var p = TypeDeclarationResolver.ResolveIdentifier("p", ctxt, null)[0] as MemberSymbol;
			
			Assert.IsTrue(ResultComparer.IsImplicitlyConvertible(p,foo_firstArg[0], ctxt));
			ctxt.Pop();
		}
	}
}
