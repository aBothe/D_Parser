using System;
using System.Collections.Generic;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;

namespace D_Parser.Dom
{
    /// <summary>
    /// Identifier, e.g. "foo"
    /// </summary>
    public class IdentifierDeclaration : AbstractTypeDeclaration
    {
		public bool ModuleScoped;
		public int IdHash;
		public string Id
		{
			get{ return Strings.TryGet (IdHash); }
			set{ IdHash = value != null ? value.GetHashCode () : 0; Strings.Add (value); }
		}

        public IdentifierDeclaration() { }
		public IdentifierDeclaration(int IdHash) { this.IdHash = IdHash; }
        public IdentifierDeclaration(string Value)
        { this.Id = Value; }

		public override string ToString(bool IncludesBase)
		{
			return (ModuleScoped?".":"")+ (IncludesBase&& InnerDeclaration != null ? (InnerDeclaration.ToString() + ".") : "") + Id;
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

	/// <summary>
	/// int, void, float
	/// </summary>
    public class DTokenDeclaration : AbstractTypeDeclaration
    {
        public byte Token;

        public DTokenDeclaration() { }
        public DTokenDeclaration(byte Token)
        { this.Token = Token; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="p">The token</param>
        /// <param name="td">Its base token</param>
        public DTokenDeclaration(byte p, ITypeDeclaration td)
        {
            Token = p;
            InnerDeclaration = td;
        }

		public override string ToString(bool IncludesBase)
		{
			return (IncludesBase && InnerDeclaration!=null?(InnerDeclaration.ToString() + '.'):"") + DTokens.GetTokenString(Token);
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

    /// <summary>
    /// Extends an identifier by an array literal.
    /// </summary>
    public class ArrayDecl : AbstractTypeDeclaration
    {
		/// <summary>
		/// Used for associative arrays; Contains all declaration parts that are located inside the square brackets.
		/// Integer by default.
		/// </summary>
        public ITypeDeclaration KeyType=new DTokenDeclaration(DTokens.Int);

		public bool ClampsEmpty = true;

		public IExpression KeyExpression;

		public bool IsRanged
		{
			get {
				return KeyExpression is PostfixExpression_Slice;
			}
		}
		public bool IsAssociative
		{
			get
			{
				return KeyType!=null && (!(KeyType is DTokenDeclaration) ||
					!DTokens.BasicTypes_Integral[(KeyType as DTokenDeclaration).Token]);
			}
		}

		/// <summary>
		/// Alias for InnerDeclaration; contains all declaration parts that are located in front of the square brackets.
		/// </summary>
		public ITypeDeclaration ValueType
		{
			get { return InnerDeclaration; }
			set { InnerDeclaration = value; }
		}

		public override string ToString(bool IncludesBase)
        {
			var ret = "";

			if (IncludesBase && ValueType != null)
				ret = ValueType.ToString();

			ret += "[";
			
			if(!ClampsEmpty)
			{
				if (KeyExpression != null)
					ret += KeyExpression.ToString();
				else if (KeyType != null)
					ret += KeyType.ToString();
			}

			return ret + "]";
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

    public class DelegateDeclaration : AbstractTypeDeclaration
    {
		/// <summary>
		/// Alias for InnerDeclaration.
		/// Contains 'int' in
		/// int delegate() foo;
		/// </summary>
        public ITypeDeclaration ReturnType
        {
            get { return InnerDeclaration; }
            set { InnerDeclaration = value; }
        }
        /// <summary>
        /// Is it a function(), not a delegate() ?
        /// </summary>
        public bool IsFunction = false;

        public List<INode> Parameters = new List<INode>();
		public DAttribute[] Modifiers;

		public override string ToString(bool IncludesBase)
        {
            string ret = (IncludesBase && ReturnType!=null? ReturnType.ToString():"") + (IsFunction ? " function" : " delegate") + "(";

            foreach (DVariable n in Parameters)
            {
                if (n.Type != null)
                    ret += n.Type.ToString();

				if (n.NameHash != 0)
                    ret += (" " + n.Name);

                if (n.Initializer != null)
                    ret += "= " + n.Initializer.ToString();

                ret += ", ";
            }
            ret = ret.TrimEnd(',', ' ') + ")";
            return ret;
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

    /// <summary>
    /// int* ptr;
    /// </summary>
    public class PointerDecl : AbstractTypeDeclaration
    {
        public PointerDecl() { }
        public PointerDecl(ITypeDeclaration BaseType) { InnerDeclaration = BaseType; }

		public override string ToString(bool IncludesBase)
        {
            return (IncludesBase&& InnerDeclaration != null ? InnerDeclaration.ToString() : "") + "*";
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

    /// <summary>
    /// const(char)
    /// </summary>
    public class MemberFunctionAttributeDecl : AbstractTypeDeclaration
    {
        /// <summary>
        /// Equals <see cref="Token"/>
        /// </summary>
		public byte Modifier=DTokens.Const;

        public ITypeDeclaration InnerType;

        public MemberFunctionAttributeDecl() { }
        public MemberFunctionAttributeDecl(byte ModifierToken) { this.Modifier = ModifierToken; }

		public override string ToString(bool IncludesBase)
        {
            return (IncludesBase&& InnerDeclaration != null ? (InnerDeclaration.ToString()+" ") : "") + DTokens.GetTokenString(Modifier) + "(" + (InnerType != null ? InnerType.ToString() : "") + ")";
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
    
    /// <summary>
    /// typeof(...)
    /// </summary>
    public class TypeOfDeclaration : AbstractTypeDeclaration
    {
    	public IExpression Expression;
    	
		public override string ToString(bool IncludesBase)
		{
			return (IncludesBase&& InnerDeclaration != null ? (InnerDeclaration.ToString()+" ") : "") + "typeof(" + (Expression != null ? Expression.ToString() : "") + ")";
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

	/// <summary>
	/// __vector(...)
	/// </summary>
	public class VectorDeclaration : AbstractTypeDeclaration
	{
		public IExpression Id;

		public override string ToString(bool IncludesBase)
		{
			return (IncludesBase && InnerDeclaration != null ? (InnerDeclaration.ToString() + " ") : "") + "__vector(" + (Id != null ? Id.ToString() : "") + ")";
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

	/// <summary>
	/// void foo(int i,...) {} -> foo(1,2,3,4); = legal
	/// </summary>
    public class VarArgDecl : AbstractTypeDeclaration
    {
        public VarArgDecl() { }
        public VarArgDecl(ITypeDeclaration BaseIdentifier) { InnerDeclaration = BaseIdentifier; }

		public override string ToString(bool IncludesBase)
        {
            return (IncludesBase&& InnerDeclaration != null ? InnerDeclaration.ToString() : "") + "...";
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
}