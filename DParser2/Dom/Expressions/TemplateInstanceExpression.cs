using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace D_Parser.Dom.Expressions
{
	public class TemplateInstanceExpression : AbstractTypeDeclaration,PrimaryExpression, ContainerExpression, IntermediateIdType
	{
		public bool ModuleScoped {
			get;
			set;
		}

		public int IdHash {
			get {
				return TemplateIdHash;
			}
		}

		public readonly int TemplateIdHash;

		public string TemplateId { get { return Strings.TryGet(TemplateIdHash); } }

		public readonly ITypeDeclaration Identifier;
		public IExpression[] Arguments;

		public TemplateInstanceExpression(ITypeDeclaration id)
		{
			this.Identifier = id;

			var curtd = id;
			while (curtd != null)
			{
				if (curtd is IntermediateIdType)
				{
					var i = curtd as IntermediateIdType;
					TemplateIdHash = i.IdHash;
					ModuleScoped = i.ModuleScoped;
					break;
				}
				curtd = curtd.InnerDeclaration;
			}
		}

		public override string ToString(bool IncludesBase)
		{
			var sb = new StringBuilder();

			if (ModuleScoped)
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

		public IEnumerable<IExpression> SubExpressions
		{
			get { return Arguments != null ? Arguments : Enumerable.Empty<IExpression>(); }
		}

		public void Accept(ExpressionVisitor vis)
		{
			vis.Visit(this);
		}

		public R Accept<R>(ExpressionVisitor<R> vis)
		{
			return vis.Visit(this);
		}

		public override void Accept(TypeDeclarationVisitor vis) => vis.Visit(this);
		public override R Accept<R>(TypeDeclarationVisitor<R> vis) => vis.Visit(this);
		public override R Accept<R, ParameterType>(ITypeDeclarationVisitor<R, ParameterType> vis, ParameterType parameter)
			=> vis.Visit(this, parameter);
	}
}

