using System;
using System.Collections.Generic;
using System.Text;

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
	
	public abstract class AtAttribute : DAttribute {}
	
	public class BuiltInAtAttribute : AtAttribute
	{
		public enum BuiltInAttributes
		{
			Disable,
			System,
			Safe,
			Property,
			Trusted
		}
		
		public readonly BuiltInAttributes Kind;
		
		public BuiltInAtAttribute(BuiltInAttributes kind)
		{
			this.Kind = kind;
		}
		
		public override string ToString()
		{
			switch(Kind)
			{
				case BuiltInAttributes.Disable:
					return "@disable";
				case BuiltInAttributes.Property:
					return "@property";
				case BuiltInAttributes.Safe:
					return "@safe";
					case BuiltInAttributes.System:
					return "@system";
				case BuiltInAttributes.Trusted:
					return "@trusted";
			}
			return "@<invalid>";
		}

		
		public override R Accept<R>(NodeVisitor<R> vis)
		{
			return vis.VisitAttribute(this);
		}
		
		public override void Accept(NodeVisitor vis)
		{
			vis.VisitAttribute(this);
		}
	}
	
	public class UserDeclarationAttribute : AtAttribute
	{
		public readonly IExpression[] AttributeExpression;
		
		public UserDeclarationAttribute(IExpression[] x)
		{
			AttributeExpression = x;
		}
		
		public override string ToString()
		{
			if(AttributeExpression == null)
				return "@()";
			if(AttributeExpression.Length == 1)
				return "@"+AttributeExpression[0];
			var sb = new StringBuilder();
			sb.Append("@(");
			for(int i = 0; i<AttributeExpression.Length;i++){
				sb.Append(AttributeExpression[i]);
				sb.Append(',');
			}
			sb.Remove(sb.Length-1,1);
			sb.Append(")");
			return sb.ToString();
		}
		
		public override R Accept<R>(NodeVisitor<R> vis)
		{
			return vis.VisitAttribute(this);
		}
		
		public override void Accept(NodeVisitor vis)
		{
			vis.VisitAttribute(this);
		}
	}

    /// <summary>
    /// Represents an attribute a declaration may have or consists of.
	/// A modifier or storage class.
    /// </summary>
    public class Modifier : DAttribute
    {
        public readonly byte Token;
		public int ContentHash;
		object content;
		public object LiteralContent { 
			get{
				return content ?? Strings.TryGet (ContentHash); 
			} 
			set{ 
				if (value is string) {
					ContentHash = value.GetHashCode ();
					Strings.Add (value as string);
					content = null;
				} else
					content = value;
			 }
		}
        public static readonly Modifier Empty = new Modifier(0xff);

        public Modifier(byte Token)
        {
            this.Token = Token;
        }

        public Modifier(byte Token, string Content)
        {
            this.Token = Token;
			LiteralContent = Content;
        }

        public override string ToString()
        {
			if (ContentHash != 0 || content != null)
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
		public static void CleanupAccessorAttributes(Stack<DAttribute> HayStack, byte furtherAttrToRemove = 0)
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

		public static void RemoveFromStack(Stack<DAttribute> HayStack, byte Token)
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

        public static bool ContainsAttribute(DAttribute[] HayStack,params byte[] NeedleToken)
        {
			if (HayStack == null || HayStack.Length == 0)
				return false;

            var l = new List<byte>(NeedleToken);
			foreach (var attr in HayStack)
				if (attr is Modifier && l.Contains(((Modifier)attr).Token))
					return true;
            
			return false;
        }
        public static bool ContainsAttribute(List<DAttribute> HayStack,params byte[] NeedleToken)
        {
            var l = new List<byte>(NeedleToken);
            if(HayStack!=null)
	            foreach (var attr in HayStack)
					if (attr is Modifier && l.Contains(((Modifier)attr).Token))
	                    return true;
            return false;
        }
		public static bool ContainsAttribute(Stack<DAttribute> HayStack, params byte[] NeedleToken)
		{
			var l = new List<byte>(NeedleToken);
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
	
		public override void Accept(NodeVisitor vis)
		{
			vis.VisitAttribute(this);
		}

		public override R Accept<R>(NodeVisitor<R> vis)
		{
			return vis.VisitAttribute(this);
		}
	}
    
    public class DeprecatedAttribute : Modifier
    {
		public IExpression DeprecationMessage {get{ return LiteralContent as IExpression; }}

		public DeprecatedAttribute(CodeLocation loc, CodeLocation endLoc, IExpression msg = null)
    		: base(DTokens.Deprecated)
    	{
    		this.Location = loc;
    		EndLocation =endLoc;
    		LiteralContent = msg;
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
					r += "," + (e != null ? e.ToString() : "");

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
		public abstract bool Equals(IDeclarationCondition other);
	}

	public class VersionCondition : DeclarationCondition
	{
		public readonly int VersionIdHash;
		public string VersionId {get{ return Strings.TryGet(VersionIdHash); }}
		public readonly int VersionNumber;
		public CodeLocation IdLocation;

		public VersionCondition(string versionIdentifier) { 
			VersionIdHash = versionIdentifier.GetHashCode();
			Strings.Add (versionIdentifier); 
		}
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
			return v != null && v.VersionIdHash == VersionIdHash && v.VersionNumber == VersionNumber;
		}
	}

	public class DebugCondition : DeclarationCondition
	{
		public readonly int DebugIdHash;
		public string DebugId {get{ return Strings.TryGet(DebugIdHash); }}
		public readonly int DebugLevel;
		public CodeLocation IdLocation;

		public DebugCondition() { }
		public DebugCondition(string debugIdentifier) { 
			DebugIdHash = debugIdentifier.GetHashCode ();
			Strings.Add(debugIdentifier); 
		}
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
				return DebugIdHash == 0 && DebugLevel == 0;
			}
		}

		public override string ToString()
		{
			return HasNoExplicitSpecification ? "debug" : ("debug(" + (DebugIdHash != 0 ? DebugId : DebugLevel.ToString()) + ")");
		}

		public override bool Equals(IDeclarationCondition other)
		{
			var v = other as DebugCondition;
			return v != null && v.DebugIdHash == DebugIdHash && v.DebugLevel == DebugLevel;
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
		
		public override void Accept(NodeVisitor vis)
		{
			vis.VisitAttribute(this);
		}

		public override R Accept<R>(NodeVisitor<R> vis)
		{
			return vis.VisitAttribute(this);
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
		
		public override void Accept(NodeVisitor vis)
		{
			vis.VisitAttribute(this);
		}

		public override R Accept<R>(NodeVisitor<R> vis)
		{
			return vis.VisitAttribute(this);
		}
	}
}