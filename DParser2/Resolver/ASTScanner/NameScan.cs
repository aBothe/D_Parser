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
		
		public override IEnumerable<INode> PrefilterSubnodes(IBlockNode bn)
		{
			return bn.Children[filterId];
		}

		protected override bool HandleItem(INode n)
		{
            if (n != null && n.Name == filterId)
            {
            	matches_types.Add(TypeDeclarationResolver.HandleNodeMatch(n, ctxt, null, idObject));
            	return true;
            }

            /*
             * Can't tell if workaround .. or just nice idea:
             * 
             * To still be able to show sub-packages e.g. when std. has been typed,
             * take the first import that begins with std.
             * In HandleNodeMatch, it'll be converted to a module package result then.
             */
            else if (n is IAbstractSyntaxTree)
            {
                var modName = (n as IAbstractSyntaxTree).ModuleName;
                if (modName.Split('.')[0] == filterId)
                {
                    foreach (var m in matches_types)
                        if (m is ModuleSymbol)
                            return false;
                    
	            	matches_types.Add(TypeDeclarationResolver.HandleNodeMatch(n, ctxt, null, idObject));
	            	return true;
                }
            }

            return false;
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
		
		protected override bool HandleItem(INode n)
		{
			if (n != null && n.Name == filterId){
            	matches_types.Add(TypeDeclarationResolver.HandleNodeMatch(n, ctxt, resultBase, idObject));
            	return true;
			}
			return false;
		}
	}
}
