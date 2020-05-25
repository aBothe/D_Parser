using D_Parser.Dom;
using D_Parser.Misc.Mangling;
using D_Parser.Resolver;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tests.Resolution;

namespace Tests.Misc
{
	[TestClass]
	public class DemanglerTests
	{
		[TestMethod]
		public void Demangling_writeln()
		{
			ITypeDeclaration q;
			var ctxt = ResolutionTests.CreateCtxt ("std.stdio", @"module std.stdio;
			void writeln() {}");
			bool isCFun;
			var t = Demangler.Demangle("_D3std5stdio35__T7writelnTC3std6stream4FileTAAyaZ7writelnFC3std6stream4FileAAyaZv", ctxt, out q, out isCFun);

			Assert.IsFalse (isCFun);
		}
	}
}