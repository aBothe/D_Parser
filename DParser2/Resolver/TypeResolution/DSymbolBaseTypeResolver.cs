using System;
using System.Collections.Generic;
using System.Linq;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using D_Parser.Resolver.ExpressionSemantics;

namespace D_Parser.Resolver.TypeResolution
{
	struct DSymbolBaseTypeResolver : IResolvedTypeVisitor<DSymbol>
	{
		readonly ResolutionContext ctxt;
		readonly ISyntaxRegion typeBase;

		[ThreadStatic]
		static Dictionary<INode, int> stackCalls;

		[System.Diagnostics.DebuggerStepThrough]
		DSymbolBaseTypeResolver(ResolutionContext ctxt, ISyntaxRegion typeBase)
		{
			this.ctxt = ctxt;
			this.typeBase = typeBase;
		}

		public static DSymbol ResolveBaseType(DSymbol symbol, ResolutionContext ctxt, ISyntaxRegion typeBase)
		{
			if (symbol == null || symbol.Base != null)
				return symbol;

			var symbolDefinition = symbol.Definition;
			BumpResolutionStackLevel(symbolDefinition);

			var visitor = new DSymbolBaseTypeResolver(ctxt, typeBase);

			try
			{
				if (HasntReachedResolutionStackPeak(symbolDefinition))
				{
					if (symbol is TemplateParameterSymbol ?
						((ctxt.Options & ResolutionOptions.NoTemplateParameterDeduction) == 0)
						: CanResolveBase(symbolDefinition, ctxt))
					{
						using (ctxt.Push(symbol))
							return symbol.Accept(visitor);
					}
				}
				return symbol;
			}
			finally
			{
				PopResolutionStackLevel(symbolDefinition);
			}
		}

		private static void PopResolutionStackLevel(DNode symbolDefinition)
		{
			int stkC;
			stackCalls.TryGetValue(symbolDefinition, out stkC);
			if (stkC == 1)
				stackCalls.Remove(symbolDefinition);
			else
				stackCalls[symbolDefinition] = stkC - 1;
		}

		private static void BumpResolutionStackLevel(DNode symbolDefinition)
		{
			// See https://github.com/aBothe/Mono-D/issues/161
			int stkC;
			if (stackCalls == null)
			{
				stackCalls = new Dictionary<INode, int>();
				stackCalls[symbolDefinition] = 1;
			}
			else
				stackCalls[symbolDefinition] = stackCalls.TryGetValue(symbolDefinition, out stkC) ? ++stkC : 1;
		}

		static bool HasntReachedResolutionStackPeak(INode nodeToResolve)
		{
			if (stackCalls != null)
			{
				int stkC;
				stackCalls.TryGetValue(nodeToResolve, out stkC);
				return stkC < 4;
			}
			return true;
		}
		public static bool CanResolveBase(INode m, ResolutionContext ctxt)
		{
			return ((ctxt.Options & ResolutionOptions.DontResolveBaseTypes) != ResolutionOptions.DontResolveBaseTypes)
					&& (!(m.Type is IdentifierDeclaration)
					|| (m.Type as IdentifierDeclaration).IdHash != m.NameHash || m.Type.InnerDeclaration != null); // pretty rough and incomplete SO prevention hack
		}

		public DSymbol VisitAliasedType(AliasedType t)
		{
			return new AliasedType(t.Definition,
				ResolveDVariableBaseType(t.Definition, ctxt, true),
				typeBase, ctxt.DeducedTypesInHierarchy);
		}

		public DSymbol VisitClassType(ClassType t)
		{
			return ClassInterfaceResolver.ResolveClassOrInterface(t.Definition, ctxt, typeBase, false, t.DeducedTypes);
		}

		public DSymbol VisitEnumType(EnumType t)
		{
			var definition = t.Definition;
			AbstractType bt;

			if (definition.Type == null)
				bt = new PrimitiveType(DTokens.Int);
			else
			{
				using (ctxt.Push(definition.Parent))
					bt = TypeDeclarationResolver.ResolveSingle(definition.Type, ctxt);
			}

			return new EnumType(definition, bt);
		}

		public DSymbol VisitInterfaceType(InterfaceType t)
		{
			return ClassInterfaceResolver.ResolveClassOrInterface(t.Definition, ctxt, typeBase, false, t.DeducedTypes);
		}

		public DSymbol VisitMemberSymbol(MemberSymbol t)
		{
			if (t.Definition is DEnumValue)
			{
				return new MemberSymbol(t.Definition,
					TypeDeclarationResolver.HandleNodeMatch(t.Definition.Parent, ctxt),
					t.DeducedTypes);
			}

			if (t.Definition is DVariable)
				return new MemberSymbol(t.Definition,
					ResolveDVariableBaseType(t.Definition as DVariable, ctxt, true), ctxt.DeducedTypesInHierarchy);

			if (t.Definition is DMethod)
				return new MemberSymbol(t.Definition, GetMethodReturnType(t.Definition as DMethod, ctxt), t.DeducedTypes);

			throw new InvalidOperationException("invalid membersymbol def type: " + t.Definition);
		}

		public static AbstractType GetMethodReturnType(DMethod method, ResolutionContext ctxt)
		{
			AbstractType returnType;

			if ((ctxt.Options & ResolutionOptions.DontResolveBaseTypes) == ResolutionOptions.DontResolveBaseTypes)
				return null;

			/*
			 * If a method's type equals null, assume that it's an 'auto' function..
			 * 1) Search for a return statement
			 * 2) Resolve the returned expression
			 * 3) Use that one as the method's type
			 */
			if (method.Type != null)
			{
				using (ctxt.Push(method)) //FIXME: Is it legal to explicitly return a nested type?
					returnType = TypeDeclarationResolver.ResolveSingle(method.Type, ctxt);

				if (returnType != null)
					returnType.NonStaticAccess = true;

				return returnType;
			}
			else if (method.Body != null)
			{
				ReturnStatement returnStmt = null;
				var list = new List<IStatement> { method.Body };
				var list2 = new List<IStatement>();

				bool foundMatch = false;
				while (!foundMatch && list.Count > 0)
				{
					foreach (var stmt in list)
					{
						if (stmt is ReturnStatement)
						{
							returnStmt = stmt as ReturnStatement;

							var te = returnStmt.ReturnExpression as TokenExpression;
							if (te == null || te.Token != DTokens.Null)
							{
								foundMatch = true;
								break;
							}
						}

						var statementContainingStatement = stmt as StatementContainingStatement;
						if (statementContainingStatement != null)
							list2.AddRange(statementContainingStatement.SubStatements);
					}

					list = list2;
					list2 = new List<IStatement>();
				}

				if (returnStmt != null && returnStmt.ReturnExpression != null)
				{
					using (ctxt.Push(method, returnStmt.Location, true))
						returnType = DResolver.StripMemberSymbols(ExpressionTypeEvaluation.EvaluateType(returnStmt.ReturnExpression, ctxt));

					if (returnType != null)
						returnType.NonStaticAccess = true;

					return returnType;
				}

				return new PrimitiveType(DTokens.Void);
			}

			return null;
		}

		public static AbstractType ResolveDVariableBaseType(DVariable variable, ResolutionContext ctxt, bool resolveBaseTypeType)
		{
			if (!DSymbolBaseTypeResolver.CanResolveBase(variable, ctxt))
				return null;

			var optBackup = ctxt.CurrentContext.ContextDependentOptions;
			if (resolveBaseTypeType)
			{
				ctxt.CurrentContext.ContextDependentOptions |= ResolutionOptions.ReturnMethodReferencesOnly;
				if (variable.Type is IdentifierDeclaration)
					ctxt.CurrentContext.ContextDependentOptions |= ResolutionOptions.NoTemplateParameterDeduction;
			}

			var bt = TypeDeclarationResolver.ResolveSingle(variable.Type, ctxt);

			ctxt.CurrentContext.ContextDependentOptions = optBackup;

			// For auto variables, use the initializer to get its type
			if (bt == null && variable.Initializer != null)
				bt = DResolver.StripMemberSymbols(ExpressionTypeEvaluation.EvaluateType(variable.Initializer, ctxt));

			// Check if inside an foreach statement header
			if (bt == null)
				bt = GetForeachIteratorType(variable, ctxt);

			if (bt != null && variable.Attributes != null && variable.Attributes.Count > 0)
			{
				var variableModifiers = variable.Attributes.FindAll((DAttribute obj) => obj is Modifier).Select((arg) => ((Modifier)arg).Token).ToArray();
				if (variableModifiers.Length > 0)
				{
					bt = ResolvedTypeCloner.Clone(bt);
					if (bt.HasModifiers)
					{
						bt.Modifiers = bt.Modifiers.Union(variableModifiers).ToArray();
					}
					else
					{
						bt.Modifiers = variableModifiers;
					}
				}
			}

			return bt;
		}

		/// <summary>
		/// string[] s;
		/// 
		/// foreach(i;s)
		/// {
		///		// i is of type 'string'
		///		writeln(i);
		/// }
		/// </summary>
		static AbstractType GetForeachIteratorType(DVariable i, ResolutionContext ctxt)
		{
			var r = new List<AbstractType>();
			var curMethod = ctxt.ScopedBlock as DMethod;
			var loc = ctxt.CurrentContext.Caret;
			loc = new CodeLocation(loc.Column - 1, loc.Line); // SearchStmtDeeplyAt only checks '<' EndLocation, we may need to have '<=' though due to completion offsets.
			var curStmt = curMethod != null ? ASTSearchHelper.SearchStatementDeeplyAt(curMethod.GetSubBlockAt(ctxt.CurrentContext.Caret), loc) : null;

			if (curStmt == null)
				return null;

			bool init = true;
			// Walk up statement hierarchy -- note that foreach loops can be nested
			while (curStmt != null)
			{
				if (init)
					init = false;
				else
					curStmt = curStmt.Parent;

				if (curStmt is ForeachStatement)
				{
					var fe = (ForeachStatement)curStmt;

					if (fe.ForeachTypeList == null)
						continue;

					// If the searched variable is declared in the header
					int iteratorIndex = -1;

					for (int j = 0; j < fe.ForeachTypeList.Length; j++)
						if (fe.ForeachTypeList[j] == i)
						{
							iteratorIndex = j;
							break;
						}

					if (iteratorIndex == -1)
						continue;

					bool keyIsSearched = iteratorIndex == 0 && fe.ForeachTypeList.Length > 1;


					// foreach(var k, var v; 0 .. 9)
					if (keyIsSearched && fe.IsRangeStatement)
					{
						// -- it's static type int, of course(?)
						return new PrimitiveType(DTokens.Int);
					}

					if (fe.Aggregate == null)
						return null;

					var aggregateType = ExpressionTypeEvaluation.EvaluateType(fe.Aggregate, ctxt);

					aggregateType = DResolver.StripMemberSymbols(aggregateType);

					if (aggregateType == null)
						return null;

					// The most common way to do a foreach
					if (aggregateType is AssocArrayType)
					{
						var ar = (AssocArrayType)aggregateType;

						return keyIsSearched ? ar.KeyType : ar.ValueType;
					}
					else if (aggregateType is PointerType)
						return keyIsSearched ? TypeDeclarationResolver.ResolveSingle(new IdentifierDeclaration("size_t"), ctxt, false /* Generally, size_t isn't templated or such..so for performance, step through additional filtering */) : (aggregateType as PointerType).Base;
					else if (aggregateType is UserDefinedType)
					{
						var tr = (UserDefinedType)aggregateType;

						if (keyIsSearched || !(tr.Definition is IBlockNode))
							continue;

						bool foundIterPropertyMatch = false;
						#region Foreach over Structs and Classes with Ranges

						// Enlist all 'back'/'front' members
						var iterPropertyTypes = new List<AbstractType>();

						foreach (var n in (IBlockNode)tr.Definition)
							if (fe.IsReverse ? n.Name == "back" : n.Name == "front")
								iterPropertyTypes.Add(TypeDeclarationResolver.HandleNodeMatch(n, ctxt));

						foreach (var iterPropType in iterPropertyTypes)
							if (iterPropType is MemberSymbol)
							{
								foundIterPropertyMatch = true;

								var itp = (MemberSymbol)iterPropType;

								// Only take non-parameterized methods
								if (itp.Definition is DMethod && ((DMethod)itp.Definition).Parameters.Count != 0)
									continue;

								// Handle its base type [return type] as iterator type
								if (itp.Base != null)
									r.Add(itp.Base);

								foundIterPropertyMatch = true;
							}

						if (foundIterPropertyMatch)
							continue;
						#endregion

						#region Foreach over Structs and Classes with opApply
						iterPropertyTypes.Clear();
						r.Clear();

						foreach (var n in (IBlockNode)tr.Definition)
							if (n is DMethod &&
								(fe.IsReverse ? n.Name == "opApplyReverse" : n.Name == "opApply"))
								iterPropertyTypes.Add(TypeDeclarationResolver.HandleNodeMatch(n, ctxt));

						foreach (var iterPropertyType in iterPropertyTypes)
							if (iterPropertyType is MemberSymbol)
							{
								var mr = (MemberSymbol)iterPropertyType;
								var dm = mr.Definition as DMethod;

								if (dm == null || dm.Parameters.Count != 1)
									continue;

								var dg = dm.Parameters[0].Type as DelegateDeclaration;

								if (dg == null || dg.Parameters.Count != fe.ForeachTypeList.Length)
									continue;

								var paramType = TypeDeclarationResolver.ResolveSingle(dg.Parameters[iteratorIndex].Type, ctxt);

								if (paramType != null)
									r.Add(paramType);
							}
						#endregion
					}

					return AmbiguousType.Get(r);
				}
			}

			return null;
		}

		public DSymbol VisitTemplateParameterSymbol(TemplateParameterSymbol t)
		{
			return ResolveTemplateParameter(ctxt, (TemplateParameter.Node)t.Definition);
		}

		public static TemplateParameterSymbol ResolveTemplateParameter(ResolutionContext ctxt, TemplateParameter.Node tpn)
		{
			TemplateParameterSymbol tpnBase;

			if (ctxt.GetTemplateParam(tpn.NameHash, out tpnBase) && tpnBase.Parameter == tpn.TemplateParameter)
				return tpnBase;

			AbstractType baseType;
			//TODO: What if there are like nested default constructs like (T = U*, U = int) ?
			var ttp = tpn.TemplateParameter as TemplateTypeParameter;
			if (ttp != null && (ttp.Default != null || ttp.Specialization != null))
				baseType = TypeDeclarationResolver.ResolveSingle(ttp.Default ?? ttp.Specialization, ctxt);
			else
				baseType = null;

			return new TemplateParameterSymbol(tpn, baseType);
		}

		// No basetype to furtherly specify:
		public DSymbol VisitEponymousTemplateType(EponymousTemplateType t) => t;
		public DSymbol VisitMixinTemplateType(MixinTemplateType t) => t;
		public DSymbol VisitStructType(StructType t) => t;
		public DSymbol VisitTemplateType(TemplateType t) => t;
		public DSymbol VisitUnionType(UnionType t) => t;
		public DSymbol VisitModuleSymbol(ModuleSymbol t) => t;

		// Not usually being returned by NodeMatchHandleVisitor:
		public DSymbol VisitStaticProperty(StaticProperty t) => throw new NotImplementedException();

		// Not a DSymbol:
		public DSymbol VisitUnknownType(UnknownType t) => throw new NotImplementedException();
		public DSymbol VisitDelegateCallSymbol(DelegateCallSymbol t) => throw new NotImplementedException();
		public DSymbol VisitDelegateType(DelegateType t) => throw new NotImplementedException();
		public DSymbol VisitDTuple(DTuple t) => throw new NotImplementedException();
		public DSymbol VisitAmbigousType(AmbiguousType t) => throw new NotImplementedException();
		public DSymbol VisitArrayAccessSymbol(ArrayAccessSymbol t) => throw new NotImplementedException();
		public DSymbol VisitArrayType(ArrayType t) => throw new NotImplementedException();
		public DSymbol VisitAssocArrayType(AssocArrayType t) => throw new NotImplementedException();
		public DSymbol VisitPackageSymbol(PackageSymbol t) => throw new NotImplementedException();
		public DSymbol VisitPointerType(PointerType t) => throw new NotImplementedException();
		public DSymbol VisitPrimitiveType(PrimitiveType t) => throw new NotImplementedException();
	}
}
