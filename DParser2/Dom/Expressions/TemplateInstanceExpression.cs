using System;
using System.Text;

namespace D_Parser.Dom.Expressions
{
	public class TemplateInstanceExpression : AbstractTypeDeclaration,PrimaryExpression,ContainerExpression
	{
		public readonly int TemplateIdHash;

		public string TemplateId { get { return Strings.TryGet(TemplateIdHash); } }

		public bool ModuleScopedIdentifier;
		public readonly ITypeDeclaration Identifier;
		public IExpression[] Arguments;

		public TemplateInstanceExpression(ITypeDeclaration id)
		{
			this.Identifier = id;

			var curtd = id;
			while (curtd != null)
			{
				if (curtd is IdentifierDeclaration)
				{
					var i = curtd as IdentifierDeclaration;
					TemplateIdHash = i.IdHash;
					ModuleScopedIdentifier = i.ModuleScoped;
					break;
				}
				curtd = curtd.InnerDeclaration;
			}
		}

		public override string ToString(bool IncludesBase)
		{
			var sb = new StringBuilder();

			if (ModuleScopedIdentifier)
				sb.Append('.');

			if (IncludesBase && InnerDeclaration != null)
				sb.Append(InnerDeclaration.ToString()).Append('.');

			if (Identifier != null)
				sb.Append(Identifier.ToString());

			sb.Append('!');

			if (Arguments != null)
			{
				if (Arguments.Length > 1)
				{
					sb.Append('(');
					foreach (var e in Arguments)
						if (e != null)
							sb.Append(e.ToString()).Append(',');
					if (sb[sb.Length - 1] == ',')
						sb.Remove(sb.Length - 1, 1);
					sb.Append(')');
				}
				else if (Arguments.Length == 1 && Arguments[0] != null)
					sb.Append(Arguments[0].ToString());
			}

			return sb.ToString();
		}

		public IExpression[] SubExpressions
		{
			get { return Arguments; }
		}

		public void Accept(ExpressionVisitor vis)
		{
			vis.Visit(this);
		}

		public R Accept<R>(ExpressionVisitor<R> vis)
		{
			return vis.Visit(this);
		}

		public override void Accept(TypeDeclarationVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(TypeDeclarationVisitor<R> vis)
		{
			return vis.Visit(this);
		}

		public override ulong GetHash()
		{
			ulong hashCode = 0uL;
			unchecked
			{
				if (Identifier != null)
					hashCode += 1000000007 * Identifier.GetHash();
				hashCode += (ulong)TemplateIdHash;
				if (Arguments != null)
					for (ulong i = (ulong)Arguments.Length; i != 0;)
						hashCode += 1000000009 * i * Arguments[(int)--i].GetHash();
			}
			return hashCode;
		}
	}
}

