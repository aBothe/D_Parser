using D_Parser.Completion;
using D_Parser.Dom;
using D_Parser.Parser;
using D_Parser.Resolver.ASTScanner;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.TypeResolution;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace D_Parser.Resolver
{
	public static class StaticProperties
	{
		public delegate ISymbolValue ValueGetterHandler(AbstractSymbolValueProvider vp, ISemantic baseValue);

		class StaticPropertyInfo
		{
			public readonly string Name;
			public readonly string Description;
			public readonly ITypeDeclaration OverrideType;
			public bool RequireThis = false;

			public Func<AbstractType, DNode> NodeGetter;
			public Func<AbstractType, ITypeDeclaration> TypeGetter;
			public ValueGetterHandler ValueGetter;

			public StaticPropertyInfo(string name, string desc, string baseTypeId)
			{ Name = name; Description = desc; OverrideType = new IdentifierDeclaration(baseTypeId); }

			public StaticPropertyInfo(string name, string desc, byte primitiveType)
			{ Name = name; Description = desc; OverrideType = new DTokenDeclaration(primitiveType); }

			public StaticPropertyInfo(string name, string desc, ITypeDeclaration overrideType = null)
			{ Name = name; Description = desc; OverrideType = overrideType; }

			public ITypeDeclaration GetPropertyType(AbstractType t)
			{
				return OverrideType ?? (TypeGetter != null && t != null ? TypeGetter(t) : null);
			}

			public static readonly List<DAttribute> StaticAttributeList = new List<DAttribute> { new Modifier(DTokens.Static) };

			public DNode GenerateRepresentativeNode(AbstractType t)
			{
				if (NodeGetter != null)
					return NodeGetter(t);

				return new DVariable()
				{
					Attributes = !RequireThis ? StaticAttributeList : null,
					Name = Name,
					Description = Description,
					Type = GetPropertyType(t)
				};
			}
		}

		#region Properties
		enum PropOwnerType
		{
			None = 0,
			Generic,
			Integral,
			FloatingPoint,
			ClassLike,
			Array,
			AssocArray,
			Delegate,
			TypeTuple,
		}
		#endregion

		#region Constructor/Init
		static Dictionary<PropOwnerType, Dictionary<int, StaticPropertyInfo>> Properties = new Dictionary<PropOwnerType, Dictionary<int, StaticPropertyInfo>>();

		static void AddProp(this Dictionary<int, StaticPropertyInfo> props, StaticPropertyInfo prop)
		{
			props[prop.Name.GetHashCode()] = prop;
		}

		static StaticProperties()
		{
			var props = new Dictionary<int, StaticPropertyInfo>();
			Properties[PropOwnerType.Generic] = props;

			props.AddProp(new StaticPropertyInfo("init", "A type's or variable's static initializer expression") { TypeGetter = help_ReflectType });
			props.AddProp(new StaticPropertyInfo("sizeof", "Size of a type or variable in bytes", "size_t"));
			props.AddProp(new StaticPropertyInfo("alignof", "Variable offset", DTokens.Int) { RequireThis = true });
			props.AddProp(new StaticPropertyInfo("mangleof", "String representing the ‘mangled’ representation of the type", "string"));
			props.AddProp(new StaticPropertyInfo("stringof", "String representing the source representation of the type", "string"));



			props = new Dictionary<int, StaticPropertyInfo>();
			Properties[PropOwnerType.Integral] = props;

			props.AddProp(new StaticPropertyInfo("max", "Maximum value") { TypeGetter = help_ReflectType });
			props.AddProp(new StaticPropertyInfo("min", "Minimum value") { TypeGetter = help_ReflectType });



			props = new Dictionary<int, StaticPropertyInfo>();
			Properties[PropOwnerType.FloatingPoint] = props;

			props.AddProp(new StaticPropertyInfo("infinity", "Infinity value") { TypeGetter = help_ReflectType });
			props.AddProp(new StaticPropertyInfo("nan", "Not-a-Number value") { TypeGetter = help_ReflectType });
			props.AddProp(new StaticPropertyInfo("dig", "Number of decimal digits of precision", DTokens.Int));
			props.AddProp(new StaticPropertyInfo("epsilon", "Smallest increment to the value 1") { TypeGetter = help_ReflectType });
			props.AddProp(new StaticPropertyInfo("mant_dig", "Number of bits in mantissa", DTokens.Int));
			props.AddProp(new StaticPropertyInfo("max_10_exp", "Maximum int value such that 10^max_10_exp is representable", DTokens.Int));
			props.AddProp(new StaticPropertyInfo("max_exp", "Maximum int value such that 2^max_exp-1 is representable", DTokens.Int));
			props.AddProp(new StaticPropertyInfo("min_10_exp", "Minimum int value such that 10^max_10_exp is representable", DTokens.Int));
			props.AddProp(new StaticPropertyInfo("min_exp", "Minimum int value such that 2^max_exp-1 is representable", DTokens.Int));
			props.AddProp(new StaticPropertyInfo("min_normal", "Number of decimal digits of precision", DTokens.Int));
			props.AddProp(new StaticPropertyInfo("re", "Real part") { TypeGetter = help_ReflectType, RequireThis = true });
			props.AddProp(new StaticPropertyInfo("in", "Imaginary part") { TypeGetter = help_ReflectType, RequireThis = true });



			props = new Dictionary<int, StaticPropertyInfo>();
			Properties[PropOwnerType.Array] = props;

			props.AddProp(new StaticPropertyInfo("length", "Array length", "size_t") { 
				RequireThis = true,
			ValueGetter = 
				(vp, v) => {
					var av = v as ArrayValue;
					return new PrimitiveValue(DTokens.Int, av.Elements != null ? av.Elements.Length : 0, null, 0m); 
				}});

			props.AddProp(new StaticPropertyInfo("dup", "Create a dynamic array of the same size and copy the contents of the array into it.") { TypeGetter = help_ReflectType, RequireThis = true });
			props.AddProp(new StaticPropertyInfo("idup", "D2.0 only! Creates immutable copy of the array") { TypeGetter = (t) => new MemberFunctionAttributeDecl(DTokens.Immutable) { InnerType = help_ReflectType(t) }, RequireThis = true });
			props.AddProp(new StaticPropertyInfo("reverse", "Reverses in place the order of the elements in the array. Returns the array.") { TypeGetter = help_ReflectType, RequireThis = true });
			props.AddProp(new StaticPropertyInfo("sort", "Sorts in place the order of the elements in the array. Returns the array.") { TypeGetter = help_ReflectType, RequireThis = true });
			props.AddProp(new StaticPropertyInfo("ptr", "Returns pointer to the array") { TypeGetter = (t) => new PointerDecl(t.TypeDeclarationOf), RequireThis = true });



			props = new Dictionary<int, StaticPropertyInfo>(props); // Copy from arrays' properties!
			Properties[PropOwnerType.AssocArray] = props;
			
			props.AddProp(new StaticPropertyInfo("length", "Returns number of values in the associative array. Unlike for dynamic arrays, it is read-only.", "size_t") { RequireThis = true });
			props.AddProp(new StaticPropertyInfo("keys", "Returns dynamic array, the elements of which are the keys in the associative array.") { TypeGetter = (t) => new ArrayDecl { ValueType = (t as AssocArrayType).KeyType.TypeDeclarationOf }, RequireThis = true });
			props.AddProp(new StaticPropertyInfo("values", "Returns dynamic array, the elements of which are the values in the associative array.") { TypeGetter = (t) => new ArrayDecl { ValueType = (t as AssocArrayType).ValueType.TypeDeclarationOf }, RequireThis = true });
			props.AddProp(new StaticPropertyInfo("rehash", "Reorganizes the associative array in place so that lookups are more efficient. rehash is effective when, for example, the program is done loading up a symbol table and now needs fast lookups in it. Returns a reference to the reorganized array.") { TypeGetter = help_ReflectType, RequireThis = true });
			props.AddProp(new StaticPropertyInfo("byKey", "Returns a delegate suitable for use as an Aggregate to a ForeachStatement which will iterate over the keys of the associative array.") { TypeGetter = (t) => new DelegateDeclaration() { ReturnType = new ArrayDecl() { ValueType = (t as AssocArrayType).KeyType.TypeDeclarationOf } }, RequireThis = true });
			props.AddProp(new StaticPropertyInfo("byValue", "Returns a delegate suitable for use as an Aggregate to a ForeachStatement which will iterate over the values of the associative array.") { TypeGetter = (t) => new DelegateDeclaration() { ReturnType = new ArrayDecl() { ValueType = (t as AssocArrayType).ValueType.TypeDeclarationOf } }, RequireThis = true });
			props.AddProp(new StaticPropertyInfo("get", null)
			{
				RequireThis = true,
				NodeGetter = (t) =>{
					var ad = t as AssocArrayType;
					var valueType = ad.ValueType.TypeDeclarationOf;
					return new DMethod()
					{
						Name = "get",
						Description = "Looks up key; if it exists returns corresponding value else evaluates and returns defaultValue.",
						Type = valueType,
						Parameters = new List<INode> {
							new DVariable(){
								Name="key",
								Type=ad.KeyType.TypeDeclarationOf
							},
							new DVariable(){
								Name="defaultValue",
								Type=valueType,
								Attributes=new List<DAttribute>{ new Modifier(DTokens.Lazy)}
							}
						}
					};
				}
			});
			props.AddProp(new StaticPropertyInfo("remove", null) {
				RequireThis = true,
				NodeGetter = (t) => new DMethod
				{
					Name = "remove",
					Description = "remove(key) does nothing if the given key does not exist and returns false. If the given key does exist, it removes it from the AA and returns true.",
					Type = new DTokenDeclaration(DTokens.Bool),
					Parameters = new List<INode> { 
						new DVariable{ Name = "key", Type = (t as AssocArrayType).KeyType.TypeDeclarationOf }
					}
				}
			});


			props = new Dictionary<int, StaticPropertyInfo>();
			Properties[PropOwnerType.TypeTuple] = props;

			props.AddProp(new StaticPropertyInfo("length", "Returns number of values in the type tuple.", "size_t") { 
				RequireThis = true,
				ValueGetter = 
				(vp, v) => {
					var tt = v as DTuple;
					if (tt == null && v is TypeValue)
						tt = (v as TypeValue).RepresentedType as DTuple;
					return tt != null ? new PrimitiveValue(DTokens.Int, tt.Items == null ? 0m : (decimal)tt.Items.Length, null, 0m) : null; 
				} });




			props = new Dictionary<int, StaticPropertyInfo>();
			Properties[PropOwnerType.Delegate] = props;


			props.AddProp(new StaticPropertyInfo("ptr", "The .ptr property of a delegate will return the frame pointer value as a void*.",
				(ITypeDeclaration)new PointerDecl(new DTokenDeclaration(DTokens.Void))) { RequireThis = true });
			props.AddProp(new StaticPropertyInfo("funcptr", "The .funcptr property of a delegate will return the function pointer value as a function type.") { RequireThis = true });




			props = new Dictionary<int, StaticPropertyInfo>();
			Properties[PropOwnerType.ClassLike] = props;

			props.AddProp(new StaticPropertyInfo("classinfo", "Information about the dynamic type of the class", (ITypeDeclaration)new IdentifierDeclaration("TypeInfo_Class") { ExpressesVariableAccess = true, InnerDeclaration = new IdentifierDeclaration("object") }) { RequireThis = true });
		}
		#endregion

		#region Static prop resolution meta helpers
		static ITypeDeclaration help_ReflectType(AbstractType t)
		{
			return t.TypeDeclarationOf;
		}
		#endregion

		#region I/O
		static PropOwnerType GetOwnerType(AbstractType t)
		{
			if (t is ArrayType)
				return PropOwnerType.Array;
			else if (t is AssocArrayType)
				return PropOwnerType.AssocArray;
			else if (t is DelegateType)
				return PropOwnerType.Delegate;
			else if (t is PrimitiveType)
			{
				var tk = (t as PrimitiveType).TypeToken;
				if (DTokens.BasicTypes_Integral[tk])
					return PropOwnerType.Integral;
				if (DTokens.BasicTypes_FloatingPoint[tk])
					return PropOwnerType.FloatingPoint;
			}
			else if (t is ClassType || t is InterfaceType || t is TemplateType || t is StructType)
				return PropOwnerType.ClassLike;
			else if (t is DTuple)
				return PropOwnerType.TypeTuple;
			return PropOwnerType.None;
		}

		static PropOwnerType GetOwnerType(ISemantic t)
		{
			if (t is ArrayValue)
				return PropOwnerType.Array;
			else if (t is AssociativeArrayValue)
				return PropOwnerType.AssocArray;
			else if (t is DelegateValue)
				return PropOwnerType.Delegate;
			else if (t is PrimitiveValue)
			{
				var tk = (t as PrimitiveValue).BaseTypeToken;
				if (DTokens.BasicTypes_Integral[tk])
					return PropOwnerType.Integral;
				if (DTokens.BasicTypes_FloatingPoint[tk])
					return PropOwnerType.FloatingPoint;
			}
			else if (t is InstanceValue)
				return PropOwnerType.ClassLike;
			else if (t is TypeValue && (t as TypeValue).RepresentedType is DTuple)
				return PropOwnerType.TypeTuple;
			return PropOwnerType.None;
		}

		public static void ListProperties(ICompletionDataGenerator gen, MemberFilter vis, AbstractType t, bool isVariableInstance)
		{
			foreach (var n in ListProperties(t, !isVariableInstance))
				if (AbstractVisitor.CanAddMemberOfType(vis, n))
					gen.Add(n);
		}

		public static IEnumerable<DNode> ListProperties(AbstractType t, bool staticOnly = false)
		{
			if (t is PointerType)
				t = (t as PointerType).Base;

			t = DResolver.StripMemberSymbols(t);

			if (t is PointerType)
				t = (t as PointerType).Base;

			if (t == null)
				yield break;

			var props = Properties[PropOwnerType.Generic];

			foreach (var kv in props)
				if(!staticOnly || !kv.Value.RequireThis)
				yield return kv.Value.GenerateRepresentativeNode(t);

			if (Properties.TryGetValue(GetOwnerType(t), out props))
				foreach (var kv in props)
					if (!staticOnly || !kv.Value.RequireThis)
						yield return kv.Value.GenerateRepresentativeNode(t);
		}

		public static StaticProperty TryEvalPropertyType(ResolutionContext ctxt, AbstractType t, int propName, bool staticOnly = false)
		{
			if (t is PointerType)
				t = (t as PointerType).Base;

			t = DResolver.StripMemberSymbols(t);

			if (t is PointerType)
				t = (t as PointerType).Base;

			if (t == null)
				return null;

			var props = Properties[PropOwnerType.Generic];
			StaticPropertyInfo prop;

			if (props.TryGetValue(propName, out prop) || (Properties.TryGetValue(GetOwnerType(t), out props) && props.TryGetValue(propName, out prop)))
			{
				var n = prop.GenerateRepresentativeNode(t);
				return new StaticProperty(n, n.Type != null ? TypeDeclarationResolver.ResolveSingle(n.Type, ctxt) : null, prop.ValueGetter);
			}

			return null;
		}

		public static ISymbolValue TryEvalPropertyValue(AbstractSymbolValueProvider vp, ISemantic baseSymbol, int propName)
		{
			var props = Properties[PropOwnerType.Generic];
			StaticPropertyInfo prop;

			if (props.TryGetValue(propName, out prop) || (Properties.TryGetValue(GetOwnerType(baseSymbol), out props) && props.TryGetValue(propName, out prop)))
			{
				if (prop.ValueGetter != null)
					return prop.ValueGetter(vp, baseSymbol);
			}

			return null;
		}
		#endregion
	}
}
