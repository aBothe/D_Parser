using D_Parser.Dom.Expressions;
using System;

namespace D_Parser.Dom
{
	public abstract class TemplateParameter : ISyntaxRegion, IVisitable<TemplateParameterVisitor>
	{
		public readonly int NameHash;
		public readonly CodeLocation NameLocation;
		public string Name {get{return Strings.TryGet (NameHash);}}

		public CodeLocation Location {
			get;
			set;
		}

		public CodeLocation EndLocation {
			get;
			set;
		}

		readonly WeakReference parent;
		public DNode Parent {get{ return parent.Target as DNode; }}

		Node representation;
		public Node Representation
		{
			get{
				if (representation == null)
					representation = new Node (this);

				return representation;
			}
		}

		public TemplateParameter(string name, CodeLocation nameLoc, DNode par) : this(name.GetHashCode(), nameLoc, par)
		{
			Strings.Add (name);
		}

		public TemplateParameter(int nameHash, CodeLocation nameLoc, DNode par)
		{
			NameHash = nameHash;
			NameLocation = nameLoc;
			this.parent = new WeakReference (par);
		}

		public static explicit operator TemplateParameter(Node tp)
		{
			return tp != null ? tp.TemplateParameter : null;
		}

		public abstract void Accept (TemplateParameterVisitor vis);
		public abstract R Accept<R>(TemplateParameterVisitor<R> vis);


		public class Node : DNode
		{
			public readonly TemplateParameter TemplateParameter;

			public Node(TemplateParameter param)
			{
				TemplateParameter = param;

				nameHash = param.NameHash;
				NameLocation = param.NameLocation;
				_Parent = param.parent;

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

	/// <summary>
	/// void foo(U) (U u) {
	///		u. -- now the type of u is needed. A ITemplateParameterDeclaration will be returned which holds U.
	/// }
	/// </summary>
	public class ITemplateParameterDeclaration : AbstractTypeDeclaration
	{
		public TemplateParameter TemplateParameter;

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

	public class TemplateTypeParameter : TemplateParameter
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
	}

	public class TemplateThisParameter : TemplateParameter
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
	}

	public class TemplateAliasParameter : TemplateValueParameter
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
	}

	public class TemplateTupleParameter : TemplateParameter
	{
		public sealed override string ToString()
		{
			return Name + " ...";
		}

		public TemplateTupleParameter(string name, CodeLocation nameLoc, DNode parent) : base(name, nameLoc, parent) {}


		public override void Accept(TemplateParameterVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(TemplateParameterVisitor<R> vis) { return vis.Visit(this); }
	}
}