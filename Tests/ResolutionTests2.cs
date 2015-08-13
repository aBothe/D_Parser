using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Misc;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.ASTScanner;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.Templates;
using D_Parser.Resolver.TypeResolution;
using NUnit.Framework;
using System.IO;
using D_Parser.Completion;

namespace Tests
{
	public partial class ResolutionTests
	{
		#region Template Parameter-related
		[Test]
		/// <summary>
		/// https://github.com/aBothe/D_Parser/issues/192
		/// </summary>
		public void TemplateValueParameterDefaultSelfRefSO(){
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
		#endregion

	}
}

