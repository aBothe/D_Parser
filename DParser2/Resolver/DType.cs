﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Resolver.Templates;
using D_Parser.Resolver.ExpressionSemantics;

namespace D_Parser.Resolver
{
	public abstract class AbstractType : ISemantic
	{
		#region Properties
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
		public AbstractType() { }
		public AbstractType(ISyntaxRegion DeclarationOrExpressionBase)
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
			if (s is ISymbolValue)
				return (s as ISymbolValue).RepresentedType;
			
			return s as AbstractType;
		}
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
				return base.TypeDeclarationOf ?? (modifier == 0 ? 
				                                  (ITypeDeclaration)new DTokenDeclaration(TypeToken) :
				                                  new MemberFunctionAttributeDecl(modifier){ 
				                                  	InnerType = new DTokenDeclaration(TypeToken)});
			}
		}
	}

	#region Derived data types
	public abstract class DerivedDataType : AbstractType
	{
		public readonly AbstractType Base;

		public DerivedDataType(AbstractType Base, ISyntaxRegion td) : base(td)
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
				return base.TypeDeclarationOf ?? new PointerDecl(Base==null ? null : Base.TypeDeclarationOf);
			}
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
			IsStaticArray = true;
		}

		public override string ToCode()
		{
			return (Base != null ? Base.ToCode() : "") + (IsStaticArray && FixedLength >= 0 ? string.Format("[{0}]",FixedLength) : "[]");
		}
	}

	public class AssocArrayType : DerivedDataType
	{
		public readonly AbstractType KeyType;

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
				return base.TypeDeclarationOf ?? new ArrayDecl { 
					ValueType = ValueType==null ? null : ValueType.TypeDeclarationOf,
					KeyType = KeyType == null ? null : KeyType.TypeDeclarationOf
				};
			}
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

		public DelegateType(AbstractType ReturnType, FunctionLiteral Literal, IEnumerable<AbstractType> Parameters = null)
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
	}
	#endregion

	public abstract class DSymbol : DerivedDataType
	{
		public DNode Definition { get; private set; }

		/// <summary>
		/// Key: Type name
		/// Value: Corresponding type
		/// </summary>
		public ReadOnlyCollection<KeyValuePair<string, TemplateParameterSymbol>> DeducedTypes;


		public string Name
		{
			get
			{
				if (Definition != null)
					return Definition.Name;
				return null;
			}
		}

		public DSymbol(DNode Node, AbstractType BaseType, ReadOnlyCollection<KeyValuePair<string, TemplateParameterSymbol>> deducedTypes, ISyntaxRegion td)
			: base(BaseType, td)
		{
			this.DeducedTypes = deducedTypes;
			this.Definition = Node;
		}

		public DSymbol(DNode Node, AbstractType BaseType, Dictionary<string, TemplateParameterSymbol> deducedTypes, ISyntaxRegion td)
			: base(BaseType, td)
		{
			if(deducedTypes!=null)
				this.DeducedTypes = new ReadOnlyCollection<KeyValuePair<string, TemplateParameterSymbol>>(deducedTypes.ToArray());
			this.Definition = Node;
		}

		public override string ToCode()
		{
			return Definition.ToString(false, true);
		}
	}

	#region User-defined types
	public abstract class UserDefinedType : DSymbol
	{
		public UserDefinedType(DNode Node, AbstractType baseType, ReadOnlyCollection<KeyValuePair<string, TemplateParameterSymbol>> deducedTypes, ISyntaxRegion td) : base(Node, baseType, deducedTypes, td) { }
	}

	public class AliasedType : MemberSymbol
	{
		public new DVariable Definition { get { return base.Definition as DVariable; } }

		public AliasedType(DVariable AliasDefinition, AbstractType Type, ISyntaxRegion td, ReadOnlyCollection<KeyValuePair<string, TemplateParameterSymbol>> deducedTypes=null)
			: base(AliasDefinition,Type, td, deducedTypes) {}

		public override string ToString()
		{
			return "(alias) " + base.ToString();
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
	}

	public class StructType : TemplateIntermediateType
	{
		public StructType(DClassLike dc, ISyntaxRegion td, Dictionary<string, TemplateParameterSymbol> deducedTypes = null) : base(dc, td, null, null, deducedTypes) { }

		public override string ToString()
		{
			return "(struct) " + base.ToString();
		}
	}

	public class UnionType : TemplateIntermediateType
	{
		public UnionType(DClassLike dc, ISyntaxRegion td, Dictionary<string, TemplateParameterSymbol> deducedTypes = null) : base(dc, td, null, null, deducedTypes) { }

		public override string ToString()
		{
			return "(union) " + base.ToString();
		}
	}

	public class ClassType : TemplateIntermediateType
	{
		public ClassType(DClassLike dc, ISyntaxRegion td, 
			TemplateIntermediateType baseType, InterfaceType[] baseInterfaces,
			ReadOnlyCollection<KeyValuePair<string, TemplateParameterSymbol>> deducedTypes)
			: base(dc, td, baseType, baseInterfaces, deducedTypes)
		{}

		public ClassType(DClassLike dc, ISyntaxRegion td, 
			TemplateIntermediateType baseType, InterfaceType[] baseInterfaces = null,
			Dictionary<string, TemplateParameterSymbol> deducedTypes = null)
			: base(dc, td, baseType, baseInterfaces, deducedTypes)
		{}

		public override string ToString()
		{
			return "(class) "+base.ToString();
		}
	}

	public class InterfaceType : TemplateIntermediateType
	{
		public InterfaceType(DClassLike dc, ISyntaxRegion td, 
			InterfaceType[] baseInterfaces=null,
			Dictionary<string, TemplateParameterSymbol> deducedTypes = null) 
			: base(dc, td, null, baseInterfaces, deducedTypes) {}

		public InterfaceType(DClassLike dc, ISyntaxRegion td,
			InterfaceType[] baseInterfaces,
			ReadOnlyCollection<KeyValuePair<string, TemplateParameterSymbol>> deducedTypes)
			: base(dc, td, null, baseInterfaces, deducedTypes) { }
	}

	public class TemplateType : TemplateIntermediateType
	{
		public TemplateType(DClassLike dc, ISyntaxRegion td, Dictionary<string, TemplateParameterSymbol> inheritedTypeParams = null) : base(dc, td, null, null, inheritedTypeParams) { }
		public TemplateType(DClassLike dc, ISyntaxRegion td, ReadOnlyCollection<KeyValuePair<string, TemplateParameterSymbol>> inheritedTypeParams = null) : base(dc, td, null, null, inheritedTypeParams) { }
	}
	
	public class MixinTemplateType : TemplateType
	{
		public MixinTemplateType(DClassLike dc, ISyntaxRegion td, Dictionary<string, TemplateParameterSymbol> inheritedTypeParams = null) : base(dc, td, inheritedTypeParams) { }
		public MixinTemplateType(DClassLike dc, ISyntaxRegion td, ReadOnlyCollection<KeyValuePair<string, TemplateParameterSymbol>> inheritedTypeParams = null) : base(dc, td, inheritedTypeParams) { }
	}

	public class TemplateIntermediateType : UserDefinedType
	{
		public new DClassLike Definition { get { return base.Definition as DClassLike; } }

		public readonly InterfaceType[] BaseInterfaces;

		public TemplateIntermediateType(DClassLike dc, ISyntaxRegion td, 
			AbstractType baseType = null, InterfaceType[] baseInterfaces = null,
			ReadOnlyCollection<KeyValuePair<string, TemplateParameterSymbol>> deducedTypes = null)
			: base(dc, baseType, deducedTypes, td)
		{
			this.BaseInterfaces = baseInterfaces;
		}

		public TemplateIntermediateType(DClassLike dc, ISyntaxRegion td, 
			AbstractType baseType, InterfaceType[] baseInterfaces,
			Dictionary<string, TemplateParameterSymbol> deducedTypes)
			: this(dc,td, baseType,baseInterfaces,
			deducedTypes != null && deducedTypes.Count != 0 ? new ReadOnlyCollection<KeyValuePair<string, TemplateParameterSymbol>>(deducedTypes.ToArray()) : null)
		{ }
	}

	public class StaticProperty : MemberSymbol
	{
		public StaticProperty(string propertyName, string propertyDescription, AbstractType propertyType, INode baseNode, ISyntaxRegion td)
			: base(new DVariable{ 
				Name = propertyName, 
				Description = propertyDescription,
				Type = propertyType.TypeDeclarationOf,
				Parent = baseNode }, propertyType, td)
		{
			
		}
	}

	public class MemberSymbol : DSymbol
	{
		public bool IsUFCSResult;
		public MemberSymbol(DNode member, AbstractType memberType, ISyntaxRegion td,
			ReadOnlyCollection<KeyValuePair<string, TemplateParameterSymbol>> deducedTypes = null)
			: base(member, memberType, deducedTypes, td) { }

		public MemberSymbol(DNode member, AbstractType memberType, ISyntaxRegion td,
			Dictionary<string, TemplateParameterSymbol> deducedTypes)
			: base(member, memberType, deducedTypes, td) { }
	}
	
	public class TemplateParameterSymbol : MemberSymbol
	{
		/// <summary>
		/// Only used for template value parameters.
		/// </summary>
		public readonly ISymbolValue ParameterValue;
		public readonly ITemplateParameter Parameter;

		public TemplateParameterSymbol(TemplateParameterNode tpn, ISemantic typeOrValue, ISyntaxRegion paramIdentifier = null)
			: base(tpn, AbstractType.Get(typeOrValue), paramIdentifier)
		{
			this.Parameter = tpn.TemplateParameter;
			this.ParameterValue = typeOrValue as ISymbolValue;
		}

		public TemplateParameterSymbol(ITemplateParameter tp,
			ISemantic representedTypeOrValue,
			ISyntaxRegion originalParameterIdentifier = null,
			DNode parentNode = null)
			: base(new TemplateParameterNode(tp) { Parent = parentNode },
			AbstractType.Get(representedTypeOrValue), originalParameterIdentifier ?? tp)
		{
			this.Parameter = tp;
			this.ParameterValue = representedTypeOrValue as ISymbolValue;
		}
		
		public override string ToCode()
		{
			if(ParameterValue!=null)
				return ParameterValue.ToCode();
			return Base == null ? Parameter.Name : Base.ToCode(); //FIXME: It's not actually code but currently needed for correct ToString() representation in e.g. parameter insight
		}

		public override string ToString()
		{
			return "<"+Parameter.Name+">"+(ParameterValue!=null ? ParameterValue.ToString() : (Base==null ? "" : Base.ToString()));
		}
	}
	
	/// <summary>
	/// Intermediate result when evaluating e.g. myArray[0]
	/// </summary>
	public class ArrayAccessSymbol : MemberSymbol
	{
		public ArrayAccessSymbol(PostfixExpression_Index indexExpr, AbstractType arrayValueType):
			base(null,arrayValueType,indexExpr)	{ }
	}

	public class ModuleSymbol : DSymbol
	{
		public new DModule Definition { get { return base.Definition as DModule; } }

		public ModuleSymbol(DModule mod, ISyntaxRegion td, PackageSymbol packageBase = null) : base(mod, packageBase, (Dictionary<string, TemplateParameterSymbol>)null, td) { }

		public override string ToString()
		{
			return "(module) "+base.ToString();
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
	}
	#endregion

	/// <summary>
	/// Contains an array of zero or more type definitions.
	/// Used for template parameter-argument deduction.
	/// </summary>
	public class TypeTuple : AbstractType
	{
		public readonly AbstractType[] Items;

		public TypeTuple(ISyntaxRegion td,IEnumerable<AbstractType> items) : base(td)
		{
			if (items is AbstractType[])
				Items = (AbstractType[])items;
			else if (items != null)
				Items = items.ToArray();
		}

		public override string ToCode()
		{
			var s = "";

			if(Items!=null)
				foreach (var i in Items)
					s += i.ToCode() + ",";

			return s.TrimEnd(',');
		}
	}

	public class ExpressionTuple : AbstractType
	{

		public override string ToCode()
		{
			throw new NotImplementedException();
		}
	}
}
