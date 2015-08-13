using D_Parser.Completion;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Resolver.ASTScanner;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.TypeResolution;
using System;
using System.Collections.Generic;
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

			public Func<AbstractType, ResolutionContext, DNode> NodeGetter;
			public Func<AbstractType, ResolutionContext, ITypeDeclaration> TypeGetter;
			public Func<AbstractType, ResolutionContext, AbstractType> ResolvedBaseTypeGetter;
			public ValueGetterHandler ValueGetter;

			public StaticPropertyInfo(string name, string desc, string baseTypeId)
			{ Name = name; Description = desc; OverrideType = new IdentifierDeclaration(baseTypeId); }

			public StaticPropertyInfo(string name, string desc, byte primitiveType)
			{ 
				Name = name;
				Description = desc; 
				OverrideType = new DTokenDeclaration(primitiveType); 
				ResolvedBaseTypeGetter = (t,ctxt) => new PrimitiveType(primitiveType) { NonStaticAccess = RequireThis }; 
			}

			public StaticPropertyInfo(string name, string desc, ITypeDeclaration overrideType = null)
			{ Name = name; Description = desc; OverrideType = overrideType; }

			public ITypeDeclaration GetPropertyType(AbstractType t, ResolutionContext ctxt)
			{
				return OverrideType ?? (TypeGetter != null && t != null ? TypeGetter(t,ctxt) : null);
			}

			public static readonly List<DAttribute> StaticAttributeList = new List<DAttribute> { new Modifier(DTokens.Static) };

			public DNode GenerateRepresentativeNode(AbstractType t, ResolutionContext ctxt)
			{
				if (NodeGetter != null)
					return NodeGetter(t, ctxt);

				return new DVariable()
				{
					Attributes = !RequireThis ? StaticAttributeList : null,
					Name = Name,
					Description = Description,
					Type = GetPropertyType(t, ctxt)
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
			Struct,
			StructElement
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

			props.AddProp(new StaticPropertyInfo("init", "A type's or variable's static initializer expression") { TypeGetter = help_ReflectType, ResolvedBaseTypeGetter = help_ReflectResolvedType });
			props.AddProp(new StaticPropertyInfo("sizeof", "Size of a type or variable in bytes", DTokens.Uint)); // Do not define it as size_t due to unnecessary recursive definition as typeof(int.sizeof)
			props.AddProp(new StaticPropertyInfo("alignof", "Variable offset", DTokens.Uint) { RequireThis = true });
			props.AddProp(new StaticPropertyInfo("mangleof", "String representing the ‘mangled’ representation of the type", "string"));
			props.AddProp(new StaticPropertyInfo("stringof", "String representing the source representation of the type", "string") { 
				ValueGetter = (vp, v) => {
					var t = AbstractType.Get(v);
					if(t == null)
						return new NullValue();
					return new ArrayValue(Evaluation.GetStringType(vp.ResolutionContext), (t is DSymbol) ? DNode.GetNodePath((t as DSymbol).Definition,true) : t.ToCode());
				}
			});



			props = new Dictionary<int, StaticPropertyInfo>();
			Properties[PropOwnerType.Integral] = props;

			props.AddProp(new StaticPropertyInfo("max", "Maximum value") { TypeGetter = help_ReflectType, ResolvedBaseTypeGetter = help_ReflectResolvedType });
			props.AddProp(new StaticPropertyInfo("min", "Minimum value") { TypeGetter = help_ReflectType, ResolvedBaseTypeGetter = help_ReflectResolvedType });



			props = new Dictionary<int, StaticPropertyInfo>();
			Properties[PropOwnerType.FloatingPoint] = props;

			props.AddProp(new StaticPropertyInfo("infinity", "Infinity value") { TypeGetter = help_ReflectType, ResolvedBaseTypeGetter = help_ReflectResolvedType });
			props.AddProp(new StaticPropertyInfo("nan", "Not-a-Number value") { TypeGetter = help_ReflectType, ResolvedBaseTypeGetter = help_ReflectResolvedType });
			props.AddProp(new StaticPropertyInfo("dig", "Number of decimal digits of precision", DTokens.Int));
			props.AddProp(new StaticPropertyInfo("epsilon", "Smallest increment to the value 1") { TypeGetter = help_ReflectType, ResolvedBaseTypeGetter = help_ReflectResolvedType });
			props.AddProp(new StaticPropertyInfo("mant_dig", "Number of bits in mantissa", DTokens.Int));
			props.AddProp(new StaticPropertyInfo("max_10_exp", "Maximum int value such that 10^max_10_exp is representable", DTokens.Int));
			props.AddProp(new StaticPropertyInfo("max_exp", "Maximum int value such that 2^max_exp-1 is representable", DTokens.Int));
			props.AddProp(new StaticPropertyInfo("min_10_exp", "Minimum int value such that 10^max_10_exp is representable", DTokens.Int));
			props.AddProp(new StaticPropertyInfo("min_exp", "Minimum int value such that 2^max_exp-1 is representable", DTokens.Int));
			props.AddProp(new StaticPropertyInfo("min_normal", "Number of decimal digits of precision", DTokens.Int));
			props.AddProp(new StaticPropertyInfo("re", "Real part") { TypeGetter = help_ReflectNonComplexType, ResolvedBaseTypeGetter = help_ReflectResolvedNonComplexType, RequireThis = true });
			props.AddProp(new StaticPropertyInfo("im", "Imaginary part") { TypeGetter = help_ReflectNonComplexType, ResolvedBaseTypeGetter = help_ReflectResolvedNonComplexType, RequireThis = true });



			props = new Dictionary<int, StaticPropertyInfo>();
			Properties[PropOwnerType.Array] = props;

			props.AddProp(new StaticPropertyInfo("length", "Array length", DTokens.Int) { 
				RequireThis = true,
			ValueGetter = 
				(vp, v) => {
					var av = v as ArrayValue;
					return new PrimitiveValue(av.Elements != null ? av.Elements.Length : 0); 
				}});

			props.AddProp(new StaticPropertyInfo("dup", "Create a dynamic array of the same size and copy the contents of the array into it.") { TypeGetter = help_ReflectType, ResolvedBaseTypeGetter = help_ReflectResolvedType, RequireThis = true });
			props.AddProp(new StaticPropertyInfo("idup", "D2.0 only! Creates immutable copy of the array") { TypeGetter = (t,ctxt) => new MemberFunctionAttributeDecl (DTokens.Immutable) { InnerType = help_ReflectType (t,ctxt) }, RequireThis = true });
			props.AddProp(new StaticPropertyInfo("reverse", "Reverses in place the order of the elements in the array. Returns the array.") { TypeGetter = help_ReflectType, ResolvedBaseTypeGetter = help_ReflectResolvedType, RequireThis = true });
			props.AddProp(new StaticPropertyInfo("sort", "Sorts in place the order of the elements in the array. Returns the array.") { TypeGetter = help_ReflectType, ResolvedBaseTypeGetter = help_ReflectResolvedType, RequireThis = true });
			props.AddProp(new StaticPropertyInfo("ptr", "Returns pointer to the array") {
				ResolvedBaseTypeGetter = (t, ctxt) => new PointerType((t as DerivedDataType).Base),
				TypeGetter = (t, ctxt) => new PointerDecl(DTypeToTypeDeclVisitor.GenerateTypeDecl((t as DerivedDataType).Base)), 
				RequireThis = true 
			});



			props = new Dictionary<int, StaticPropertyInfo>(props); // Copy from arrays' properties!
			Properties[PropOwnerType.AssocArray] = props;
			
			props.AddProp(new StaticPropertyInfo("length", "Returns number of values in the associative array. Unlike for dynamic arrays, it is read-only.", "size_t") { RequireThis = true });
			props.AddProp(new StaticPropertyInfo("keys", "Returns dynamic array, the elements of which are the keys in the associative array.") { TypeGetter = (t, ctxt) => new ArrayDecl { ValueType = DTypeToTypeDeclVisitor.GenerateTypeDecl((t as AssocArrayType).KeyType) }, RequireThis = true });
			props.AddProp(new StaticPropertyInfo("values", "Returns dynamic array, the elements of which are the values in the associative array.") { TypeGetter = (t, ctxt) => new ArrayDecl { ValueType = DTypeToTypeDeclVisitor.GenerateTypeDecl((t as AssocArrayType).ValueType) }, RequireThis = true });
			props.AddProp(new StaticPropertyInfo("rehash", "Reorganizes the associative array in place so that lookups are more efficient. rehash is effective when, for example, the program is done loading up a symbol table and now needs fast lookups in it. Returns a reference to the reorganized array.") { TypeGetter = help_ReflectType, ResolvedBaseTypeGetter = help_ReflectResolvedType, RequireThis = true });
			props.AddProp(new StaticPropertyInfo("byKey", "Returns a delegate suitable for use as an Aggregate to a ForeachStatement which will iterate over the keys of the associative array.") { TypeGetter = (t, ctxt) => new DelegateDeclaration() { ReturnType = new ArrayDecl() { ValueType = DTypeToTypeDeclVisitor.GenerateTypeDecl((t as AssocArrayType).KeyType) } }, RequireThis = true });
			props.AddProp(new StaticPropertyInfo("byValue", "Returns a delegate suitable for use as an Aggregate to a ForeachStatement which will iterate over the values of the associative array.") { TypeGetter = (t, ctxt) => new DelegateDeclaration() { ReturnType = new ArrayDecl() { ValueType = DTypeToTypeDeclVisitor.GenerateTypeDecl((t as AssocArrayType).ValueType) } }, RequireThis = true });
			props.AddProp(new StaticPropertyInfo("get", null)
			{
				RequireThis = true,
				NodeGetter = (t, ctxt) =>
				{
					var ad = t as AssocArrayType;
					var valueType = DTypeToTypeDeclVisitor.GenerateTypeDecl(ad.ValueType);
					var dm = new DMethod () {
						Name = "get",
						Description = "Looks up key; if it exists returns corresponding value else evaluates and returns defaultValue.",
						Type = valueType
					};
					dm.Parameters.Add(new DVariable () {
						Name = "key",
						Type = DTypeToTypeDeclVisitor.GenerateTypeDecl(ad.KeyType)
					});
					dm.Parameters.Add(new DVariable () {
						Name = "defaultValue",
						Type = valueType,
						Attributes = new List<DAttribute>{ new Modifier (DTokens.Lazy) }
					});
					return dm;
				}
			});
			props.AddProp(new StaticPropertyInfo("remove", null) {
				RequireThis = true,
				NodeGetter = (t, ctxt) => { 
					var dm =new DMethod	{
						Name = "remove",
						Description = "remove(key) does nothing if the given key does not exist and returns false. If the given key does exist, it removes it from the AA and returns true.",
						Type = new DTokenDeclaration (DTokens.Bool)
					};
					dm.Parameters.Add(new DVariable {
						Name = "key",
						Type = DTypeToTypeDeclVisitor.GenerateTypeDecl((t as AssocArrayType).KeyType)
					});
					return dm;
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
					return tt != null ? new PrimitiveValue(tt.Items == null ? 0 : tt.Items.Length) : null; 
				} });




			props = new Dictionary<int, StaticPropertyInfo>();
			Properties[PropOwnerType.Delegate] = props;


			props.AddProp(new StaticPropertyInfo("ptr", "The .ptr property of a delegate will return the frame pointer value as a void*.",
				(ITypeDeclaration)new PointerDecl(new DTokenDeclaration(DTokens.Void))) { RequireThis = true });
			props.AddProp(new StaticPropertyInfo("funcptr", "The .funcptr property of a delegate will return the function pointer value as a function type.") { RequireThis = true });




			props = new Dictionary<int, StaticPropertyInfo>();
			Properties[PropOwnerType.ClassLike] = props;

			props.AddProp(new StaticPropertyInfo("classinfo", "Information about the dynamic type of the class", (ITypeDeclaration)new IdentifierDeclaration("TypeInfo_Class") { ExpressesVariableAccess = true, InnerDeclaration = new IdentifierDeclaration("object") }) { RequireThis = true });

			props = new Dictionary<int, StaticPropertyInfo>();
			Properties[PropOwnerType.Struct] = props;

			props.AddProp(new StaticPropertyInfo("sizeof", "Size in bytes of struct", DTokens.Uint));
			props.AddProp(new StaticPropertyInfo("alignof", "Size boundary struct needs to be aligned on", DTokens.Uint));
			props.AddProp(new StaticPropertyInfo("tupleof", "Gets type tuple of fields")
			{
				TypeGetter = (t, ctxt) =>
				{
					var members = GetStructMembers(t as StructType, ctxt);
					var l = new List<IExpression>();

					var vis = new DTypeToTypeDeclVisitor();
					foreach (var member in members)
					{
						var mt = DResolver.StripMemberSymbols(member);
						if(mt == null){
							l.Add(null);
							continue;
						}
						var td = mt.Accept(vis);
						if (td == null)
						{
							l.Add(null);
							continue;
						}
						l.Add(td as IExpression ?? new TypeDeclarationExpression(td));
					}

					return new TemplateInstanceExpression(new IdentifierDeclaration("Tuple")) { Arguments =  l.ToArray() };
				},

				ResolvedBaseTypeGetter = (t, ctxt) =>
				{
					var members = GetStructMembers(t as StructType, ctxt);
					var tupleItems = new List<ISemantic>();

					foreach (var member in members)
					{
						var mt = DResolver.StripMemberSymbols(member);
						if (mt != null)
							tupleItems.Add(mt);
					}

					return new DTuple(tupleItems);
				}
			});

		}
		#endregion

		#region Static prop resolution meta helpers
		/// <summary>
		/// returns float/double/creal when cfloat/cdouble/creal are given
		/// </summary>
		/// <returns>The reflect non complex type.</returns>
		/// <param name="t">T.</param>
		/// <param name="ctxt">Ctxt.</param>
		static ITypeDeclaration help_ReflectNonComplexType(AbstractType t, ResolutionContext ctxt)
		{
			var pt = t as PrimitiveType;
			if (pt != null) {
				switch (pt.TypeToken) {
					case DTokens.Cfloat:
						return new DTokenDeclaration (DTokens.Float);
					case DTokens.Cdouble:
						return new DTokenDeclaration (DTokens.Double);
					case DTokens.Creal:
						return new DTokenDeclaration (DTokens.Real);
				}
			}
			return DTypeToTypeDeclVisitor.GenerateTypeDecl(t);
		}

		static AbstractType help_ReflectResolvedNonComplexType(AbstractType t, ResolutionContext ctxt)
		{
			var pt = t as PrimitiveType;
			if (pt != null) {
				switch (pt.TypeToken) {
					case DTokens.Cfloat:
						return new PrimitiveType (DTokens.Float);
					case DTokens.Cdouble:
						return new PrimitiveType (DTokens.Double);
					case DTokens.Creal:
						return new PrimitiveType (DTokens.Real);
				}
			}
			return t;
		}

		static ITypeDeclaration help_ReflectType(AbstractType t, ResolutionContext ctxt)
		{
			return DTypeToTypeDeclVisitor.GenerateTypeDecl(t);
		}

		static AbstractType help_ReflectResolvedType(AbstractType t, ResolutionContext ctxt)
		{
			return t;
		}

		[ThreadStatic]
		static DClassLike lastStructHandled;
		[ThreadStatic]
		static IEnumerable<AbstractType> lastStructMembersEnlisted;
		static IEnumerable<AbstractType> GetStructMembers(StructType t, ResolutionContext ctxt)
		{
			if(lastStructMembersEnlisted == null ||
				lastStructHandled != t.Definition)
			{
				lastStructHandled = t.Definition;
				var children = ItemEnumeration.EnumChildren(t, ctxt, MemberFilter.Variables);
				lastStructMembersEnlisted = TypeDeclarationResolver.HandleNodeMatches(children, ctxt, t);
			}
			
			return lastStructMembersEnlisted;
		}
		#endregion

		#region I/O
		static PropOwnerType GetOwnerType(ISemantic t)
		{
			if (t is TypeValue)
				t = (t as TypeValue).RepresentedType;

			if (t is ArrayValue || t is ArrayType)
				return PropOwnerType.Array;
			else if (t is AssociativeArrayValue || t is AssocArrayType)
				return PropOwnerType.AssocArray;
			else if (t is DelegateValue || t is DelegateType)
				return PropOwnerType.Delegate;
			else if (t is PrimitiveValue || t is PrimitiveType)
			{
				var tk = t is PrimitiveType ? (t as PrimitiveType).TypeToken : (t as PrimitiveValue).BaseTypeToken;
				if (DTokens.IsBasicType_Integral(tk))
					return PropOwnerType.Integral;
				if (DTokens.IsBasicType_FloatingPoint(tk))
					return PropOwnerType.FloatingPoint;
			}
			else if (t is InstanceValue || t is ClassType || t is InterfaceType || t is TemplateType)
				return PropOwnerType.ClassLike;
			else if (t is StructType)
				return PropOwnerType.Struct;
			else if (t is DTuple)
				return PropOwnerType.TypeTuple;
			else if (t is TemplateParameterSymbol)
			{
				var tps = t as TemplateParameterSymbol;
				if (tps != null &&
					(tps.Parameter is TemplateThisParameter ?
						(tps.Parameter as TemplateThisParameter).FollowParameter : tps.Parameter) is TemplateTupleParameter)
					return PropOwnerType.TypeTuple;
			}
			else if (t is MemberSymbol)
			{
				var ms = (t as MemberSymbol).Definition.Parent as DClassLike;
				if (ms != null && ms.ClassType == DTokens.Struct)
					return PropOwnerType.StructElement;
			}
			return PropOwnerType.None;
		}

		public static void ListProperties(ICompletionDataGenerator gen, ResolutionContext ctxt, MemberFilter vis, AbstractType t, bool isVariableInstance)
		{
			foreach (var n in ListProperties(t, ctxt, !isVariableInstance))
				if (AbstractVisitor.CanAddMemberOfType(vis, n))
					gen.Add(n);
		}

		static void GetLookedUpType(ref AbstractType t)
		{
			while (t is AliasedType || t is PointerType)
				t = (t as DerivedDataType).Base;

			var tps = t as TemplateParameterSymbol;
			if (tps != null && tps.Base == null && 
				(tps.Parameter is TemplateThisParameter ? (tps.Parameter as TemplateThisParameter).FollowParameter : tps.Parameter) is TemplateTupleParameter)
				return;
			else
				t = DResolver.StripMemberSymbols(t);

			while (t is AliasedType || t is PointerType)
				t = (t as DerivedDataType).Base;
		}

		public static IEnumerable<DNode> ListProperties(AbstractType t, ResolutionContext ctxt, bool staticOnly = false)
		{
			GetLookedUpType (ref t);

			if (t == null)
				yield break;

			var props = Properties[PropOwnerType.Generic];

			foreach (var kv in props)
				if(!staticOnly || !kv.Value.RequireThis)
					yield return kv.Value.GenerateRepresentativeNode(t, ctxt);

			if (Properties.TryGetValue(GetOwnerType(t), out props))
				foreach (var kv in props)
					if (!staticOnly || !kv.Value.RequireThis)
						yield return kv.Value.GenerateRepresentativeNode(t, ctxt);
		}

		public static StaticProperty TryEvalPropertyType(ResolutionContext ctxt, AbstractType t, int propName, bool staticOnly = false)
		{
			GetLookedUpType (ref t);

			if (t == null)
				return null;

			var props = Properties[PropOwnerType.Generic];
			StaticPropertyInfo prop;

			if (props.TryGetValue(propName, out prop) || (Properties.TryGetValue(GetOwnerType(t), out props) && props.TryGetValue(propName, out prop)))
			{
				var n = prop.GenerateRepresentativeNode(t, ctxt);

				AbstractType baseType;
				if (prop.ResolvedBaseTypeGetter != null)
					baseType = prop.ResolvedBaseTypeGetter(t, ctxt);
				else if (n.Type != null)
					baseType = TypeDeclarationResolver.ResolveSingle(n.Type, ctxt);
				else
					baseType = null;

				return new StaticProperty(n, baseType, prop.ValueGetter);
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
