using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using D_Parser.Completion;
using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Misc;

namespace D_Parser.Resolver.ASTScanner
{
	/// <summary>
	/// A whitelisting filter for members to show in completion menus.
	/// </summary>
	[Flags]
	public enum MemberFilter
	{
		None=0,
		Variables=1,
		Methods=1<<2,
		Classes=1<<3,
		Interfaces=1<<4,
		Templates=1<<5,
		StructsAndUnions=1<<6,
		Enums=1<<7,
		Keywords=1<<8,
		TypeParameters=1<<9,

		Types = Classes | Interfaces | Templates | StructsAndUnions,
		All = Variables | Methods | Types | Enums | Keywords | TypeParameters
	}

	public class ItemEnumeration : AbstractVisitor
	{
		protected ItemEnumeration(ResolutionContext ctxt): base(ctxt) { }

		public static IEnumerable<INode> EnumAllAvailableMembers(
			ResolutionContext ctxt,
			CodeLocation Caret,
			MemberFilter VisibleMembers)
		{
			var en = new ItemEnumeration(ctxt);

			en.IterateThroughScopeLayers(Caret, VisibleMembers);

			return en.Nodes.Count <1 ? null : en.Nodes;
		}

		List<INode> Nodes = new List<INode>();
		protected override bool HandleItem(INode n)
		{
			Nodes.Add(n);
			return false;
		}

		protected override bool HandleItems(IEnumerable<INode> nodes)
		{
			Nodes.AddRange(nodes);
			return false;
		}
		
		protected override bool HandleItem(PackageSymbol pack)
		{
			return false;
		}
	}
	
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
			if(isVarInst || !(n is DMethod || dv != null || n is TemplateParameterNode) || 
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
