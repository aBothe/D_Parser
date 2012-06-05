using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using D_Parser.Unittest;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;

namespace DParser2.Unittest
{
	[TestClass]
	public class ImplicitConversionTests
	{
		[TestMethod]
		public void ClassInheritanceTest()
		{
			var pcl=ResolutionTests.CreateCache(@"module modA;
				class A{}
				class B:A {}
				class C:A {}
				class D:C {}");
			var ctxt=new ResolverContextStack(pcl, new ResolverContext{ ScopedBlock=pcl[0]["modA"] });

			var A = TypeDeclarationResolver.ResolveIdentifier("A", ctxt, null)[0];
			var B = TypeDeclarationResolver.ResolveIdentifier("B", ctxt, null)[0];
			var C = TypeDeclarationResolver.ResolveIdentifier("C", ctxt, null)[0];
			var D = TypeDeclarationResolver.ResolveIdentifier("D", ctxt, null)[0];

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
	}
}
