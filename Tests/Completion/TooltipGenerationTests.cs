using D_Parser.Dom;
using D_Parser.Resolver;
using NUnit.Framework;

namespace Tests.Completion
{
    public class TooltipGenerationTests
    {
        [Test]
		public void TooltipGeneration_MethodSignature()
		{
			var tooltipGen = new D_Parser.Completion.ToolTips.NodeTooltipRepresentationGen();

			var ctxt = ResolutionTestHelper.CreateDefCtxt(@"module A;
void foo(int a, string b) {}");

			var foo = ResolutionTestHelper.N<DMethod>(ctxt, "A.foo");

			var fooSymbol = new MemberSymbol(foo);

			var signature = tooltipGen.GenTooltipSignature(fooSymbol, false, 1);
			signature = signature.Replace("\r\n", "\n");
			var expected = @"void A.foo(
  int a,
  <span underline='single'>string b</span>
)".Replace("\r\n", "\n");
			Assert.AreEqual(expected, signature);
		}
		[Test]
		public void TooltipGeneration_Modifiers()
		{
			var tooltipGen = new D_Parser.Completion.ToolTips.NodeTooltipRepresentationGen();

			var ctxt = ResolutionTestHelper.CreateDefCtxt(@"module A;
static private const double eps;");

			var eps = ResolutionTestHelper.N<DVariable>(ctxt, "A.eps");

			var signature = tooltipGen.GenTooltipSignature(eps, false, 1);
			Assert.AreEqual(@"private static const(double) A.eps", signature);
		}


		[Test]
		public void TooltipGeneration_MethodDDoc_SimpleSummary()
		{
			var tooltipGen = new D_Parser.Completion.ToolTips.NodeTooltipRepresentationGen();

			var ctxt = ResolutionTestHelper.CreateDefCtxt(@"module A;
/// Does magic.
void foo(int a, string b) {}");

			var foo = ResolutionTestHelper.N<DMethod>(ctxt, "A.foo");

			tooltipGen.GenToolTipBody(foo, out var summary, out var categories);
			Assert.AreEqual(@"Does magic.", summary);
		}

		[Test]
		public void TooltipGeneration_MethodDDoc_Categories()
		{
			var tooltipGen = new D_Parser.Completion.ToolTips.NodeTooltipRepresentationGen();

			var ctxt = ResolutionTestHelper.CreateDefCtxt(@"module A;
/**
 * Read the file.
 * Returns: The contents of the file.
 *
 * License: xyz
 *
 * Params:
 *      x =     is for this
 *              and not for that
 *      y =     is for that
 */
void foo(int a, string b) {}");

			var foo = ResolutionTestHelper.N<DMethod>(ctxt, "A.foo");

			tooltipGen.GenToolTipBody(foo, out var summary, out var categories);
			Assert.AreEqual(2, categories.Count);
			Assert.IsTrue(categories.ContainsKey("Returns"));
			Assert.IsTrue(categories.ContainsKey("Params"));
		}
    }
}