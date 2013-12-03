using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using D_Parser.Completion;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using System.Linq;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Misc;

namespace D_Parser.Resolver.TypeResolution
{
	/// <summary>
	/// Generic class for resolve module relations and/or declarations
	/// </summary>
	public partial class DResolver
	{
		[Flags]
		public enum AstReparseOptions
		{
			AlsoParseBeyondCaret=1,
			OnlyAssumeIdentifierList=2,

			/// <summary>
			/// Returns the expression without scanning it down depending on the caret location
			/// </summary>
			ReturnRawParsedExpression=4,

			DontCheckForCommentsOrStringSurrounding = 16,
		}

		/// <summary>
		/// Reparses the code of the current scope and returns the object (either IExpression or ITypeDeclaration derivative)
		/// that is beneath the caret location.
		/// 
		/// Used for code completion/symbol resolution.
		/// Mind the extra options that might be passed via the Options parameter.
		/// </summary>
		/// <param name="ctxt">Can be null</param>
		public static ISyntaxRegion GetScopedCodeObject(IEditorData editor,
			AstReparseOptions Options = AstReparseOptions.AlsoParseBeyondCaret,
			ResolutionContext ctxt=null)
		{
			if (ctxt == null)
				ctxt = ResolutionContext.Create(editor);

			var code = editor.ModuleCode;

			int start = 0;
			var startLocation = CodeLocation.Empty;
			bool IsExpression = false;

			if (ctxt.CurrentContext.ScopedStatement is IExpressionContainingStatement)
			{
				var exprs = ((IExpressionContainingStatement)ctxt.CurrentContext.ScopedStatement).SubExpressions;
				IExpression targetExpr = null;

				if (exprs != null)
					foreach (var ex in exprs)
						if ((targetExpr = ExpressionHelper.SearchExpressionDeeply(ex, editor.CaretLocation))
							!= ex)
							break;

				if (targetExpr != null && editor.CaretLocation >= targetExpr.Location && editor.CaretLocation <= targetExpr.EndLocation)
				{
					startLocation = targetExpr.Location;
					start = DocumentHelper.GetOffsetByRelativeLocation(editor.ModuleCode, editor.CaretLocation, editor.CaretOffset, startLocation);
					IsExpression = true;
				}
			}

			if (!IsExpression)
			{
				// First check if caret is inside a comment/string etc.
				int lastStart = 0;
				int lastEnd = 0;
				if ((Options & AstReparseOptions.DontCheckForCommentsOrStringSurrounding) == 0)
				{
					var caretContext = CaretContextAnalyzer.GetTokenContext(code, editor.CaretOffset, out lastStart, out lastEnd);

					// Return if comment etc. found
					if (caretContext != TokenContext.None)
						return null;
				}

				// Could be somewhere in an ITypeDeclaration..

				if (editor.CaretOffset < 0 || editor.CaretOffset >= code.Length)
					return null;
				
				if(Lexer.IsIdentifierPart(code[editor.CaretOffset]))
					start = editor.CaretOffset;
				else if (editor.CaretOffset > 0 && Lexer.IsIdentifierPart(code[editor.CaretOffset - 1]))
					start = editor.CaretOffset - 1;

				
				start = CaretContextAnalyzer.SearchExpressionStart(code, start,
					(lastEnd > 0 && lastEnd < editor.CaretOffset) ? lastEnd : 0);
				startLocation = DocumentHelper.OffsetToLocation(editor.ModuleCode, start);
			}

			if (start < 0 || editor.CaretOffset < start)
				return null;

			var sv = new StringView(code, start, Options.HasFlag(AstReparseOptions.AlsoParseBeyondCaret) ? code.Length - start : editor.CaretOffset - start);
			var parser = DParser.Create(sv);
			parser.Lexer.SetInitialLocation(startLocation);
			parser.Step();

			ITypeDeclaration td;

			if (!IsExpression && Options.HasFlag(AstReparseOptions.OnlyAssumeIdentifierList) && parser.Lexer.LookAhead.Kind == DTokens.Identifier)
			{
				td = parser.IdentifierList();
			}
			else if (IsExpression || parser.IsAssignExpression())
			{
				if (Options.HasFlag(AstReparseOptions.ReturnRawParsedExpression))
					return parser.AssignExpression();
				else
					return ExpressionHelper.SearchExpressionDeeply(parser.AssignExpression(), editor.CaretLocation);
			}
			else
				td = parser.Type();

			if (Options.HasFlag(AstReparseOptions.ReturnRawParsedExpression))
				return td;

			while (td != null && td.InnerDeclaration != null && editor.CaretLocation <= td.InnerDeclaration.EndLocation)
				td = td.InnerDeclaration;

			return td;
		}

		public static AbstractType[] ResolveType(IEditorData editor, AstReparseOptions Options = AstReparseOptions.AlsoParseBeyondCaret, ResolutionContext ctxt = null)
		{
			if (ctxt == null)
				ctxt = ResolutionContext.Create(editor);

			var o = GetScopedCodeObject(editor, Options, ctxt);

			var optionBackup = ctxt.CurrentContext.ContextDependentOptions;
			ctxt.CurrentContext.ContextDependentOptions |= ResolutionOptions.ReturnMethodReferencesOnly;

			AbstractType[] ret;

			if (o is IExpression)
				ret = Evaluation.EvaluateTypes((IExpression)o, ctxt);
			else if(o is ITypeDeclaration)
				ret = TypeDeclarationResolver.Resolve((ITypeDeclaration)o, ctxt);
			else
				ret = null;

			ctxt.CurrentContext.ContextDependentOptions = optionBackup;

			return ret;
		}

		public enum NodeResolutionAttempt
		{
			Normal,
			NoParameterOrTemplateDeduction,
			RawSymbolLookup
		}

		public static AbstractType[] ResolveTypeLoosely(IEditorData editor, out NodeResolutionAttempt resolutionAttempt, ResolutionContext ctxt = null)
		{
			if (ctxt == null)
				ctxt = ResolutionContext.Create(editor);

			var o = GetScopedCodeObject(editor, ctxt:ctxt);

			var optionBackup = ctxt.CurrentContext.ContextDependentOptions;
			ctxt.CurrentContext.ContextDependentOptions |= ResolutionOptions.ReturnMethodReferencesOnly;
			resolutionAttempt = NodeResolutionAttempt.Normal;

			AbstractType[] ret;

			if (o is IExpression)
				ret = Evaluation.EvaluateTypes((IExpression)o, ctxt);
			else if(o is ITypeDeclaration)
				ret = TypeDeclarationResolver.Resolve((ITypeDeclaration)o, ctxt);
			else
				ret = null;

			if (ret == null) {
				resolutionAttempt = NodeResolutionAttempt.NoParameterOrTemplateDeduction;

				if (o is PostfixExpression_MethodCall)
					o = (o as PostfixExpression_MethodCall).PostfixForeExpression;

				if (o is IdentifierExpression)
					ret = Evaluation.GetOverloads (o as IdentifierExpression, ctxt, false);
				else if (o is ITypeDeclaration) {
					ctxt.CurrentContext.ContextDependentOptions |= ResolutionOptions.NoTemplateParameterDeduction;
					ret = TypeDeclarationResolver.Resolve (o as ITypeDeclaration, ctxt);
				}
			}

			if (ret == null) {
				resolutionAttempt = NodeResolutionAttempt.RawSymbolLookup;
				ret = TypeDeclarationResolver.HandleNodeMatches (LookupIdRawly (editor, o as ISyntaxRegion), ctxt);
			}

			ctxt.CurrentContext.ContextDependentOptions = optionBackup;
			return ret;
		}

		public static List<DNode> LookupIdRawly(IEditorData ed, ISyntaxRegion o)
		{
			// Extract a concrete id from that syntax object. (If access expression/nested decl, use the inner-most one)
			int idHash=0;

			chkAgain:
			if (o is ITypeDeclaration)
			{
				var td = ((ITypeDeclaration)o).InnerMost;

				if (td is IdentifierDeclaration)
					idHash = ((IdentifierDeclaration)td).IdHash;
				else if (td is TemplateInstanceExpression)
					idHash = ((TemplateInstanceExpression)td).TemplateIdHash;
			}
			else if (o is IExpression)
			{
				var x = (IExpression)o;

				while (x is PostfixExpression)
					x = ((PostfixExpression)x).PostfixForeExpression;

				if (x is IdentifierExpression && ((IdentifierExpression)x).IsIdentifier)
					idHash = ((IdentifierExpression)x).ValueStringHash;
				else if (x is TemplateInstanceExpression)
					idHash = ((TemplateInstanceExpression)x).TemplateIdHash;
				else if (x is NewExpression)
				{
					o = ((NewExpression)x).Type;
					goto chkAgain;
				}
			}

			if (idHash == 0)
				return null;

			var l = new List<DNode> ();

			// Rawly scan through all modules' roots of the parse cache to find that id.
			foreach(var pc in ed.ParseCache)
				foreach (var mod in pc)
				{
					if (mod.NameHash == idHash)
						l.Add(mod);

					var ch = mod[idHash];
					if(ch!=null)
						foreach (var c in ch)
						{
							var dn = c as DNode;

							// TODO: At least check for proper protection attributes properly!
							if (dn != null && !dn.ContainsAttribute(DTokens.Package, DTokens.Private, DTokens.Protected)) // Can this
								l.Add(dn);
						}

					//TODO: Mixins
				}

			return l;
		}


		static int bcStack = 0;
		/// <summary>
		/// Takes the class passed via the tr, and resolves its base class and/or implemented interfaces.
		/// Also usable for enums.
		/// 
		/// Never returns null. Instead, the original 'tr' object will be returned if no base class was resolved.
		/// Will clone 'tr', whereas the new object will contain the base class.
		/// </summary>
		public static UserDefinedType ResolveBaseClasses(UserDefinedType tr, ResolutionContext ctxt, bool ResolveFirstBaseIdOnly=false)
		{
			if (bcStack > 8)
			{
				bcStack--;
				return tr;
			}

			if (tr is EnumType)
			{
				var et = tr as EnumType;

				AbstractType bt = null;

				if(et.Definition.Type == null)
					bt = new PrimitiveType(DTokens.Int);
				else
				{
					if(tr.Definition.Parent is IBlockNode)
						ctxt.PushNewScope((IBlockNode)tr.Definition.Parent);

					var bts=TypeDeclarationResolver.Resolve(et.Definition.Type, ctxt);

					if (tr.Definition.Parent is IBlockNode)
						ctxt.Pop();

					ctxt.CheckForSingleResult(bts, et.Definition.Type);

					if(bts!=null && bts.Length!=0)
						bt=bts[0];
				}

				return new EnumType(et.Definition, bt, et.DeclarationOrExpressionBase);
			}

			var dc = tr.Definition as DClassLike;
			// Return immediately if searching base classes of the Object class
			if (dc == null || ((dc.BaseClasses == null || dc.BaseClasses.Count < 1) && dc.Name == "Object"))
				return tr;

			// If no base class(es) specified, and if it's no interface that is handled, return the global Object reference
			// -- and do not throw any error message, it's ok
			if(dc.BaseClasses == null || dc.BaseClasses.Count < 1)
			{
				if(tr is ClassType) // Only Classes can inherit from non-interfaces
					return new ClassType(dc, tr.DeclarationOrExpressionBase, ctxt.ParseCache.ObjectClassResult);
				return tr;
			}

			#region Base class & interface resolution
			TemplateIntermediateType baseClass=null;
			var interfaces = new List<InterfaceType>();

			if (!(tr is ClassType || tr is InterfaceType))
			{
				if (dc.BaseClasses.Count != 0)
					ctxt.LogError(dc,"Only classes and interfaces may inherit from other classes/interfaces");
				return tr;
			}

			for (int i = 0; i < (ResolveFirstBaseIdOnly ? 1 : dc.BaseClasses.Count); i++)
			{
				var type = dc.BaseClasses[i];

				// If there's an explicit 'Object' inheritance, also return the pre-resolved object class
				if (type is IdentifierDeclaration && ((IdentifierDeclaration)type).Id == "Object")
				{
					if (baseClass!=null)
					{
						ctxt.LogError(new ResolutionError(dc, "Class must not have two base classes"));
						continue;
					}
					else if (i != 0)
					{
						ctxt.LogError(new ResolutionError(dc, "The base class name must preceed base interfaces"));
						continue;
					}

					baseClass = ctxt.ParseCache.ObjectClassResult;
					continue;
				}

				if (type == null || type.ToString(false) == dc.Name || dc.NodeRoot == dc)
				{
					ctxt.LogError(new ResolutionError(dc, "A class cannot inherit from itself"));
					continue;
				}

				ctxt.PushNewScope(dc.Parent as IBlockNode);

				bcStack++;

				var res= DResolver.StripAliasSymbols(TypeDeclarationResolver.Resolve(type, ctxt));

				ctxt.CheckForSingleResult(res, type);

				if(res!=null && res.Length != 0)
				{
					var r = res[0];
					if (r is ClassType || r is TemplateType)
					{
						if (tr is InterfaceType)
							ctxt.LogError(new ResolutionError(type, "An interface cannot inherit from non-interfaces"));
						else if (i == 0)
						{
							baseClass = (TemplateIntermediateType)r;
						}
						else
							ctxt.LogError(new ResolutionError(dc, "The base "+(r is ClassType ?  "class" : "template")+" name must preceed base interfaces"));
					}
					else if (r is InterfaceType)
					{
						interfaces.Add((InterfaceType)r);
					}
					else
					{
						ctxt.LogError(new ResolutionError(type, "Resolved class is neither a class nor an interface"));
						continue;
					}
				}

				bcStack--;

				ctxt.Pop();
			}
			#endregion

			if (baseClass == null && interfaces.Count == 0)
				return tr;

			if (tr is ClassType)
				return new ClassType(dc, tr.DeclarationOrExpressionBase, baseClass, interfaces.Count == 0 ? null : interfaces.ToArray(), tr.DeducedTypes);
			else if (tr is InterfaceType)
				return new InterfaceType(dc, tr.DeclarationOrExpressionBase, interfaces.Count == 0 ? null : interfaces.ToArray(), tr.DeducedTypes);
			
			// Method should end here
			return tr;
		}

		public static IBlockNode SearchBlockAt(IBlockNode Parent, CodeLocation Where)
		{
			IStatement s;
			return SearchBlockAt(Parent, Where, out s);
		}

		public static IBlockNode SearchBlockAt(IBlockNode Parent, CodeLocation Where, out IStatement ScopedStatement)
		{
			ScopedStatement = null;

			if (Parent == null)
				return null;

			var pCount = Parent.Count;
			while (pCount != 0)
			{
				var children = Parent.Children;
				int start = 0;
				INode midElement = null;
				int midIndex = 0;
				int len = pCount;

				while (len > 0)
				{
					midIndex = (len % 2 + len) / 2;

					// Take an element from the middle
					if ((midElement = children[start + midIndex - 1]) == null)
						break;

					// If 'Where' is beyond its start location
					if (Where > midElement.Location)
					{
						start += midIndex;

						// If we've reached the (temporary) goal, break immediately
						if (Where < midElement.EndLocation)
							break;
						// If it's the last tested element and if the caret is beyond the end location, 
						// return the Parent instead the last tested child
						else if (midIndex == len)
						{
							midElement = null;
							break;
						}
					}
					else if (midIndex == len)
					{
						midElement = null;
						break;
					}

					len -= midIndex;
				}

				if (midElement is IBlockNode) {
					Parent = (IBlockNode)midElement;
					pCount = Parent.Count;
				}
				else
					break;
			}

			var dm = Parent as DMethod;
			if (dm != null)
			{
				// Do an extra re-scan for anonymous methods etc.
				foreach (var ch in dm.AdditionalChildren)
					if (Where >= ch.Location && Where <= ch.EndLocation) {
						if (ch is IBlockNode)
							Parent = ch as IBlockNode;
						dm = Parent as DMethod;

						if (dm == null)
							return Parent;
						break;
					}

				var body = dm.GetSubBlockAt(Where);

				// First search the deepest statement under the caret
				if (body != null){
					ScopedStatement = body.SearchStatementDeeply(Where);

					if (ScopedStatement is IDeclarationContainingStatement)
					{
						var dcs = (ScopedStatement as IDeclarationContainingStatement).Declarations;

						if (dcs != null && dcs.Length != 0)
							foreach (var decl in dcs)
								if (decl is IBlockNode &&
								    Where > decl.Location &&
								    Where < decl.EndLocation)
									return SearchBlockAt (decl as IBlockNode, Where, out ScopedStatement);
					}
				}
			} else if(Parent is DBlockNode) {
				var db = Parent as DBlockNode;
				if (db.StaticStatements.Count != 0)
					foreach (var ss in db.StaticStatements)
						if (Where >= ss.Location && Where <= ss.EndLocation) {
							ScopedStatement = ss;
							if(ss is StatementContainingStatement)
								ScopedStatement = (ss as StatementContainingStatement).SearchStatementDeeply(Where);
							break;
						}
			}

			return Parent;
		}

		public static IBlockNode SearchClassLikeAt(IBlockNode Parent, CodeLocation Where)
		{
			if (Parent != null && Parent.Count > 0)
				foreach (var n in Parent)
				{
					var dc = n as DClassLike;
					if (dc==null)
						continue;

					if (Where > dc.BlockStartLocation && Where < dc.EndLocation)
						return SearchClassLikeAt(dc, Where);
				}

			return Parent;
		}

		public static IEnumerable<T> FilterOutByResultPriority<T>(
			ResolutionContext ctxt,
			IEnumerable<T> results) where T : AbstractType
		{
			if (results == null)
				return null;

			var newRes = new List<T>();

			foreach (var rb in results)
			{
				var n = GetResultMember(rb);
				if (n != null)
				{
					// Put priority on locals
					if (n is DVariable &&
						(n as DVariable).IsLocal)
						return new[] { rb };

					// If member/type etc. is part of the actual module, omit external symbols
					if (n.NodeRoot != ctxt.CurrentContext.ScopedBlock.NodeRoot)
					{
						bool omit = false;
						foreach (var r in newRes)
						{
							var k = GetResultMember(r);
							if (k != null && k.NodeRoot == ctxt.CurrentContext.ScopedBlock.NodeRoot)
							{
								omit = true;
								break;
							}
						}

						if (omit)
							continue;
					}
					else
						foreach (var r in newRes.ToArray())
						{
							var k = GetResultMember(r);
							if (k != null && k.NodeRoot != ctxt.CurrentContext.ScopedBlock.NodeRoot)
								newRes.Remove(r);
						}
				}
				
				newRes.Add(rb);
			}

			return newRes.Count > 0 ? newRes.ToArray():null;
		}

		public static DNode GetResultMember(ISemantic res)
		{
			if(res is DSymbol)
				return ((DSymbol)res).Definition;

			return null;
		}

		/// <summary>
		/// If an aliased type result has been passed to this method, it'll return the resolved type.
		/// If aliases were done multiple times, it also tries to skip through these.
		/// 
		/// alias char[] A;
		/// alias A B;
		/// 
		/// var resolvedType=TryRemoveAliasesFromResult(% the member result from B %);
		/// --> resolvedType will be StaticTypeResult from char[]
		/// 
		/// </summary>
		public static AbstractType StripAliasSymbol(AbstractType r)
		{
			while(r is AliasedType)
				r = (r as DerivedDataType).Base;

			return r;
		}

		public static AbstractType[] StripAliasSymbols(IEnumerable<AbstractType> symbols)
		{
			var l = new List<AbstractType>();

			if(symbols != null)
				foreach (var r in symbols)
					l.Add(StripAliasSymbol(r));

			return l.ToArray();
		}

		/// <summary>
		/// Removes all kinds of members from the given results.
		/// </summary>
		/// <param name="resolvedMember">True if a member (not an alias!) had to be bypassed</param>
		public static AbstractType StripMemberSymbols(AbstractType r)
		{
			r = StripAliasSymbol(r);

			if (r is ArrayAccessSymbol)
				r = (r as ArrayAccessSymbol).Base;

			var ms = r as MemberSymbol;
			if(ms != null)
				r = (ms as DerivedDataType).Base;
			
			if(r is TemplateParameterSymbol)
				r = (r as TemplateParameterSymbol).Base;

			// There's one special case to handle (TODO: are there further cases?):
			// auto o = new Class(); -- o will be MemberSymbol and its base type will be a MemberSymbol either (i.e. the constructor reference)
			ms = r as MemberSymbol;			
			if(ms!=null && ms.Definition is DMethod && ms.Name == DMethod.ConstructorIdentifier)
				r = ms.Base;

			return StripAliasSymbol(r);
		}

		public static AbstractType[] StripMemberSymbols(IEnumerable<AbstractType> symbols)
		{
			var l = new List<AbstractType>();

			if(symbols != null)
				foreach (var r in symbols)
				{
					l.Add(StripMemberSymbols(r));
				}

			return l.ToArray();
		}
	}
}
