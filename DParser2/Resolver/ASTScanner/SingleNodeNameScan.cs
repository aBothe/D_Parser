using D_Parser.Dom;
using System.Collections.Generic;

namespace D_Parser.Resolver.ASTScanner
{
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
			var parms = new ItemCheckParameters(MemberFilter.All);

			if (t is TemplateIntermediateType)
				scan.DeepScanClass(t as UserDefinedType, parms);
			else if (t.Definition is IBlockNode)
				scan.ScanBlock(t.Definition as IBlockNode, CodeLocation.Empty, parms);

			return scan.GetMatches();
		}
	}
}
