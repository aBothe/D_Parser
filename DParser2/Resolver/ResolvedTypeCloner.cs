using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;

namespace D_Parser.Resolver
{
	class ResolvedTypeCloner : IResolvedTypeVisitor<AbstractType>
	{
		public class CloneOptions
		{
			public AbstractType newBase;
			public bool resetDeducedTypes = false;
			public IEnumerable<TemplateParameterSymbol> templateParameterSymbols;
		}

		readonly CloneOptions options;

		ResolvedTypeCloner(CloneOptions options) { this.options = options; }

		public static AbstractTypeT Clone<AbstractTypeT>(
			AbstractTypeT t,
			IEnumerable<TemplateParameterSymbol> templateParameterSymbols = null,
			AbstractType newBase = null)
			where AbstractTypeT : AbstractType
		{
			return Clone<AbstractTypeT>(t, new CloneOptions() { newBase = newBase, templateParameterSymbols = templateParameterSymbols });
		}

		public static AbstractTypeT Clone<AbstractTypeT>(AbstractTypeT t, CloneOptions options) where AbstractTypeT : AbstractType
		{
			if (t == null)
				return default(AbstractTypeT);

			return (AbstractTypeT)t.Accept(new ResolvedTypeCloner(options));
		}

		AbstractType TryCloneBase(DerivedDataType derivedDataType)
		{
			if(options.newBase != null)
			{
				try { return options.newBase; }
				finally { options.newBase = null; }
			}
			return /*cloneBase && derivedDataType.Base != null ? derivedDataType.Base.Accept(this) : */derivedDataType.Base;
		}

		IEnumerable<TemplateParameterSymbol> TryMergeDeducedTypes(DSymbol ds)
		{
			if (options.templateParameterSymbols == null && !options.resetDeducedTypes)
				return ds.DeducedTypes;

			var deducedTypes = new Dictionary<TemplateParameter, TemplateParameterSymbol>();
			if(options.resetDeducedTypes)
				foreach (var tps in ds.DeducedTypes)
					deducedTypes[tps.Parameter] = tps;

			if (options.templateParameterSymbols != null)
			{
				foreach (var tps in options.templateParameterSymbols)
					if (tps != null)
						deducedTypes[tps.Parameter] = tps;

				options.templateParameterSymbols = null;
			}

			return deducedTypes.Values;
		}

		public AbstractType VisitAliasedType(AliasedType t)
		{
			return new AliasedType(t.Definition, TryCloneBase(t), TryMergeDeducedTypes(t)) { Modifiers = t.Modifiers };
		}

		public AbstractType VisitAmbigousType(AmbiguousType t)
		{
			return new AmbiguousType(t.Overloads);
		}

		public AbstractType VisitArrayAccessSymbol(ArrayAccessSymbol t)
		{
			return new ArrayAccessSymbol(t.indexExpression, TryCloneBase(t));
		}

		public AbstractType VisitArrayType(ArrayType t)
		{
			ArrayType type;
			if (t.IsStaticArray)
				type = new ArrayType(TryCloneBase(t));
			else
				type = new ArrayType(TryCloneBase(t), t.FixedLength);
			type.IsStringLiteral = t.IsStringLiteral;
			return type;
		}

		public AbstractType VisitAssocArrayType(AssocArrayType t)
		{
			return new AssocArrayType(TryCloneBase(t),
				/*cloneBase && t.KeyType != null ? t.KeyType.Accept(this) :*/ t.KeyType);
		}

		public AbstractType VisitClassType(ClassType t)
		{
			return new ClassType(t.Definition, TryCloneBase(t) as TemplateIntermediateType, t.BaseInterfaces, TryMergeDeducedTypes(t))
			{
				Modifiers = t.Modifiers
			};
		}

		public AbstractType VisitDelegateCallSymbol(DelegateCallSymbol t)
		{
			return new DelegateCallSymbol(/*cloneBase && t.Delegate != null ? t.Delegate.Accept(this) as DelegateType :*/ t.Delegate, t.callExpression);
		}

		public AbstractType VisitDelegateType(DelegateType t)
		{
			//TODO: Clone parameters
			if (t.IsFunctionLiteral)
				return new DelegateType(TryCloneBase(t), t.delegateTypeBase as FunctionLiteral, t.Parameters);

			return new DelegateType(TryCloneBase(t), t.delegateTypeBase as DelegateDeclaration, t.Parameters);
		}

		public AbstractType VisitDTuple(DTuple t)
		{
			return new DTuple(t.Items) { Modifiers = t.Modifiers };
		}

		public AbstractType VisitEnumType(EnumType t)
		{
			return new EnumType(t.Definition, TryCloneBase(t)) { Modifiers = t.Modifiers };
		}

		public AbstractType VisitEponymousTemplateType(EponymousTemplateType t)
		{
			return new EponymousTemplateType(t.Definition, TryMergeDeducedTypes(t));
		}

		public AbstractType VisitInterfaceType(InterfaceType t)
		{
			return new InterfaceType(t.Definition, t.BaseInterfaces, TryMergeDeducedTypes(t)) { Modifiers = t.Modifiers };
		}

		public AbstractType VisitMemberSymbol(MemberSymbol t)
		{
			return new MemberSymbol(t.Definition, TryCloneBase(t), TryMergeDeducedTypes(t)) { Modifiers = t.Modifiers };
		}

		public AbstractType VisitMixinTemplateType(MixinTemplateType t)
		{
			return new MixinTemplateType(t.Definition, TryMergeDeducedTypes(t)) { Modifiers = t.Modifiers };
		}

		public AbstractType VisitModuleSymbol(ModuleSymbol t)
		{
			return new ModuleSymbol(t.Definition, TryCloneBase(t) as PackageSymbol) { Modifiers = t.Modifiers };
		}

		public AbstractType VisitPackageSymbol(PackageSymbol t)
		{
			return new PackageSymbol(t.Package) { Modifiers = t.Modifiers };
		}

		public AbstractType VisitPointerType(PointerType t)
		{
			return new PointerType(TryCloneBase(t));
		}

		public AbstractType VisitPrimitiveType(PrimitiveType t)
		{
			return new PrimitiveType(t.TypeToken, t.Modifiers);
		}

		public AbstractType VisitStaticProperty(StaticProperty t)
		{
			return new StaticProperty(t.Definition, TryCloneBase(t), t.ValueGetter);
		}

		public AbstractType VisitStructType(StructType t)
		{
			return new StructType(t.Definition, TryMergeDeducedTypes(t)) { Modifiers = t.Modifiers };
		}

		public AbstractType VisitTemplateParameterSymbol(TemplateParameterSymbol t)
		{
			return new TemplateParameterSymbol(t.Parameter, t.ParameterValue ?? TryCloneBase(t) as ISemantic) { Modifiers = t.Modifiers };
		}

		public AbstractType VisitTemplateType(TemplateType t)
		{
			return new TemplateType(t.Definition, TryMergeDeducedTypes(t)) { Modifiers = t.Modifiers };
		}

		public AbstractType VisitUnionType(UnionType t)
		{
			return new UnionType(t.Definition, TryMergeDeducedTypes(t)) { Modifiers = t.Modifiers };
		}

		public AbstractType VisitUnknownType(UnknownType t)
		{
			return new UnknownType(t.BaseExpression);
		}
	}
}
