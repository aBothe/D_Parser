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
			Assert.That(signature, Is.EqualTo(expected));
		}
		[Test]
		public void TooltipGeneration_Modifiers()
		{
			var tooltipGen = new D_Parser.Completion.ToolTips.NodeTooltipRepresentationGen();

			var ctxt = ResolutionTestHelper.CreateDefCtxt(@"module A;
static private const double eps;");

			var eps = ResolutionTestHelper.N<DVariable>(ctxt, "A.eps");

			var signature = tooltipGen.GenTooltipSignature(eps, false, 1);
			Assert.That(signature, Is.EqualTo(@"private static const(double) A.eps"));
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
			Assert.That(summary, Is.EqualTo(@"Does magic."));
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
			Assert.That(categories.Count, Is.EqualTo(2));
			Assert.That(categories.ContainsKey("Returns"));
			Assert.That(categories.ContainsKey("Params"));
		}
    }
}