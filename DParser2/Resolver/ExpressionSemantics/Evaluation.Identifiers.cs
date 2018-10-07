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
		class CTFEOrValueRefsVisitor : IResolvedTypeVisitor<ISymbolValue>
		{
			IExpression idOrTemplateInstance;
			IEnumerable<ISymbolValue> executionArguments;
			AbstractSymbolValueProvider ValueProvider;

			public CTFEOrValueRefsVisitor(AbstractSymbolValueProvider vp,IExpression idOrTemplateInstance, IEnumerable<ISymbolValue> executionArguments = null)
			{
				ValueProvider = vp;
				this.idOrTemplateInstance = idOrTemplateInstance;
				this.executionArguments = executionArguments;
			}

			public ISymbolValue VisitPrimitiveType(PrimitiveType pt)
			{
				return new TypeValue(pt);
			}

			public ISymbolValue VisitPointerType(PointerType pt)
			{
				return new TypeValue(pt);
			}

			public ISymbolValue VisitArrayType(ArrayType at)
			{
				return new TypeValue(at);
			}

			public ISymbolValue VisitAssocArrayType(AssocArrayType aa)
			{
				return new TypeValue(aa);
			}

			public ISymbolValue VisitDelegateCallSymbol(DelegateCallSymbol dg)
			{
				return new TypeValue(dg);
			}

			public ISymbolValue VisitDelegateType(DelegateType dg)
			{
				return new TypeValue(dg);
			}

			public ISymbolValue VisitAliasedType(AliasedType at)
			{
				return new TypeValue(at); // ?
			}

			public ISymbolValue VisitEnumType(EnumType t)
			{
				return new TypeValue(t);
			}

			public ISymbolValue VisitStructType(StructType t)
			{
				return new TypeValue(t);
			}

			public ISymbolValue VisitUnionType(UnionType t)
			{
				return new TypeValue(t);
			}

			public ISymbolValue VisitClassType(ClassType t)
			{
				return new TypeValue(t);
			}

			public ISymbolValue VisitInterfaceType(InterfaceType t)
			{
				return new TypeValue(t);
			}

			public ISymbolValue VisitTemplateType(TemplateType t)
			{
				return new TypeValue(t);
			}

			public ISymbolValue VisitMixinTemplateType(MixinTemplateType t)
			{
				return new TypeValue(t);
			}

			public ISymbolValue VisitEponymousTemplateType(EponymousTemplateType t)
			{
				return new TypeValue(t);
			}

			public ISymbolValue VisitStaticProperty(StaticProperty p)
			{
				return VisitMemberSymbol(p);
			}

			public ISymbolValue VisitMemberSymbol(MemberSymbol mr)
			{
				if (mr.Definition is DVariable)
					return new VariableValue(mr);

				// If we've got a function here, execute it
				if (mr.Definition is DMethod)
				{
					Dictionary<DVariable, ISymbolValue> targetArgs;
					if(!FunctionEvaluation.AssignCallArgumentsToIC(mr, executionArguments, ValueProvider, out targetArgs))
						return null;

					return FunctionEvaluation.Execute(mr, targetArgs, ValueProvider);
				}

				// Are there other types to execute/handle?
				return null;
			}

			public ISymbolValue VisitTemplateParameterSymbol(TemplateParameterSymbol tps)
			{
				if ((tps.Parameter is TemplateTypeParameter ||
					tps.Parameter is TemplateAliasParameter))
					return new TypeValue(tps.Base ?? tps);
				if (tps.Parameter is TemplateValueParameter)
					return tps.ParameterValue;
				if (tps.Parameter is TemplateTupleParameter)
					return new TypeValue(tps.Base);
				//TODO: Are there other evaluable template parameters?
				return null;
			}

			public ISymbolValue VisitArrayAccessSymbol(ArrayAccessSymbol tps)
			{
				// correct?
				return tps.Base != null ? tps.Base.Accept(this) : null;
			}

			public ISymbolValue VisitModuleSymbol(ModuleSymbol t)
			{
				return new TypeValue(t);
			}

			public ISymbolValue VisitPackageSymbol(PackageSymbol t)
			{
				return new TypeValue(t);
			}

			public ISymbolValue VisitDTuple(DTuple t)
			{
				return new TypeValue(t);
			}

			public ISymbolValue VisitUnknownType(UnknownType t)
			{
				return null;
			}

			public ISymbolValue VisitAmbigousType(AmbiguousType t)
			{
				var results = new List<ISymbolValue>(t.Overloads.Length);

				foreach (var o in t.Overloads)
				{
					var newValue = o.Accept(this);
					if (newValue != null)
						results.Add(newValue);
				}

				results.TrimExcess();
				return results.Count > 1 ? new InternalOverloadValue(results) : results.Count > 0 ? results[0] : null;
			}
		}

		/// <summary>
		/// Evaluates the identifier/template instance as usual.
		/// If the id points to a variable, the initializer/dynamic value will be evaluated using its initializer.
		/// </summary>
		ISymbolValue TryDoCTFEOrGetValueRefs(AbstractType r, IExpression idOrTemplateInstance, IEnumerable<ISymbolValue> executionArguments=null)
		{
			return r?.Accept(new CTFEOrValueRefsVisitor(ValueProvider, idOrTemplateInstance, executionArguments)) : null;
		}

		public ISymbolValue Visit(TemplateInstanceExpression tix)
		{
			return TryDoCTFEOrGetValueRefs(AmbiguousType.Get(ExpressionTypeEvaluation.GetOverloads(tix, ctxt)), tix);
		}

		public ISymbolValue Visit(IdentifierExpression id)
		{
			var o = ExpressionTypeEvaluation.EvaluateType(id, ctxt, false);

			if (o == null)
			{
				EvalError(id, "Symbol could not be found");
				return null;
			}

			return TryDoCTFEOrGetValueRefs(o, id);
		}

		public ISymbolValue VisitScalarConstantExpression(ScalarConstantExpression id)
		{
			byte tt;
			switch (id.Format)
			{
				case Parser.LiteralFormat.CharLiteral:
					var tk = id.Subformat == LiteralSubformat.Utf32 ? DTokens.Dchar :
						id.Subformat == LiteralSubformat.Utf16 ? DTokens.Wchar :
						DTokens.Char;

					return new PrimitiveValue(tk, Convert.ToDecimal((int)(char)id.Value));

				case LiteralFormat.FloatingPoint | LiteralFormat.Scalar:
					var im = id.Subformat.HasFlag(LiteralSubformat.Imaginary);

					tt = im ? DTokens.Idouble : DTokens.Double;

					if (id.Subformat.HasFlag(LiteralSubformat.Float))
						tt = im ? DTokens.Ifloat : DTokens.Float;
					else if (id.Subformat.HasFlag(LiteralSubformat.Real))
						tt = im ? DTokens.Ireal : DTokens.Real;

					var v = Convert.ToDecimal(id.Value);

					return new PrimitiveValue(tt, im ? 0 : v, im ? v : 0);

				case LiteralFormat.Scalar:
					var unsigned = id.Subformat.HasFlag(LiteralSubformat.Unsigned);

					if (id.Subformat.HasFlag(LiteralSubformat.Long))
						tt = unsigned ? DTokens.Ulong : DTokens.Long;
					else
						tt = unsigned ? DTokens.Uint : DTokens.Int;

					return new PrimitiveValue(tt, Convert.ToDecimal(id.Value));

				default:
					return null;
			}
		}
	}
}
