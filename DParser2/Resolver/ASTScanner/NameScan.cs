using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Parser;

namespace D_Parser.Resolver.ASTScanner
{
	public class NameScan : AbstractVisitor
	{
		protected readonly string filterId;
		protected readonly object idObject;
		protected readonly List<AbstractType> matches_types = new List<AbstractType>();

		protected NameScan(ResolutionContext ctxt, string filterId, object idObject) : base(ctxt)
		{
			this.filterId = filterId;
			this.idObject = idObject;
		}

		public static List<AbstractType> SearchAndResolve(ResolutionContext ctxt, CodeLocation caret, string name, object idObject=null)
		{
			var scan = new NameScan(ctxt, name, idObject);

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
					if(nodes != null && nodes.Count != 0)
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
			return bn.Children[filterId];
		}
		
		public override IEnumerable<IAbstractSyntaxTree> PrefilterSubnodes(ModulePackage pack, out ModulePackage[] subPackages)
		{
			ModulePackage subPack;
			if(pack.Packages.TryGetValue(filterId, out subPack))
				subPackages = new[]{ subPack };
			else
				subPackages = null;
			
			IAbstractSyntaxTree ast;
			if(pack.Modules.TryGetValue(filterId, out ast))
				return new[]{ast};
			return null;
		}

		protected override bool HandleItem(INode n)
		{
            if (n != null && n.Name == filterId)
            {
            	matches_types.Add(TypeDeclarationResolver.HandleNodeMatch(n, ctxt, null, idObject));
            	return true;
            }

            return false;
		}
		
		protected override bool HandleItem(PackageSymbol pack)
		{
			matches_types.Add(pack);
			return true;
		}
	}
	
	public class SingleNodeNameScan : NameScan
	{
		AbstractType resultBase;
		protected SingleNodeNameScan(ResolutionContext ctxt, string filter, object idObject) : base(ctxt, filter, idObject) {}
		
		/// <summary>
		/// Scans a block node. Not working with DMethods.
		/// Automatically resolves node matches so base types etc. will be specified directly after the search operation.
		/// </summary>
		public static List<AbstractType> SearchChildrenAndResolve(ResolutionContext ctxt, AbstractType resultBase, IBlockNode block, string name, object idObject = null)
		{
			var scan = new SingleNodeNameScan(ctxt, name, idObject) { resultBase = resultBase };

			bool _unused=false;
			scan.ScanBlock(block, CodeLocation.Empty, MemberFilter.All, ref _unused);

			return scan.matches_types;
		}
	}
}
