using System;
using System.Collections.Generic;
using D_Parser.Completion;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.Templates;

namespace D_Parser.Resolver.TypeResolution
{
	/// <summary>
	/// Generic class for resolve module relations and/or declarations
	/// </summary>
	public partial class DResolver
	{
		/// <summary>Used for code completion/symbol resolution.</summary>
		/// <param name="editor">Can be null</param>
		public static ISyntaxRegion GetScopedCodeObject(IEditorData editor)
		{
			var block = ASTSearchHelper.SearchBlockAt(editor.SyntaxTree, editor.CaretLocation);

			IStatement stmt = null;
			if (block is DMethod)
				stmt = (block as DMethod).GetSubBlockAt(editor.CaretLocation);
			
			var vis = new ScopedObjectVisitor(editor.CaretLocation);
			if (stmt != null)
				stmt.Accept(vis);
			else
				block.Accept(vis);

			return vis.IdNearCaret;
		}

		public static AbstractType ResolveType(IEditorData editor, ResolutionContext ctxt = null)
		{
			var o = GetScopedCodeObject(editor);
			if (ctxt == null)
				ctxt = ResolutionContext.Create(editor, false);

			AbstractType ret = null;

			CodeCompletion.DoTimeoutableCompletionTask(null, ctxt, () =>
			{
				ctxt.Push(editor);
				
				var optionBackup = ctxt.CurrentContext.ContextDependentOptions;
				ctxt.CurrentContext.ContextDependentOptions |= ResolutionOptions.ReturnMethodReferencesOnly;

				if (o is IExpression)
					ret = ExpressionTypeEvaluation.EvaluateType((IExpression)o, ctxt, false);
				else if (o is ITypeDeclaration)
					ret = TypeDeclarationResolver.ResolveSingle((ITypeDeclaration)o, ctxt);
				else if (o is INode)
					ret = TypeDeclarationResolver.HandleNodeMatch(o as INode, ctxt);

				ctxt.CurrentContext.ContextDependentOptions = optionBackup;
			}, editor.CancelToken);

			return ret;
		}

		static readonly int ObjectNameHash = "Object".GetHashCode();

		static ClassType ResolveObjectClass(ResolutionContext ctxt)
		{
			using (ctxt.Push(ctxt.ScopedBlock == null ? null : ctxt.ScopedBlock.NodeRoot)) //TODO: understand why we're passing null
				return TypeDeclarationResolver.ResolveSingle (new IdentifierDeclaration (ObjectNameHash), ctxt, false) as ClassType;
		}

		[ThreadStatic]
		static int bcStack = 0;
		[ThreadStatic]
		static List<ISyntaxRegion> parsedClassInstanceDecls;
		/// <summary>
		/// Takes the class passed via the tr, and resolves its base class and/or implemented interfaces.
		/// Also usable for enums.
		/// 
		/// Never returns null. Instead, the original 'tr' object will be returned if no base class was resolved.
		/// Will clone 'tr', whereas the new object will contain the base class.
		/// </summary>
		public static TemplateIntermediateType ResolveClassOrInterface(DClassLike dc, ResolutionContext ctxt, ISyntaxRegion instanceDeclaration, bool ResolveFirstBaseIdOnly=false, IEnumerable<TemplateParameterSymbol> extraDeducedTemplateParams = null)
		{
			if (parsedClassInstanceDecls == null)
				parsedClassInstanceDecls = new List<ISyntaxRegion> ();

			switch (dc.ClassType)
			{
				case DTokens.Class:
				case DTokens.Interface:
					break;
				default:
					if (dc.BaseClasses.Count != 0)
						ctxt.LogError(dc, "Only classes and interfaces may inherit from other classes/interfaces");
					return null;
			}

			bool isClass = dc.ClassType == DTokens.Class;

			if (bcStack > 6 || (instanceDeclaration != null && parsedClassInstanceDecls.Contains(instanceDeclaration)))
			{
				return isClass ? new ClassType(dc, null) as TemplateIntermediateType : new InterfaceType(dc);
			}

			if (instanceDeclaration != null)
				parsedClassInstanceDecls.Add(instanceDeclaration);
			bcStack++;

			var deducedTypes = new DeducedTypeDictionary(dc);
			var tix = instanceDeclaration as TemplateInstanceExpression;
			if (tix != null && (ctxt.Options & ResolutionOptions.NoTemplateParameterDeduction) == 0)
			{
				// Pop a context frame as we still need to resolve the template instance expression args in the place where the expression occurs, not the instantiated class' location
				var backup = ctxt.CurrentContext;
				ctxt.Pop ();

				if (ctxt.CurrentContext == null)
					ctxt.Push (backup);

				var givenTemplateArguments = TemplateInstanceHandler.PreResolveTemplateArgs(new[] { dc }, tix, ctxt);

				if (ctxt.CurrentContext != backup) {
					foreach (var kv in ctxt.CurrentContext.DeducedTemplateParameters) {
						backup.DeducedTemplateParameters [kv.Key] = kv.Value;
						deducedTypes [kv.Key] = kv.Value;
					}
					ctxt.Push (backup);
				}

				if (!TemplateInstanceHandler.DeduceParams(givenTemplateArguments, false, ctxt, null, dc, deducedTypes))
				{
					parsedClassInstanceDecls.Remove(instanceDeclaration);
					bcStack--;
					return null;
				}
			}

			if (extraDeducedTemplateParams != null)
				foreach (var tps in extraDeducedTemplateParams)
					deducedTypes[tps.Parameter] = tps;


			if(dc.BaseClasses == null || dc.BaseClasses.Count < 1)
			{
				parsedClassInstanceDecls.Remove (instanceDeclaration);
				bcStack--;

				// The Object class has no further base class;
				// Normal class instances have the object as base class;
				// Interfaces must not have any default base class/interface
				return isClass ? new ClassType(dc, dc.NameHash != ObjectNameHash ? ResolveObjectClass(ctxt) : null, null, deducedTypes) :
					new InterfaceType(dc, null, deducedTypes) as TemplateIntermediateType;
			}


			#region Base class & interface resolution
			TemplateIntermediateType baseClass = null;
			var interfaces = new List<InterfaceType>();

			var back = ctxt.ScopedBlock;
			using (ctxt.Push(dc.Parent))
			{
				var pop = back != ctxt.ScopedBlock;

				ctxt.CurrentContext.DeducedTemplateParameters.Add(deducedTypes);

				try
				{
					for (int i = 0; i < (ResolveFirstBaseIdOnly ? 1 : dc.BaseClasses.Count); i++)
					{
						var type = dc.BaseClasses[i];

						// If there's an explicit 'Object' inheritance, also return the pre-resolved object class
						if (type is IdentifierDeclaration &&
							(type as IdentifierDeclaration).IdHash == ObjectNameHash)
						{
							if (baseClass != null)
							{
								ctxt.LogError(new ResolutionError(dc, "Class must not have two base classes"));
								continue;
							}
							else if (i != 0)
							{
								ctxt.LogError(new ResolutionError(dc, "The base class name must preceed base interfaces"));
								continue;
							}

							baseClass = ResolveObjectClass(ctxt);
							continue;
						}

						if (type == null || (type is IdentifierDeclaration && (type as IdentifierDeclaration).IdHash == dc.NameHash) || dc.NodeRoot == dc)
						{
							ctxt.LogError(new ResolutionError(dc, "A class cannot inherit from itself"));
							continue;
						}

						var r = DResolver.StripMemberSymbols(TypeDeclarationResolver.ResolveSingle(type, ctxt));

						if (r is ClassType || r is TemplateType)
						{
							if (!isClass)
								ctxt.LogError(new ResolutionError(type, "An interface cannot inherit from non-interfaces"));
							else if (i == 0)
							{
								baseClass = r as TemplateIntermediateType;
							}
							else
								ctxt.LogError(new ResolutionError(dc, "The base " + (r is ClassType ? "class" : "template") + " name must preceed base interfaces"));
						}
						else if (r is InterfaceType)
						{
							interfaces.Add(r as InterfaceType);

							if (isClass && dc.NameHash != ObjectNameHash && baseClass == null)
								baseClass = ResolveObjectClass(ctxt);
						}
						else
						{
							ctxt.LogError(new ResolutionError(type, "Resolved class is neither a class nor an interface"));
							continue;
						}
					}
				}
				finally
				{
					bcStack--;
					parsedClassInstanceDecls.Remove(instanceDeclaration);
				}

				if (!pop)
					ctxt.CurrentContext.DeducedTemplateParameters.Remove(deducedTypes); // May be backup old tps?
			}
			#endregion

			if (isClass)
				return new ClassType(dc, baseClass, interfaces.Count == 0 ? null : interfaces.ToArray(), deducedTypes);

			return new InterfaceType(dc, interfaces.Count == 0 ? null : interfaces.ToArray(), deducedTypes);
		}

		/// <summary>
		/// Removes all kinds of members from the given results.
		/// </summary>
		public static AbstractType StripMemberSymbols(AbstractType r)
		{
			var ds = r as DerivedDataType;
			if (ds != null && ds.Base != null) {
				if (ds is ArrayAccessSymbol || ds is MemberSymbol || ds is DelegateCallSymbol) {
					r = ds.Base;
					ds = r as DSymbol;
				}

				if (r is TemplateParameterSymbol) {
					if (ds.Base == null)
						return r;
					r = ds.Base;
					ds = r as DSymbol;
				}

				// There's one special case to handle (TODO: are there further cases?):
				// auto o = new Class(); -- o will be MemberSymbol and its base type will be a MemberSymbol either (i.e. the constructor reference)
				if(ds is MemberSymbol && (ds as DSymbol).Definition is DMethod && (ds as DSymbol).NameHash == DMethod.ConstructorIdentifierHash)
					r = ds.Base;
			}

			return r;
		}
	}
}
