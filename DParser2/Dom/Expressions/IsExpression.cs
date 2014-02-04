using System;
using D_Parser.Parser;

namespace D_Parser.Dom.Expressions
{
	public class IsExpression : PrimaryExpression,ContainerExpression
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

		public IExpression[] SubExpressions
		{
			get
			{ 
				if (TestedType != null)
					return new[] { new TypeDeclarationExpression(TestedType) };

				return null;
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

		public ulong GetHash()
		{
			ulong hashCode = DTokens.Is;
			unchecked
			{
				if (TestedType != null)
					hashCode += 1000000007 * TestedType.GetHash();
				hashCode += 1000000009 * (ulong)TypeAliasIdentifierHash;
				if (ptp != null)//TODO: Create hash functions of template type parameters
					hashCode += 1000000021 * (ulong)ptp.ToString().GetHashCode();
				hashCode += 1000000033uL * (EqualityTest ? 2uL : 1uL);
				if (TypeSpecialization != null)
					hashCode += 1000000087 * TypeSpecialization.GetHash();
				hashCode += 1000000093 * (ulong)TypeSpecializationToken;
				if (TemplateParameterList != null)
					for (int i = TemplateParameterList.Length; i != 0;)
						hashCode += 1000000097 * (ulong)(i * TemplateParameterList[--i].ToString().GetHashCode());
			}
			return hashCode;
		}
	}
}

