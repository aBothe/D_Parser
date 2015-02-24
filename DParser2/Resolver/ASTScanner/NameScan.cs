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
		
		protected NameScan(ResolutionContext ctxt, int filterHash, ISyntaxRegion idObject) : base(ctxt)
		{
			this.filterHash = filterHash;
			this.idObject = idObject;
		}

		[ThreadStatic]
		static int stackSize = 0;

		public static List<AbstractType> SearchAndResolve(ResolutionContext ctxt, CodeLocation caret, int nameHash, ISyntaxRegion idObject=null)
		{
			var scan = new NameScan(ctxt, nameHash, idObject);

			if(stackSize++ < 7)
				scan.IterateThroughScopeLayers(caret);
			stackSize--;

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
		
		public override IEnumerable<INode> PrefilterSubnodes (IBlockNode bn)
		{
			return bn.Children.GetNodes (filterHash);
		}

		public override IEnumerable<DModule> PrefilterSubnodes (ModulePackage pack, out ModulePackage[] subPackages)
		{
			var subPack = pack.GetPackage (filterHash);
			if (subPack != null)
				subPackages = new[]{ subPack };
			else
				subPackages = null;
			
			var ast = pack.GetModule (filterHash);
			if (ast != null)
				return new[]{ ast };
			return null;
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

			return scan.matches_types;
		}
	}
}
