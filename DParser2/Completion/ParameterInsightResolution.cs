using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Completion
{
	public class ArgumentsResolutionResult
	{
		public bool IsMethodArguments;
		public bool IsTemplateInstanceArguments;

		public IExpression ParsedExpression;

		/// <summary>
		/// Usually some part of the ParsedExpression.
		/// For instance in a PostfixExpression_MethodCall it'd be the PostfixForeExpression.
		/// </summary>
		public object MethodIdentifier;

		public AbstractType[] ResolvedTypesOrMethods;

		public readonly Dictionary<IExpression, AbstractType> TemplateArguments = new Dictionary<IExpression, AbstractType>();
		/// <summary>
		/// Stores the already typed arguments (Expressions) + their resolved types.
		/// The value part will be null if nothing could get returned.
		/// </summary>
		public readonly Dictionary<IExpression, AbstractType> Arguments = new Dictionary<IExpression, AbstractType>();

		/// <summary>
		///	Identifies the currently called method overload. Is an index related to <see cref="ResolvedTypesOrMethods"/>
		/// </summary>
		public int CurrentlyCalledMethod;
		public IExpression CurrentlyTypedArgument
		{
			get
			{
				if (Arguments != null && Arguments.Count > CurrentlyTypedArgumentIndex)
				{
					int i = 0;
					foreach (var kv in Arguments)
					{
						if (i == CurrentlyTypedArgumentIndex)
							return kv.Key;
						i++;
					}
				}
				return null;
			}
		}
		public int CurrentlyTypedArgumentIndex;
	}

	public class ParameterInsightResolution
	{
		
		/// <summary>
		/// Reparses the given method's fucntion body until the cursor position,
		/// searches the last occurring method call or template instantiation,
		/// counts its already typed arguments
		/// and returns a wrapper containing all the information.
		/// </summary>
		public static ArgumentsResolutionResult ResolveArgumentContext(
			IEditorData Editor,
			ResolutionContext ctxt)
		{
			ParserTrackerVariables trackVars;
			IBlockNode curBlock = null;
			IStatement curStmt;

			var sr = CtrlSpaceCompletionProvider.FindCurrentCaretContext(Editor, out trackVars, ref curBlock, out curStmt);

			IExpression lastParamExpression = null;

			var parsedStmtBlock = sr as IStatement;
			if (parsedStmtBlock != null)
			{
				// Search the returned statement block (i.e. function body) for the current statement;
				if (parsedStmtBlock is StatementContainingStatement)
					parsedStmtBlock = (parsedStmtBlock as StatementContainingStatement).SearchStatementDeeply (Editor.CaretLocation);

				lastParamExpression = ExpressionHelper.SearchForMethodCallsOrTemplateInstances(parsedStmtBlock, Editor.CaretLocation);
			}
			else if (trackVars != null && trackVars.IsParsingInitializer)
			{
				if (trackVars.InitializedNode is DVariable)
					lastParamExpression = 
						ExpressionHelper.SearchExpressionDeeply((trackVars.InitializedNode as DVariable).Initializer, Editor.CaretLocation);
			}

			if (lastParamExpression == null)
			{
				// Give it a last chance by handling the lastly parsed object 
				// - which is a TemplateInstanceExpression in quite all cases
				lastParamExpression = trackVars.LastParsedObject as IExpression;
			}

			/*
			 * Then handle the lastly found expression regarding the following points:
			 * 
			 * 1) foo(			-- normal arguments only
			 * 2) foo!(...)(	-- normal arguments + template args
			 * 3) foo!(		-- template args only
			 * 4) new myclass(  -- ctor call
			 * 5) new myclass!( -- ditto
			 * 6) new myclass!(...)(
			 * 7) mystruct(		-- opCall call
			 */

			var res = new ArgumentsResolutionResult() { 
				ParsedExpression = lastParamExpression
			};

			// 1), 2)
			if (lastParamExpression is PostfixExpression_MethodCall)
			{
				res.IsMethodArguments = true;
				var call = (PostfixExpression_MethodCall) lastParamExpression;

				res.MethodIdentifier = call.PostfixForeExpression;
				res.ResolvedTypesOrMethods = Evaluation.GetUnfilteredMethodOverloads(call.PostfixForeExpression, ctxt, call);

				if (call.Arguments != null)
					res.CurrentlyTypedArgumentIndex = call.ArgumentCount;
			}
			// 3)
			else if (lastParamExpression is TemplateInstanceExpression)
			{
				var templ = (TemplateInstanceExpression)lastParamExpression;

				res.IsTemplateInstanceArguments = true;

				res.MethodIdentifier = templ;
				res.ResolvedTypesOrMethods = Evaluation.GetOverloads(templ, ctxt, null, false);

				if (templ.Arguments != null)
					res.CurrentlyTypedArgumentIndex = templ.Arguments.Length;
				else
					res.CurrentlyTypedArgumentIndex = 0;
			}
			else if (lastParamExpression is PostfixExpression_Access)
			{
				var acc = (PostfixExpression_Access)lastParamExpression;

				res.MethodIdentifier = acc.PostfixForeExpression;
				res.ResolvedTypesOrMethods = Evaluation.GetUnfilteredMethodOverloads(acc.PostfixForeExpression, ctxt, acc);

				if (res.ResolvedTypesOrMethods == null)
					return res;

				if (acc.AccessExpression is NewExpression)
					CalculateCurrentArgument(acc.AccessExpression as NewExpression, res, Editor.CaretLocation, ctxt, res.ResolvedTypesOrMethods);
			}
			else if (lastParamExpression is NewExpression)
				HandleNewExpression((NewExpression)lastParamExpression,res,Editor,ctxt,curBlock);

			/*
			 * alias int function(int a, bool b) myDeleg;
			 * alias myDeleg myDeleg2;
			 * 
			 * myDeleg dg;
			 * 
			 * dg( -- it's not needed to have myDeleg but the base type for what it stands for
			 * 
			 * ISSUE:
			 * myDeleg( -- not allowed though
			 * myDeleg2( -- allowed neither!
			 */
			if (res.ResolvedTypesOrMethods != null)
				res.ResolvedTypesOrMethods = DResolver.StripAliasSymbols(res.ResolvedTypesOrMethods);

			return res;
		}

		static void HandleNewExpression(NewExpression nex, 
			ArgumentsResolutionResult res, 
			IEditorData Editor, 
			ResolutionContext ctxt,
			IBlockNode curBlock)
		{
			res.MethodIdentifier = nex;
			CalculateCurrentArgument(nex, res, Editor.CaretLocation, ctxt);

			var type = TypeDeclarationResolver.Resolve(nex.Type, ctxt);
			
			if(type != null)
			{
				var _ctors = new List<AbstractType>();
				
				foreach(var t in type)
				{
					//TODO: Inform the user that only classes can be instantiated
					if (t is ClassType || t is StructType)
					{
						var udt = t as TemplateIntermediateType;
						bool explicitCtorFound = false;
						var constructors = new List<DMethod>();
		
						foreach (var member in udt.Definition)
						{
							var dm = member as DMethod;
		
							if (dm != null && dm.SpecialType == DMethod.MethodType.Constructor)
							{
								explicitCtorFound = true;
								if (!dm.IsPublic)
								{
									var curNode = curBlock;
									bool pass = false;
									do
									{
										if (curNode == udt.Definition)
										{
											pass = true;
											break;
										}
									}
									while ((curNode = curNode.Parent as IBlockNode) != curNode);
		
									if (!pass)
										continue;
								}
		
								constructors.Add(dm);
							}
						}
		
						if (constructors.Count == 0)
						{
							if (explicitCtorFound)
							{
								// TODO: Somehow inform the user that the current class can't be instantiated
							}
							else
							{
								// Introduce default constructor
								constructors.Add(new DMethod(DMethod.MethodType.Constructor)
								{
									Description = "Default constructor for " + udt.Name,
									Parent = udt.Definition
								});
							}
						}
						
						// Wrapp all ctor members in MemberSymbols
						foreach (var ctor in constructors)
							_ctors.Add(new MemberSymbol(ctor, t, nex.Type));
					}
				}
				
				res.ResolvedTypesOrMethods = _ctors.ToArray();

				//TODO: Probably pre-select the current ctor by handling previously typed arguments etc.
			}
		}

		static void CalculateCurrentArgument(NewExpression nex, 
			ArgumentsResolutionResult res, 
			CodeLocation caretLocation, 
			ResolutionContext ctxt,
			IEnumerable<AbstractType> resultBases=null)
		{
			if (nex.Arguments != null)
				res.CurrentlyTypedArgumentIndex = nex.Arguments.Length;
				/*{
				int i = 0;
				foreach (var arg in nex.Arguments)
				{
					if (caretLocation >= arg.Location && caretLocation <= arg.EndLocation)
					{
						res.CurrentlyTypedArgumentIndex = i;
						break;
					}
					i++;
				}
			}*/
		}

		public static ArgumentsResolutionResult ResolveArgumentContext(IEditorData editorData)
		{
			return ResolveArgumentContext(editorData, ResolutionContext.Create(editorData));
		}
	}
}
