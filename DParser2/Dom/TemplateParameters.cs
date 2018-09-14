using D_Parser.Dom.Expressions;
using System;

namespace D_Parser.Dom
{
	public abstract class TemplateParameter : ISyntaxRegion, IVisitable<TemplateParameterVisitor>
	{
		public readonly int NameHash;
		public readonly CodeLocation NameLocation;
		public string Name { get { return Strings.TryGet (NameHash); } }

		public CodeLocation Location { get; set; }
		public CodeLocation EndLocation { get; set; }

		DNode parent;
		public DNode Parent {
			set {
				if (representation != null)
					representation.Parent = value;
				parent = value;
			}
			get
			{
				return parent;
			}
		}

		Node representation;
		public Node Representation
		{
			get
			{
				return representation ?? (representation = new Node(this));
			}
		}

		protected TemplateParameter(string name, CodeLocation nameLoc, DNode par) : this(name != null ? name.GetHashCode() : 0, nameLoc, par)
		{
			Strings.Add (name);
		}

		protected TemplateParameter(int nameHash, CodeLocation nameLoc, DNode par)
		{
			NameHash = nameHash;
			NameLocation = nameLoc;
			Parent = par;
		}

		public static explicit operator TemplateParameter(Node tp)
		{
			return tp != null ? tp.TemplateParameter : null;
		}

		public abstract void Accept (TemplateParameterVisitor vis);
		public abstract R Accept<R>(TemplateParameterVisitor<R> vis);
		public abstract R Accept<R, ParameterType>(ITemplateParameterVisitor<R, ParameterType> vis, ParameterType parameter);

		public sealed class Node : DNode
		{
			public readonly TemplateParameter TemplateParameter;

			public Node(TemplateParameter param)
			{
				TemplateParameter = param;

				NameHash = param.NameHash;
				NameLocation = param.NameLocation;
				Parent = param.Parent;

				Location = param.Location;
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
	}

	public sealed class TemplateTypeParameter : TemplateParameter
	{
		public ITypeDeclaration Specialization;
		public ITypeDeclaration Default;

		public TemplateTypeParameter(string name, CodeLocation nameLoc, DNode parent) : base(name, nameLoc, parent) {}
		public TemplateTypeParameter(int nameHash, CodeLocation nameLoc, DNode parent) : base(nameHash, nameLoc, parent) {}

		public sealed override string ToString()
		{
			var ret = Name;

			if (Specialization != null)
				ret += ":" + Specialization.ToString();

			if (Default != null)
				ret += "=" + Default.ToString();

			return ret;
		}

		public override void Accept(TemplateParameterVisitor vis) { vis.Visit(this);	}
		public override R Accept<R>(TemplateParameterVisitor<R> vis) { return vis.Visit(this); }
		public override R Accept<R, ParameterType>(ITemplateParameterVisitor<R, ParameterType> vis, ParameterType parameter) => vis.Visit(this, parameter);
	}

	public sealed class TemplateThisParameter : TemplateParameter
	{
		public readonly TemplateParameter FollowParameter;

		public TemplateThisParameter(TemplateParameter followParam, DNode parent) 
		: base( followParam != null ? followParam.Name : string.Empty, 
			       followParam != null ? followParam.NameLocation : new CodeLocation(), parent) {
			FollowParameter = followParam;
		}

		public sealed override string ToString()
		{
			return "this" + (FollowParameter != null ? (" " + FollowParameter.ToString()) : "");
		}

		public override void Accept(TemplateParameterVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(TemplateParameterVisitor<R> vis) { return vis.Visit(this); }
		public override R Accept<R, ParameterType>(ITemplateParameterVisitor<R, ParameterType> vis, ParameterType parameter) => vis.Visit(this, parameter);
	}

	public class TemplateValueParameter : TemplateParameter
	{
		public ITypeDeclaration Type;

		public IExpression SpecializationExpression;
		public IExpression DefaultExpression;

		public TemplateValueParameter(string name, CodeLocation nameLoc, DNode parent) : base(name, nameLoc, parent) {}
		public TemplateValueParameter(int name, CodeLocation nameLoc, DNode parent) : base(name, nameLoc, parent) {}

		public override string ToString()
		{
			return (Type != null ? (Type.ToString() + " ") : "") + Name/*+ (SpecializationExpression!=null?(":"+SpecializationExpression.ToString()):"")+
				(DefaultExpression!=null?("="+DefaultExpression.ToString()):"")*/;
		}

		public override void Accept(TemplateParameterVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(TemplateParameterVisitor<R> vis) { return vis.Visit(this); }
		public override R Accept<R, ParameterType>(ITemplateParameterVisitor<R, ParameterType> vis, ParameterType parameter) => vis.Visit(this, parameter);
	}

	public sealed class TemplateAliasParameter : TemplateValueParameter
	{
		public ITypeDeclaration SpecializationType;
		public ITypeDeclaration DefaultType;

		public TemplateAliasParameter(string name, CodeLocation nameLoc, DNode parent) : base(name, nameLoc, parent) {}
		public TemplateAliasParameter(int name, CodeLocation nameLoc, DNode parent) : base(name, nameLoc, parent) {}

		public sealed override string ToString()
		{
			return "alias " + base.ToString();
		}

		public override void Accept(TemplateParameterVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(TemplateParameterVisitor<R> vis) { return vis.Visit(this); }
		public override R Accept<R, ParameterType>(ITemplateParameterVisitor<R, ParameterType> vis, ParameterType parameter) => vis.Visit(this, parameter);
	}

	public sealed class TemplateTupleParameter : TemplateParameter
	{
		public sealed override string ToString()
		{
			return Name + " ...";
		}

		public TemplateTupleParameter(string name, CodeLocation nameLoc, DNode parent) : base(name, nameLoc, parent) {}


		public override void Accept(TemplateParameterVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(TemplateParameterVisitor<R> vis) { return vis.Visit(this); }
		public override R Accept<R, ParameterType>(ITemplateParameterVisitor<R, ParameterType> vis, ParameterType parameter) => vis.Visit(this, parameter);
	}
}