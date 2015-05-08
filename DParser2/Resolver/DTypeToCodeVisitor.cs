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

			if (pretty)
			{
				var aliasTag = t.Tag<TypeDeclarationResolver.AliasTag>(TypeDeclarationResolver.AliasTag.Id);
				if (aliasTag != null)
					sb.Append(aliasTag.typeBase != null ? aliasTag.typeBase.ToString() : aliasTag.aliasDefinition.ToString(false, false)).Append('=');
			}

			if (t.Modifier != 0)
				sb.Append(DTokens.GetTokenString(t.Modifier)).Append('(');

			t.Accept(this);

			if (t.Modifier != 0)
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
			sb.Append(def != null ? def.ToString(false, true) : "<Node object no longer exists>");
		}

		public void VisitAliasedType(AliasedType t)
		{
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
			sb.Append('(');

			if (t.Items != null && t.Items.Length != 0)
			{
				foreach (var i in t.Items)
				{
					if(i is AbstractType)
						AcceptType(i as AbstractType);
					sb.Append(',');
				}
				if (sb[sb.Length - 1] == ',')
					sb.Length--;
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
