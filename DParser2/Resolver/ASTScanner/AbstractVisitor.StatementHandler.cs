using System;
using System.Collections.Generic;
using System.Linq;
using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Resolver.ASTScanner.Util;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Resolver.ASTScanner
{
	partial class AbstractVisitor
	{
		struct StatementHandler : StatementVisitor<bool>, NodeVisitor<bool>, IDisposable
		{
			#region Properties
			public readonly ItemCheckParameters parms;
			public bool caretInsensitive { get { return Caret.IsEmpty; } }
			public readonly CodeLocation Caret;
			readonly ResolutionContext ctxt;

			readonly AbstractVisitor v;
			readonly bool pushResolvedCurScope;
			IDisposable frameToPop;

			readonly IBlockNode parentNodeOfVisitedStmt;
			#endregion

			#region Constuctor/IO
			public StatementHandler(
				IBlockNode parentNodeOfVisitedStmt,
				AbstractVisitor v,
				ItemCheckParameters parms,
				CodeLocation caret,
				bool pushResolvedCurScope = false)
			{
				this.parentNodeOfVisitedStmt = parentNodeOfVisitedStmt;
				this.v = v;
				ctxt = v.ctxt;
				this.Caret = caret;
				this.parms = parms;

				frameToPop = null;
				this.pushResolvedCurScope = pushResolvedCurScope;
			}

			void TryPushCurScope()
			{
				if (frameToPop == null && pushResolvedCurScope)
				{
					frameToPop = ctxt.Push(parms.resolvedCurScope);
				}
			}

			public void Dispose()
			{
				if (frameToPop != null)
					frameToPop.Dispose();
			}

			#endregion

			#region Nodes
			public bool Visit(DEnumValue dEnumValue)
			{
				throw new NotImplementedException();
			}

			public bool Visit(DMethod dMethod)
			{
				return false;
			}

			public bool Visit(DClassLike dc)
			{
				return Visit(dc as DBlockNode);
			}

			public bool Visit(DEnum de)
			{
				return Visit(de as DBlockNode);
			}

			public bool Visit(DModule dbn)
			{
				// Every module imports 'object' implicitly
				bool ret = false;

				if (!parms.publicImportsOnly)
					ret = _objectImport.Accept(this);

				ret |= Visit(dbn as DBlockNode);

				return ret;
			}

			public bool Visit(TemplateParameter.Node templateParameterNode)
			{
				throw new NotImplementedException();
			}

			public bool Visit(NamedTemplateMixinNode n)
			{
				throw new NotImplementedException();
			}

			public bool Visit(DBlockNode dbn)
			{
				bool ret = false;
				if (dbn != null && dbn.StaticStatements != null)
				{
					foreach (var ss in dbn.StaticStatements)
						ret |= ss != null && ss.Accept(this);
				}

				return ret;
			}


			public bool VisitDVariable(DVariable dv)
			{
				if (dv.Initializer != null &&
					(caretInsensitive || (dv.Initializer.Location > Caret && dv.Initializer.EndLocation < Caret)))
				{
					TryPushCurScope();
					v.HandleExpression(dv.Initializer, parms);

					if (v.StopEnumerationOnNextScope)
						return true;
				}

				return VisitDNode(dv);
			}

			public bool VisitDNode(DNode decl)
			{
				if (!(caretInsensitive || Caret >= decl.Location))
					return false;

				TryPushCurScope();
				v.HandleItemInternal(decl, parms);
				return v.StopEnumerationOnNextScope;
			}

			public bool Visit(ModuleAliasNode n)
			{
				return VisitDVariable(n);
			}

			public bool Visit(ImportSymbolNode n)
			{
				return VisitDVariable(n);
			}

			public bool Visit(ImportSymbolAlias n)
			{
				return VisitDVariable(n);
			}

			public bool Visit(EponymousTemplate n)
			{
				return VisitDVariable(n);
			}
			#endregion

			#region Attributes
			public bool VisitAttribute(Modifier attr)
			{
				throw new NotImplementedException();
			}

			public bool VisitAttribute(DeprecatedAttribute a)
			{
				throw new NotImplementedException();
			}

			public bool VisitAttribute(PragmaAttribute attr)
			{
				throw new NotImplementedException();
			}

			public bool VisitAttribute(BuiltInAtAttribute a)
			{
				throw new NotImplementedException();
			}

			public bool VisitAttribute(UserDeclarationAttribute a)
			{
				throw new NotImplementedException();
			}

			public bool VisitAttribute(VersionCondition a)
			{
				throw new NotImplementedException();
			}

			public bool VisitAttribute(DebugCondition a)
			{
				throw new NotImplementedException();
			}

			public bool VisitAttribute(StaticIfCondition a)
			{
				throw new NotImplementedException();
			}

			public bool VisitAttribute(NegatedDeclarationCondition a)
			{
				throw new NotImplementedException();
			}
			#endregion

			#region Static Statements
			public bool VisitExpressionStmt(IExpressionContainingStatement Statement)
			{
				if (Statement.Location < Caret && Statement.EndLocation >= Caret && Statement.SubExpressions != null)
					foreach (var x in Statement.SubExpressions)
						if (x != null && x.Location < Caret && x.EndLocation >= Caret)
						{
							TryPushCurScope();
							v.HandleExpression(x, parms);
							if (v.StopEnumerationOnNextScope)
								return true;
						}
				return false;
			}

			public bool VisitSubStatements(StatementContainingStatement stmtContainer, bool ignoreBounds = false)
			{
				if (!(ignoreBounds || caretInsensitive) &&
					(stmtContainer.Location > Caret || stmtContainer.EndLocation < Caret))
					return false;

				var ss = stmtContainer.SubStatements;
				if (ss != null)
				{
					foreach (var s in ss)
					{
						if (s == null)
							continue;

						if ((!caretInsensitive && s.Location >= Caret) || ctxt.CancellationToken.IsCancellationRequested)
							break;

						if (s.Accept(this))
							return true;
					}
				}

				return false;
			}

			public bool Visit(ImportStatement impStmt)
			{
				if ((parms.publicImportsOnly && impStmt != null && !impStmt.IsPublic))
					return false;

				TryPushCurScope();

				if (!ctxt.CurrentContext.MatchesDeclarationEnvironment(impStmt.Attributes))
					return false;

				/*
					* Mainly used for selective imports/import module aliases
					*/
				foreach (var decl in impStmt.PseudoAliases)
				{
					v.HandleItemInternal(decl, parms);
					if (v.StopEnumerationOnNextScope) //TODO: Handle visibility?
						return true;
				}

				var ret = false;

				if (!impStmt.IsStatic)
				{
					foreach (var imp in impStmt.Imports)
						ret |= imp.ModuleAlias == null && imp.Accept(this);
				}

				return ret;
			}

			public bool VisitImport(ImportStatement.Import imp)
			{
				if (imp == null || imp.ModuleIdentifier == null)
					return false;

				TryPushCurScope();

				DModule mod;
				var thisModuleName = (ctxt.ScopedBlock != null && (mod = ctxt.ScopedBlock.NodeRoot as DModule) != null) ? mod.NameHash : 0;

				if (thisModuleName == 0)
					return false;

				var moduleName = imp.ModuleIdentifier.ToString();

				List<string> seenModules;
				if (!v.scannedModules.TryGetValue(thisModuleName, out seenModules))
				{
					seenModules = new List<string>();
					v.scannedModules[thisModuleName] = seenModules;
				}
				else if (seenModules.Contains(moduleName))
					return false;
				seenModules.Add(moduleName);

				if (ctxt.ParseCache == null)
					return false;

				var scopedModule = parentNodeOfVisitedStmt.NodeRoot as DModule;

				var childscanParameters = new ItemCheckParameters(parms) { publicImportsOnly = true };
				foreach (var module in ctxt.ParseCache.LookupModuleName(scopedModule, moduleName)) //TODO: Only take the first module? Notify the user about ambigous module names?
				{
					if (module.FileName == null || module.FileName != scopedModule.FileName)
						v.scanChildren(module, childscanParameters);

					if (v.StopEnumerationOnNextScope)
						return true;

					if (ctxt.CancellationToken.IsCancellationRequested)
						break;
				}
				return false;
			}
			#endregion

			#region Template Mixins
			/// <summary>
			/// http://dlang.org/template-mixin.html#TemplateMixin
			/// </summary>
			public bool Visit(TemplateMixin tmx)
			{
				if (ctxt.CompletionOptions.DisableMixinAnalysis)
					return false;

				if (templateMixinsBeingAnalyzed == null)
					templateMixinsBeingAnalyzed = new List<TemplateMixin>();

				if (templateMixinsBeingAnalyzed.Contains(tmx))
					return false;
				templateMixinsBeingAnalyzed.Add(tmx);


				TryPushCurScope();

				if (!ctxt.CurrentContext.MatchesDeclarationEnvironment(tmx.Attributes))
					return false;

				var tmxTemplate = GetTemplateMixinContent(ctxt, tmx, false);

				if (tmxTemplate == null)
					ctxt.LogError(tmx.Qualifier, "Mixin qualifier must resolve to a mixin template declaration.");
				else using (ctxt.Push(tmxTemplate))
					{
						v.DeepScanClass(tmxTemplate, new ItemCheckParameters(parms) { dontHandleTemplateParamsInNodeScan = true });
					}

				templateMixinsBeingAnalyzed.Remove(tmx);

				return v.StopEnumerationOnNextScope;
			}

			[ThreadStatic]
			static List<TemplateMixin> templateMixinsBeingAnalyzed;
			#endregion

			#region Mixin
			/// <summary>
			/// Evaluates the literal given as expression and tries to parse it as a string.
			/// Important: Assumes all its compilation conditions to be checked already!
			/// </summary>
			public bool VisitMixinStatement(MixinStatement mx)
			{
				if (ctxt.CompletionOptions.DisableMixinAnalysis)
					return false;

				TryPushCurScope();

				if (!ctxt.CurrentContext.MatchesDeclarationEnvironment(mx.Attributes))
					return false;

				VariableValue vv;
				// If in a class/module block => MixinDeclaration
				if (caretInsensitive)
				{
					var ast = MixinAnalysis.ParseMixinDeclaration(mx, ctxt, out vv);

					if (ast == null)
						return false;

					if (vv != null)
						ctxt.CurrentContext.IntroduceTemplateParameterTypes(vv.RepresentedType);

					// take ast.Endlocation because the cursor must be beyond the actual mixin expression 
					// - and therewith _after_ each declaration
					v.scanChildren(ast, new ItemCheckParameters(parms) { isMixinAst = true });

					if (vv != null)
						ctxt.CurrentContext.RemoveParamTypesFromPreferredLocals(vv.RepresentedType);
				}
				else // => MixinStatement
				{
					var bs = MixinAnalysis.ParseMixinStatement(mx, ctxt, out vv);

					// As above, disregard the caret position because 1) caret and parsed code do not match 
					// and 2) the caret must be located somewhere after the mixin statement's end
					if (bs != null)
					{
						if (vv != null)
							ctxt.CurrentContext.IntroduceTemplateParameterTypes(vv.RepresentedType);

						bs.Accept(new StatementHandler(parentNodeOfVisitedStmt, v, parms, CodeLocation.Empty));

						if (vv != null)
							ctxt.CurrentContext.RemoveParamTypesFromPreferredLocals(vv.RepresentedType);
					}
				}

				return v.StopEnumerationOnNextScope;
			}

			static MixinTemplateType GetTemplateMixinContent(ResolutionContext ctxt, TemplateMixin tmx, bool pushOnAnalysisStack = true)
			{
				if (pushOnAnalysisStack)
				{
					if (templateMixinsBeingAnalyzed == null)
						templateMixinsBeingAnalyzed = new List<TemplateMixin>();

					if (templateMixinsBeingAnalyzed.Contains(tmx))
						return null;
					templateMixinsBeingAnalyzed.Add(tmx);
				}

				AbstractType t;
				t = TypeDeclarationResolver.ResolveSingle(tmx.Qualifier, ctxt);

				if (pushOnAnalysisStack)
					templateMixinsBeingAnalyzed.Remove(tmx);

				return t as MixinTemplateType;
			}
			#endregion

			#region In-Method statements
			public bool Visit(ModuleStatement moduleStatement)
			{
				return false;
			}

			public bool VisitImport(ImportStatement.ImportBinding importBinding)
			{
				return false;
			}

			public bool VisitImport(ImportStatement.ImportBindings bindings)
			{
				return false;
			}

			public bool Visit(BlockStatement s)
			{
				return VisitSubStatements(s);
			}

			public bool Visit(LabeledStatement s)
			{
				return false;
			}

			public bool Visit(IfStatement s)
			{
				if (s.IfVariable != null && VisitDVariable(s.IfVariable))
					return true;

				return VisitExpressionStmt(s) || VisitSubStatements(s);
			}

			public bool Visit(WhileStatement s)
			{
				return VisitExpressionStmt(s) || VisitSubStatements(s);
			}

			public bool Visit(ForStatement s)
			{
				return VisitExpressionStmt(s) || VisitSubStatements(s);
			}

			public bool Visit(ForeachStatement s)
			{
				if (s.Location < Caret &&
					(s.ScopedStatement == null || s.ScopedStatement.EndLocation >= Caret) &&
					s.ForeachTypeList != null)
					foreach (var n in s.ForeachTypeList)
						if (n != null && n.Accept(this))
							return true;

				return VisitExpressionStmt(s) || VisitSubStatements(s);
			}

			public bool Visit(SwitchStatement s)
			{
				return VisitExpressionStmt(s) || VisitSubStatements(s);
			}

			public bool Visit(SwitchStatement.CaseStatement s)
			{
				return VisitExpressionStmt(s) || VisitSubStatements(s);
			}

			public bool Visit(SwitchStatement.DefaultStatement s)
			{
				return VisitSubStatements(s);
			}

			public bool Visit(ContinueStatement s)
			{
				return VisitExpressionStmt(s);
			}

			public bool Visit(BreakStatement s)
			{
				return VisitExpressionStmt(s);
			}

			public bool Visit(ReturnStatement s)
			{
				return VisitExpressionStmt(s);
			}

			public bool Visit(GotoStatement s)
			{
				return VisitExpressionStmt(s);
			}

			/// <summary>
			/// http://dlang.org/statement.html#WithStatement
			/// </summary>
			public bool Visit(WithStatement ws)
			{
				if (ws.ScopedStatement == null ||
					(!caretInsensitive && Caret < ws.ScopedStatement.Location))
					return false;

				TryPushCurScope();

				var back = ctxt.CurrentContext.Caret;
				ctxt.CurrentContext.Set(ctxt.ScopedBlock, ws.Parent.Location);

				AbstractType r;
				// Must be an expression that returns an object reference
				if (ws.WithExpression != null)
					r = ExpressionTypeEvaluation.EvaluateType(ws.WithExpression, ctxt);
				else if (ws.WithSymbol != null) // This symbol will be used as default
					r = TypeDeclarationResolver.ResolveSingle(ws.WithSymbol, ctxt);
				else
					r = null;

				ctxt.CurrentContext.Set(ctxt.ScopedBlock, back);

				if ((r = DResolver.StripMemberSymbols(r)) is UserDefinedType)
				{
					v.DeepScanClass(r as UserDefinedType, parms);
					if (v.StopEnumerationOnNextScope)
						return true;
				}

				return VisitSubStatements(ws);
			}

			public bool Visit(SynchronizedStatement s)
			{
				return VisitExpressionStmt(s) || VisitSubStatements(s);
			}

			public bool Visit(TryStatement s)
			{
				return VisitSubStatements(s);
			}

			public bool Visit(TryStatement.CatchStatement s)
			{
				return (s.CatchParameter != null && VisitDVariable(s.CatchParameter)) || VisitSubStatements(s);
			}

			public bool Visit(TryStatement.FinallyStatement s)
			{
				return VisitSubStatements(s);
			}

			public bool Visit(ThrowStatement s)
			{
				return VisitExpressionStmt(s);
			}

			public bool Visit(ScopeGuardStatement s)
			{
				return VisitSubStatements(s);
			}

			public bool VisitAsmStatement(AsmStatement s)
			{
				return VisitSubStatements(s);
			}

			public bool Visit(PragmaStatement s)
			{
				return VisitSubStatements(s);
			}

			public bool Visit(StatementCondition sc)
			{
				if ((ctxt.Options & ResolutionOptions.IgnoreDeclarationConditions) != 0)
				{
					if (sc.ScopedStatement != null && sc.ScopedStatement.Accept(this))
						return false;

					return sc.ElseStatement != null && sc.ElseStatement.Accept(this);
				}

				TryPushCurScope();

				if (!ctxt.CurrentContext.MatchesDeclarationEnvironment(sc.Condition))
					return false;

				if (sc.Condition is StaticIfCondition && sc.Location < Caret && sc.EndLocation >= Caret)
				{
					v.HandleExpression((sc.Condition as StaticIfCondition).Expression, parms);
					if (v.StopEnumerationOnNextScope)
						return true;
				}

				if (sc.ScopedStatement is BlockStatement &&
					VisitSubStatements(sc.ScopedStatement as StatementContainingStatement, true))
					return true;
				else if (sc.ScopedStatement != null && sc.ScopedStatement.Accept(this))
					return true;

				return sc.ElseStatement != null && sc.ElseStatement.Accept(this);
			}

			public bool Visit(VolatileStatement s)
			{
				return VisitSubStatements(s);
			}

			public bool Visit(ExpressionStatement s)
			{
				return VisitExpressionStmt(s);
			}

			public bool Visit(DeclarationStatement s)
			{
				if (s.Declarations != null)
					foreach (DNode decl in s.Declarations)
						if (decl is DVariable ? VisitDVariable(decl as DVariable) : VisitDNode(decl))
							return true;

				return false;
			}

			public bool Visit(DebugSpecification s)
			{
				return false;
			}

			public bool Visit(VersionSpecification s)
			{
				return false;
			}

			public bool Visit(StaticAssertStatement s)
			{
				return VisitExpressionStmt(s);
			}

			IEnumerable<KeyValuePair<ISymbolValue, ISymbolValue>> DetermineForeachKeyValuePairs(StaticForeachStatement s)
			{
				if (s.IsRangeStatement)
				{
					var lowerBound = Evaluation.EvaluateValue(s.Aggregate, ctxt);
					var upperBound = Evaluation.EvaluateValue(s.UpperAggregate, ctxt);

					var lowerPrimitive = lowerBound as PrimitiveValue;
					var upperPrimitive = upperBound as PrimitiveValue;

					if (lowerPrimitive == null)
					{
						ctxt.LogError(s.Aggregate, "couldn't resolve lower-bound aggregate number");
						return null;
					}
					else if (upperPrimitive == null)
					{
						ctxt.LogError(s.UpperAggregate, "couldn't resolve lower-bound aggregate number");
						return null;
					}

					if (lowerPrimitive.Value > upperPrimitive.Value)
					{
						ctxt.LogError(s, "lower aggregate " + lowerPrimitive.Value + " must be less than " + upperPrimitive.Value);
						return null;
					}

					return new IotaEnumerable((PrimitiveType)lowerPrimitive.RepresentedType, lowerPrimitive.Value, upperPrimitive.Value);
				}
				else
				{
					var aggregate = Evaluation.EvaluateValue(s.Aggregate, ctxt);

					if (aggregate is AssociativeArrayValue)
					{
						var aa = aggregate as AssociativeArrayValue;
						return aa.Elements;
					}
					else if (aggregate is ArrayValue)
					{
						var av = aggregate as ArrayValue;
						IEnumerable<ISymbolValue> values;
						if (av.IsString)
						{
							var arrayType = av.RepresentedType as ArrayType;
							values = new StringCharValuesEnumerable((PrimitiveType)arrayType.ValueType, av.StringValue);
						}
						else if (av.Elements != null)
							values = av.Elements;
						else
							values = Enumerable.Empty<ISymbolValue>();

						return new IndexKeyExtendingEnumerable(values);
					}
					else
					{
						ctxt.LogError(s, "aggregate must be (optionally associative) array");
						return null;
					}
				}
			}

			public bool Visit(StaticForeachStatement s)
			{
				if(s.ForeachTypeList == null || s.ForeachTypeList.Length == 0)
					return Visit(s as ForeachStatement);

				var  keyValues = DetermineForeachKeyValuePairs(s);
				if(keyValues == null)
					return Visit(s as ForeachStatement);

				var valueHoldingIterator = s.ForeachTypeList.Length > 1 ? s.ForeachTypeList[1] : s.ForeachTypeList[0];
				var keyHoldingIterator = s.ForeachTypeList.Length > 1 ? s.ForeachTypeList[0] : null;

				var pseudoValueTemplateParameter = new TemplateValueParameter(valueHoldingIterator.NameHash, valueHoldingIterator.NameLocation, valueHoldingIterator.Parent as DNode).Representation;
				var pseudoKeyTemplateParameter = keyHoldingIterator != null ? new TemplateValueParameter(keyHoldingIterator.NameHash, keyHoldingIterator.NameLocation, keyHoldingIterator.Parent as DNode).Representation : null;

				var deducedTypes = ctxt.CurrentContext.DeducedTemplateParameters;

				foreach (var kv in keyValues)
				{
					// Set iterator/optinal key variables as template parameter
					deducedTypes[pseudoValueTemplateParameter.TemplateParameter] = new TemplateParameterSymbol(pseudoValueTemplateParameter, kv.Value);
					if(pseudoKeyTemplateParameter != null)
						deducedTypes[pseudoKeyTemplateParameter.TemplateParameter] = new TemplateParameterSymbol(pseudoKeyTemplateParameter, kv.Key);

					if (s.ScopedStatement.Accept(this))
						break;

					if (v.ctxt.CancellationToken.IsCancellationRequested ||
						v.StopEnumerationOnNextScope)
						break;
				}

				deducedTypes.Remove(pseudoValueTemplateParameter.TemplateParameter);
				if(pseudoKeyTemplateParameter != null)
					deducedTypes.Remove(pseudoKeyTemplateParameter.TemplateParameter);

				return false;
			}

			public bool VisitAsmInstructionStatement(AsmInstructionStatement instrStatement)
			{
				return false;
			}

			public bool VisitAsmRawDataStatement(AsmRawDataStatement dataStatement)
			{
				return false;
			}

			public bool VisitAsmAlignStatement(AsmAlignStatement alignStatement)
			{
				return false;
			}
			#endregion
		}
	}
}