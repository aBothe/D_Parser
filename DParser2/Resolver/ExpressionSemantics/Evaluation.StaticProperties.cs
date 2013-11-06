using System;
using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Misc;
using D_Parser.Parser;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public class StaticPropertyResolver
	{
		const int init = 3237136;//"init".GetHashCode ();
		const int _sizeof = -901910120;//"sizeof".GetHashCode ();
		const int length = -1106363674;//"length".GetHashCode ();
		const int ptr = 111342;//"ptr".GetHashCode ();
		const int dup = 99839;//"dup".GetHashCode ();
		const int idup = 3227894;//"idup".GetHashCode ();
		const int reverse = 1099846370;//"reverse".GetHashCode ();
		const int sort = 3536286;//"sort".GetHashCode ();
		const int keys = 3288564;//"keys".GetHashCode ();
		const int values = -823812830;//"values".GetHashCode ();
		const int rehash = -934773311;//"rehash".GetHashCode ();
		const int byKey = 94185096;//"byKey".GetHashCode ();
		const int byValue = 327594842;//"byValue".GetHashCode ();
		const int _get = 102230;//"get".GetHashCode ();
		const int funcptr = -509646422;//"funcptr".GetHashCode ();
		const int min = 108114;
		const int max = 107876;
		const int infinity = 173173288;
		const int nan = 108827;
		const int dig = 99458;
		const int epsilon = -1535503510;
		const int mant_dig = 140573981;
		const int max_10_exp = -627124424;
		const int max_exp = 844434626;
		const int min_10_exp = 1844874890;
		const int min_exp = 1064232624;
		const int min_normal = -644959372;
		const int re = 3635;
		const int im = 3364;
		const int alignof = -914360420;
		const int stringof = 1795009960;
		const int mangleof = 128956637;
		const int classinfo = -9072090;

		public static ISemantic TryResolveStaticProperties (
			ISemantic InitialResult, 
			string propertyIdentifier, 
			ResolutionContext ctxt = null, 
			bool Evaluate = false,
			IdentifierDeclaration idContainter = null)
		{
			return TryResolveStaticProperties (InitialResult, propertyIdentifier.GetHashCode (), ctxt, Evaluate, idContainter);
		}

		/// <summary>
		/// Tries to resolve a static property's name.
		/// Returns a result describing the theoretical member (".init"-%gt;MemberResult; ".typeof"-&gt;TypeResult etc).
		/// Returns null if nothing was found.
		/// </summary>
		/// <param name="InitialResult"></param>
		/// <returns></returns>
		public static ISemantic TryResolveStaticProperties (
			ISemantic InitialResult, 
			int propertyIdentifierHash, 
			ResolutionContext ctxt = null, 
			bool Evaluate = false,
			IdentifierDeclaration idContainter = null)
		{
			INode relatedNode = null;

			// If a pointer'ed type is given, take its base type
			if (InitialResult is PointerType)
				InitialResult = ((PointerType)InitialResult).Base;

			if (InitialResult == null || InitialResult is ModuleSymbol)
				return null;

			if (!Evaluate && InitialResult is ISymbolValue)
				InitialResult = AbstractType.Get (InitialResult);

			if (InitialResult is MemberSymbol) {
				relatedNode = ((MemberSymbol)InitialResult).Definition;
				InitialResult = DResolver.StripMemberSymbols ((AbstractType)InitialResult);
			}

			var val = InitialResult as ISymbolValue;
			
			if (InitialResult is ISymbolValue)
				InitialResult = (InitialResult as ISymbolValue).RepresentedType;

			/*
			 * Parameter configurations:
			 *	InitialResult is AbstractType:
			 *	{
			 *		if(eval){
			 *		-- Let this remain open
			 *		}
			 *		else{}
			 *	}
			 *	else if(InitialResult is ISymbolValue)
			 *	{
			 *		if(eval) {}
			 *		else {}
			 *	}
			 */

			StaticProperty prop = null;

			#region AbstractTypes
			if (InitialResult is AbstractType) {
				if (InitialResult is ArrayType) {
					var at = (ArrayType)InitialResult;
					switch (propertyIdentifierHash) {
						case init:
							prop = new StaticProperty ("init",
							                          at.IsStaticArray ? "Returns an array literal with each element of the literal being the .init property of the array element type." : "Returns null.",
							                          at, relatedNode, idContainter);
							//TODO
							break;
						case _sizeof:
							prop = new StaticProperty ("sizeof", 
							                          "Returns the array length multiplied by the number of bytes per array element.",
							                          ctxt.ParseCache.SizeT, relatedNode, idContainter);
							
							break;
						case length:
							if (Evaluate && val is ArrayValue) {
								var av = val as ArrayValue;
								return new PrimitiveValue (DTokens.Int, av.IsString ? av.StringValue.Length : av.Elements.Length, null);
							}
							prop = new StaticProperty ("length", 
							                          "Returns the number of elements in the array. This is a fixed quantity for static arrays.",
							                          ctxt.ParseCache.SizeT, relatedNode, idContainter);
							break;
						case ptr:
							if (!Evaluate) {
								return new StaticProperty ("ptr", 
								                          "Returns a pointer to the first element of the array.",
								                          new PointerType (at.ValueType, idContainter), relatedNode, idContainter);
							}
							break;
						case dup:
							if (!Evaluate)
								return new StaticProperty ("dup", 
								                          "Create a dynamic array of the same size and copy the contents of the array into it.",
								                          at, relatedNode, idContainter);
							break;
						case idup:
							if (!Evaluate)
								return new StaticProperty ("idup",
								                          "Create a dynamic array of the same size and copy the contents of the array into it. The copy is typed as being immutable. D 2.0 only",
								                          at, relatedNode, idContainter);
							break;
						case reverse:
							if (!Evaluate)
								return new StaticProperty ("reverse",
								                          "Reverses in place the order of the elements in the array. Returns the array.",
								                          at, relatedNode, idContainter);
							break;
						case sort:
							if (!Evaluate)
								return new StaticProperty ("sort",
								                          "Sorts in place the order of the elements in the array. Returns the array.",
								                          at, relatedNode, idContainter);
							break;
					}
				} else if (InitialResult is AssocArrayType) {
					var aat = (AssocArrayType)InitialResult;
					switch (propertyIdentifierHash) {
						case _sizeof:
							if (!Evaluate)
								return new StaticProperty ("sizeof",
								                          "Returns the size of the reference to the associative array; it is 4 in 32-bit builds and 8 on 64-bit builds.",
								                          ctxt.ParseCache.SizeT, relatedNode, idContainter);
							break;
						case length:
							if (!Evaluate)
								return new StaticProperty ("length",
								                          "Returns number of values in the associative array. Unlike for dynamic arrays, it is read-only.",
								                          ctxt.ParseCache.SizeT, relatedNode, idContainter);
							break;
						case keys:
							if (!Evaluate)
								return new StaticProperty ("keys",
								                          "Returns dynamic array, the elements of which are the keys in the associative array.",
								                          new ArrayType (aat.KeyType, idContainter), relatedNode, idContainter);
							break;
						case values:
							if (!Evaluate)
								return new StaticProperty ("values",
								                          "Returns dynamic array, the elements of which are the values in the associative array.",
								                          new ArrayType (aat.ValueType, idContainter), relatedNode, idContainter);
							break;
						case rehash:
							if (!Evaluate)
								return new StaticProperty ("rehash",
								                          "Reorganizes the associative array in place so that lookups are more efficient. rehash is effective when, for example, the program is done loading up a symbol table and now needs fast lookups in it. Returns a reference to the reorganized array.",
								                          aat, relatedNode, idContainter);
							break;
						case byKey:
							if (!Evaluate)
								return new StaticProperty ("byKey",
								                          "Returns a delegate suitable for use as an Aggregate to a ForeachStatement which will iterate over the keys of the associative array.",
								                          new DelegateType (aat.KeyType, 
								                  new DelegateDeclaration { ReturnType = aat.KeyType.DeclarationOrExpressionBase as ITypeDeclaration }), 
								                          relatedNode, idContainter);
							break;
						case byValue:
							if (!Evaluate)
								return new StaticProperty ("byValue",
								                          "Returns a delegate suitable for use as an Aggregate to a ForeachStatement which will iterate over the values of the associative array.",
								                          new DelegateType (aat.ValueType,
								                  new DelegateDeclaration { ReturnType = aat.ValueType.DeclarationOrExpressionBase as ITypeDeclaration }),
								                          relatedNode, idContainter);
							break;
						case _get:
							if (!Evaluate)
								return new StaticProperty ("get",
								                          "Looks up key; if it exists returns corresponding value else evaluates and returns defaultValue.",
								                          new DelegateType (aat.ValueType, 
								                  new DelegateDeclaration { 
									ReturnType = aat.ValueType.DeclarationOrExpressionBase as ITypeDeclaration, 
									Parameters = new List<INode>{
												new DVariable(){
													Name="key",
													Type=aat.KeyType.DeclarationOrExpressionBase as ITypeDeclaration
												},
												new DVariable(){
													Name="defaultValue",
													Type=aat.ValueType.DeclarationOrExpressionBase as ITypeDeclaration,
													Attributes=new List<DAttribute>{ new Modifier(DTokens.Lazy)}
											}}
								}, new[] { aat.KeyType, aat.ValueType }),
								                          relatedNode, idContainter);
							break;
					}
				} else if (InitialResult is DTuple) {
					var tt = InitialResult as DTuple;
					
					if (propertyIdentifierHash == length) {
						if (Evaluate)
							return new PrimitiveValue (DTokens.Uint, tt.Items == null ? 0m : (decimal)tt.Items.Length, null, 0m);
						return new StaticProperty ("length",
						                          "Returns number of values in the type tuple.",
						                          ctxt.ParseCache.SizeT, relatedNode, idContainter);							
					}

					//TODO: Implement remaining properties
				} else if (InitialResult is DelegateType) {
					if (!Evaluate) {
						if (propertyIdentifierHash == ptr)
							return new PointerType (new PrimitiveType (DTokens.Void), idContainter);
						else if (propertyIdentifierHash == funcptr)
							return InitialResult;
					}
				} else if (InitialResult is PrimitiveType) {
					// See http://dlang.org/property.html
					var pt = (PrimitiveType)InitialResult;
					
					if (DTokens.BasicTypes_Integral [pt.TypeToken])
						switch (propertyIdentifierHash) {
							case init:
								if (!Evaluate)
									return new StaticProperty ("init", "Initializer (0)", pt, relatedNode, idContainter);
								break;
							case min:
								if (!Evaluate)
									return new StaticProperty ("min", "Maximum value", pt, relatedNode, idContainter);
								break;
							case max:
								if (!Evaluate)
									return new StaticProperty ("max", "Minimum value", pt, relatedNode, idContainter);
								break;
						}
					else if (DTokens.BasicTypes_FloatingPoint [pt.TypeToken])
						switch (propertyIdentifierHash) {
							case init:
								if (!Evaluate)
									return new StaticProperty ("init", "Initializer (NaN)", pt, relatedNode, idContainter);
								break;
							case infinity:
								if (!Evaluate)
									return new StaticProperty ("infinity", "Infinity value", pt, relatedNode, idContainter);
								break;
							case nan:
								if (!Evaluate)
									return new StaticProperty ("nan", "NaN value", pt, relatedNode, idContainter);
								break;
							case dig:
								if (!Evaluate)
									return new StaticProperty ("dig", "Number of decimal digits of precision", 
									                          new PrimitiveType (DTokens.Int), relatedNode, idContainter);
								break;
							case epsilon:
								if (!Evaluate)
									return new StaticProperty ("epsilon", "Smallest increment to the value 1", pt, relatedNode, idContainter);
								break;
							case mant_dig:
								if (!Evaluate)
									return new StaticProperty ("mant_dig", "Number of bits in mantissa",
									                          new PrimitiveType (DTokens.Int), relatedNode, idContainter);
								break;
							case max_10_exp:
								if (!Evaluate)
									return new StaticProperty ("max_10_exp", "Maximum int value such that 10^^max_10_exp is representable",
									                          new PrimitiveType (DTokens.Int), relatedNode, idContainter);
								break;
							case max_exp:
								if (!Evaluate)
									return new StaticProperty ("max_exp", "Maximum int value such that 2^^max_exp-1 is representable",
									                          new PrimitiveType (DTokens.Int), relatedNode, idContainter);
								break;
							case min_10_exp:
								if (!Evaluate)
									return new StaticProperty ("min_10_exp", "Minimum int value such that 10^^min_10_exp is representable as a normalized value",
									                          new PrimitiveType (DTokens.Int), relatedNode, idContainter);
								break;
							case min_exp:
								if (!Evaluate)
									return new StaticProperty ("min_exp", "Minimum int value such that 2^^min_exp-1 is representable as a normalized value",
									                          new PrimitiveType (DTokens.Int), relatedNode, idContainter);
								break;
							case max:
								if (!Evaluate)
									return new StaticProperty ("max", "Largest representable value that's not infinity", pt, relatedNode, idContainter);
								break;
							case min_normal:
								if (!Evaluate)
									return new StaticProperty ("min_normal", "Smallest representable normalized value that's not 0", pt, relatedNode, idContainter);
								break;
							case re:
								if (!Evaluate)
									return new StaticProperty ("re", "Real part", pt, relatedNode, idContainter);
								break;
							case im:
								if (!Evaluate)
									return new StaticProperty ("im", "Imaginary part", pt, relatedNode, idContainter);
								break;
						}
				}
			}
			#endregion

			if (prop != null)
				return prop;

			#region init
			if (propertyIdentifierHash == init) {
				var prop_Init = new DVariable {
					Name = "init",
					Description = "Initializer"
				};

				if (relatedNode != null) {
					if (!(relatedNode is DVariable)) {
						prop_Init.Parent = relatedNode.Parent;
						prop_Init.Type = new IdentifierDeclaration (relatedNode.NameHash);
					} else {
						prop_Init.Parent = relatedNode;
						prop_Init.Initializer = (relatedNode as DVariable).Initializer;
						prop_Init.Type = relatedNode.Type;
					}
				}

				return new MemberSymbol (prop_Init, DResolver.StripAliasSymbol (AbstractType.Get (InitialResult)), idContainter);
			}
			#endregion

			#region sizeof
			if (propertyIdentifierHash == _sizeof)
				return new MemberSymbol (new DVariable {
					Name = "sizeof",
					Type = new DTokenDeclaration(DTokens.Int),
					Initializer = new IdentifierExpression(4),
					Description = "Size in bytes (equivalent to C's sizeof(type))"
				}, new PrimitiveType (DTokens.Int), idContainter);
			#endregion

			#region alignof
			if (propertyIdentifierHash == alignof) {
				if (Evaluate) {
					
				}

				return new StaticProperty (
					"alignof", 
					".alignof gives the aligned size of an expression or type. For example, an aligned size of 1 means that it is aligned on a byte boundary, 4 means it is aligned on a 32 bit boundary.",
					new PrimitiveType (DTokens.Int) { 
					DeclarationOrExpressionBase = new DTokenDeclaration(DTokens.Int)
				}, relatedNode, idContainter);
			}
			#endregion

			#region mangleof
			if (propertyIdentifierHash == mangleof) {
				if (Evaluate)
					return new ArrayValue (Evaluation.GetStringType (ctxt), "<todo>"/*NameMangling.Mangle(AbstractType.Get(InitialResult))*/);
				
				return new StaticProperty (
					"mangleof", 
					"String representing the ‘mangled’ representation of the type",
					Evaluation.GetStringType (ctxt), relatedNode, idContainter);
			}
			#endregion

			#region stringof
			if (propertyIdentifierHash == stringof)
				return new StaticProperty ("stringof", "String representing the source representation of the type",
				                          Evaluation.GetStringType (ctxt), relatedNode, idContainter);
			#endregion

			#region classinfo
			else if (propertyIdentifierHash == classinfo) {
				var tr = DResolver.StripMemberSymbols (AbstractType.Get (InitialResult)) as TemplateIntermediateType;

				if (tr is ClassType || tr is InterfaceType) {
					var ci = new IdentifierDeclaration ("TypeInfo_Class") {
						InnerDeclaration = new IdentifierDeclaration("object"),
						ExpressesVariableAccess = true,
					};

					var ti = TypeDeclarationResolver.Resolve (ci, ctxt);

					ctxt.CheckForSingleResult (ti, ci);

					return new StaticProperty ("classinfo", 
					                          ".classinfo provides information about the dynamic type of a class object. It returns a reference to type object.TypeInfo_Class." + 
						Environment.NewLine +
						".classinfo applied to an interface gives the information for the interface, not the class it might be an instance of.",
					                          ti == null || ti.Length == 0 ? null : ti [0], relatedNode, idContainter);
				}
			}
			#endregion

			return prop;
		}
	}
}
