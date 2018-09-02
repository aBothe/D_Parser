using System;
using System.Collections.Generic;
using D_Parser.Parser;

namespace D_Parser.Dom.Expressions
{
	public class IsExpression : PrimaryExpression, ContainerExpression
	{
		public ITypeDeclaration TestedType;
		public int TypeAliasIdentifierHash;

		public string TypeAliasIdentifier { get { return Strings.TryGet(TypeAliasIdentifierHash); } }

		public CodeLocation TypeAliasIdLocation;
		private TemplateTypeParameter ptp;

		/// <summary>
		/// Persistent parameter object that keeps information about the first specialization 
		/// </summary>
		public TemplateTypeParameter ArtificialFirstSpecParam
		{
			get
			{
				if (ptp == null)
				{
					return ptp = new TemplateTypeParameter(TypeAliasIdentifierHash, TypeAliasIdLocation, null) { 
						Specialization = TypeSpecialization
					};
				}
				return ptp;
			}
		}

		/// <summary>
		/// True if Type == TypeSpecialization instead of Type : TypeSpecialization
		/// </summary>
		public bool EqualityTest;
		public ITypeDeclaration TypeSpecialization;
		public byte TypeSpecializationToken;
		public TemplateParameter[] TemplateParameterList;

		public override string ToString()
		{
			var ret = "is(";

			if (TestedType != null)
				ret += TestedType.ToString();

			if (TypeAliasIdentifierHash != 0)
				ret += ' ' + TypeAliasIdentifier;

			if (TypeSpecialization != null || TypeSpecializationToken != 0)
				ret += (EqualityTest ? "==" : ":") + (TypeSpecialization != null ? 
					TypeSpecialization.ToString() : // Either the specialization declaration
					DTokens.GetTokenString(TypeSpecializationToken)); // or the spec token

			if (TemplateParameterList != null)
			{
				ret += ",";
				foreach (var p in TemplateParameterList)
					ret += p.ToString() + ",";
			}

			return ret.TrimEnd(' ', ',') + ")";
		}

		public CodeLocation Location
		{
			get;
			set;
		}

		public CodeLocation EndLocation
		{
			get;
			set;
		}

		public IEnumerable<IExpression> SubExpressions
		{
			get
			{ 
				if (TestedType != null)
					yield return new TypeDeclarationExpression(TestedType);
			}
		}

		public void Accept(ExpressionVisitor vis)
		{
			vis.Visit(this);
		}

		public R Accept<R>(ExpressionVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}
}

