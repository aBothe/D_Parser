using System;

namespace D_Parser.Dom
{
	public abstract class AbstractNode:INode
	{
		ITypeDeclaration _Type;
		string _Name="";
		protected WeakReference _Parent;
		string _Description="";
		CodeLocation _StartLocation;
		CodeLocation _EndLocation;

		public CodeLocation EndLocation
		{
			get { return _EndLocation; }
			set { _EndLocation = value; }
		}

		public CodeLocation Location
		{
			get { return _StartLocation; }
			set { _StartLocation = value; }
		}

		public virtual string Description
		{
			get { return _Description; }
			set { _Description = value; }
		}

		public virtual ITypeDeclaration Type
		{
			get { return _Type; }
			set { _Type = value; }
		}

		public string Name
		{
			get { return _Name; }
			set { _Name = value; }
		}

		public CodeLocation NameLocation
		{
			get;
			set;
		}

		public bool IsAnonymous { get { return string.IsNullOrEmpty(Name); } }

		public INode Parent
		{
			get { 
				return _Parent != null ? _Parent.Target as INode : null; 
			}
			set { 
				if (_Parent == null)
					_Parent = new WeakReference (value); 
				else 
					_Parent.Target = value;
			}
		}

		public override string ToString()
		{
			return ToString(true,true);
		}

		public string ToString(bool IncludePath)
		{
			return ToString(true, IncludePath);
		}

		public static string GetNodePath(INode n,bool includeActualNodesName)
		{
			string path = "";
			var curParent = includeActualNodesName?n:n.Parent;
			while (curParent != null)
			{
				// Also include module path
				if (curParent is DModule)
					path = (curParent as DModule).ModuleName + "." + path;
				else
					path = curParent.Name + "." + path;

				curParent = curParent.Parent;
			}
			return path.Trim('.');
		}

		public virtual string ToString(bool Attributes,bool IncludePath)
		{
			string s = "";
			// Type
			if (Type != null)
				s += Type.ToString() + " ";

			// Path + Name
			if (IncludePath)
				s += GetNodePath(this, true);
			else
				s += Name;

			return s.Trim();
		}

		public virtual void AssignFrom(INode other)
		{
			Type = other.Type;
			Name = other.Name;
			NameLocation = other.NameLocation;

			Parent = other.Parent;
			Description = other.Description;
			Location = other.Location;
			EndLocation = other.EndLocation;
		}

		public INode NodeRoot
		{
			get
			{
				var t = _Parent != null ? _Parent.Target as INode : null;
				return t != null ? t.NodeRoot : this;
			}
		}

		public abstract void Accept(NodeVisitor vis);
		public abstract R Accept<R>(NodeVisitor<R> vis);
	}
}
