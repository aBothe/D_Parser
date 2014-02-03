using System;
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
		/// If the id points to a variable, the initializer/dynamic value will be evaluated using its initializer.
		/// 
		/// If ImplicitlyExecute is false but value evaluation is switched on, an InternalOverloadValue-object will be returned
		/// that keeps all overloads passed via 'overloads'
		/// </summary>
		ISymbolValue TryDoCTFEOrGetValueRefs(AbstractType[] overloads, IExpression idOrTemplateInstance, bool ImplicitlyExecute = true, ISymbolValue[] executionArguments=null)
		{
			if (overloads == null || overloads.Length == 0){
				EvalError(idOrTemplateInstance, "No symbols found");
				return null;
			}

			var r = overloads[0];
			const string ambigousExprMsg = "Ambiguous expression";

			if(r is TemplateParameterSymbol)
			{
				var tps = (TemplateParameterSymbol)r;

				if((tps.Parameter is TemplateTypeParameter ||
					tps.Parameter is TemplateAliasParameter))
					return new TypeValue(tps.Base ?? tps);
				if(tps.Parameter is TemplateValueParameter)
					return tps.ParameterValue;
				if(tps.Parameter is TemplateTupleParameter)
					return new TypeValue(tps.Base);
				//TODO: Are there other evaluable template parameters?
			}
			else if (r is UserDefinedType || r is PackageSymbol || r is ModuleSymbol || r is AliasedType)
			{
				if (overloads.Length > 1)
				{
					EvalError(idOrTemplateInstance, ambigousExprMsg, overloads);
					return null;
				}
				return new TypeValue(r);
			}
			else if (r is MemberSymbol)
			{
				var mr = (MemberSymbol)r;

				// If we've got a function here, execute it
				if (mr.Definition is DMethod)
				{
					if (ImplicitlyExecute)
					{
						if (overloads.Length > 1){
							EvalError(idOrTemplateInstance, ambigousExprMsg, overloads);
							return null;
						}
						return FunctionEvaluation.Execute(mr, executionArguments, ValueProvider);
					}
					
					return new InternalOverloadValue(overloads);
				}
				else if (mr.Definition is DVariable)
				{
					if (overloads.Length > 1)
					{
						EvalError(idOrTemplateInstance, ambigousExprMsg, overloads);
						return null;
					}
					return new VariableValue(mr);
				}
			}

			EvalError(idOrTemplateInstance, "Could neither execute nor evaluate symbol value", overloads);
			return null;
		}

		bool ImplicitlyExecute = true;

		public ISymbolValue Visit(TemplateInstanceExpression tix)
		{
			var ImplicitlyExecute = this.ImplicitlyExecute;
			this.ImplicitlyExecute = true;

			var o = DResolver.StripAliasSymbols(ExpressionTypeEvaluation.GetOverloads(tix, ctxt));
			
			return TryDoCTFEOrGetValueRefs(o, tix, ImplicitlyExecute);
		}

		public ISymbolValue Visit(IdentifierExpression id)
		{
			var ImplicitlyExecute = this.ImplicitlyExecute;
			this.ImplicitlyExecute = true;

			if (id.IsIdentifier)
			{
				var o = ExpressionTypeEvaluation.GetOverloads(id, ctxt);

				if (o == null || o.Length == 0)
				{
					EvalError(id, "Symbol could not be found");
					return null;
				}

				return TryDoCTFEOrGetValueRefs(o, id, ImplicitlyExecute);
			}

			byte tt;
			switch (id.Format)
			{
				case Parser.LiteralFormat.CharLiteral:
					var tk = id.Subformat == LiteralSubformat.Utf32 ? DTokens.Dchar :
						id.Subformat == LiteralSubformat.Utf16 ? DTokens.Wchar :
						DTokens.Char;

					return new PrimitiveValue(tk, Convert.ToDecimal((int)(char)id.Value), id);

				case LiteralFormat.FloatingPoint | LiteralFormat.Scalar:
					var im = id.Subformat.HasFlag(LiteralSubformat.Imaginary);

					tt = im ? DTokens.Idouble : DTokens.Double;

					if (id.Subformat.HasFlag(LiteralSubformat.Float))
						tt = im ? DTokens.Ifloat : DTokens.Float;
					else if (id.Subformat.HasFlag(LiteralSubformat.Real))
						tt = im ? DTokens.Ireal : DTokens.Real;

					var v = Convert.ToDecimal(id.Value);

					return new PrimitiveValue(tt, im ? 0 : v, id, im ? v : 0);

				case LiteralFormat.Scalar:
					var unsigned = id.Subformat.HasFlag(LiteralSubformat.Unsigned);

					if (id.Subformat.HasFlag(LiteralSubformat.Long))
						tt = unsigned ? DTokens.Ulong : DTokens.Long;
					else
						tt = unsigned ? DTokens.Uint : DTokens.Int;

					return new PrimitiveValue(tt, Convert.ToDecimal(id.Value), id);

				case Parser.LiteralFormat.StringLiteral:
				case Parser.LiteralFormat.VerbatimStringLiteral:
					return new ArrayValue(GetStringType(id.Subformat), id);
				default:
					return null;
			}
		}
	}
}
