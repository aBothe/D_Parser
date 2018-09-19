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
		Dictionary<string, object> tags;
		public void Tag(string id, object tag) {
			if (tags == null)
				tags = new Dictionary<string, object> ();
			tags [id] = tag;
		}
		public T Tag<T>(string id) where T : class{
			object o;
			if (tags == null || !tags.TryGetValue (id, out o))
				return default(T);
			return (T)o;
		}

		public void AssignTagsFrom(AbstractType t)
		{
			if (t.tags == null)
				tags = null;
			else
				tags = new Dictionary<string, object> (t.tags);
		}

		public virtual bool NonStaticAccess { get; set; }

		/// <summary>
		/// e.g. const, immutable
		/// </summary>
		public virtual byte[] Modifiers {
			get;
			set;
		}

		public bool HasModifiers {
			get { return Modifiers != null && Modifiers.Length > 0; }
		}

		public bool HasModifier(byte modifier){
			if (!HasModifiers)
				return false;

			foreach (byte mod in Modifiers)
				if (mod == modifier)
					return true;

			return false;
		}
		#endregion

		#region Constructor/Init
		protected AbstractType() { }
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
			if (s is ISymbolValue)
				return (s as ISymbolValue).RepresentedType;
			
			return s as AbstractType;
		}

		public static List<AbstractType> Get<R>(IEnumerable<R> at)
			where R : class,ISemantic
		{
			var l = new List<AbstractType>();

			if (at != null)
				foreach (var t in at)
				{
					if (t is AbstractType)
						l.Add(t as AbstractType);
					else if (t is ISymbolValue)
						l.Add(((ISymbolValue)t).RepresentedType);
				}

			return l;
		}

		public abstract AbstractType Clone(bool cloneBase);

		public abstract void Accept(IResolvedTypeVisitor vis);
		public abstract R Accept<R>(IResolvedTypeVisitor<R> vis);
	}

	#region Special types
	public class UnknownType : AbstractType
	{
		public readonly ISyntaxRegion BaseExpression;
		public UnknownType(ISyntaxRegion BaseExpression) {
			this.BaseExpression = BaseExpression;
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
			return new UnknownType (BaseExpression);
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

		public static AbstractType Get(IEnumerable<AbstractType> types)
		{
			if (types == null)
				return null;
			using (var en = types.GetEnumerator())
			{
				if (!en.MoveNext())
					return null;
				var first = en.Current;
				if (!en.MoveNext())
					return first;
			}

			return new AmbiguousType(types);
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

		public override byte[] Modifiers
		{
			get
			{
				if (Overloads.Length != 0)
					return Overloads[0].Modifiers;
				return base.Modifiers;
			}
			set
			{
				foreach (var ov in Overloads) {
					ov.Modifiers = value;
					DResolver.StripMemberSymbols (ov).Modifiers = value;
				}

				base.Modifiers = value;
			}
		}

		public AmbiguousType(IEnumerable<AbstractType> o)
		{
			if (o == null)
				throw new ArgumentNullException("o");

			var l = new List<AbstractType>();
			foreach (var ov in o)
				if (ov != null)
					l.Add(ov);
			Overloads = l.ToArray();
		}

		public override AbstractType Clone(bool cloneBase)
		{
			return new AmbiguousType(Overloads);
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

		public PrimitiveType(byte TypeToken, params byte[] Modifiers)
		{
			this.TypeToken = TypeToken;
			this.Modifiers = Modifiers;
		}

		public override AbstractType Clone(bool cloneBase)
		{
			return new PrimitiveType(TypeToken, Modifiers);
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

		protected DerivedDataType(AbstractType Base)
		{
			this.Base = Base;
		}
	}

	public class PointerType : DerivedDataType
	{
		public PointerType(AbstractType Base) : base(Base) { }

		public override AbstractType Clone(bool cloneBase)
		{
			return new PointerType(cloneBase && Base != null ? Base.Clone(true) : Base);
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

		public bool IsStringLiteral { get; set; }

		public ArrayType(AbstractType ValueType)
			: base(ValueType, null) { FixedLength = -1; }

		public ArrayType(AbstractType ValueType, int ArrayLength)
			: base(ValueType, null)
		{
			FixedLength = ArrayLength;
			IsStaticArray = ArrayLength >= 0;
		}

		public override AbstractType Clone(bool cloneBase)
		{
			ArrayType type;
			if(IsStaticArray)
				type = new ArrayType(cloneBase && Base != null ? Base.Clone(true) : Base);
			else
				type = new ArrayType(cloneBase && Base != null ? Base.Clone(true) : Base, FixedLength);
			type.IsStringLiteral = IsStringLiteral;
			return type;
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
				return this is ArrayType && pt != null && DTokensSemanticHelpers.IsBasicType_Character(pt.TypeToken);
			}
		}

		/// <summary>
		/// Aliases <see cref="Base"/>
		/// </summary>
		public AbstractType ValueType { get { return Base; } }

		public AssocArrayType(AbstractType ValueType, AbstractType KeyType)
			: base(ValueType)
		{
			if (ValueType != null)
				ValueType.NonStaticAccess = true;
			this.KeyType = KeyType;
		}

		public override AbstractType Clone(bool cloneBase)
		{
			return new AssocArrayType(cloneBase && Base != null ? Base.Clone(true) : Base, cloneBase && KeyType != null ? KeyType.Clone(true) : KeyType);
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
		internal readonly PostfixExpression_MethodCall callExpression;

		public DelegateCallSymbol (DelegateType dg, PostfixExpression_MethodCall callExpression) : base (dg.Base)
		{
			this.Delegate = dg;
			this.callExpression = callExpression;
		}

		public override AbstractType Clone(bool cloneBase)
		{
			return new DelegateCallSymbol(cloneBase && Delegate != null ? Delegate.Clone(true) as DelegateType : Delegate, callExpression);
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
		public readonly ISyntaxRegion delegateTypeBase;
		public bool IsFunctionLiteral { get { return delegateTypeBase is FunctionLiteral; } }
		public AbstractType[] Parameters { get; set; }

		public DelegateType(AbstractType ReturnType,DelegateDeclaration Declaration, IEnumerable<AbstractType> Parameters = null) : base(ReturnType)
		{
			delegateTypeBase = Declaration;

			this.IsFunction = Declaration.IsFunction;
			if (ReturnType != null)
				ReturnType.NonStaticAccess = true;

			if (Parameters is AbstractType[])
				this.Parameters = (AbstractType[])Parameters;
			else if(Parameters!=null)
				this.Parameters = Parameters.ToArray();
		}

		public DelegateType(AbstractType ReturnType, FunctionLiteral Literal, IEnumerable<AbstractType> Parameters)
			: base(ReturnType)
		{
			delegateTypeBase = Literal;
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
			if (IsFunctionLiteral)
				return new DelegateType (cloneBase && Base != null ? Base.Clone (true) : Base, delegateTypeBase as FunctionLiteral, Parameters);
			
			return new DelegateType(cloneBase && Base != null ? Base.Clone(true) : Base, delegateTypeBase as DelegateDeclaration, Parameters);
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

		public ReadOnlyCollection<TemplateParameterSymbol> DeducedTypes {
			get;
			private set;
		}
		public bool HasDeducedTypes {get{ return DeducedTypes.Count != 0; }}

		public void SetDeducedTypes(IEnumerable<TemplateParameterSymbol> s)
		{
			if (s == null) {
				DeducedTypes = new ReadOnlyCollection<TemplateParameterSymbol>(new List<TemplateParameterSymbol>(0));
				return;
			}

			var l = new List<TemplateParameterSymbol> ();

			foreach (var tps in s)
				if (tps != null && tps != this && tps.Base != this)
					l.Add (tps);

			DeducedTypes = new ReadOnlyCollection<TemplateParameterSymbol> (l);
		}

		public readonly int NameHash;
		public string Name {get{return Strings.TryGet (NameHash);}}

		protected DSymbol(DNode Node, AbstractType BaseType, IEnumerable<TemplateParameterSymbol> deducedTypes)
			: base(BaseType)
		{
			SetDeducedTypes (deducedTypes);
			
			if (Node == null)
				throw new ArgumentNullException ("Node");

			this.definition = new WeakReference(Node);
			NameHash = Node.NameHash;
		}
	}

	#region User-defined types
	public abstract class UserDefinedType : DSymbol
	{
		protected UserDefinedType(DNode Node, AbstractType baseType, IEnumerable<TemplateParameterSymbol> deducedTypes) : base(Node, baseType, deducedTypes) { }
	}

	public class AliasedType : MemberSymbol
	{
		public new DVariable Definition { get { return base.Definition as DVariable; } }

		public AliasedType(DVariable AliasDefinition, AbstractType Type, IEnumerable<TemplateParameterSymbol> deducedTypes = null)
			: base(AliasDefinition, Type, deducedTypes) {
				if (Type != null)
					Type.NonStaticAccess = false;
		}

		public override string ToString()
		{
			return base.ToString();
		}

		public override AbstractType Clone(bool cloneBase)
		{
			return new AliasedType(Definition, cloneBase && Base != null ? Base.Clone(true) : Base, DeducedTypes) { Modifiers = Modifiers };
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

		public EnumType(DEnum Enum, AbstractType BaseType) : base(Enum, BaseType, null) { }
		public EnumType(DEnum Enum) : base(Enum, new PrimitiveType(DTokens.Int, DTokens.Enum), null) { }

		public override string ToString()
		{
			return "(enum) " + base.ToString();
		}

		public override AbstractType Clone(bool cloneBase)
		{
			return new EnumType(Definition, cloneBase && Base != null ? Base.Clone(true) : Base) { Modifiers = Modifiers };
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
		public StructType(DClassLike dc, IEnumerable<TemplateParameterSymbol> deducedTypes = null) : base(dc, null, null, deducedTypes) { }

		public override string ToString()
		{
			return "(struct) " + base.ToString();
		}

		public override AbstractType Clone(bool cloneBase)
		{
			return new StructType(Definition, DeducedTypes) { Modifiers = Modifiers };
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
		public UnionType(DClassLike dc, IEnumerable<TemplateParameterSymbol> deducedTypes = null) : base(dc, null, null, deducedTypes) { }

		public override string ToString()
		{
			return "(union) " + base.ToString();
		}

		public override AbstractType Clone(bool cloneBase)
		{
			return new UnionType(Definition, DeducedTypes) { Modifiers = Modifiers };
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
		public ClassType(DClassLike dc, 
			TemplateIntermediateType baseType, InterfaceType[] baseInterfaces = null,
			IEnumerable<TemplateParameterSymbol> deducedTypes = null)
			: base(dc, baseType, baseInterfaces, deducedTypes)
		{}

		public override string ToString()
		{
			return "(class) "+base.ToString();
		}

		public override AbstractType Clone(bool cloneBase)
		{
			return new ClassType(Definition, cloneBase && Base != null ? Base.Clone(true) as TemplateIntermediateType : Base as TemplateIntermediateType, BaseInterfaces, DeducedTypes) { Modifiers = Modifiers };
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
		public InterfaceType(DClassLike dc, 
			InterfaceType[] baseInterfaces=null,
			IEnumerable<TemplateParameterSymbol> deducedTypes = null) 
			: base(dc, null, baseInterfaces, deducedTypes) {}
		
		public override AbstractType Clone(bool cloneBase)
		{
			return new InterfaceType(Definition, BaseInterfaces, DeducedTypes) { Modifiers = Modifiers };
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

		public TemplateType(DClassLike dc, IEnumerable<TemplateParameterSymbol> inheritedTypeParams = null) : base(dc, null, null, inheritedTypeParams) { }

		public override AbstractType Clone(bool cloneBase)
		{
			return new TemplateType(Definition, DeducedTypes) { Modifiers = Modifiers };
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
		public MixinTemplateType(DClassLike dc, IEnumerable<TemplateParameterSymbol> inheritedTypeParams = null) : base(dc, inheritedTypeParams) { }

		public override AbstractType Clone(bool cloneBase)
		{
			return new MixinTemplateType(Definition, DeducedTypes) { Modifiers = Modifiers };
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
		public ISyntaxRegion instanciationSyntax;
		public new DClassLike Definition { get { return base.Definition as DClassLike; } }

		public readonly InterfaceType[] BaseInterfaces;

		public TemplateIntermediateType(DClassLike dc, 
			AbstractType baseType, InterfaceType[] baseInterfaces,
			IEnumerable<TemplateParameterSymbol> deducedTypes)
			: base(dc, baseType, deducedTypes)
		{
			this.BaseInterfaces = baseInterfaces;
		}
	}

	public class EponymousTemplateType : UserDefinedType
	{
		public new EponymousTemplate Definition { get { return base.Definition as EponymousTemplate; } }

		public EponymousTemplateType(EponymousTemplate ep, IEnumerable<TemplateParameterSymbol> deducedTypes = null) : base(ep, null, deducedTypes) { }

		public override string ToString ()
		{
			return "(Eponymous Template Type) "+ Definition;
		}

		public override AbstractType Clone(bool cloneBase)
		{
			return new EponymousTemplateType(Definition, DeducedTypes);
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
		public MemberSymbol(DNode member, AbstractType memberType = null,
			IEnumerable<TemplateParameterSymbol> deducedTypes = null)
			: base(member, memberType, deducedTypes) {
				if (memberType != null)
					memberType.NonStaticAccess = true;
		}

		public override AbstractType Clone(bool cloneBase)
		{
			return new MemberSymbol(Definition, cloneBase && Base != null ? Base.Clone(true) : Base, DeducedTypes) { Modifiers = Modifiers };
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

		public TemplateParameterSymbol(TemplateParameter.Node tpn, ISemantic typeOrValue)
			: base(tpn, AbstractType.Get(typeOrValue))
		{
			IsKnowinglyUndetermined = TemplateInstanceHandler.IsNonFinalArgument(typeOrValue);
			this.Parameter = tpn.TemplateParameter;
			this.ParameterValue = typeOrValue as ISymbolValue;
		}

		public TemplateParameterSymbol(TemplateParameter tpn, ISemantic typeOrValue)
			: base(tpn != null ? tpn.Representation : null, AbstractType.Get(typeOrValue))
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
			return new TemplateParameterSymbol(Parameter, ParameterValue ?? (cloneBase && Base != null ? Base.Clone(true) : Base) as ISemantic) { Modifiers = Modifiers };
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
		public readonly PostfixExpression_ArrayAccess indexExpression;

		public ArrayAccessSymbol(PostfixExpression_ArrayAccess indexExpr, AbstractType arrayValueType):
		base(arrayValueType)	{ this.indexExpression = indexExpr; }

		public override AbstractType Clone(bool cloneBase)
		{
			return new ArrayAccessSymbol(indexExpression, cloneBase && Base != null ? Base.Clone(true) : Base);
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

		public ModuleSymbol(DModule mod, PackageSymbol packageBase = null) : base(mod, packageBase, (IEnumerable<TemplateParameterSymbol>)null) {	}

		public override string ToString()
		{
			return "(module) "+base.ToString();
		}

		public override AbstractType Clone(bool cloneBase)
		{
			return new ModuleSymbol(Definition, cloneBase && Base != null ? Base.Clone(true) as PackageSymbol : Base as PackageSymbol) { Modifiers = Modifiers };
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

		public PackageSymbol(ModulePackage pack) {
			this.Package = pack;
		}

		public override string ToString()
		{
			return "(package) "+base.ToString();
		}

		public override AbstractType Clone(bool cloneBase)
		{
			return new PackageSymbol(Package) { Modifiers = Modifiers };
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

		public DTuple(IEnumerable<ISemantic> items)
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
			return new DTuple(Items) { Modifiers = Modifiers };
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
