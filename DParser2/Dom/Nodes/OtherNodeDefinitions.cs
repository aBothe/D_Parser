using System.Collections.Generic;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using System.Text;

namespace D_Parser.Dom
{
	public class DVariable : DNode
	{
		public IExpression Initializer; // Variable

		public const string AliasThisIdentifier = "this";
		public static readonly int AliasThisIdentifierHash = AliasThisIdentifier.GetHashCode();
		public bool IsAlias = false;
		public bool IsAliasThis;

		public bool IsLocal
		{
			get { return Parent is DMethod; }
		}
		public bool IsParameter
		{
			get { return IsLocal && (Parent as DMethod).Parameters.Contains(this); }
		}

		public override string ToString(bool Attributes, bool IncludePath)
		{
			return ToString(Attributes, IncludePath, true);
		}

		public string ToString(bool Attributes, bool IncludePath, bool initializer)
		{
			return (IsAlias ? "alias " : "") + base.ToString(Attributes, IncludePath) + (initializer && Initializer != null ? (" = " + Initializer.ToString()) : "");
		}

		public virtual bool IsConst
		{
			get
			{
				return ContainsAttribute(DTokens.Const, DTokens.Enum); // TODO: Are there more tokens that indicate a const value?
			}
		}

		public override void AssignFrom(INode other)
		{
			if (other is DVariable)
			{
				var dv = other as DVariable;
				Initializer = dv.Initializer;
				IsAlias = dv.IsAlias;
			}

			base.AssignFrom(other);
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

	public class DMethod : DNode, IBlockNode
	{
		public readonly List<INode> Parameters = new List<INode>();
		public MethodType SpecialType = MethodType.Normal;

		BlockStatement _In;
		BlockStatement _Out;
		public IdentifierDeclaration OutResultVariable;
		BlockStatement _Body;

		readonly NodeDictionary children;

		/// <summary>
		/// Used to identify constructor methods. Since it'd be a token otherwise it cannot be used as a regular method's name.
		/// </summary>
		public const string ConstructorIdentifier = "__ctor";
		public readonly static int ConstructorIdentifierHash = ConstructorIdentifier.GetHashCode();

		public DMethod()
		{
			children = new NodeDictionary(this);
		}

		public BlockStatement GetSubBlockAt(CodeLocation Where)
		{
			if (_In != null && _In.Location <= Where && _In.EndLocation >= Where)
				return _In;

			if (_Out != null && _Out.Location <= Where && _Out.EndLocation >= Where)
				return _Out;

			if (_Body != null && _Body.Location <= Where && _Body.EndLocation >= Where)
				return _Body;

			return null;
		}

		public override void AssignFrom(INode other)
		{
			if (other is DMethod)
			{
				var dm = (DMethod)other;

				Parameters.Clear();
				Parameters.AddRange(dm.Parameters);
				SpecialType = dm.SpecialType;
				_In = dm._In;
				_Out = dm._Out;
				_Body = dm._Body;
				children.Clear ();
				children.AddRange(dm.children);
			}

			base.AssignFrom(other);
		}

		public CodeLocation InToken;
		public CodeLocation OutToken;
		public CodeLocation BodyToken;
		
		public BlockStatement In { get { return _In; } set { _In = value; } }
		public BlockStatement Out { get { return _Out; } set { _Out = value; } }
		public BlockStatement Body { get { return _Body; } set { _Body = value; } }

		public NodeDictionary Children
		{
			get { return children; }
		}

		public enum MethodType
		{
			Normal = 0,
			Delegate,
			AnonymousDelegate,
			Lambda,
			Constructor,
			Allocator,
			Destructor,
			Deallocator,
			Unittest,
			ClassInvariant
		}

		public DMethod(MethodType Type) : this() { SpecialType = Type; }

		public override string ToString(bool Attributes, bool IncludePath)
		{
			var s = base.ToString(Attributes, IncludePath) + "(";
			foreach (var p in Parameters)
				s += (p is AbstractNode ? (p as AbstractNode).ToString(false) : p.ToString()) + ", ";
			return s.Trim(',',' ') + ")";
		}

		public CodeLocation BlockStartLocation
		{
			get
			{
				if (_In != null && _Out != null)
					return _In.Location < _Out.Location ? _In.Location : _Out.Location;
				else if (_In != null)
					return _In.Location;
				else if (_Out != null)
					return _Out.Location;
				else if (_Body != null)
					return _Body.Location;

				return CodeLocation.Empty;
			}
			set { }
		}

		public void Add(INode Node)
		{
			children.Add(Node);
		}

		public void AddRange(IEnumerable<INode> Nodes)
		{
			children.AddRange(Nodes);
		}

		public int Count
		{
			get
			{
				if (children == null)
					return 0;
				return children.Count;
			}
		}

		public IEnumerable<INode> this[string Name]
		{
			get
			{
				return children[Name];
			}
		}

		public IEnumerable<INode> this[int NameHash]
		{
			get
			{
				return children.GetNodes(NameHash);
			}
		}

		public IEnumerator<INode> GetEnumerator()
		{
			return children.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return children.GetEnumerator();
		}


		public void Clear()
		{
			children.Clear();
		}

		/// <summary>
		/// Returns true if the function has got at least one parameter and is a direct child of an abstract syntax tree.
		/// </summary>
		public bool IsUFCSReady
		{
			get
			{
				return Parameters.Count != 0 && Parent is DModule;
			}
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

	public class DClassLike : DBlockNode
	{
		public bool IsAnonymousClass = false;

		public List<ITypeDeclaration> BaseClasses = new List<ITypeDeclaration>();
		public byte ClassType = DTokens.Class;

		public DClassLike() { }
		public DClassLike(byte ClassType)
		{
			this.ClassType = ClassType;
		}

		public override string ToString(bool Attributes, bool IncludePath)
		{
			var sb = new StringBuilder();
			if (Attributes)
				sb.Append(AttributeString).Append(' ');
			sb.Append(DTokens.GetTokenString(ClassType)).Append(' ');

			sb.Append(IncludePath ? GetNodePath(this, true) : Name);

			if (TemplateParameters != null && TemplateParameters.Length > 0)
			{
				sb.Append('(');
				foreach (var tp in TemplateParameters)
				{
					if (tp != null)
						sb.Append(tp.ToString());
					sb.Append(',');
				}
				if (TemplateParameters.Length > 0)
					sb.Remove(sb.Length - 1, 1);
				sb.Append(')');
			}

			if (BaseClasses.Count > 0)
			{
				sb.Append(':');
				foreach (var c in BaseClasses)
					sb.Append(c.ToString()).Append(", ");
				sb.Remove(sb.Length - 2, 2);
			}
			return sb.ToString();
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

	public class DEnum : DBlockNode
	{
		public override string ToString(bool Attributes, bool IncludePath)
		{
			return (Attributes ? (AttributeString + " ") : "") + "enum " + (IncludePath ? GetNodePath(this, true) : Name);
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

	public class DEnumValue : DVariable
	{
		public override void Accept(NodeVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(NodeVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}
	
	public class NamedTemplateMixinNode : DVariable
	{
		public TemplateMixin _mixin;
		public TemplateMixin Mixin
		{
			get{return _mixin;}
			set{
				_mixin=value;
				Name = value.MixinId;
				Type = value.Qualifier;
				Location = value.Location;
				EndLocation = value.EndLocation;
				if(value.Attributes!=null)
					Attributes = new List<DAttribute>(value.Attributes);
				Parent = value.ParentNode;
			}
		}
		
		public NamedTemplateMixinNode(TemplateMixin tmx)
		{
			Mixin = tmx;
		}
		
		public override void AssignFrom(INode other)
		{
			base.AssignFrom(other);
			
			var tmxNode = other as NamedTemplateMixinNode;
			if(tmxNode != null)
				Mixin = tmxNode.Mixin;
		}
		
		public override R Accept<R>(NodeVisitor<R> vis)
		{
			return vis.Visit(this);
		}
		
		public override void Accept(NodeVisitor vis)
		{
			vis.Visit(this);
		}
	}

	/// <summary>
	/// enum isIntOrFloat(T) = is(T == int) || is(T == float);
	/// </summary>
	public class EponymousTemplate : DVariable
	{
		public override bool IsConst {
			get {
				return true;
			}
		}

		public override string ToString (bool Attributes, bool IncludePath)
		{
			var sb = new StringBuilder();
			if (Attributes)
				sb.Append(AttributeString).Append(' ');

			sb.Append(IncludePath ? GetNodePath(this, true) : Name);

			if (TemplateParameters != null && TemplateParameters.Length > 0)
			{
				sb.Append('(');
				foreach (var tp in TemplateParameters)
				{
					if (tp != null)
						sb.Append(tp.ToString());
					sb.Append(',');
				}
				if (TemplateParameters.Length > 0)
					sb.Remove(sb.Length - 1, 1);
				sb.Append(')');
			}

			sb.Append (" = ");

			if (Initializer != null)
				sb.Append (Initializer.ToString ());

			return sb.ToString();
		}

		public override R Accept<R>(NodeVisitor<R> vis)
		{
			return vis.Visit(this);
		}

		public override void Accept(NodeVisitor vis)
		{
			vis.Visit(this);
		}
	}
}
