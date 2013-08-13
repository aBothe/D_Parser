using System;
using System.Collections.Generic;
using System.Linq;

using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Resolver.ExpressionSemantics.CTFE;
using D_Parser.Resolver.Templates;
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
		ISemantic TryDoCTFEOrGetValueRefs(AbstractType[] overloads, IExpression idOrTemplateInstance, bool ImplicitlyExecute = true, ISymbolValue[] executionArguments=null)
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

		ISemantic E(TemplateInstanceExpression tix, bool ImplicitlyExecute = true)
		{
			var o = DResolver.StripAliasSymbols(GetOverloads(tix, ctxt));

			if (eval)
				return TryDoCTFEOrGetValueRefs(o, tix, ImplicitlyExecute);
			else
			{
				ctxt.CheckForSingleResult(o, tix);
				if (o != null)
					if (o.Length == 1)
						return o[0];
					else if (o.Length > 1)
						return new InternalOverloadValue(o);
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
					{
						EvalError(id, "Symbol could not be found");
						return null;
					}

					return TryDoCTFEOrGetValueRefs(o, id, ImplicitlyExecute);
				}
				else
				{
					ctxt.CheckForSingleResult(o, id);
					if (o != null)
						if (o.Length == 1)
							return o[0];
						else if (o.Length > 1)
							return new InternalOverloadValue(o);
					return null;
				}
			}
			else
				return EvaluateLiteral(id);
		}

		ISemantic EvaluateLiteral(IdentifierExpression id)
		{
			byte tt = 0;

			switch (id.Format)
			{
				case Parser.LiteralFormat.CharLiteral:
					var tk = id.Subformat == LiteralSubformat.Utf32 ? DTokens.Dchar :
						id.Subformat == LiteralSubformat.Utf16 ? DTokens.Wchar :
						DTokens.Char;

					if (eval)
						return new PrimitiveValue(tk, Convert.ToDecimal((int)(char)id.Value), id);
					else
						return new PrimitiveType(tk, 0, id);

				case LiteralFormat.FloatingPoint | LiteralFormat.Scalar:
					var im = id.Subformat.HasFlag(LiteralSubformat.Imaginary);

					tt = im ? DTokens.Idouble : DTokens.Double;

					if (id.Subformat.HasFlag(LiteralSubformat.Float))
						tt = im ? DTokens.Ifloat : DTokens.Float;
					else if (id.Subformat.HasFlag(LiteralSubformat.Real))
						tt = im ? DTokens.Ireal : DTokens.Real;

					var v = Convert.ToDecimal(id.Value);

					if (eval)
						return new PrimitiveValue(tt, im ? 0 : v, id, im? v : 0);
					else
						return new PrimitiveType(tt, 0, id);

				case LiteralFormat.Scalar:
					var unsigned = id.Subformat.HasFlag(LiteralSubformat.Unsigned);

					if (id.Subformat.HasFlag(LiteralSubformat.Long))
						tt = unsigned ? DTokens.Ulong : DTokens.Long;
					else
						tt = unsigned ? DTokens.Uint : DTokens.Int;

					return eval ? (ISemantic)new PrimitiveValue(tt, Convert.ToDecimal(id.Value), id) : new PrimitiveType(tt, 0, id);

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

		public static AbstractType[] GetOverloads(TemplateInstanceExpression tix, ResolutionContext ctxt, IEnumerable<AbstractType> resultBases = null, bool deduceParameters = true)
		{
			if(resultBases == null && tix.InnerDeclaration != null)
				resultBases = TypeDeclarationResolver.Resolve(tix.InnerDeclaration, ctxt);
			
			AbstractType[] res;
			if (resultBases == null)
				res = TypeDeclarationResolver.ResolveIdentifier(tix.TemplateIdHash, ctxt, tix, tix.ModuleScopedIdentifier);
			else
				res = TypeDeclarationResolver.ResolveFurtherTypeIdentifier(tix.TemplateIdHash, resultBases, ctxt, tix);

			return (ctxt.Options & ResolutionOptions.NoTemplateParameterDeduction) == 0 && deduceParameters ?
				TemplateInstanceHandler.DeduceParamsAndFilterOverloads(res, tix, ctxt) : res;
		}

		public AbstractType[] GetOverloads(IdentifierExpression id, bool deduceParameters = true)
		{
			return GetOverloads(id, ctxt, deduceParameters);
		}

		public static AbstractType[] GetOverloads(IdentifierExpression id, ResolutionContext ctxt, bool deduceParameters = true)
		{
			var raw=TypeDeclarationResolver.ResolveIdentifier(id.ValueStringHash, ctxt, id, id.ModuleScoped);
			var f = DResolver.FilterOutByResultPriority(ctxt, raw);
			
			if(f==null)
				return null;

			return (ctxt.Options & ResolutionOptions.NoTemplateParameterDeduction) == 0 && deduceParameters ?
				TemplateInstanceHandler.DeduceParamsAndFilterOverloads(f, null, false, ctxt) : 
				f.ToArray();
		}
	}
}
