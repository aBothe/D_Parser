using D_Parser.Parser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace D_Parser.Resolver
{
	public class DTypeToCodeVisitor : IResolvedTypeVisitor
	{
		StringBuilder sb = new StringBuilder();

		private DTypeToCodeVisitor()
		{

		}

		public static string GenerateCode(AbstractType t)
		{
			var vis = new DTypeToCodeVisitor();

			vis.AcceptType(t);

			return vis.sb.ToString();
		}

		void AcceptType(AbstractType t)
		{
			if (t == null)
				return;

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
			sb.Append(t.DeclarationOrExpressionBase.ToString());
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
			if (t.DeclarationOrExpressionBase != null)
				sb.Append(t.DeclarationOrExpressionBase.ToString());
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
