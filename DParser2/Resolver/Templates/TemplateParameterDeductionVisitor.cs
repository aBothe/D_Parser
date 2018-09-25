using System;
using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Resolver.Templates
{
	class TemplateParameterDeductionVisitor : ITemplateParameterVisitor<bool, ISemantic>
	{
		public readonly ResolutionContext ctxt;
		public readonly DeducedTypeDictionary TargetDictionary;

		/// <summary>
		/// If true and deducing a type parameter,
		/// the equality of the given and expected type is required instead of their simple convertibility.
		/// Used when evaluating IsExpressions.
		/// </summary>
		public bool EnforceTypeEqualityWhenDeducing
		{
			get;
			set;
		}

		[System.Diagnostics.DebuggerStepThrough]
		public TemplateParameterDeductionVisitor(ResolutionContext ctxt, DeducedTypeDictionary TargetDictionary)
		{
			this.ctxt = ctxt;
			this.TargetDictionary = TargetDictionary;
		}

		/// <summary>
		/// Returns false if the item has already been set before and if the already set item is not equal to 'r'.
		/// Inserts 'r' into the target dictionary and returns true otherwise.
		/// </summary>
		public bool Set(TemplateParameter p, ISemantic r, int nameHash)
		{
			return Set(ctxt, TargetDictionary, p, r, nameHash);
		}

		public static bool Set(ResolutionContext ctxt, DeducedTypeDictionary TargetDictionary,TemplateParameter p, ISemantic r, int nameHash)
		{
			if (p == null)
			{
				if (nameHash != 0 && TargetDictionary.ExpectedParameters != null)
				{
					foreach (var tpar in TargetDictionary.ExpectedParameters)
						if (tpar.NameHash == nameHash)
						{
							p = tpar;
							break;
						}
				}
			}

			if (p == null)
			{
				ctxt.LogError(null, "no fitting template parameter found!");
				return false;
			}

			// void call(T)(T t) {}
			// call(myA) -- T is *not* myA but A, so only assign myA's type to T. 
			if (p is TemplateTypeParameter)
			{
				var newR = Resolver.TypeResolution.DResolver.StripMemberSymbols(AbstractType.Get(r));
				if (newR != null)
					r = newR;
			}

			TemplateParameterSymbol rl;
			if (!TargetDictionary.TryGetValue(p, out rl) || rl == null)
			{
				TargetDictionary[p] = new TemplateParameterSymbol(p, r);
				return true;
			}
			else
			{
				if (ResultComparer.IsEqual(rl.Base, r))
				{
					TargetDictionary[p] = new TemplateParameterSymbol(p, r);
					return true;
				}

				// Error: Ambiguous assignment

				return false;
			}
		}

		public bool Visit(TemplateTypeParameter p, ISemantic arg)
		{
			// if no argument given, try to handle default arguments
			if (arg == null)
				return TryAssignDefaultType(p);

			// If no spezialization given, assign argument immediately
			if (p.Specialization == null)
				return Set(p, arg, 0);

			if(!TemplateTypeParameterTypeMatcher.TryMatchTypeDeclAgainstResolvedResult(p.Specialization, arg, ctxt, TargetDictionary, EnforceTypeEqualityWhenDeducing))
				return false;

			// Apply the entire argument to parameter p if there hasn't been no explicit association yet
			TemplateParameterSymbol tps;
			if (!TargetDictionary.TryGetValue(p, out tps) || tps == null)
				TargetDictionary[p] = new TemplateParameterSymbol(p, arg);

			return true;
		}

		bool TryAssignDefaultType(TemplateTypeParameter p)
		{
			if (p == null || p.Default == null)
				return false;

			using (ctxt.Push(DResolver.SearchBlockAt(ctxt.ScopedBlock.NodeRoot as IBlockNode, p.Default.Location), p.Default.Location))
			{
				var defaultTypeRes = TypeDeclarationResolver.ResolveSingle(p.Default, ctxt);
				return defaultTypeRes != null && Set(p, defaultTypeRes, 0);
			}
		}

		public bool Visit(TemplateThisParameter tp, ISemantic parameter)
		{
			// Only special handling required for method calls
			return tp.FollowParameter != null && tp.FollowParameter.Accept(this, parameter);
		}

		public bool Visit(TemplateValueParameter p, ISemantic arg)
		{
			// Handle default arg case
			if (arg == null)
			{
				if (p.DefaultExpression != null)
				{
					var eval = Evaluation.EvaluateValue(p.DefaultExpression, ctxt);

					if (eval == null)
						return false;

					return Set(p, eval, 0);
				}
				else
					return false;
			}

			var valueArgument = arg as ISymbolValue;

			// There must be a constant expression given!
			if (valueArgument == null)
				return false;

			// Check for param type <-> arg expression type match
			var paramType = TypeDeclarationResolver.ResolveSingle(p.Type, ctxt);

			if (paramType == null ||
				valueArgument.RepresentedType == null ||
				!ResultComparer.IsImplicitlyConvertible(paramType, valueArgument.RepresentedType))
				return false;

			// If spec given, test for equality (only ?)
			if (p.SpecializationExpression != null)
			{
				var specVal = Evaluation.EvaluateValue(p.SpecializationExpression, ctxt);

				if (specVal == null || !SymbolValueComparer.IsEqual(specVal, valueArgument))
					return false;
			}

			return Set(p, arg, 0);
		}

		public bool Visit(TemplateAliasParameter p, ISemantic arg)
		{
			#region Handle parameter defaults
			if (arg == null)
			{
				if (p.DefaultExpression != null)
				{
					var eval = Evaluation.EvaluateValue(p.DefaultExpression, ctxt);

					if (eval == null)
						return false;

					return Set(p, eval, 0);
				}
				else if (p.DefaultType != null)
				{
					var res = TypeDeclarationResolver.ResolveSingle(p.DefaultType, ctxt);

					return res != null && Set(p, res, 0);
				}
				return false;
			}
			#endregion

			#region Given argument must be a symbol - so no built-in type but a reference to a node or an expression
			var t = AbstractType.Get(arg);

			if (t == null)
				return false;

			if (!(t is DSymbol))
				while (t != null)
				{
					if (t is PrimitiveType) // arg must not base on a primitive type.
						return false;

					if (t is DerivedDataType)
						t = ((DerivedDataType)t).Base;
					else
						break;
				}
			#endregion

			#region Specialization check
			if (p.SpecializationExpression != null)
			{
				// LANGUAGE ISSUE: Can't do anything here - dmd won't let you use MyClass!(2) though you have class MyClass(alias X:2)
				return false;
			}
			else if (p.SpecializationType != null)
			{
				// ditto
				return false;
			}
			#endregion

			return Set(p, arg, 0);
		}

		public bool Visit(TemplateTupleParameter tp, ISemantic parameter)
		{
			var l = new List<ISemantic>();

			if (parameter is DTuple)
				foreach (var arg in (parameter as DTuple).Items)
					if (arg is DTuple) // If a type tuple was given already, add its items instead of the tuple itself
					{
						var tt = arg as DTuple;
						if (tt.Items != null)
							l.AddRange(tt.Items);
					}
					else
						l.Add(arg);
			else if (parameter != null)
				l.Add(parameter);

			return Set(tp, new DTuple(l.Count == 0 ? null : l), 0);
		}

		public bool VisitTemplateParameter(TemplateParameter tp, ISemantic parameter)
		{
			throw new NotImplementedException();
		}
	}
}
