using D_Parser.Completion;
using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Misc;
using D_Parser.Resolver.ASTScanner;
using D_Parser.Resolver;
using System.Threading;
using System.Threading.Tasks;

namespace D_Parser.Completion
{
	class MemberCompletionEnumeration : AbstractVisitor
	{
		bool isVarInst;
		readonly ICompletionDataGenerator gen;
		protected MemberCompletionEnumeration(ResolutionContext ctxt, ICompletionDataGenerator gen) : base(ctxt) 
		{
			this.gen = gen;
		}
		
		public static void EnumAllAvailableMembers(ICompletionDataGenerator cdgen, IBlockNode ScopedBlock
			, CodeLocation Caret,
		    ParseCacheView CodeCache,
			MemberFilter VisibleMembers,
			ConditionalCompilationFlags compilationEnvironment = null)
		{
			var ctxt = ResolutionContext.Create(CodeCache, compilationEnvironment);

			CodeCompletion.DoTimeoutableCompletionTask(cdgen, ctxt, () =>
			{
				ctxt.Push(ScopedBlock, Caret);
				var en = new MemberCompletionEnumeration(ctxt, cdgen) { isVarInst = true };
				en.IterateThroughScopeLayers(Caret, VisibleMembers);
			});
		}
		
		public static void EnumChildren(ICompletionDataGenerator cdgen,ResolutionContext ctxt, UserDefinedType udt, 
			MemberFilter vis = MemberFilter.Methods | MemberFilter.Types | MemberFilter.Variables | MemberFilter.Enums)
		{
			vis ^= MemberFilter.TypeParameters;

			var scan = new MemberCompletionEnumeration(ctxt, cdgen) { isVarInst = udt.NonStaticAccess };

			scan.DeepScanClass(udt, vis);
		}
		
		public static void EnumChildren(ICompletionDataGenerator cdgen,ResolutionContext ctxt, IBlockNode block, bool isVarInstance,
			MemberFilter vis = MemberFilter.Methods | MemberFilter.Types | MemberFilter.Variables | MemberFilter.Enums, bool publicImports = false)
		{
			var scan = new MemberCompletionEnumeration(ctxt, cdgen) { isVarInst = isVarInstance };

			scan.ScanBlock(block, CodeLocation.Empty, vis, publicImports);
		}
		
		protected override bool HandleItem(INode n)
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
			if(isVarInst || !(n is DMethod || dv != null || n is TemplateParameter.Node) || 
			   (n as DNode).IsStatic || n is DEnumValue ||
			   (dv != null && (dv.IsConst || dv.IsAlias)))
			{
				if(n is DModule)
					gen.AddModule(n as DModule);
				else
					gen.Add(n);
			}
			return false;
		}
		
		protected override bool HandleItem(PackageSymbol pack)
		{
			gen.AddPackage(pack.Package.Name);
			return false;
		}
	}
}
