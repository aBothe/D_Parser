using D_Parser.Dom.Expressions;

namespace D_Parser.Dom
{
	public interface ITemplateParameter : ISyntaxRegion, IVisitable<TemplateParameterVisitor>
	{
		string Name { get; }
		DNode Parent {get;}

		R Accept<R>(TemplateParameterVisitor<R> vis);
	}

	/// <summary>
	/// void foo(U) (U u) {
	///		u. -- now the type of u is needed. A ITemplateParameterDeclaration will be returned which holds U.
	/// }
	/// </summary>
	public class ITemplateParameterDeclaration : AbstractTypeDeclaration
	{
		public ITemplateParameter TemplateParameter;

		public override string ToString(bool IncludesBase)
		{
			return TemplateParameter.ToString();
		}

		public override void Accept(TypeDeclarationVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(TypeDeclarationVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}

	public class TemplateParameterNode : DNode
	{
		public readonly ITemplateParameter TemplateParameter;

		public TemplateParameterNode(ITemplateParameter param)
		{
			TemplateParameter = param;

			Name = param.Name;
			Parent = param.Parent;

			Location = NameLocation = param.Location;
			EndLocation = param.EndLocation;
		}

		public sealed override string ToString()
		{
			return TemplateParameter.ToString();
		}

		public sealed override string ToString(bool Attributes, bool IncludePath)
		{
			return (GetNodePath(this, false) + "." + ToString()).TrimEnd('.');
		}

		public override void Accept(NodeVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(NodeVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}

	public class TemplateTypeParameter : ITemplateParameter
	{
		public string Name { get; set; }
		public DNode Parent {get; set;}

		public ITypeDeclaration Specialization;
		public ITypeDeclaration Default;

		public sealed override string ToString()
		{
			var ret = Name;

			if (Specialization != null)
				ret += ":" + Specialization.ToString();

			if (Default != null)
				ret += "=" + Default.ToString();

			return ret;
		}

		public CodeLocation Location { get; set; }
		public CodeLocation EndLocation { get; set; }

		public void Accept(TemplateParameterVisitor vis) { vis.Visit(this);	}
		public R Accept<R>(TemplateParameterVisitor<R> vis) { return vis.Visit(this); }
	}

	public class TemplateThisParameter : ITemplateParameter
	{
		public string Name { get { return FollowParameter.Name; } }
		public DNode Parent {get; set;}

		public ITemplateParameter FollowParameter;

		public sealed override string ToString()
		{
			return "this" + (FollowParameter != null ? (" " + FollowParameter.ToString()) : "");
		}

		public CodeLocation Location { get; set; }
		public CodeLocation EndLocation { get; set; }

		public void Accept(TemplateParameterVisitor vis) { vis.Visit(this); }
		public R Accept<R>(TemplateParameterVisitor<R> vis) { return vis.Visit(this); }
	}

	public class TemplateValueParameter : ITemplateParameter
	{
		public string Name { get; set; }
		public DNode Parent {get; set;}
		public ITypeDeclaration Type;

		public IExpression SpecializationExpression;
		public IExpression DefaultExpression;

		public override string ToString()
		{
			return (Type != null ? (Type.ToString() + " ") : "") + Name/*+ (SpecializationExpression!=null?(":"+SpecializationExpression.ToString()):"")+
				(DefaultExpression!=null?("="+DefaultExpression.ToString()):"")*/;
		}

		public CodeLocation Location { get; set; }
		public CodeLocation EndLocation { get; set; }

		public virtual void Accept(TemplateParameterVisitor vis) { vis.Visit(this); }
		public virtual R Accept<R>(TemplateParameterVisitor<R> vis) { return vis.Visit(this); }
	}

	public class TemplateAliasParameter : TemplateValueParameter
	{
		public ITypeDeclaration SpecializationType;
		public ITypeDeclaration DefaultType;

		public sealed override string ToString()
		{
			return "alias " + base.ToString();
		}

		public override void Accept(TemplateParameterVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(TemplateParameterVisitor<R> vis) { return vis.Visit(this); }
	}

	public class TemplateTupleParameter : ITemplateParameter
	{
		public string Name { get; set; }
		public DNode Parent {get; set;}

		public sealed override string ToString()
		{
			return Name + " ...";
		}

		public CodeLocation Location { get; set; }
		public CodeLocation EndLocation { get; set; }

		public void Accept(TemplateParameterVisitor vis) { vis.Visit(this); }
		public R Accept<R>(TemplateParameterVisitor<R> vis) { return vis.Visit(this); }
	}
}