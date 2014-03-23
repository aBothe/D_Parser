using D_Parser.Dom;
using D_Parser.Parser;
using D_Parser.Resolver.ExpressionSemantics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace D_Parser.Resolver.ResolutionHooks
{
	class TupleHook : IHook
	{
		public string HookedSymbol
		{
			get { return "std.typecons.Tuple"; }
		}

		public bool SupersedesMultipleOverloads
		{
			get { return true; }
		}

		public AbstractType TryDeduce(DSymbol ds, IEnumerable<ISemantic> templateArguments, ref INode n)
		{
			TemplateTypeParameter tp;
			var t = ds as TemplateType;
			if (t == null)
				return null;

			var orig = ds.Definition;
			var tupleStruct = new DClassLike(DTokens.Struct) { 
				NameHash = ds.NameHash, 
				Parent = orig.Parent, 
				Location = orig.Location, 
				EndLocation = orig.EndLocation, 
				NameLocation = orig.NameLocation
			};
			
			var ded = new Templates.DeducedTypeDictionary(tupleStruct);

			if (templateArguments != null)
			{
				var typeList = new List<AbstractType>();

				var en = templateArguments.GetEnumerator();
				if(en.MoveNext())
				{
					var next = en.Current;
					int i = 0;
					for (; ; i++)
					{
						var fieldType = AbstractType.Get(next);

						if (fieldType == null)
							break;

						fieldType.NonStaticAccess = true;

						typeList.Add(fieldType);

						if (!en.MoveNext())
							break;

						next = en.Current;

						if (next is ArrayValue && (next as ArrayValue).IsString)
						{
							var name = (next as ArrayValue).StringValue;
							var templateParamName = "_" + i.ToString();
							tp = new TemplateTypeParameter(templateParamName, CodeLocation.Empty, tupleStruct);
							ded[tp] = new TemplateParameterSymbol(tp, fieldType);

							tupleStruct.Add(new DVariable { Name = name, Type = new IdentifierDeclaration(templateParamName) });

							if (!en.MoveNext())
								break;

							next = en.Current;
						}
					}
				}

				var tupleName = "Types";
				tp = new TemplateTypeParameter(tupleName, CodeLocation.Empty, tupleStruct);
				ded[tp] = new TemplateParameterSymbol(tp, new DTuple(null, typeList));

				tupleStruct.Add(new DVariable { NameHash = DVariable.AliasThisIdentifierHash, IsAlias = true, IsAliasThis = true, Type = new IdentifierDeclaration(tupleName) });
			}

			n = tupleStruct;
			return new StructType(tupleStruct, ds.DeclarationOrExpressionBase, ded.Count != 0 ? ded.Values : null);

			//TODO: Ensure renaming and other AST-based things run properly
		}
	}
}
