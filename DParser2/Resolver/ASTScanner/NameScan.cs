using D_Parser.Dom;
using D_Parser.Parser;
using D_Parser.Resolver.TypeResolution;
using System.Collections.Generic;

namespace D_Parser.Resolver.ASTScanner
{
	class NameScan : AbstractVisitor
	{
		protected readonly int filterHash;
		protected readonly ISyntaxRegion idObject;
		protected readonly List<AbstractType> matches_types = new List<AbstractType>();
		
		protected NameScan(ResolutionContext ctxt, int filterHash, ISyntaxRegion idObject) : base(ctxt)
		{
			this.filterHash = filterHash;
			this.idObject = idObject;
		}

		public static List<AbstractType> SearchAndResolve(ResolutionContext ctxt, CodeLocation caret, int nameHash, ISyntaxRegion idObject=null)
		{
			var scan = new NameScan(ctxt, nameHash, idObject);

			scan.IterateThroughScopeLayers(caret);

			return scan.matches_types;
		}
		
		public static AbstractType ScanForCFunction(ResolutionContext ctxt, string funcName, bool isCFunction = true)
		{
			var extC = new Modifier(DTokens.Extern, "C");
			foreach(var pc in ctxt.ParseCache)
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
		
		public override IEnumerable<INode> PrefilterSubnodes(IBlockNode bn)
		{
			return bn.Children.GetNodes(filterHash);
		}
		
		public override IEnumerable<DModule> PrefilterSubnodes(ModulePackage pack, out ModulePackage[] subPackages)
		{
			var subPack = pack.GetPackage(filterHash);
			if(subPack != null)
				subPackages = new[]{ subPack };
			else
				subPackages = null;
			
			var ast = pack.GetModule(filterHash);
			if(ast != null)
				return new[]{ast};
			return null;
		}

		protected override bool HandleItem(INode n)
		{
            if (n != null && n.NameHash == filterHash)
            {
            	matches_types.Add(TypeDeclarationResolver.HandleNodeMatch(n, ctxt, TemporaryResolvedNodeParent, idObject));
            	return true;
            }

            return false;
		}
		
		protected override bool HandleItem(PackageSymbol pack)
		{
			// Packages were filtered in PrefilterSubnodes already..so just add & return
			matches_types.Add(pack);
			return true;
		}
	}
	
	class SingleNodeNameScan : NameScan
	{
		protected SingleNodeNameScan(ResolutionContext ctxt, int filterHash, ISyntaxRegion idObject) : base(ctxt, filterHash, idObject) {}
		/*
		public static List<AbstractType> SearchChildrenAndResolve(ResolutionContext ctxt, IBlockNode block, string name, ISyntaxRegion idObject = null)
		{
			return SearchChildrenAndResolve (ctxt, block, name.GetHashCode(), idObject);
		}

		/// <summary>
		/// Scans a block node. Not working with DMethods.
		/// Automatically resolves node matches so base types etc. will be specified directly after the search operation.
		/// </summary>
		public static List<AbstractType> SearchChildrenAndResolve(ResolutionContext ctxt, IBlockNode block, int nameHash, ISyntaxRegion idObject = null)
		{
			var scan = new SingleNodeNameScan(ctxt, nameHash, idObject);

			scan.ScanBlock(block, CodeLocation.Empty, MemberFilter.All);

			return scan.matches_types;
		}*/

		public static List<AbstractType> SearchChildrenAndResolve(ResolutionContext ctxt, DSymbol t, string name, ISyntaxRegion idObject = null)
		{
			return SearchChildrenAndResolve(ctxt, t, name.GetHashCode(), idObject);
		}

		/// <summary>
		/// Scans a block node. Not working with DMethods.
		/// Automatically resolves node matches so base types etc. will be specified directly after the search operation.
		/// </summary>
		public static List<AbstractType> SearchChildrenAndResolve(ResolutionContext ctxt, DSymbol t, int nameHash, ISyntaxRegion idObject = null)
		{
			var scan = new SingleNodeNameScan(ctxt, nameHash, idObject);

			if (t is TemplateIntermediateType)
				scan.DeepScanClass(t as UserDefinedType, MemberFilter.All);
			else if (t.Definition is IBlockNode)
				scan.ScanBlock(t.Definition as IBlockNode, CodeLocation.Empty, MemberFilter.All);

			return scan.matches_types;
		}
	}
}
