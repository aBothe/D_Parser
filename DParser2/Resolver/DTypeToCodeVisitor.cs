using D_Parser.Parser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Resolver
{
	public class DTypeToCodeVisitor : IResolvedTypeVisitor
	{
		readonly StringBuilder sb = new StringBuilder();
		readonly bool pretty;

		private DTypeToCodeVisitor(bool pretty)
		{
			this.pretty = pretty;
		}

		public static string GenerateCode(AbstractType t, bool pretty = false)
		{
			var vis = new DTypeToCodeVisitor(pretty);

			vis.AcceptType(t);

			return vis.sb.ToString();
		}

		void AcceptType(AbstractType t)
		{
			if (t == null)
				return;

			if (t.HasModifiers){
				foreach(byte modifier in t.Modifiers){
					if (t.Modifiers.Length > 1) {
						sb.Append (' ');
					}
					sb.Append (DTokens.GetTokenString (modifier));
				}
				sb.Append ('(');
			}

			t.Accept(this);

			if (t.HasModifiers)
				sb.Append(')');
		}

		public void VisitPrimitiveType(PrimitiveType t)
		{
			sb.Append(DTokens.GetTokenString(t.TypeToken));
		}

		public void VisitPointerType(PointerType t)
		{
			AcceptType(t.Base);
			sb.Append('*');
		}

		public void VisitArrayType(ArrayType t)
		{
			AcceptType(t.Base);
			sb.Append(t.IsStaticArray && t.FixedLength >= 0 ? string.Format("[{0}]", t.FixedLength) : "[]");
		}

		public void VisitAssocArrayType(AssocArrayType t)
		{
			AcceptType(t.Base);
			sb.Append('[');
			AcceptType(t.KeyType);
			sb.Append(']');
		}

		public void VisitDelegateCallSymbol(DelegateCallSymbol t)
		{
			AcceptType (t.Delegate);
		}

		public void VisitDelegateType(DelegateType t)
		{
			AcceptType(t.Base);
			sb.Append(' ').Append(t.IsFunction ? "function" : "delegate").Append(" (");

			if (t.Parameters != null)
				foreach (var p in t.Parameters)
				{
					AcceptType(p); 
					sb.Append(',');
				}

			if (sb[sb.Length - 1] == ',')
				sb.Length--;

			sb.Append(')');
		}

		void VisitDSymbol(DSymbol t)
		{
			var def = t.Definition;
			if (def == null)
				sb.Append("<Node object no longer exists>");
			else
			{
				sb.Append(def.Name);
				if (def.TemplateParameters != null)
				{
					// TODO: to match dmd, do not emit "()" if argument is
					//  string, basic type, single (unresolved) identifier,
					//  number, "null" or "this"
					sb.Append("!(");
					if (t.DeducedTypes.Count() > 0)
						AcceptType(t.DeducedTypes[0]);
					for (int i = 1; i < t.DeducedTypes.Count(); i++)
					{
						sb.Append(", ");
						AcceptType(t.DeducedTypes[i]);
					}
					sb.Append(")");
				}
			}
		}

		public void VisitAliasedType(AliasedType t)
		{
			if(pretty)
				sb.Append(t.declaration != null ? t.declaration.ToString() : t.Definition.ToString(false, false)).Append('=');

			if (t.Base != null)
				AcceptType(t.Base);
			else
				VisitDSymbol(t);
		}

		public void VisitEnumType(EnumType t)
		{
			VisitDSymbol(t);
		}

		public void VisitStructType(StructType t)
		{
			VisitDSymbol(t);
		}

		public void VisitUnionType(UnionType t)
		{
			VisitDSymbol(t);
		}

		public void VisitClassType(ClassType t)
		{
			VisitDSymbol(t);
		}

		public void VisitInterfaceType(InterfaceType t)
		{
			VisitDSymbol(t);
		}

		public void VisitTemplateType(TemplateType t)
		{
			VisitDSymbol(t);
		}

		public void VisitMixinTemplateType(MixinTemplateType t)
		{
			VisitDSymbol(t);
		}

		public void VisitEponymousTemplateType(EponymousTemplateType t)
		{
			VisitDSymbol(t);
		}

		public void VisitStaticProperty(StaticProperty t)
		{
			VisitDSymbol(t);
		}

		public void VisitMemberSymbol(MemberSymbol t)
		{
			VisitDSymbol(t);
		}

		public void VisitTemplateParameterSymbol(TemplateParameterSymbol t)
		{
			if (t.ParameterValue != null)
				sb.Append(t.ParameterValue.ToCode());
			else if(t.Base == null) 
				sb.Append(t.Parameter == null ? "(unknown template parameter)" : t.Parameter.Name);
			else
				AcceptType(t.Base); //FIXME: It's not actually code but currently needed for correct ToString() representation in e.g. parameter insight
		}

		public void VisitArrayAccessSymbol(ArrayAccessSymbol t)
		{
			AcceptType(t.Base);
			
			sb.Append('[');
			if (t.indexExpression != null)
				sb.Append(t.indexExpression.ToString());
			sb.Append(']');
		}

		public void VisitModuleSymbol(ModuleSymbol t)
		{
			VisitDSymbol(t);
		}

		public void VisitPackageSymbol(PackageSymbol t)
		{
			sb.Append(t.Package.Path);
		}

		public void VisitDTuple(DTuple t)
		{
			sb.Append("tuple(");
			if (t.Items != null && t.Items.Length > 0)
			{
				foreach (var semantic in t.Items)
				{
					if (semantic == null)
						sb.Append("<null>");
					else if (semantic is AbstractType)
					{
						var type = (AbstractType)semantic;
						if (type is DSymbol ds)
							sb.Append(ds.Definition.Name);
						else
							AcceptType(type);
					}
					else
						sb.Append(semantic.ToString());
					sb.Append(", ");
				}
				sb.Length = sb.Length - 2;
			}

			sb.Append(")");
		}

		public void VisitUnknownType(UnknownType t)
		{
			sb.Append('?');
		}

		public void VisitAmbigousType(AmbiguousType t)
		{
			sb.Append("<Overloads>");
		}
	}
}
