using System;
using System.Collections.Generic;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;

namespace D_Parser.Dom
{
	public abstract class DAttribute : ISyntaxRegion, IVisitable<NodeVisitor>
	{ 
		public CodeLocation Location
		{
			get; set;
		}

		public CodeLocation EndLocation
		{
			get; set;
		}
	
		public abstract void Accept(NodeVisitor vis);
		public abstract R Accept<R>(NodeVisitor<R> vis);
	}

    /// <summary>
    /// Represents an attrribute a declaration may have or consists of.
	/// A modifier or storage class.
    /// </summary>
    public class Modifier : DAttribute
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
		public static void CleanupAccessorAttributes(List<DAttribute> HayStack)
		{
			foreach (var i in HayStack.ToArray())
			{
				if (i is Modifier && DTokens.VisModifiers[((Modifier)i).Token])
					HayStack.Remove(i);
			}
		}

		/// <summary>
		/// Removes all public,private,protected or package attributes from the stack
		/// </summary>
		public static void CleanupAccessorAttributes(Stack<DAttribute> HayStack, int furtherAttrToRemove = -1)
		{
			var l=new List<DAttribute>();

			while(HayStack.Count>0)
			{
				var attr=HayStack.Pop();
				var m = attr as Modifier;
				if (m==null || (!DTokens.VisModifiers[m.Token] && m.Token != furtherAttrToRemove))
					l.Add(attr);
			}

			foreach (var i in l)
				HayStack.Push(i);
		}

		public static void RemoveFromStack(Stack<DAttribute> HayStack, int Token)
		{
			var l = new List<DAttribute>();

			while (HayStack.Count > 0)
			{
				var attr = HayStack.Pop();
				if (!(attr is Modifier) || ((Modifier)attr).Token!=Token)
					l.Add(attr);
			}

			foreach (var i in l)
				HayStack.Push(i);
		}

		public static bool ContainsAccessorAttribute(Stack<DAttribute> HayStack)
		{
			foreach (var i in HayStack)
				if (i is Modifier && DTokens.VisModifiers[((Modifier)i).Token])
					return true;
			return false;
		}

        public static bool ContainsAttribute(DAttribute[] HayStack,params int[] NeedleToken)
        {
			if (HayStack == null || HayStack.Length == 0)
				return false;

            var l = new List<int>(NeedleToken);
			foreach (var attr in HayStack)
				if (attr is Modifier && l.Contains(((Modifier)attr).Token))
					return true;
            
			return false;
        }
        public static bool ContainsAttribute(List<DAttribute> HayStack,params int[] NeedleToken)
        {
            var l = new List<int>(NeedleToken);
            if(HayStack!=null)
	            foreach (var attr in HayStack)
					if (attr is Modifier && l.Contains(((Modifier)attr).Token))
	                    return true;
            return false;
        }
		public static bool ContainsAttribute(Stack<DAttribute> HayStack, params int[] NeedleToken)
		{
			var l = new List<int>(NeedleToken);
			foreach (var attr in HayStack)
				if (attr is Modifier && l.Contains(((Modifier)attr).Token))
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

	public abstract class DeclarationCondition : DAttribute, IDeclarationCondition
	{
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
		public readonly string VersionId;
		public readonly int VersionNumber;
		public CodeLocation IdLocation;

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

		public override bool Equals(IDeclarationCondition other)
		{
			var v = other as VersionCondition;
			return v != null && v.VersionId == VersionId && v.VersionNumber == VersionNumber;
		}
	}

	public class DebugCondition : DeclarationCondition
	{
		public string DebugId { get; private set; }
		public int DebugLevel { get; private set; }
		public CodeLocation IdLocation;

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

		public bool HasNoExplicitSpecification
		{
			get {
				return DebugId == null && DebugLevel == 0;
			}
		}

		public override string ToString()
		{
			return HasNoExplicitSpecification ? "debug" : ("debug(" + (DebugId ?? DebugLevel.ToString()) + ")");
		}

		public override bool Equals(IDeclarationCondition other)
		{
			var v = other as DebugCondition;
			return v != null && v.DebugId == DebugId && v.DebugLevel == DebugLevel;
		}
	}

	public class StaticIfCondition : DeclarationCondition
	{
		public readonly IExpression Expression;

		public StaticIfCondition(IExpression x) { Expression = x; }

		public override bool Equals(IDeclarationCondition other)
		{
			var cd = other as StaticIfCondition;
			return cd != null && cd.Expression == Expression;
		}

		public override string ToString()
		{
			return "static if(" + (Expression == null ? "" : Expression.ToString()) + ")";
		}
	}

	public class NegatedDeclarationCondition : DeclarationCondition
	{
		public readonly DeclarationCondition FirstCondition;

		public NegatedDeclarationCondition(DeclarationCondition dc)
		{
			this.FirstCondition = dc;
		}

		public override bool Equals(IDeclarationCondition other)
		{
			var o = other as NegatedDeclarationCondition;
			return o != null && o.FirstCondition.Equals(FirstCondition);
		}

		public override string ToString()
		{
			return "<not>"+FirstCondition.ToString();
		}
	}
}