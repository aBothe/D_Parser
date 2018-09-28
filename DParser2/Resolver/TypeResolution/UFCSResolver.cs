using System.Collections.Generic;
using D_Parser.Dom.Expressions;
using D_Parser.Dom;
using D_Parser.Resolver.ASTScanner;
using D_Parser.Parser;
using System;

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
			public const string Id = "UfcsTag";
			public ISemantic firstArgument;
		}

		public static bool IsUfcsResult(AbstractType t, out ISemantic firstArgument)
		{
			var o = t.Tag<UfcsTag>(UfcsTag.Id);

			if (o == null) {
				firstArgument = null;
				return false;
			}

			firstArgument = o.firstArgument;
			return true;
		}

		protected override bool PreCheckItem (INode n)
		{
			if (ctxt.CancellationToken.IsCancellationRequested)
				return false;

			if ((nameFilterHash != 0 && n.NameHash != nameFilterHash) || (!(n is ImportSymbolNode) && !(n.Parent is DModule)))
				return false;

			if (n is DClassLike)
				return (n as DClassLike).ClassType == DTokens.Template;

			if (n is DVariable)
				return (n as DVariable).IsAlias;

			return n is DMethod;
		}

		protected override void HandleItem (INode n)
		{
			DSymbol ds;
			DVariable dv;
			var dc = n as DClassLike;
			if (dc != null && dc.ClassType == DTokens.Template) {
				if (sr is TemplateInstanceExpression || nameFilterHash == 0) {
					var templ = TypeDeclarationResolver.HandleNodeMatch (dc, ctxt, null, sr);
					templ.Tag(UfcsTag.Id, new UfcsTag{ firstArgument=firstArgument });
					matches.Add (templ);
				}
			}
			else if(n is DMethod)
				HandleMethod (n as DMethod);
			else if ((dv = n as DVariable) != null && dv.IsAlias)
			{
				var t = DResolver.StripAliasedTypes(TypeDeclarationResolver.HandleNodeMatch(n, ctxt, null, sr));

				foreach (var ov in AmbiguousType.TryDissolve(t))
				{
					ds = DResolver.StripAliasedTypes(ov) as DSymbol;
					if (ds is MemberSymbol && ds.Definition is DMethod)
						HandleMethod(ds.Definition as DMethod, ov as MemberSymbol);
					else if (ds != null && (dc = ds.Definition as DClassLike) != null && dc.ClassType == DTokens.Template)
					{
						if (sr is TemplateInstanceExpression || nameFilterHash == 0)
						{
							ds.Tag(UfcsTag.Id, new UfcsTag { firstArgument = firstArgument });
							matches.Add(ds);
						}
					}
					// Perhaps other types may occur here as well - but which remain then to be added?
				}
			}
		}

		void HandleMethod(DMethod dm, MemberSymbol alreadyResolvedMethod = null)
		{
			if (dm != null && dm.Parameters.Count > 0 && dm.Parameters[0].Type != null)
			{
				var loc = dm.Body != null ? dm.Body.Location : dm.Location;
				using (alreadyResolvedMethod != null ? ctxt.Push(alreadyResolvedMethod, loc) : ctxt.Push(dm, loc))
				{
					var t = TypeDeclarationResolver.ResolveSingle(dm.Parameters[0].Type, ctxt);
					if (ResultComparer.IsImplicitlyConvertible(firstArgument, t, ctxt))
					{
						var res = alreadyResolvedMethod ?? TypeDeclarationResolver.HandleNodeMatch(dm, ctxt, typeBase: sr);
						res.Tag(UfcsTag.Id, new UfcsTag { firstArgument = firstArgument });
						matches.Add(res);
					}
				}
			}
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

		public override IEnumerable<DModule> PrefilterSubnodes (ModulePackage pack, Action<ModulePackage> packageHandler)
		{
			yield break;
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
				name = ((IdentifierExpression)acc.AccessExpression).IdHash;
			else if (acc.AccessExpression is TemplateInstanceExpression)
				name = ((TemplateInstanceExpression)acc.AccessExpression).TemplateIdHash;
			else
				return new List<AbstractType> ();

			return TryResolveUFCS (firstArgument, name, acc.PostfixForeExpression.Location, ctxt, acc);
		}
	}
}
