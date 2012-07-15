using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Parser;
using D_Parser.Dom;
using D_Parser.Evaluation;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public partial class Evaluation
	{
		ISemantic EvaluateIdOrTemplateInstance(IExpression x)
		{
			
		}

		ISemantic E(IdentifierExpression id)
		{
			int tt = 0;

			if (id.IsIdentifier)
				return TypeDeclarationResolver.ResolveSingle(id.Value as string, ctxt, id, id.ModuleScoped) as AbstractType;

			switch (id.Format)
			{
				case Parser.LiteralFormat.CharLiteral:
					var tk = id.Subformat == LiteralSubformat.Utf32 ? DTokens.Dchar :
						id.Subformat == LiteralSubformat.Utf16 ? DTokens.Wchar :
						DTokens.Char;

					if (eval)
						return new PrimitiveValue(tk, id.Value, id);
					else
						return new PrimitiveType(tk, 0, id);

				case LiteralFormat.FloatingPoint | LiteralFormat.Scalar:
					var im = id.Subformat.HasFlag(LiteralSubformat.Imaginary);

					tt = im ? DTokens.Idouble : DTokens.Double;

					if (id.Subformat.HasFlag(LiteralSubformat.Float))
						tt = im ? DTokens.Ifloat : DTokens.Float;
					else if (id.Subformat.HasFlag(LiteralSubformat.Real))
						tt = im ? DTokens.Ireal : DTokens.Real;

					if (eval)
						return new PrimitiveValue(tt, id.Value, id);
					else
						return new PrimitiveType(tt, 0, id);

				case LiteralFormat.Scalar:
					var unsigned = id.Subformat.HasFlag(LiteralSubformat.Unsigned);

					if (id.Subformat.HasFlag(LiteralSubformat.Long))
						tt = unsigned ? DTokens.Ulong : DTokens.Long;
					else
						tt = unsigned ? DTokens.Uint : DTokens.Int;

					return eval ? (ISemantic)new PrimitiveValue(tt, id.Value, id) : new PrimitiveType(tt, 0, id);

				case Parser.LiteralFormat.StringLiteral:
				case Parser.LiteralFormat.VerbatimStringLiteral:

					var _t = GetStringType(id.Subformat);
					return eval ? (ISemantic)new ArrayValue(_t, id) : _t;
			}
			return null;
		}

		public static AbstractType[] GetOverloads(TemplateInstanceExpression tix, ResolverContextStack ctxt, IEnumerable<AbstractType> resultBases = null, bool deduceParameters = true)
		{
			AbstractType[] res = null;
			if (resultBases == null)
				res = TypeDeclarationResolver.Convert(TypeDeclarationResolver.ResolveIdentifier(tix.TemplateIdentifier.Id, ctxt, tix, tix.TemplateIdentifier.ModuleScoped));
			else
				res = TypeDeclarationResolver.ResolveFurtherTypeIdentifier(tix.TemplateIdentifier.Id, resultBases, ctxt, tix);

			return !ctxt.Options.HasFlag(ResolutionOptions.NoTemplateParameterDeduction) && deduceParameters ?
				TemplateInstanceHandler.EvalAndFilterOverloads(res, tix, ctxt) : res;
		}

		public static ISemantic[] GetOverloads(IdentifierExpression id, ResolverContextStack ctxt)
		{
			return TypeDeclarationResolver.ResolveIdentifier(id.Value as string, ctxt, id, id.ModuleScoped);
		}
	}
}
