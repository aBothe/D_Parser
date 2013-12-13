using System.Collections.Generic;
using D_Parser.Dom.Expressions;
using D_Parser.Dom;
using D_Parser.Resolver.ASTScanner;
using D_Parser.Parser;
using D_Parser.Resolver.Templates;

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
			if ((nameFilterHash != 0 && n.NameHash != nameFilterHash) || !(n.Parent is DModule))
				return false;

			var dc = n as DClassLike;
			if (dc != null && dc.ClassType == DTokens.Template) {
				if (sr is TemplateInstanceExpression || nameFilterHash == 0) {
					var templ = TypeDeclarationResolver.HandleNodeMatch (dc, ctxt, null, sr);
					templ.Tag = new UfcsTag{ firstArgument=firstArgument };
					matches.Add (templ);
				}
			}
			else
				HandleMethod (n as DMethod);

			return false;
		}

		void HandleMethod(DMethod dm, MemberSymbol alreadyResolvedMethod = null)
		{
			if (dm != null && dm.Parameters.Count > 0 && dm.Parameters[0].Type != null)
			{
				var pop = ctxt.ScopedBlock != dm;
				if (pop)
					ctxt.PushNewScope (dm);

				var t = TypeDeclarationResolver.ResolveSingle (dm.Parameters [0].Type, ctxt);
				if (ResultComparer.IsImplicitlyConvertible (firstArgument, t, ctxt)) {
					var res = alreadyResolvedMethod ?? new MemberSymbol (dm, null, sr);
					res.Tag = new UfcsTag{ firstArgument=firstArgument };
					matches.Add (res);
				}

				if (pop)
					ctxt.Pop ();
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
			var us = new UFCSResolver (ctxt, firstArgument, nameHash, nameSr);
			us.IterateThroughScopeLayers (nameLoc, MemberFilter.Methods | MemberFilter.Templates);
			return us.matches;
		}

		public static List<AbstractType> TryResolveUFCS(
			ISemantic firstArgument, 
			PostfixExpression_Access acc, 
			ResolutionContext ctxt)
		{
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
