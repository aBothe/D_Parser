namespace D_Parser.Resolver.ASTScanner
{
	public struct ItemCheckParameters
	{
		public ItemCheckParameters(ItemCheckParameters essentialThingsToCopy)
		{
			VisibleMembers = essentialThingsToCopy.VisibleMembers;
			resolvedCurScope = essentialThingsToCopy.resolvedCurScope;
			dontHandleTemplateParamsInNodeScan = essentialThingsToCopy.dontHandleTemplateParamsInNodeScan;

			publicImportsOnly = false;
			isBaseClass = false;
			isMixinAst = false;
			takeStaticChildrenOnly = false;
			scopeIsInInheritanceHierarchy = false;
		}

		public ItemCheckParameters(MemberFilter vis) { 
			VisibleMembers = vis;
			publicImportsOnly = false;
			isBaseClass = false;
			isMixinAst = false;
			takeStaticChildrenOnly = false;
			scopeIsInInheritanceHierarchy = false;
			resolvedCurScope = null;
			dontHandleTemplateParamsInNodeScan = false;
		}

		public MemberFilter VisibleMembers;
		public bool publicImportsOnly;
		public bool isBaseClass;
		public bool isMixinAst;
		public bool takeStaticChildrenOnly;
		public bool scopeIsInInheritanceHierarchy; 
		public DSymbol resolvedCurScope;

		/// <summary>
		/// Temporary flag that is used for telling scanChildren() not to handle template parameters.
		/// Used to prevent the insertion of a template mixin's parameter set into the completion list etc.
		/// </summary>
		public bool dontHandleTemplateParamsInNodeScan;
	}
}
