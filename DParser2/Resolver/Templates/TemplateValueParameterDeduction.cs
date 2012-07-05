using D_Parser.Dom;
using D_Parser.Evaluation;
using D_Parser.Resolver.TypeResolution;
using System.Collections.Generic;

namespace D_Parser.Resolver.Templates
{
	partial class TemplateParameterDeduction
	{
		bool Handle(TemplateValueParameter p, ISemantic arg)
		{
			// Handle default arg case
			if (arg == null)
			{
				if (p.DefaultExpression != null)
				{
					var eval = ExpressionEvaluator.Resolve(p.DefaultExpression, ctxt);

					if (eval == null)
						return false;

					return Set(p.Name, eval);
				}
				else
					return false;
			}

			var valueArgument = arg as ISymbolValue;

			// There must be a constant expression given!
			if (valueArgument == null)
				return false;

			// Check for param type <-> arg expression type match
			var paramType = TypeDeclarationResolver.Resolve(p.Type, ctxt);

			if (paramType == null || paramType.Length == 0)
				return false;

			if (valueArgument.RepresentedType == null ||
				!ResultComparer.IsImplicitlyConvertible(paramType[0], valueArgument.RepresentedType))
				return false;

			// If spec given, test for equality (only ?)
			if (p.SpecializationExpression != null) 
			{
				var specVal = ExpressionEvaluator.Evaluate(p.SpecializationExpression, new StandardValueProvider(ctxt));

				if (specVal == null || !SymbolValueComparer.IsEqual(specVal, valueArgument))
					return false;
			}

			return Set(p.Name, arg);
		}
	}
}
