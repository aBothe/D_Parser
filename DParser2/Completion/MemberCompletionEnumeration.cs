using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using D_Parser.Completion;
using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Misc;
using D_Parser.Resolver.ASTScanner;
using D_Parser.Resolver;

namespace D_Parser.Completion
{
	public class MemberCompletionEnumeration : AbstractVisitor
	{
		bool isVarInst;
		readonly ICompletionDataGenerator gen;
		protected MemberCompletionEnumeration(ResolutionContext ctxt, ICompletionDataGenerator gen) : base(ctxt) 
		{
			this.gen = gen;
		}
		
		public static void EnumAllAvailableMembers(ICompletionDataGenerator cdgen, IBlockNode ScopedBlock
			, IStatement ScopedStatement,
			CodeLocation Caret,
		    ParseCacheView CodeCache,
			MemberFilter VisibleMembers,
			ConditionalCompilationFlags compilationEnvironment = null)
		{
			var ctxt = ResolutionContext.Create(CodeCache, compilationEnvironment, ScopedBlock, ScopedStatement);
			
			var en = new MemberCompletionEnumeration(ctxt, cdgen) {isVarInst = true};

			en.IterateThroughScopeLayers(Caret, VisibleMembers);
		}
		
		public static void EnumChildren(ICompletionDataGenerator cdgen,ResolutionContext ctxt, UserDefinedType udt, bool isVarInstance, 
			MemberFilter vis = MemberFilter.Methods | MemberFilter.Types | MemberFilter.Variables | MemberFilter.Enums)
		{
			var scan = new MemberCompletionEnumeration(ctxt, cdgen) { isVarInst = isVarInstance };

			scan.DeepScanClass(udt, vis);
		}
		
		public static void EnumChildren(ICompletionDataGenerator cdgen,ResolutionContext ctxt, IBlockNode block, bool isVarInstance,
			MemberFilter vis = MemberFilter.Methods | MemberFilter.Types | MemberFilter.Variables | MemberFilter.Enums)
		{
			var scan = new MemberCompletionEnumeration(ctxt, cdgen) { isVarInst = isVarInstance };

			scan.ScanBlock(block, CodeLocation.Empty, vis);
		}
		
		protected override bool HandleItem(INode n)
		{
			var dv = n as DVariable;
			if(isVarInst || !(n is DMethod || dv != null || n is TemplateParameter.Node) || 
			   (n as DNode).IsStatic ||
			   (dv != null && dv.IsConst))
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
