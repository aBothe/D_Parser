using D_Parser.Dom;
using D_Parser.Parser;
using D_Parser.Resolver.TypeResolution;
using System;
using System.Collections.Generic;

namespace D_Parser.Resolver.ASTScanner
{
	class NameScan : AbstractVisitor
	{
		protected readonly int filterHash;
		protected readonly ISyntaxRegion idObject;
		protected readonly List<AbstractType> matches_types = new List<AbstractType>();
		protected bool stopEnumeration = false;
		protected override bool StopEnumerationOnNextScope { get { return stopEnumeration; } }
		
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

				if (stackSize++	< 7)
					scan.IterateThroughScopeLayers(caret);
				stackSize--;

				if (idObject !=	null)
					ctxt.NameScanCache.Add(scan, idObject, nameHash);
			}
			return scan.matches_types;
		}
		
		public static AbstractType ScanForCFunction(ResolutionContext ctxt, string funcName, bool isCFunction = true)
		{
			var extC = new Modifier(DTokens.Extern, "C");
			foreach(var pc in ctxt.ParseCache.EnumRootPackagesSurroundingModule(ctxt.ScopedBlock))
			{
				foreach(var mod in pc)
				{
					var nodes = mod[funcName];
					if(nodes != null)
					{
						foreach(var n in nodes){
							if(n is DMethod)
							{
								var dm = n as DMethod;
								if(!isCFunction || dm.ContainsAttribute(extC))
									return TypeDeclarationResolver.HandleNodeMatch(n, ctxt);
							}
						}
					}
				}
			}
			return null;
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
			var res = TypeDeclarationResolver.HandleNodeMatch (n, ctxt, resolvedCurrentScope, idObject);
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
