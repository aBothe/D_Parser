using System;
using System.Collections.Generic;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;

namespace D_Parser.Dom
{
	public interface ITypeDeclaration : ISyntaxRegion, IVisitable<TypeDeclarationVisitor>
	{
		new CodeLocation Location { get; set; }
		new CodeLocation EndLocation { get; set; }

		ITypeDeclaration InnerDeclaration { get; set; }
		ITypeDeclaration InnerMost { get; set; }

		/// <summary>
		/// Used e.g. if it's known that a type declaration expresses a variable's name
		/// </summary>
		bool ExpressesVariableAccess { get; set; }

		string ToString();
		string ToString(bool IncludesBase);

		R Accept<R>(TypeDeclarationVisitor<R> vis);
		
		ulong GetHash();
	}
	
	public abstract class AbstractTypeDeclaration : ITypeDeclaration
	{
		public ITypeDeclaration InnerMost
		{
			get
			{
				if (InnerDeclaration == null)
					return this;
				else
					return InnerDeclaration.InnerMost;
			}
			set
			{
				if (InnerDeclaration == null)
					InnerDeclaration = value;
				else
					InnerDeclaration.InnerMost = value;
			}
		}
	
		public ITypeDeclaration InnerDeclaration
		{
			get;
			set;
		}
	
		public override string ToString()
		{
			return ToString(true);
		}
	
		public abstract string ToString(bool IncludesBase);
	
		public static implicit operator String(AbstractTypeDeclaration d)
		{
			return d == null? null : d.ToString(false);
		}
	
		CodeLocation _loc=CodeLocation.Empty;
	
		/// <summary>
		/// The type declaration's start location.
		/// If inner declaration given, its start location will be returned.
		/// </summary>
		public CodeLocation Location
		{
			get 
			{
				if (_loc != CodeLocation.Empty || InnerDeclaration==null)
					return _loc;
	
				return InnerMost.Location;
			}
			set { _loc = value; }
		}
	
		/// <summary>
		/// The actual start location without regarding inner declarations.
		/// </summary>
		public CodeLocation NonInnerTypeDependendLocation
		{
			get { return _loc; }
		}
	
		public CodeLocation EndLocation
		{
			get;
			set;
		}
	
	
		public bool ExpressesVariableAccess
		{
			get;
			set;
		}
	
		public abstract void Accept(TypeDeclarationVisitor vis);
		public abstract R Accept<R>(TypeDeclarationVisitor<R> vis);
		
		public virtual ulong GetHash()
		{
			return (ulong)ToString().GetHashCode();
		}
	}
}
