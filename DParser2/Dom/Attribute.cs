using System;
using System.Collections.Generic;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;

namespace D_Parser.Dom
{
	public abstract class Attribute : ISyntaxRegion, IVisitable<NodeVisitor>
	{ 
		public CodeLocation Location
		{
			get; protected set;
		}

		public CodeLocation EndLocation
		{
			get; protected set;
		}
	
		public abstract void Accept(NodeVisitor vis);
		public abstract R Accept<R>(NodeVisitor<R> vis);
	}

    /// <summary>
    /// Represents an attrribute a declaration may have or consists of.
	/// A modifier or storage class.
    /// </summary>
    public class Modifier : Attribute
    {
        public int Token;
        public object LiteralContent;
        public static readonly Modifier Empty = new Modifier(-1);

        public Modifier(int Token)
        {
            this.Token = Token;
            LiteralContent = null;
        }

        public Modifier(int Token, object Content)
        {
            this.Token = Token;
            this.LiteralContent = Content;
        }

        public override string ToString()
        {
			if (Token == DTokens.PropertyAttribute)
				return "@" + (LiteralContent==null?"": LiteralContent.ToString());
            if (LiteralContent != null)
                return DTokens.GetTokenString(Token) + "(" + LiteralContent.ToString() + ")";
			return DTokens.GetTokenString(Token);
        }

		/// <summary>
		/// Removes all public,private,protected or package attributes from the list
		/// </summary>
		public static void CleanupAccessorAttributes(List<Modifier> HayStack)
		{
			foreach (var i in HayStack.ToArray())
			{
				if (DTokens.VisModifiers[i.Token])
					HayStack.Remove(i);
			}
		}

		/// <summary>
		/// Removes all public,private,protected or package attributes from the stack
		/// </summary>
		public static void CleanupAccessorAttributes(Stack<Modifier> HayStack, int furtherAttrToRemove = -1)
		{
			var l=new List<Modifier>();

			while(HayStack.Count>0)
			{
				var attr=HayStack.Pop();
				if (!DTokens.VisModifiers[attr.Token] && attr.Token != furtherAttrToRemove)
					l.Add(attr);
			}

			foreach (var i in l)
				HayStack.Push(i);
		}

		public static void RemoveFromStack(Stack<Modifier> HayStack, int Token)
		{
			var l = new List<Modifier>();

			while (HayStack.Count > 0)
			{
				var attr = HayStack.Pop();
				if (attr.Token!=Token)
					l.Add(attr);
			}

			foreach (var i in l)
				HayStack.Push(i);
		}

		public static bool ContainsAccessorAttribute(Stack<Modifier> HayStack)
		{
			foreach (var i in HayStack)
				if (DTokens.VisModifiers[i.Token])
					return true;
			return false;
		}

        public static bool ContainsAttribute(Modifier[] HayStack,params int[] NeedleToken)
        {
            var l = new List<int>(NeedleToken);
			if(HayStack!=null)
				foreach (var attr in HayStack)
					if (l.Contains(attr.Token))
						return true;
            return false;
        }
        public static bool ContainsAttribute(List<Modifier> HayStack,params int[] NeedleToken)
        {
            var l = new List<int>(NeedleToken);
            foreach (var attr in HayStack)
                if (l.Contains(attr.Token))
                    return true;
            return false;
        }

        public static bool ContainsAttribute(Stack<Modifier> HayStack,params int[] NeedleToken)
        {
            var l = new List<int>(NeedleToken);
            foreach (var attr in HayStack)
                if (l.Contains(attr.Token))
                    return true;
            return false;
        }


        public bool IsStorageClass
        {
            get
            {
                return DTokens.StorageClass[Token];
            }
        }

		public bool IsProperty
		{
			get { return Token == DTokens.PropertyAttribute; }
		}
	
		public override void Accept(NodeVisitor vis)
		{
			vis.VisitAttribute(this);
		}

		public override R Accept<R>(NodeVisitor<R> vis)
		{
			return vis.VisitAttribute(this);
		}
	}

	public class PragmaAttribute : Modifier
	{
		/// <summary>
		/// Alias for LiteralContent.
		/// </summary>
		public string Identifier
		{
			get { return LiteralContent as string; }
			set { LiteralContent = value; }
		}

		public IExpression[] Arguments;

		public PragmaAttribute() : base(DTokens.Pragma) { }

		public override string ToString()
		{
			var r = "pragma(" + Identifier;

			if (Arguments != null && Arguments.Length > 0)
				foreach (var e in Arguments)
					r += "," + e != null ? e.ToString() : "";

			return r + ")";
		}

		public override void Accept(NodeVisitor vis)
		{
			vis.VisitAttribute(this);
		}

		public override R Accept<R>(NodeVisitor<R> vis)
		{
			return vis.VisitAttribute(this);
		}
	}

	public abstract class DeclarationCondition : Attribute, IDeclarationCondition, ICloneable
	{
		public bool IsNegated { get; protected set; }

		public abstract object Clone();
	
		public override void Accept(NodeVisitor vis)
		{
 			throw new NotImplementedException();
		}

		public override R Accept<R>(NodeVisitor<R> vis)
		{
 			throw new NotImplementedException();
		}
	
		public abstract bool Equals(IDeclarationCondition other);
	}

	public class VersionCondition : DeclarationCondition
	{
		public string VersionId { get; private set; }
		public int VersionNumber { get; private set; }

		public VersionCondition(string versionIdentifier) { VersionId = versionIdentifier; }
		public VersionCondition(int versionNumber) { VersionNumber = versionNumber; }

		public override void Accept(NodeVisitor vis)
		{
			vis.VisitAttribute(this);
		}

		public override R Accept<R>(NodeVisitor<R> vis)
		{
			return vis.VisitAttribute(this);
		}

		public override string ToString()
		{
			return "version("+(VersionId ?? VersionNumber.ToString())+")";
		}
	}

	public class DebugCondition : DeclarationCondition
	{
		public string DebugId { get; private set; }
		public int DebugLevel { get; private set; }

		public DebugCondition() { }
		public DebugCondition(string debugIdentifier) { DebugId = debugIdentifier; }
		public DebugCondition(int debugLevel) { DebugLevel = debugLevel; }

		public override void Accept(NodeVisitor vis)
		{
			vis.VisitAttribute(this);
		}

		public override R Accept<R>(NodeVisitor<R> vis)
		{
			return vis.VisitAttribute(this);
		}

		public override string ToString()
		{
			if (string.IsNullOrEmpty(DebugId) && DebugLevel==0)
				return "debug";

			return "debug(" + (DebugId ?? DebugLevel.ToString()) + ")";
		}
	}
}