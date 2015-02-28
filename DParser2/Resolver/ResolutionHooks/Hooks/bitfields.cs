using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Resolver.ExpressionSemantics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace D_Parser.Resolver.ResolutionHooks
{
	class bitfields : IHook
	{
		public string HookedSymbol	{	get { return "std.bitmanip.bitfields"; }	}
		public bool SupersedesMultipleOverloads{	get { return true; }	}

		public AbstractType TryDeduce(DSymbol ds, IEnumerable<ISemantic> templateArguments, ref INode n)
		{
			TemplateTypeParameter tp;
			var t = ds as TemplateType;
			if (t == null)
				return null;

			var orig = ds.Definition;
			var tupleStruct = new DClassLike(DTokens.Struct)
			{
				NameHash = ds.NameHash,
				Parent = orig.Parent,
				Location = orig.Location,
				EndLocation = orig.EndLocation,
				NameLocation = orig.NameLocation
			};

			var ded = new Templates.DeducedTypeDictionary(tupleStruct);

			var sb = new StringBuilder();

			if (templateArguments != null)
			{
				var en = templateArguments.GetEnumerator();
				if (en.MoveNext())
				{
					var next = en.Current;
					int i = 0;
					for (; ; i++)
					{
						var fieldType = AbstractType.Get(next);

						if (fieldType == null)
							break;

						fieldType.NonStaticAccess = true;

						if (!en.MoveNext())
							break;

						next = en.Current;

						if (next is ArrayValue && (next as ArrayValue).IsString)
						{
							var name = (next as ArrayValue).StringValue;
							if (!string.IsNullOrWhiteSpace(name))
							{
								var templateParamName = "_" + i.ToString();
								tp = new TemplateTypeParameter(templateParamName, CodeLocation.Empty, tupleStruct);
								ded[tp] = new TemplateParameterSymbol(tp, fieldType);

								// getter
								sb.Append("@property @safe ").Append(templateParamName).Append(' ').Append(name).AppendLine("() pure nothrow const {}");
								// setter
								sb.Append("@property @safe void ").Append(name).AppendLine("(").Append(templateParamName).AppendLine(" v) pure nothrow {}");
								// constants
								sb.Append("enum ").Append(templateParamName).Append(" ").Append(name).Append("_min = cast(").Append(templateParamName).AppendLine(") 0;");
								sb.Append("enum ").Append(templateParamName).Append(" ").Append(name).Append("_max = cast(").Append(templateParamName).AppendLine(") 0;");
							}
							if (!en.MoveNext())
								break;
						}
						else
							break;

						if (!en.MoveNext()) // Skip offset
							break;

						next = en.Current;
					}
				}

				tupleStruct.Add(new DVariable { 
					NameHash = tupleStruct.NameHash, 
					Attributes = new List<DAttribute> { new Modifier(DTokens.Enum) }, 
					Initializer = new IdentifierExpression(sb.ToString(), LiteralFormat.StringLiteral, LiteralSubformat.Utf8)
				});
			}

			n = tupleStruct;
			return new TemplateType(tupleStruct, ded.Count != 0 ? ded.Values : null);
		}
	}
}
