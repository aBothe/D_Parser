using D_Parser.Dom;
using D_Parser.Evaluation;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Resolver.Templates
{
	partial class TemplateParameterDeduction
	{
		public bool Handle(TemplateValueParameter p, ResolveResult arg)
		{
			//TODO: Introduce previously deduced parameters into current resolution context somehow 
			// -- So add some further name->ResolveResult[] associations to the ResolverContext object

			// Handle default arg case
			if (arg == null)
			{
				if (p.DefaultExpression != null)
				{
					Set(p.Name, new ExpressionValueResult
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
