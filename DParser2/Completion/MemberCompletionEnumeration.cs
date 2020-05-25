using System.Collections.Generic;
using System.Threading;
using D_Parser.Dom;
using D_Parser.Misc;
using D_Parser.Resolver;
using D_Parser.Resolver.ASTScanner;

namespace D_Parser.Completion
{
	sealed class MemberCompletionEnumeration : AbstractVisitor
	{
		bool isVarInst;
		readonly ICompletionDataGenerator gen;
		HashSet<int> addedPackageSymbolNames = new HashSet<int>();

		MemberCompletionEnumeration(ResolutionContext ctxt, ICompletionDataGenerator gen) : base(ctxt) 
		{
			this.gen = gen;
		}
		
		public static void EnumAllAvailableMembers(ICompletionDataGenerator cdgen, IBlockNode ScopedBlock
			, CodeLocation Caret,
		    ParseCacheView CodeCache,
			MemberFilter VisibleMembers,
			CancellationToken cancelToken,
			ConditionalCompilationFlags compilationEnvironment = null)
		{
			var ctxt = ResolutionContext.Create(CodeCache, compilationEnvironment);

			CodeCompletion.DoTimeoutableCompletionTask(cdgen, ctxt, () =>
			{
				ctxt.Push(ScopedBlock, Caret);
				var en = new MemberCompletionEnumeration(ctxt, cdgen) { isVarInst = true };
				en.IterateThroughScopeLayers(Caret, VisibleMembers);
			}, cancelToken);
		}
		
		public static void EnumChildren(ICompletionDataGenerator cdgen,ResolutionContext ctxt, UserDefinedType udt, 
			MemberFilter vis = MemberFilter.Methods | MemberFilter.Types | MemberFilter.Variables | MemberFilter.Enums)
		{
			vis ^= MemberFilter.TypeParameters;

			var scan = new MemberCompletionEnumeration(ctxt, cdgen) { isVarInst = udt.NonStaticAccess };

			scan.DeepScanClass(udt, new ItemCheckParameters(vis));
		}
		
		public static void EnumChildren(ICompletionDataGenerator cdgen,ResolutionContext ctxt, IBlockNode block, bool isVarInstance,
			MemberFilter vis = MemberFilter.Methods | MemberFilter.Types | MemberFilter.Variables | MemberFilter.Enums, bool publicImports = false)
		{
			var scan = new MemberCompletionEnumeration(ctxt, cdgen) { isVarInst = isVarInstance };

			scan.ScanBlock(block, CodeLocation.Empty, new ItemCheckParameters(vis){ publicImportsOnly = publicImports });
		}

		protected override bool PreCheckItem (INode n)
		{
			switch (n.NameHash)
			{
			case -1:
			case 0:
			case 1:
				return false;
			default:
				if (n.NameHash == D_Parser.Parser.DTokens.IncompleteIdHash)
					return false;
				break;
			}

			var dv = n as DVariable;
			return isVarInst || !(n is DMethod || dv != null || n is TemplateParameter.Node) ||	(n as DNode).IsStatic || n is DEnumValue ||	(dv != null && (dv.IsConst || dv.IsAlias));
		}
		
		protected override void HandleItem(INode n, AbstractType currentlyResolvedScope)
		{
			if(n is DModule module)
				gen.AddModule(module);
			else
				gen.Add(n);
		}
		
		protected override void HandleItem(PackageSymbol pack)
		{
			if (addedPackageSymbolNames.Add (pack.Package.NameHash))
				gen.AddPackage (pack.Package.Name);
		}
	}
}
