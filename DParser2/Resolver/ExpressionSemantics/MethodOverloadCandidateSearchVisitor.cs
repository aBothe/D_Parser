using System;
using System.Collections.Generic;
using System.Diagnostics;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Resolver.ExpressionSemantics
{
	class MethodOverloadCandidateSearchVisitor : IResolvedTypeVisitor<IEnumerable<AbstractType>>
	{
		readonly ResolutionContext ctxt;
		readonly AbstractSymbolValueProvider valueProvider;
		readonly PostfixExpression_MethodCall call;

		readonly bool returnBaseTypeOnly;

		public bool returnInstantly = false;
		bool requireStaticItems = true;
		public ISymbolValue delegateValue;

		public MethodOverloadCandidateSearchVisitor (ResolutionContext ctxt,
			AbstractSymbolValueProvider valueProvider,
			PostfixExpression_MethodCall call,
			bool returnBaseTypeOnly)
		{
			this.ctxt = ctxt;
			this.valueProvider = valueProvider;
			this.call = call;
			this.returnBaseTypeOnly = returnBaseTypeOnly;
		}

		public IEnumerable<AbstractType> VisitAliasedType (AliasedType t)
		{
			return HandleInvalidTypes (t);
		}

		public IEnumerable<AbstractType> VisitAmbigousType (AmbiguousType t)
		{
			return t.Overloads;
		}

		public IEnumerable<AbstractType> VisitArrayAccessSymbol (ArrayAccessSymbol t)
		{
			return HandleInvalidTypes (t);
		}

		public IEnumerable<AbstractType> VisitArrayType (ArrayType t)
		{
			return HandleInvalidTypes (t);
		}

		public IEnumerable<AbstractType> VisitAssocArrayType (AssocArrayType t)
		{
			return HandleInvalidTypes (t);
		}

		public IEnumerable<AbstractType> VisitClassType (ClassType t)
		{
			return VisitClassOrStructType (t);
		}

		IEnumerable<AbstractType> VisitClassOrStructType (TemplateIntermediateType tit)
		{
			/*
			 * auto a = MyStruct(); -- opCall-Overloads can be used
			 */
			var classDef = tit.Definition;

			if (classDef == null)
				yield break;

			bool hasMethodOverloadsReturned = false;

			foreach (var i in ExpressionTypeEvaluation.GetOpCalls (tit, requireStaticItems)) {
				hasMethodOverloadsReturned = true;
				yield return TypeDeclarationResolver.HandleNodeMatch (i, ctxt, tit, call) as MemberSymbol;
			}
			/*
			 * Every struct can contain a default ctor:
			 * 
			 * struct S { int a; bool b; }
			 * 
			 * auto s = S(1,true); -- ok
			 * auto s2= new S(2,false); -- error, no constructor found!
			 */
			if (tit is StructType && !hasMethodOverloadsReturned) {
				//TODO: Deduce parameters
				returnInstantly = true;
				yield return tit;
			}
		}

		public IEnumerable<AbstractType> VisitDelegateCallSymbol (DelegateCallSymbol t)
		{
			return HandleInvalidTypes (t);
		}

		public IEnumerable<AbstractType> VisitDelegateType (DelegateType dg)
		{
			ISemantic ret;
			if (returnBaseTypeOnly) {
				if (valueProvider != null)
					ret = new TypeValue (dg.Base);
				else
					ret = dg.Base;

				if (ret is ISymbolValue) {
					delegateValue = ret as ISymbolValue;
					returnInstantly = true;
					yield break;
				}
				if (ret is AbstractType) {
					returnInstantly = true;
					yield return ret as AbstractType;
				}
			} else {
				yield return GetCallDelegateType (dg);
			}
		}

		AbstractType GetCallDelegateType (DelegateType dg)
		{
			/*
			 * int a = delegate(x) { return x*2; } (12); // a is 24 after execution
			 * auto dg=delegate(x) {return x*3;};
			 * int b = dg(4);
			 */

			if (valueProvider == null) {
				return dg;
			}

			// If it's just wanted to pass back the delegate's return type, skip the remaining parts of this method.
			//EvalError(dg.DeclarationOrExpressionBase as IExpression, "TODO", dg);
			valueProvider.LogError (dg.delegateTypeBase, "Ctfe not implemented yet");
			return null;
		}

		public IEnumerable<AbstractType> VisitDTuple (DTuple t)
		{
			return HandleInvalidTypes (t);
		}

		public IEnumerable<AbstractType> VisitEnumType (EnumType t)
		{
			return HandleInvalidTypes (t);
		}

		public IEnumerable<AbstractType> VisitEponymousTemplateType (EponymousTemplateType t)
		{
			return HandleInvalidTypes (t);
		}

		public IEnumerable<AbstractType> VisitInterfaceType (InterfaceType t)
		{
			return HandleInvalidTypes (t);
		}

		public IEnumerable<AbstractType> VisitMemberSymbol (MemberSymbol mr)
		{
			if (mr.Definition is DMethod) {
				return new [] { mr };
			}
			if (mr.Definition is DVariable) {
				// If we've got a variable here, get its base type/value reference
				if (valueProvider != null) {
					var dgVal = valueProvider [(DVariable)mr.Definition] as DelegateValue;

					if (dgVal != null) {
						return dgVal.Definition.Accept (this);
					}

					valueProvider.LogError (call, "Variable must be a delegate, not anything else");
					returnInstantly = true;
					return new AbstractType [] { };
				}

				var bt = mr.Base ?? TypeDeclarationResolver.ResolveSingle (mr.Definition.Type, ctxt);

				bool requireStaticItems_Backup = requireStaticItems;

				// Should be of type delegate
				if (bt is DelegateType) {
					requireStaticItems = true;
				} else {
					/*
					 * If mr.Node is not a method, so e.g. if it's a variable
					 * pointing to a delegate
					 * 
					 * class Foo
					 * {
					 *	string opCall() {  return "asdf";  }
					 * }
					 * 
					 * Foo f=new Foo();
					 * f(); -- calls opCall, opCall is not static
					 */
					requireStaticItems = false;
				}

				var returnedTypes = bt.Accept (this);
				requireStaticItems = requireStaticItems_Backup;
				return returnedTypes;
			}

			return new AbstractType [] { };
		}

		public IEnumerable<AbstractType> VisitMixinTemplateType (MixinTemplateType t)
		{
			return HandleInvalidTypes (t);
		}

		public IEnumerable<AbstractType> VisitModuleSymbol (ModuleSymbol t)
		{
			return HandleInvalidTypes (t);
		}

		public IEnumerable<AbstractType> VisitPackageSymbol (PackageSymbol t)
		{
			return HandleInvalidTypes (t);
		}

		public IEnumerable<AbstractType> VisitPointerType (PointerType t)
		{
			return HandleInvalidTypes (t);
		}

		/// dmd 2.066: Uniform Construction Syntax. creal(3) is of type creal.
		public IEnumerable<AbstractType> VisitPrimitiveType (PrimitiveType t)
		{
			yield return t;
		}

		public IEnumerable<AbstractType> VisitStaticProperty (StaticProperty t)
		{
			return HandleInvalidTypes (t);
		}

		public IEnumerable<AbstractType> VisitStructType (StructType t)
		{
			return VisitClassOrStructType (t);
		}

		public IEnumerable<AbstractType> VisitTemplateParameterSymbol (TemplateParameterSymbol t)
		{
			yield return t.Base;
		}

		/// If the overload is a template, it quite exclusively means that we'll handle a method that is the only
		/// child inside a template + that is named as the template.
		public IEnumerable<AbstractType> VisitTemplateType (TemplateType t)
		{
			yield return t;
		}

		public IEnumerable<AbstractType> VisitUnionType (UnionType t)
		{
			return HandleInvalidTypes (t);
		}

		public IEnumerable<AbstractType> VisitUnknownType (UnknownType t)
		{
			return HandleInvalidTypes (t);
		}

		IEnumerable<AbstractType> HandleInvalidTypes (AbstractType t)
		{
#if TRACE
			Trace.WriteLine ("MethodOverloadCandidateSearch: Couldn't handle " + t);
#endif
			yield break;
		}
	}
}
