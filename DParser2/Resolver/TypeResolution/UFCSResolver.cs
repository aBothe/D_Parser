using System.Collections.Generic;
using D_Parser.Dom.Expressions;
using D_Parser.Dom;
using D_Parser.Resolver.ASTScanner;
using D_Parser.Parser;

namespace D_Parser.Resolver.TypeResolution
{
	/// <summary>
	/// UFCS: User function call syntax;
	/// A base expression will be used as a method's first call parameter 
	/// so it looks like the first expression had a respective sub-method.
	/// Example:
	/// assert("fdas".reverse() == "asdf"); -- reverse() will be called with "fdas" as the first argument.
	/// 
	/// </summary>
	public class UFCSResolver : AbstractVisitor
	{
		readonly int nameFilterHash;
		readonly ISemantic firstArgument;
		readonly ISyntaxRegion sr;
		readonly List<AbstractType> matches = new List<AbstractType>();

		UFCSResolver(ResolutionContext ctxt, ISemantic firstArg, int nameHash = 0, ISyntaxRegion sr = null) : base(ctxt)
		{
			this.firstArgument = firstArg;
			this.nameFilterHash = nameHash;
			this.sr = sr;
		}

		class UfcsTag
		{
			public ISemantic firstArgument;
		}

		public static bool IsUfcsResult(AbstractType t, out ISemantic firstArgument)
		{
			var o = t.Tag as UfcsTag;

			if (o == null) {
				firstArgument = null;
				return false;
			}

			firstArgument = o.firstArgument;
			return true;
		}

		protected override bool HandleItem (INode n)
		{
			if ((nameFilterHash != 0 && n.NameHash != nameFilterHash) || (!(n is ImportSymbolNode) && !(n.Parent is DModule)))
				return false;
			DSymbol ds;
			DVariable dv;
			var dc = n as DClassLike;
			if (dc != null && dc.ClassType == DTokens.Template) {
				if (sr is TemplateInstanceExpression || nameFilterHash == 0) {
					var templ = TypeDeclarationResolver.HandleNodeMatch (dc, ctxt, null, sr);
					templ.Tag = new UfcsTag{ firstArgument=firstArgument };
					matches.Add (templ);
				}
			}
			else if(n is DMethod)
				HandleMethod (n as DMethod);
			else if ((dv = n as DVariable) != null && dv.IsAlias)
			{
				var t = TypeDeclarationResolver.HandleNodeMatch(n, ctxt, null, sr);

				if (t is AmbiguousType)
				{
					foreach (var ov in (t as AmbiguousType).Overloads)
					{
						ds = ov as DSymbol;
						if (ds is MemberSymbol && ds.Definition is DMethod)
							HandleMethod(ds.Definition as DMethod, ov as MemberSymbol);
						else if (ds != null && ds.Definition is DClassLike)
						{
							ds.Tag = new UfcsTag { firstArgument = firstArgument };
							matches.Add(ds);
						}
					}
				}
				else
				{
					ds = t as DSymbol;
					if (t is MemberSymbol && ds.Definition is DMethod)
						HandleMethod(ds.Definition as DMethod, t as MemberSymbol);
					else if (ds != null && ds.Definition is DClassLike)
					{
						t.Tag = new UfcsTag { firstArgument = firstArgument };
						matches.Add(ds);
					}
					// Perhaps other types may occur here as well - but which remain then to be added?
				}
			}

			return false;
		}

		void HandleMethod(DMethod dm, MemberSymbol alreadyResolvedMethod = null)
		{
			if (dm != null && dm.Parameters.Count > 0 && dm.Parameters[0].Type != null)
			{
				using (alreadyResolvedMethod != null ? ctxt.Push(alreadyResolvedMethod, dm.Body) : ctxt.Push(dm, dm.Body))
				{
					var t = TypeDeclarationResolver.ResolveSingle(dm.Parameters[0].Type, ctxt);
					if (ResultComparer.IsImplicitlyConvertible(firstArgument, t, ctxt))
					{
						var res = alreadyResolvedMethod ?? TypeDeclarationResolver.HandleNodeMatch(dm, ctxt, typeBase: sr);
						res.Tag = new UfcsTag { firstArgument = firstArgument };
						matches.Add(res);
					}
				}
			}
		}

		protected override bool HandleItem (PackageSymbol pack)
		{
			return false;
		}

		public override IEnumerable<INode> PrefilterSubnodes(IBlockNode bn)
		{
			if (bn is DModule)
			{
				if (nameFilterHash == 0)
					return bn.Children;
				return bn [nameFilterHash];
			}
			return null;
		}

		public override IEnumerable<DModule> PrefilterSubnodes (ModulePackage pack, out ModulePackage[] subPackages)
		{
			subPackages = null;
			return null;
		}

		public static List<AbstractType> TryResolveUFCS(
			ISemantic firstArgument,int nameHash,CodeLocation nameLoc,
			ResolutionContext ctxt, ISyntaxRegion nameSr = null)
		{
			if (firstArgument == null || ctxt == null)
				return new List<AbstractType>();

			var us = new UFCSResolver (ctxt, firstArgument, nameHash, nameSr);
			us.IterateThroughScopeLayers (nameLoc, MemberFilter.Methods | MemberFilter.Templates);
			return us.matches;
		}

		public static List<AbstractType> TryResolveUFCS(
			ISemantic firstArgument, 
			PostfixExpression_Access acc, 
			ResolutionContext ctxt)
		{
			if (firstArgument == null || acc == null || ctxt == null)
				return new List<AbstractType>();

			int name;

			if (acc.AccessExpression is IdentifierExpression)
				name = ((IdentifierExpression)acc.AccessExpression).ValueStringHash;
			else if (acc.AccessExpression is TemplateInstanceExpression)
				name = ((TemplateInstanceExpression)acc.AccessExpression).TemplateIdHash;
			else
				return new List<AbstractType> ();

			return TryResolveUFCS (firstArgument, name, acc.PostfixForeExpression.Location, ctxt, acc);
		}
	}
}
