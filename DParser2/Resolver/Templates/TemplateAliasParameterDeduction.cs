using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Dom.Expressions;

namespace D_Parser.Resolver.Templates
{
	partial class TemplateParameterDeduction
	{
		bool Handle(TemplateAliasParameter p, ResolveResult arg)
		{
			#region Handle parameter defaults
			if (arg == null)
			{
				if (p.DefaultExpression != null)
				{
					var eval = Evaluation.ExpressionEvaluator.Evaluate(p.DefaultExpression, ctxt);

					if (eval == null)
						return false;

					return Set(p.Name, new ExpressionValueResult {
						DeclarationOrExpressionBase=p.DefaultExpression,
						Value=eval
					});
				}
				else if (p.DefaultType != null)
				{
					var res = TypeDeclarationResolver.Resolve(p.DefaultType, ctxt);

					if (res == null)
						return false;

					bool ret = false;
					foreach(var r in res)
						if (!Set(p.Name, r))
						{
							ret = true;
						}

					if (ret)
						return false;
				}
				return false;
			}
			#endregion

			#region Given argument must be a symbol - so no built-in type but a reference to a node or an expression
			var _t=DResolver.TryRemoveAliasesFromResult(new[]{arg});

			if (_t == null)
				return false;
			var arg_NoAlias = _t.First();

			while (arg_NoAlias != null)
			{
				if (arg_NoAlias is StaticTypeResult)
					return false;

				arg_NoAlias = arg_NoAlias.ResultBase;
			}
			#endregion

			#region Specialization check
			if (p.SpecializationExpression != null)
			{

			}
			else if (p.SpecializationType != null)
			{

			}
			#endregion

			return Set(p.Name,arg);
		}
	}
}
