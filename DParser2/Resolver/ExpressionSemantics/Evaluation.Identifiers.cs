using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Resolver.ExpressionSemantics.CTFE;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public partial class Evaluation
	{
		/// <summary>
		/// Evaluates the identifier/template instance as usual.
		/// If the id points to a variable, the initializer/dynamic value will be evaluated using the 
		/// </summary>
		ISemantic TryEvaluateInitializerOrDoCTFE(AbstractType[] overloads, IExpression idOrTemplateInstance, bool ImplicitlyExecute = true)
		{
			if (overloads == null || overloads.Length == 0)
				throw new EvaluationException(idOrTemplateInstance, "No symbols found");

			var r = overloads[0];
			var ex = new EvaluationException(idOrTemplateInstance, "Ambiguous expression", overloads);

			if (r is MemberSymbol)
			{
				var mr = (MemberSymbol)r;

				// If we've got a function here, execute it
				if (mr.Definition is DMethod)
				{
					if (ImplicitlyExecute)
					{
						if (overloads.Length > 1)
							throw ex;
						return FunctionEvaluation.Execute((DMethod)mr.Definition, null, ValueProvider);
					}
					else
						return new InternalOverloadValue(overloads, idOrTemplateInstance);
				}
				else if (mr.Definition is DVariable)
				{
					if (overloads.Length > 1)
						throw ex;
					return ValueProvider[(DVariable)mr.Definition];
				}
			}
			else if (r is UserDefinedType)
			{
				if (overloads.Length > 1)
					throw ex;
				return new TypeValue(r, idOrTemplateInstance);
			}

			return null;
		}

		ISemantic E(TemplateInstanceExpression tix, bool ImplicitlyExecute = true)
		{
			var o = GetOverloads(tix, ctxt);

			if (eval)
				return TryEvaluateInitializerOrDoCTFE(o, tix, ImplicitlyExecute);
			else
			{
				ctxt.CheckForSingleResult(o, tix);
				if (o != null && o.Length != 0)
					return o[0];
				return null;
			}
		}

		ISemantic E(IdentifierExpression id, bool ImplicitlyExecute = true)
		{
			if (id.IsIdentifier)
			{
				var o = GetOverloads(id, ctxt);

				if (eval)
				{
					if (o == null || o.Length == 0)
						return ValueProvider[((IdentifierExpression)id).Value as string];

					return TryEvaluateInitializerOrDoCTFE(o, id, ImplicitlyExecute);
				}
				else
				{
					ctxt.CheckForSingleResult(o, id);
					if (o != null && o.Length != 0)
						return o[0];
					return null;
				}
			}
			else
				return EvaluateLiteral(id);
		}

		ISemantic EvaluateLiteral(IdentifierExpression id)
		{
			int tt = 0;

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




		public AbstractType[] GetOverloads(TemplateInstanceExpression tix, IEnumerable<AbstractType> resultBases = null, bool deduceParameters = true)
		{
			return GetOverloads(tix, ctxt, resultBases, deduceParameters);
		}

		public static AbstractType[] GetOverloads(TemplateInstanceExpression tix, ResolverContextStack ctxt, IEnumerable<AbstractType> resultBases = null, bool deduceParameters = true)
		{
			AbstractType[] res = null;
			if (resultBases == null)
				res = TypeDeclarationResolver.ResolveIdentifier(tix.TemplateIdentifier.Id, ctxt, tix, tix.TemplateIdentifier.ModuleScoped);
			else
				res = TypeDeclarationResolver.ResolveFurtherTypeIdentifier(tix.TemplateIdentifier.Id, resultBases, ctxt, tix);

			return !ctxt.Options.HasFlag(ResolutionOptions.NoTemplateParameterDeduction) && deduceParameters ?
				TemplateInstanceHandler.EvalAndFilterOverloads(res, tix, ctxt) : res;
		}

		public AbstractType[] GetOverloads(IdentifierExpression id)
		{
			return GetOverloads(id, ctxt);
		}

		public static AbstractType[] GetOverloads(IdentifierExpression id, ResolverContextStack ctxt)
		{
			return TypeDeclarationResolver.ResolveIdentifier(id.Value as string, ctxt, id, id.ModuleScoped);
		}
	}
}
