using D_Parser.Dom;
using D_Parser.Resolver.TypeResolution;
using System;
using System.Collections.Generic;

namespace D_Parser.Resolver.ASTScanner
{
	class NameScan : AbstractVisitor
	{
		protected readonly int filterHash;
		protected readonly ISyntaxRegion idObject;
		private readonly List<AbstractType> matches_types = new List<AbstractType>();
		protected bool stopEnumeration = false;
		protected override bool StopEnumerationOnNextScope { get { return stopEnumeration; } }
		
		protected List<AbstractType> GetMatches()
		{
			var resolvedMatches = new List<AbstractType>();
			if (stackSize >= 7)
				return matches_types;

			try
			{
				stackSize++;
				foreach (var match in matches_types)
				{
					if (match is DSymbol)
						resolvedMatches.Add(DSymbolBaseTypeResolver.ResolveBaseType(match as DSymbol, ctxt, idObject));
					else
						resolvedMatches.Add(match);
				}
			}
			finally
			{
				stackSize--;
			}
			return resolvedMatches;
		}

		[System.Diagnostics.DebuggerStepThrough]
		protected NameScan(ResolutionContext ctxt, int filterHash, ISyntaxRegion idObject) : base(ctxt)
		{
			this.filterHash = filterHash;
			this.idObject = idObject;
		}

		[ThreadStatic]
		static int stackSize = 0;

		public static List<AbstractType> SearchAndResolve(ResolutionContext ctxt, CodeLocation caret, int nameHash, ISyntaxRegion idObject=null)
		{
			NameScan scan = null;
			if (idObject !=	null)
				scan = ctxt.NameScanCache.TryGetType(idObject, nameHash);

			if (scan ==	null)
			{
				scan = new NameScan(ctxt, nameHash,	idObject);

				scan.IterateThroughScopeLayers(caret);

				if (idObject !=	null)
					ctxt.NameScanCache.Add(scan, idObject, nameHash);
			}
			return scan.GetMatches();
		}

		public override IEnumerable<INode> PrefilterSubnodes (IBlockNode bn)
		{
			return bn.Children.GetNodes (filterHash);
		}

		public override IEnumerable<DModule> PrefilterSubnodes (ModulePackage pack, Action<ModulePackage> packageHandler)
		{
			var subPack = pack.GetPackage (filterHash);
			if (subPack != null)
				packageHandler(subPack);
			
			var ast = pack.GetModule (filterHash);
			if (ast != null)
				yield return ast;
		}

		protected override bool PreCheckItem (INode n)
		{
			return n.NameHash == filterHash;
		}

		protected override void HandleItem (INode n, AbstractType resolvedCurrentScope)
		{
			var res = TypeDeclarationResolver.HandleNodeMatch (n, ctxt, resolvedCurrentScope, idObject, false);
			if (res != null)
				matches_types.Add (res);
			stopEnumeration = true;
		}

		protected override void HandleItem (PackageSymbol pack)
		{
			// Packages were filtered in PrefilterSubnodes already..so just add & return
			matches_types.Add (pack);
			stopEnumeration = true;
		}
	}
}
