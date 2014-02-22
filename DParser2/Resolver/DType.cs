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

		/// <summary>
		/// Returns either the original type declaration that was used to instantiate this abstract type
		/// OR creates an artificial type declaration that represents this type.
		/// May returns null.
		/// </summary>
		public virtual ITypeDeclaration TypeDeclarationOf{get{	return DeclarationOrExpressionBase as ITypeDeclaration;	}}

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

		public abstract string ToCode();

		public override string ToString()
		{
			return ToCode();
		}

		public static AbstractType Get(ISemantic s)
		{
			//FIXME: What to do with the other overloads?
			if (s is InternalOverloadValue)
				return (s as InternalOverloadValue).Overloads[0];
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

		public override string ToCode()
		{
			if(Modifier!=0)
				return DTokens.GetTokenString(Modifier)+"("+DTokens.GetTokenString(TypeToken)+")";

			return DTokens.GetTokenString(TypeToken);
		}

		public override ITypeDeclaration TypeDeclarationOf
		{
			get
			{
				return modifier == 0 ? 
				                                  (ITypeDeclaration)new DTokenDeclaration(TypeToken) :
				                                  new MemberFunctionAttributeDecl(modifier){ 
				                                  	InnerType = new DTokenDeclaration(TypeToken)};
			}
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

		public override string ToCode()
		{
			return (Base != null ? Base.ToCode() : "") + "*";
		}

		public override ITypeDeclaration TypeDeclarationOf
		{
			get
			{
				return new PointerDecl(Base==null ? null : Base.TypeDeclarationOf);
			}
		}

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
			: base(ValueType, new PrimitiveType(DTokens.Int, 0), td) { FixedLength = -1; }

		public ArrayType(AbstractType ValueType, int ArrayLength, ISyntaxRegion td)
			: this(ValueType, td)
		{
			FixedLength = ArrayLength;
			IsStaticArray = ArrayLength >= 0;
		}

		public override string ToCode()
		{
			return (Base != null ? Base.ToCode() : "") + (IsStaticArray && FixedLength >= 0 ? string.Format("[{0}]",FixedLength) : "[]");
		}

		public override AbstractType Clone(bool cloneBase)
		{
			if(IsStaticArray)
				return new ArrayType(cloneBase && Base != null ? Base.Clone(true) : Base, base.DeclarationOrExpressionBase);
			return new ArrayType(cloneBase && Base != null ? Base.Clone(true) : Base, FixedLength, base.DeclarationOrExpressionBase);
		}

		public override ITypeDeclaration TypeDeclarationOf
		{
			get
			{
				return new ArrayDecl { 
					ValueType = (Base != null ? Base.TypeDeclarationOf : null), 
					KeyExpression = IsStaticArray ? new IdentifierExpression(FixedLength, LiteralFormat.Scalar) : null };
			}
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
				var kt = DResolver.StripMemberSymbols (KeyType);
				return (kt == null || (kt is PrimitiveType &&
					((kt as PrimitiveType).TypeToken == DTokens.Int))) &&
					(kt = DResolver.StripMemberSymbols (ValueType)) is PrimitiveType &&
					DTokens.IsBasicType_Character((kt as PrimitiveType).TypeToken);
			}
		}

		/// <summary>
		/// Aliases <see cref="Base"/>
		/// </summary>
		public AbstractType ValueType { get { return Base; } }

		public AssocArrayType(AbstractType ValueType, AbstractType KeyType, ISyntaxRegion td)
			: base(ValueType, td)
		{
			this.KeyType = KeyType;
		}

		public override string ToCode()
		{
			return (Base!=null ? Base.ToCode():"") + "[" + (KeyType!=null ? KeyType.ToCode() : "" )+ "]";
		}

		public override ITypeDeclaration TypeDeclarationOf
		{
			get
			{
				return new ArrayDecl { 
					ValueType = ValueType==null ? null : ValueType.TypeDeclarationOf,
					KeyType = KeyType == null ? null : KeyType.TypeDeclarationOf
				};
			}
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

		public override string ToCode ()
		{
			return DeclarationOrExpressionBase.ToString ();
		}

		public override ITypeDeclaration TypeDeclarationOf {
			get {
				return Delegate.TypeDeclarationOf;
			}
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

			if (Parameters is AbstractType[])
				this.Parameters = (AbstractType[])Parameters;
			else if(Parameters!=null)
				this.Parameters = Parameters.ToArray();
		}

		public DelegateType(AbstractType ReturnType, FunctionLiteral Literal, IEnumerable<AbstractType> Parameters)
			: base(ReturnType, Literal)
		{
			this.IsFunction = Literal.LiteralToken == DTokens.Function;
			
			if (Parameters is AbstractType[])
				this.Parameters = (AbstractType[])Parameters;
			else if (Parameters != null)
				this.Parameters = Parameters.ToArray();
		}

		public override string ToCode()
		{
			var c = (Base != null ? Base.ToCode() : "") + " " + (IsFunction ? "function" : "delegate") + " (";

			if (Parameters != null)
				foreach (var p in Parameters)
					c += p.ToCode() + ",";

			return c.TrimEnd(',') + ")";
		}

		public AbstractType ReturnType { get { return Base; } }

		public override ITypeDeclaration TypeDeclarationOf
		{
			get
			{
				var td = base.TypeDeclarationOf;
				
				if(td!=null)
					return td;

				var dd = new DelegateDeclaration { 
					ReturnType = ReturnType==null ? null : ReturnType.TypeDeclarationOf,
 					IsFunction = this.IsFunction
				};
				//TODO: Modifiers?
				if(Parameters!=null)
					foreach (var p in Parameters)
						dd.Parameters.Add(new DVariable { Type = p.TypeDeclarationOf });

				return dd;
			}
		}

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

		public override string ToCode()
		{
			var def = Definition;
			return def != null ? def.ToString(false, true) : "<Node object no longer exists>";
		}

		public override ITypeDeclaration TypeDeclarationOf
		{
			get
			{
				var def = Definition;
				ITypeDeclaration td = new IdentifierDeclaration(def != null ? def.NameHash : 0);

				if (def != null && DeducedTypes != null && def.TemplateParameters != null)
				{
					var args = new List<IExpression>();
					foreach (var tp in def.TemplateParameters)
					{
						IExpression argEx = null;
						foreach(var tps in DeducedTypes)
							if (tps.Parameter == tp)
							{
								if (tps.ParameterValue != null){
									//TODO: Convert ISymbolValues back to IExpression
								}
								else
									argEx = new TypeDeclarationExpression(tps.TypeDeclarationOf);
								break;
							}

						args.Add(argEx ?? new IdentifierExpression(tp.NameHash));
					}

					td = new TemplateInstanceExpression(td) { Arguments = args.ToArray() };
				}

				var ret = td;
				
				while (def != (def = def.Parent as DNode) &&
					def != null && !(def is DModule))
				{
					td = td.InnerDeclaration = new IdentifierDeclaration(def.NameHash);
				}

				return ret;
			}
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
			: base(AliasDefinition,Type, td, deducedTypes) {}
		public AliasedType(DVariable AliasDefinition, AbstractType Type, ISyntaxRegion td, IEnumerable<TemplateParameterSymbol> deducedTypes)
			: base(AliasDefinition, Type, td, deducedTypes) { }

		public override string ToString()
		{
			return base.ToString();
		}

		public override string ToCode()
		{
			return Base != null ? Base.ToCode() : Definition.ToString(false,false);
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
			: base(member, memberType, deducedTypes, td) { }

		public MemberSymbol(DNode member, AbstractType memberType, ISyntaxRegion td,
			IEnumerable<TemplateParameterSymbol> deducedTypes)
			: base(member, memberType, deducedTypes, td) { }

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

		public TemplateParameterSymbol(TemplateParameter.Node tpn, ISemantic typeOrValue, ISyntaxRegion paramIdentifier = null)
			: base(tpn, AbstractType.Get(typeOrValue), paramIdentifier)
		{
			this.Parameter = tpn.TemplateParameter;
			this.ParameterValue = typeOrValue as ISymbolValue;
		}

		public TemplateParameterSymbol(TemplateParameter tpn, ISemantic typeOrValue, ISyntaxRegion paramIdentifier = null)
			: base(tpn != null ? tpn.Representation : null, AbstractType.Get(typeOrValue), paramIdentifier)
		{
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
		
		public override string ToCode()
		{
			if(ParameterValue!=null)
				return ParameterValue.ToCode();
			return Base == null ? (Parameter == null ? "(unknown template parameter)" : Parameter.Name) : Base.ToCode(); //FIXME: It's not actually code but currently needed for correct ToString() representation in e.g. parameter insight
		}

		public override string ToString()
		{
			return "<"+(Parameter == null ? "(unknown)" : Parameter.Name)+">"+(ParameterValue!=null ? ParameterValue.ToString() : (Base==null ? "" : Base.ToString()));
		}

		public override AbstractType Clone(bool cloneBase)
		{
			return new TemplateParameterSymbol(Parameter, ParameterValue ?? (cloneBase && Base != null ? Base.Clone(true) : Base) as ISemantic, DeclarationOrExpressionBase);
		}

		public override ITypeDeclaration TypeDeclarationOf
		{
			get
			{
				return Base != null ? Base.TypeDeclarationOf : (DeclarationOrExpressionBase as ITypeDeclaration ?? new IdentifierDeclaration(Parameter.NameHash));
			}
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
		public ArrayAccessSymbol(PostfixExpression_Index indexExpr, AbstractType arrayValueType):
			base(arrayValueType,indexExpr)	{ }

		public override string ToCode ()
		{
			return (Base != null ? Base.ToCode () : string.Empty) + "[" + 
				base.DeclarationOrExpressionBase.ToString() + "]";
		}

		public override AbstractType Clone(bool cloneBase)
		{
			return new ArrayAccessSymbol(DeclarationOrExpressionBase as PostfixExpression_Index, cloneBase && Base != null ? Base.Clone(true) : Base);
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

		public ModuleSymbol(DModule mod, ISyntaxRegion td, PackageSymbol packageBase = null) : base(mod, packageBase, (IEnumerable<TemplateParameterSymbol>)null, td) { }

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

		public override string ToCode()
		{
			return Package.Path;
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

		public override string ToCode()
		{
			var sb = new StringBuilder("(");

			if (Items != null && Items.Length != 0)
				foreach (var i in Items)
				{
					sb.Append(i.ToCode()).Append(',');
				}
			else
				return "()";

			return sb.Remove(sb.Length-1,1).Append(')').ToString();
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
