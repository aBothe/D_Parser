using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Resolver.ExpressionSemantics;
using System.Text;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Resolver
{
	public abstract class AbstractType : ISemantic, IVisitable<IResolvedTypeVisitor>
	{
		#region Properties
		public object Tag;
		public ISyntaxRegion DeclarationOrExpressionBase;
		public virtual bool NonStaticAccess { get; set; }

		protected byte modifier;

		/// <summary>
		/// e.g. const, immutable
		/// </summary>
		public virtual byte Modifier
		{
			get
			{
				if (modifier != 0)
					return modifier;

				if (DeclarationOrExpressionBase is MemberFunctionAttributeDecl)
					return ((MemberFunctionAttributeDecl)DeclarationOrExpressionBase).Modifier;

				return 0;
			}
			set
			{
				modifier = value;
			}
		}
		#endregion

		#region Constructor/Init
		protected AbstractType() { }
		protected AbstractType(ISyntaxRegion DeclarationOrExpressionBase)
		{
			this.DeclarationOrExpressionBase = DeclarationOrExpressionBase;
		}
		#endregion

		public override string ToString()
		{
			return ToCode(true);
		}

		public string ToCode()
		{
			return DTypeToCodeVisitor.GenerateCode(this);
		}

		public string ToCode(bool pretty)
		{
			return DTypeToCodeVisitor.GenerateCode(this, pretty);
		}

		public static AbstractType Get(ISemantic s)
		{
			//FIXME: What to do with the other overloads?
			if (s is InternalOverloadValue)
				return new AmbiguousType((s as InternalOverloadValue).Overloads);
			if (s is VariableValue)
				return (s as VariableValue).Member;
			if (s is ISymbolValue)
				return (s as ISymbolValue).RepresentedType;
			
			return s as AbstractType;
		}

		public abstract AbstractType Clone(bool cloneBase);

		public abstract void Accept(IResolvedTypeVisitor vis);
		public abstract R Accept<R>(IResolvedTypeVisitor<R> vis);
	}

	#region Special types
	public class UnknownType : AbstractType
	{
		public UnknownType(ISyntaxRegion typeBase) : base(typeBase) {
		}

		public override void Accept (IResolvedTypeVisitor vis)
		{
			vis.VisitUnknownType (this);
		}

		public override R Accept<R> (IResolvedTypeVisitor<R> vis)
		{
			return vis.VisitUnknownType (this);
		}

		public override AbstractType Clone (bool cloneBase)
		{
			return new UnknownType (DeclarationOrExpressionBase);
		}
	}

	public class AmbiguousType : AbstractType
	{
		public readonly AbstractType[] Overloads;

		public override bool NonStaticAccess
		{
			get
			{
				return base.NonStaticAccess;
			}
			set
			{
				base.NonStaticAccess = value;
				foreach (var o in Overloads)
					o.NonStaticAccess = value;
			}
		}

		public static AbstractType Get(IEnumerable<AbstractType> types, ISyntaxRegion typeBase = null)
		{
			if (types == null)
				return null;
			var en = types.GetEnumerator();
			if (!en.MoveNext())
				return null;
			var first = en.Current;
			if (!en.MoveNext())
				return first;
			en.Dispose();

			return new AmbiguousType(types, typeBase);
		}

		public static IEnumerable<AbstractType> TryDissolve(AbstractType t)
		{
			if (t is AmbiguousType)
			{
				foreach (var o in (t as AmbiguousType).Overloads)
					yield return o;
			}
			else if (t != null)
				yield return t;
		}

		public override byte Modifier
		{
			get
			{
				if (Overloads.Length != 0)
					return Overloads[0].Modifier;
				return base.Modifier;
			}
			set
			{
				foreach (var ov in Overloads)
					ov.Modifier = value;
				base.Modifier = value;
			}
		}

		public AmbiguousType(IEnumerable<AbstractType> o, ISyntaxRegion typeBase = null)
		{
			if (o == null)
				throw new ArgumentNullException("o");

			DeclarationOrExpressionBase = typeBase;
			var l = new List<AbstractType>();
			foreach (var ov in o)
				if (ov != null)
					l.Add(ov);
			Overloads = l.ToArray();
		}

		public override AbstractType Clone(bool cloneBase)
		{
			return new AmbiguousType(Overloads, DeclarationOrExpressionBase);
		}

		public override void Accept(IResolvedTypeVisitor vis)
		{
			vis.VisitAmbigousType(this);
		}

		public override R Accept<R>(IResolvedTypeVisitor<R> vis)
		{
			return vis.VisitAmbigousType(this);
		}
	}
	#endregion

	public class PrimitiveType : AbstractType
	{
		public readonly byte TypeToken;

		public PrimitiveType(byte TypeToken, byte Modifier = 0)
		{
			this.TypeToken = TypeToken;
			this.modifier = Modifier;
		}

		public PrimitiveType(byte TypeToken, byte Modifier, ISyntaxRegion td)
			: base(td)
		{
			this.TypeToken = TypeToken;
			this.modifier = Modifier;
		}

		public override AbstractType Clone(bool cloneBase)
		{
			return new PrimitiveType(TypeToken, modifier);
		}

		public override void Accept(IResolvedTypeVisitor vis)
		{
			vis.VisitPrimitiveType(this);
		}

		public override R Accept<R>(IResolvedTypeVisitor<R> vis)
		{
			return vis.VisitPrimitiveType(this);
		}
	}

	#region Derived data types
	public abstract class DerivedDataType : AbstractType
	{
		public readonly AbstractType Base;

		protected DerivedDataType(AbstractType Base, ISyntaxRegion td) : base(td)
		{
			this.Base = Base;
		}
	}

	public class PointerType : DerivedDataType
	{
		public PointerType(AbstractType Base, ISyntaxRegion td) : base(Base, td) { }

		public override AbstractType Clone(bool cloneBase)
		{
			return new PointerType(cloneBase && Base != null ? Base.Clone(true) : Base, base.DeclarationOrExpressionBase);
		}

		public override void Accept(IResolvedTypeVisitor vis)
		{
			vis.VisitPointerType(this);
		}

		public override R Accept<R>(IResolvedTypeVisitor<R> vis)
		{
			return vis.VisitPointerType(this);
		}
	}

	public class ArrayType : AssocArrayType
	{
		public readonly int FixedLength;
		public readonly bool IsStaticArray;

		public ArrayType(AbstractType ValueType, ISyntaxRegion td)
			: base(ValueType, null, td) { FixedLength = -1; }

		public ArrayType(AbstractType ValueType, int ArrayLength, ISyntaxRegion td)
			: this(ValueType, td)
		{
			FixedLength = ArrayLength;
			IsStaticArray = ArrayLength >= 0;
		}

		public override AbstractType Clone(bool cloneBase)
		{
			if(IsStaticArray)
				return new ArrayType(cloneBase && Base != null ? Base.Clone(true) : Base, base.DeclarationOrExpressionBase);
			return new ArrayType(cloneBase && Base != null ? Base.Clone(true) : Base, FixedLength, base.DeclarationOrExpressionBase);
		}

		public override void Accept(IResolvedTypeVisitor vis)
		{
			vis.VisitArrayType(this);
		}

		public override R Accept<R>(IResolvedTypeVisitor<R> vis)
		{
			return vis.VisitArrayType(this);
		}
	}

	public class AssocArrayType : DerivedDataType
	{
		public readonly AbstractType KeyType;

		public bool IsString
		{
			get{
				var pt = DResolver.StripMemberSymbols(ValueType) as PrimitiveType;
				return this is ArrayType && pt != null && DTokens.IsBasicType_Character(pt.TypeToken);
			}
		}

		/// <summary>
		/// Aliases <see cref="Base"/>
		/// </summary>
		public AbstractType ValueType { get { return Base; } }

		public AssocArrayType(AbstractType ValueType, AbstractType KeyType, ISyntaxRegion td)
			: base(ValueType, td)
		{
			if (ValueType != null)
				ValueType.NonStaticAccess = true;
			this.KeyType = KeyType;
		}

		public override AbstractType Clone(bool cloneBase)
		{
			return new AssocArrayType(cloneBase && Base != null ? Base.Clone(true) : Base, cloneBase && KeyType != null ? KeyType.Clone(true) : KeyType, base.DeclarationOrExpressionBase);
		}

		public override void Accept(IResolvedTypeVisitor vis)
		{
			vis.VisitAssocArrayType(this);
		}

		public override R Accept<R>(IResolvedTypeVisitor<R> vis)
		{
			return vis.VisitAssocArrayType(this);
		}
	}

	/// <summary>
	/// Represents calling a delegate. 
	/// Used to determine whether a delegate was called or just has been referenced.
	/// </summary>
	public class DelegateCallSymbol : DerivedDataType
	{
		public readonly DelegateType Delegate;

		public DelegateCallSymbol (DelegateType dg, ISyntaxRegion callExpression) : base (dg.Base, callExpression)
		{
			this.Delegate = dg;
		}

		public override AbstractType Clone(bool cloneBase)
		{
			return new DelegateCallSymbol(cloneBase && Delegate != null ? Delegate.Clone(true) as DelegateType : Delegate, base.DeclarationOrExpressionBase);
		}

		public override void Accept(IResolvedTypeVisitor vis)
		{
			vis.VisitDelegateCallSymbol(this);
		}

		public override R Accept<R>(IResolvedTypeVisitor<R> vis)
		{
			return vis.VisitDelegateCallSymbol(this);
		}
	}

	public class DelegateType : DerivedDataType
	{
		public readonly bool IsFunction;
		public bool IsFunctionLiteral { get { return DeclarationOrExpressionBase is FunctionLiteral; } }
		public AbstractType[] Parameters { get; set; }

		public DelegateType(AbstractType ReturnType,DelegateDeclaration Declaration, IEnumerable<AbstractType> Parameters = null) : base(ReturnType, Declaration)
		{
			this.IsFunction = Declaration.IsFunction;
			if (ReturnType != null)
				ReturnType.NonStaticAccess = true;

			if (Parameters is AbstractType[])
				this.Parameters = (AbstractType[])Parameters;
			else if(Parameters!=null)
				this.Parameters = Parameters.ToArray();
		}

		public DelegateType(AbstractType ReturnType, FunctionLiteral Literal, IEnumerable<AbstractType> Parameters)
			: base(ReturnType, Literal)
		{
			this.IsFunction = Literal.LiteralToken == DTokens.Function;
			if (ReturnType != null)
				ReturnType.NonStaticAccess = true;
			
			if (Parameters is AbstractType[])
				this.Parameters = (AbstractType[])Parameters;
			else if (Parameters != null)
				this.Parameters = Parameters.ToArray();
		}

		public AbstractType ReturnType { get { return Base; } }

		public override AbstractType Clone(bool cloneBase)
		{
			//TODO: Clone parameters
			return new DelegateType(cloneBase && Base != null ? Base.Clone(true) : Base, base.DeclarationOrExpressionBase as DelegateDeclaration, Parameters);
		}

		public override void Accept(IResolvedTypeVisitor vis)
		{
			vis.VisitDelegateType(this);
		}

		public override R Accept<R>(IResolvedTypeVisitor<R> vis)
		{
			return vis.VisitDelegateType(this);
		}
	}
	#endregion

	public abstract class DSymbol : DerivedDataType
	{
		protected WeakReference definition;

		public DNode Definition { get {
				return definition.Target as DNode;
			}
		}

		public bool ValidSymbol
		{
			get{ return definition.IsAlive; }
		}

		/// <summary>
		/// Key: Type name
		/// Value: Corresponding type
		/// </summary>
		public ReadOnlyCollection<TemplateParameterSymbol> DeducedTypes;


		public readonly int NameHash;
		public string Name {get{return Strings.TryGet (NameHash);}}

		protected DSymbol(DNode Node, AbstractType BaseType, ReadOnlyCollection<TemplateParameterSymbol> deducedTypes, ISyntaxRegion td)
			: base(BaseType, td)
		{
			this.DeducedTypes = deducedTypes;

			if (Node == null)
				throw new ArgumentNullException ("Node");

			this.definition = new WeakReference(Node);
			NameHash = Node.NameHash;
		}

		protected DSymbol(DNode Node, AbstractType BaseType, IEnumerable<TemplateParameterSymbol> deducedTypes, ISyntaxRegion td)
			: base(BaseType, td)
		{
			if(deducedTypes!=null)
				this.DeducedTypes = new ReadOnlyCollection<TemplateParameterSymbol>(deducedTypes.ToList());

			if (Node == null)
				throw new ArgumentNullException ("Node");

			this.definition = new WeakReference(Node);
			NameHash = Node.NameHash;
		}
	}

	#region User-defined types
	public abstract class UserDefinedType : DSymbol
	{
		protected UserDefinedType(DNode Node, AbstractType baseType, ReadOnlyCollection<TemplateParameterSymbol> deducedTypes, ISyntaxRegion td) : base(Node, baseType, deducedTypes, td) { }
	}

	public class AliasedType : MemberSymbol
	{
		public new DVariable Definition { get { return base.Definition as DVariable; } }

		public AliasedType(DVariable AliasDefinition, AbstractType Type, ISyntaxRegion td, ReadOnlyCollection<TemplateParameterSymbol> deducedTypes=null)
			: base(AliasDefinition,Type, td, deducedTypes) {
				if (Type != null)
					Type.NonStaticAccess = false;
		}
		public AliasedType(DVariable AliasDefinition, AbstractType Type, ISyntaxRegion td, IEnumerable<TemplateParameterSymbol> deducedTypes)
			: base(AliasDefinition, Type, td, deducedTypes) {
				if (Type != null)
					Type.NonStaticAccess = false;
		}

		public override string ToString()
		{
			return base.ToString();
		}

		public override AbstractType Clone(bool cloneBase)
		{
			return new AliasedType(Definition, cloneBase && Base != null ? Base.Clone(true) : Base, DeclarationOrExpressionBase, DeducedTypes);
		}

		public override void Accept(IResolvedTypeVisitor vis)
		{
			vis.VisitAliasedType(this);
		}

		public override R Accept<R>(IResolvedTypeVisitor<R> vis)
		{
			return vis.VisitAliasedType(this);
		}
	}

	public class EnumType : UserDefinedType
	{
		public new DEnum Definition { get { return base.Definition as DEnum; } }
		public override bool NonStaticAccess
		{
			get { return true; }
			set { }
		}

		public EnumType(DEnum Enum, AbstractType BaseType, ISyntaxRegion td) : base(Enum, BaseType, null, td) { }
		public EnumType(DEnum Enum, ISyntaxRegion td) : base(Enum, new PrimitiveType(DTokens.Int, 0), null, td) { }

		public override string ToString()
		{
			return "(enum) " + base.ToString();
		}

		public override AbstractType Clone(bool cloneBase)
		{
			return new EnumType(Definition, cloneBase && Base != null ? Base.Clone(true) : Base, DeclarationOrExpressionBase);
		}

		public override void Accept(IResolvedTypeVisitor vis)
		{
			vis.VisitEnumType(this);
		}

		public override R Accept<R>(IResolvedTypeVisitor<R> vis)
		{
			return vis.VisitEnumType(this);
		}
	}

	public class StructType : TemplateIntermediateType
	{
		public StructType(DClassLike dc, ISyntaxRegion td, IEnumerable<TemplateParameterSymbol> deducedTypes = null) : base(dc, td, null, null, deducedTypes) { }

		public override string ToString()
		{
			return "(struct) " + base.ToString();
		}

		public override AbstractType Clone(bool cloneBase)
		{
			return new StructType(Definition, DeclarationOrExpressionBase, DeducedTypes);
		}

		public override void Accept(IResolvedTypeVisitor vis)
		{
			vis.VisitStructType(this);
		}

		public override R Accept<R>(IResolvedTypeVisitor<R> vis)
		{
			return vis.VisitStructType(this);
		}
	}

	public class UnionType : TemplateIntermediateType
	{
		public UnionType(DClassLike dc, ISyntaxRegion td, IEnumerable<TemplateParameterSymbol> deducedTypes = null) : base(dc, td, null, null, deducedTypes) { }

		public override string ToString()
		{
			return "(union) " + base.ToString();
		}

		public override AbstractType Clone(bool cloneBase)
		{
			return new UnionType(Definition, DeclarationOrExpressionBase, DeducedTypes);
		}

		public override void Accept(IResolvedTypeVisitor vis)
		{
			vis.VisitUnionType(this);
		}

		public override R Accept<R>(IResolvedTypeVisitor<R> vis)
		{
			return vis.VisitUnionType(this);
		}
	}

	public class ClassType : TemplateIntermediateType
	{
		public ClassType(DClassLike dc, ISyntaxRegion td, 
			TemplateIntermediateType baseType, InterfaceType[] baseInterfaces,
			ReadOnlyCollection<TemplateParameterSymbol> deducedTypes)
			: base(dc, td, baseType, baseInterfaces, deducedTypes)
		{}

		public ClassType(DClassLike dc, ISyntaxRegion td, 
			TemplateIntermediateType baseType, InterfaceType[] baseInterfaces = null,
			IEnumerable<TemplateParameterSymbol> deducedTypes = null)
			: base(dc, td, baseType, baseInterfaces, deducedTypes)
		{}

		public override string ToString()
		{
			return "(class) "+base.ToString();
		}

		public override AbstractType Clone(bool cloneBase)
		{
			return new ClassType(Definition, DeclarationOrExpressionBase, cloneBase && Base != null ? Base.Clone(true) as TemplateIntermediateType : Base as TemplateIntermediateType, BaseInterfaces, DeducedTypes);
		}

		public override void Accept(IResolvedTypeVisitor vis)
		{
			vis.VisitClassType(this);
		}

		public override R Accept<R>(IResolvedTypeVisitor<R> vis)
		{
			return vis.VisitClassType(this);
		}
	}

	public class InterfaceType : TemplateIntermediateType
	{
		public InterfaceType(DClassLike dc, ISyntaxRegion td, 
			InterfaceType[] baseInterfaces=null,
			IEnumerable<TemplateParameterSymbol> deducedTypes = null) 
			: base(dc, td, null, baseInterfaces, deducedTypes) {}

		public InterfaceType(DClassLike dc, ISyntaxRegion td,
			InterfaceType[] baseInterfaces,
			ReadOnlyCollection<TemplateParameterSymbol> deducedTypes)
			: base(dc, td, null, baseInterfaces, deducedTypes) { }

		public override AbstractType Clone(bool cloneBase)
		{
			return new InterfaceType(Definition, DeclarationOrExpressionBase, BaseInterfaces, DeducedTypes);
		}

		public override void Accept(IResolvedTypeVisitor vis)
		{
			vis.VisitInterfaceType(this);
		}

		public override R Accept<R>(IResolvedTypeVisitor<R> vis)
		{
			return vis.VisitInterfaceType(this);
		}
	}

	public class TemplateType : TemplateIntermediateType
	{
		public override bool NonStaticAccess
		{
			get
			{
				/*
				 * template t(){ void foo() { } }
				 * t!().foo must be offered for completion
				 */
				/*if(t.Base == null)
					isVariableInstance = true;
				*/
				return true;
			}
			set
			{
				
			}
		}

		public TemplateType(DClassLike dc, ISyntaxRegion td, IEnumerable<TemplateParameterSymbol> inheritedTypeParams = null) : base(dc, td, null, null, inheritedTypeParams) { }
		public TemplateType(DClassLike dc, ISyntaxRegion td, ReadOnlyCollection<TemplateParameterSymbol> inheritedTypeParams = null) : base(dc, td, null, null, inheritedTypeParams) { }

		public override AbstractType Clone(bool cloneBase)
		{
			return new TemplateType(Definition, DeclarationOrExpressionBase, DeducedTypes);
		}

		public override void Accept(IResolvedTypeVisitor vis)
		{
			vis.VisitTemplateType(this);
		}

		public override R Accept<R>(IResolvedTypeVisitor<R> vis)
		{
			return vis.VisitTemplateType(this);
		}
	}
	
	public class MixinTemplateType : TemplateType
	{
		public MixinTemplateType(DClassLike dc, ISyntaxRegion td, IEnumerable<TemplateParameterSymbol> inheritedTypeParams = null) : base(dc, td, inheritedTypeParams) { }
		public MixinTemplateType(DClassLike dc, ISyntaxRegion td, ReadOnlyCollection<TemplateParameterSymbol> inheritedTypeParams = null) : base(dc, td, inheritedTypeParams) { }

		public override AbstractType Clone(bool cloneBase)
		{
			return new MixinTemplateType(Definition, DeclarationOrExpressionBase, DeducedTypes);
		}

		public override void Accept(IResolvedTypeVisitor vis)
		{
			vis.VisitMixinTemplateType(this);
		}

		public override R Accept<R>(IResolvedTypeVisitor<R> vis)
		{
			return vis.VisitMixinTemplateType(this);
		}
	}

	public abstract class TemplateIntermediateType : UserDefinedType
	{
		public new DClassLike Definition { get { return base.Definition as DClassLike; } }

		public readonly InterfaceType[] BaseInterfaces;

		public TemplateIntermediateType(DClassLike dc, ISyntaxRegion td, 
			AbstractType baseType = null, InterfaceType[] baseInterfaces = null,
			ReadOnlyCollection<TemplateParameterSymbol> deducedTypes = null)
			: base(dc, baseType, deducedTypes, td)
		{
			this.BaseInterfaces = baseInterfaces;
		}

		public TemplateIntermediateType(DClassLike dc, ISyntaxRegion td, 
			AbstractType baseType, InterfaceType[] baseInterfaces,
			IEnumerable<TemplateParameterSymbol> deducedTypes)
			: this(dc,td, baseType,baseInterfaces,
			deducedTypes != null ? new ReadOnlyCollection<TemplateParameterSymbol>(deducedTypes.ToArray()) : null)
		{ }
	}

	public class EponymousTemplateType : UserDefinedType
	{
		public new EponymousTemplate Definition { get { return base.Definition as EponymousTemplate; } }

		public EponymousTemplateType(EponymousTemplate ep,
			ReadOnlyCollection<TemplateParameterSymbol> deducedTypes = null, ISyntaxRegion td = null) : base(ep, null, deducedTypes, td) { }

		public override string ToString ()
		{
			return "(Eponymous Template Type) "+ Definition;
		}

		public override AbstractType Clone(bool cloneBase)
		{
			return new EponymousTemplateType(Definition, DeducedTypes, DeclarationOrExpressionBase);
		}

		public override void Accept(IResolvedTypeVisitor vis)
		{
			vis.VisitEponymousTemplateType(this);
		}

		public override R Accept<R>(IResolvedTypeVisitor<R> vis)
		{
			return vis.VisitEponymousTemplateType(this);
		}
	}

	public class StaticProperty : MemberSymbol
	{
		/// <summary>
		/// For keeping the weak reference up!
		/// </summary>
		DNode n;
		public readonly StaticProperties.ValueGetterHandler ValueGetter;

		public StaticProperty(DNode n, AbstractType bt, StaticProperties.ValueGetterHandler valueGetter) : base(n, bt, null)
		{
			this.n = n;
			this.ValueGetter = valueGetter;
		}

		public override AbstractType Clone(bool cloneBase)
		{
			return new StaticProperty(Definition, cloneBase && Base != null ? Base.Clone(true) : Base, ValueGetter);
		}

		public override void Accept(IResolvedTypeVisitor vis)
		{
			vis.VisitStaticProperty(this);
		}

		public override R Accept<R>(IResolvedTypeVisitor<R> vis)
		{
			return vis.VisitStaticProperty(this);
		}
	}

	public class MemberSymbol : DSymbol
	{
		public MemberSymbol(DNode member, AbstractType memberType, ISyntaxRegion td,
			ReadOnlyCollection<TemplateParameterSymbol> deducedTypes = null)
			: base(member, memberType, deducedTypes, td) {
				if (memberType != null)
					memberType.NonStaticAccess = true;
		}

		public MemberSymbol(DNode member, AbstractType memberType, ISyntaxRegion td,
			IEnumerable<TemplateParameterSymbol> deducedTypes)
			: base(member, memberType, deducedTypes, td) {
				if (memberType != null)
					memberType.NonStaticAccess = true;
		}

		public override AbstractType Clone(bool cloneBase)
		{
			return new MemberSymbol(Definition, cloneBase && Base != null ? Base.Clone(true) : Base, DeclarationOrExpressionBase , DeducedTypes);
		}

		public override void Accept(IResolvedTypeVisitor vis)
		{
			vis.VisitMemberSymbol(this);
		}

		public override R Accept<R>(IResolvedTypeVisitor<R> vis)
		{
			return vis.VisitMemberSymbol(this);
		}
	}
	
	public class TemplateParameterSymbol : MemberSymbol
	{
		public readonly TemplateParameter Parameter;
		/// <summary>
		/// Only used for template value parameters.
		/// </summary>
		public readonly ISymbolValue ParameterValue;
		public bool IsKnowinglyUndetermined;

		public TemplateParameterSymbol(TemplateParameter.Node tpn, ISemantic typeOrValue, ISyntaxRegion paramIdentifier = null)
			: base(tpn, AbstractType.Get(typeOrValue), paramIdentifier)
		{
			IsKnowinglyUndetermined = TemplateInstanceHandler.IsNonFinalArgument(typeOrValue);
			this.Parameter = tpn.TemplateParameter;
			this.ParameterValue = typeOrValue as ISymbolValue;
		}

		public TemplateParameterSymbol(TemplateParameter tpn, ISemantic typeOrValue, ISyntaxRegion paramIdentifier = null)
			: base(tpn != null ? tpn.Representation : null, AbstractType.Get(typeOrValue), paramIdentifier)
		{
			IsKnowinglyUndetermined = TemplateInstanceHandler.IsNonFinalArgument(typeOrValue);
			this.Parameter = tpn;
			this.ParameterValue = typeOrValue as ISymbolValue;
		}
		/*
		public TemplateParameterSymbol(TemplateParameter tp,
			ISemantic representedTypeOrValue,
			ISyntaxRegion originalParameterIdentifier = null,
			DNode parentNode = null)
			: base(new TemplateParameterNode(tp) { Parent = parentNode },
			AbstractType.Get(representedTypeOrValue), originalParameterIdentifier ?? tp)
		{
			this.Parameter = tp;
			this.ParameterValue = representedTypeOrValue as ISymbolValue;
		}*/

		public override string ToString()
		{
			return "<"+(Parameter == null ? "(unknown)" : Parameter.Name)+">"+(ParameterValue!=null ? ParameterValue.ToString() : (Base==null ? "" : Base.ToString()));
		}

		public override AbstractType Clone(bool cloneBase)
		{
			return new TemplateParameterSymbol(Parameter, ParameterValue ?? (cloneBase && Base != null ? Base.Clone(true) : Base) as ISemantic, DeclarationOrExpressionBase);
		}

		public override void Accept(IResolvedTypeVisitor vis)
		{
			vis.VisitTemplateParameterSymbol(this);
		}

		public override R Accept<R>(IResolvedTypeVisitor<R> vis)
		{
			return vis.VisitTemplateParameterSymbol(this);
		}
	}
	
	/// <summary>
	/// Intermediate result when evaluating e.g. myArray[0]
	/// Required for proper completion of array access expressions (e.g. foo[0].)
	/// </summary>
	public class ArrayAccessSymbol : DerivedDataType
	{
		public ArrayAccessSymbol(PostfixExpression_ArrayAccess indexExpr, AbstractType arrayValueType):
			base(arrayValueType,indexExpr)	{ }

		public override AbstractType Clone(bool cloneBase)
		{
			return new ArrayAccessSymbol(DeclarationOrExpressionBase as PostfixExpression_ArrayAccess, cloneBase && Base != null ? Base.Clone(true) : Base);
		}

		public override void Accept(IResolvedTypeVisitor vis)
		{
			vis.VisitArrayAccessSymbol(this);
		}

		public override R Accept<R>(IResolvedTypeVisitor<R> vis)
		{
			return vis.VisitArrayAccessSymbol(this);
		}
	}

	public class ModuleSymbol : DSymbol
	{
		public new DModule Definition { get { return base.Definition as DModule; } }
		public override bool NonStaticAccess
		{
			get	{ return true; }
			set	{}
		}

		public ModuleSymbol(DModule mod, ISyntaxRegion td, PackageSymbol packageBase = null) : base(mod, packageBase, (IEnumerable<TemplateParameterSymbol>)null, td) {	}

		public override string ToString()
		{
			return "(module) "+base.ToString();
		}

		public override AbstractType Clone(bool cloneBase)
		{
			return new ModuleSymbol(Definition, DeclarationOrExpressionBase, cloneBase && Base != null ? Base.Clone(true) as PackageSymbol : Base as PackageSymbol);
		}

		public override void Accept(IResolvedTypeVisitor vis)
		{
			vis.VisitModuleSymbol(this);
		}

		public override R Accept<R>(IResolvedTypeVisitor<R> vis)
		{
			return vis.VisitModuleSymbol(this);
		}
	}

	public class PackageSymbol : AbstractType
	{
		public readonly ModulePackage Package;

		public PackageSymbol(ModulePackage pack,ISyntaxRegion td) : base(td) {
			this.Package = pack;
		}

		public override string ToString()
		{
			return "(package) "+base.ToString();
		}

		public override AbstractType Clone(bool cloneBase)
		{
			return new PackageSymbol(Package, DeclarationOrExpressionBase);
		}

		public override void Accept(IResolvedTypeVisitor vis)
		{
			vis.VisitPackageSymbol(this);
		}

		public override R Accept<R>(IResolvedTypeVisitor<R> vis)
		{
			return vis.VisitPackageSymbol(this);
		}
	}
	#endregion

	/// <summary>
	/// A Tuple is not a type, an expression, or a symbol. It is a sequence of any mix of types, expressions or symbols.
	/// </summary>
	public class DTuple : AbstractType
	{
		public readonly ISemantic[] Items;

		public DTuple(ISyntaxRegion td,IEnumerable<ISemantic> items) : base(td)
		{
			if (items is ISemantic[])
				Items = (ISemantic[])items;
			else if (items != null)
				Items = items.ToArray();
		}

		public bool IsExpressionTuple
		{
			get {
				return Items != null && Items.All(i => i is ISymbolValue);
			}
		}

		public bool IsTypeTuple
		{
			get
			{
				return Items != null && Items.All(i => i is AbstractType);
			}
		}

		public override AbstractType Clone(bool cloneBase)
		{
			return new DTuple(DeclarationOrExpressionBase, Items);
		}

		public override void Accept(IResolvedTypeVisitor vis)
		{
			vis.VisitDTuple(this);
		}

		public override R Accept<R>(IResolvedTypeVisitor<R> vis)
		{
			return vis.VisitDTuple(this);
		}
	}
}
