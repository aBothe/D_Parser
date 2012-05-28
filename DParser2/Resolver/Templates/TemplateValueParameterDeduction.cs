using D_Parser.Dom;
using D_Parser.Evaluation;
using D_Parser.Resolver.TypeResolution;
using System.Collections.Generic;

namespace D_Parser.Resolver.Templates
{
	partial class TemplateParameterDeduction
	{
		public bool HandleWithAlreadyDeductedParamIntroduction(ITemplateParameter p, ResolveResult arg)
		{
			//TODO: Handle __FILE__ and __LINE__ correctly - so don't evaluate them at the template declaration but at the point of instantiation

			/*
			 * Introduce previously deduced parameters into current resolution context
			 * to allow value parameter to be of e.g. type T whereas T is already set somewhere before 
			 */
			var _prefLocalsBackup = ctxt.CurrentContext.PreferredLocals;

			var d = new Dictionary<string, ResolveResult[]>();
			foreach (var kv in TargetDictionary)
				if (kv.Value != null && kv.Value.Length != 0)
					d[kv.Key] = kv.Value;
			ctxt.CurrentContext.PreferredLocals = d;

			bool res=false;

			if (p is TemplateAliasParameter) // Test alias ones before value ones due to type hierarchy
				res = Handle((TemplateAliasParameter)p, arg);
			else if (p is TemplateValueParameter)
				res = Handle((TemplateValueParameter)p, arg);

			ctxt.CurrentContext.PreferredLocals = _prefLocalsBackup;

			return res;
		}

		bool Handle(TemplateValueParameter p, ResolveResult arg)
		{
			// Handle default arg case
			if (arg == null)
			{
				if (p.DefaultExpression != null)
				{
					var b=Set(p.Name, new ExpressionValueResult
					{
						Value = ExpressionEvaluator.Evaluate(p.DefaultExpression, ctxt),
						DeclarationOrExpressionBase = p.DefaultExpression
					});
					return true;
				}
				else
					return false;
			}

			var valResult = arg as ExpressionValueResult;

			// There must be a constant expression given!
			if (valResult == null || valResult.Value == null)
				return false;

			// Check for param type <-> arg expression type match
			var paramType = TypeDeclarationResolver.Resolve(p.Type, ctxt);

			if (paramType == null || paramType.Length == 0)
				return false;

			var argType = TypeDeclarationResolver.Resolve(valResult.Value.RepresentedType, ctxt);

			if (argType == null ||
				argType.Length == 0 ||
				!ResultComparer.IsImplicitlyConvertible(paramType[0], argType[0]))
				return false;

			// If spec given, test for equality (only ?)
			if (p.SpecializationExpression != null) 
			{
				var specVal = ExpressionEvaluator.Evaluate(p.SpecializationExpression, ctxt);

				if (specVal == null || specVal.Value == null ||
					!ExpressionEvaluator.IsEqual(specVal, valResult.Value))
					return false;
			}

			return Set(p.Name, arg);
		}
	}
}
