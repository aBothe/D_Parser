using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Resolver.ASTScanner;
using D_Parser.Resolver.ExpressionSemantics.CTFE;
using D_Parser.Resolver.TypeResolution;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public class ExpressionTypeEvaluation : ExpressionVisitor<AbstractType>
	{
		#region Properties
		public bool TryReturnMethodReturnType;
		private readonly ResolutionContext ctxt;
		public readonly List<EvaluationException> Errors;
		bool ignoreErrors;
		#endregion

		ArrayType GetStringType(LiteralSubformat fmt = LiteralSubformat.Utf8)
		{
			return Evaluation.GetStringType(ctxt, fmt);
		}

		#region Errors
		internal void EvalError(EvaluationException ex)
		{
			if (!ignoreErrors)
				Errors.Add(ex);
		}

		internal void EvalError(IExpression x, string msg, ISemantic[] lastResults = null)
		{
			if (!ignoreErrors)
				Errors.Add(new EvaluationException(x, msg, lastResults));
		}

		internal void EvalError(IExpression x, string msg, ISemantic lastResult)
		{
			if (!ignoreErrors)
				Errors.Add(new EvaluationException(x, msg, new[] { lastResult }));
		}
		#endregion

		#region Ctor/IO
		public ExpressionTypeEvaluation(ResolutionContext ctxt)
		{
			this.ctxt = ctxt;
			TryReturnMethodReturnType = true;
			Errors = new List<EvaluationException> ();
			ignoreErrors = false;
		}

		public static AbstractType EvaluateType(IExpression x, ResolutionContext ctxt, bool tryReturnMethodReturnType = true)
		{
			if (x == null)
				return null;

			if (ctxt.CancellationToken.IsCancellationRequested)
				return new UnknownType(x);

			#if TRACE
			Trace.WriteLine("Evaluating type of "+x);
			Trace.Indent();
			#endif

			long cacheHashBias = tryReturnMethodReturnType ? 31 : 0;

			AbstractType t;
			if ((t = ctxt.Cache.TryGetType (x, cacheHashBias)) != null &&
			    (t.Tag<TypeDeclarationResolver.AliasTag> (TypeDeclarationResolver.AliasTag.Id) == null)) { // Don't accept aliases because their Tag does/base type might differ from the target alias definition.

				#if TRACE
				Trace.WriteLine("Return cached item "+(t != null ? t.ToString() : string.Empty));
				Trace.Unindent();
				#endif

				return t;
			}

			t = x.Accept(new ExpressionTypeEvaluation(ctxt) { TryReturnMethodReturnType = tryReturnMethodReturnType });

			if(!(t is TemplateParameterSymbol) || !ctxt.DeducedTypesInHierarchy.Any((tps)=> tps.Parameter == (t as TemplateParameterSymbol).Parameter)) // Don't allow caching parameters that affect the caching context.
				ctxt.Cache.Add(t ?? new UnknownType(x), x, cacheHashBias);

			#if TRACE
			Trace.Unindent();
			#endif

			return t;
		}
		#endregion

		#region Method (overloads)
		public AbstractType Visit(PostfixExpression_MethodCall call)
		{
			List<ISemantic> callArgs;
			ISymbolValue delegValue;

			AbstractType[] baseExpression;
			TemplateInstanceExpression tix;

			GetRawCallOverloads(ctxt, call.PostfixForeExpression, out baseExpression, out tix);

			return Evaluation.EvalMethodCall(baseExpression, null, tix, ctxt, call, out callArgs, out delegValue, !ctxt.Options.HasFlag(ResolutionOptions.ReturnMethodReferencesOnly));
		}


		AbstractType TryPretendMethodExecution(AbstractType b, ISyntaxRegion typeBase = null, AbstractType[] args = null)
		{
			if (!TryReturnMethodReturnType || (ctxt.Options & ResolutionOptions.ReturnMethodReferencesOnly) != 0)
				return b;

			if (b is AmbiguousType)
			{
				AbstractType first = null;

				foreach (var ov in (b as AmbiguousType).Overloads)
				{
					if (ov is MemberSymbol)
					{
						var next = TryPretendMethodExecution_(ov as MemberSymbol, args);
						if (first == null && next != ov)
						{
							first = next;
							continue;
						}
						// Error - ambiguous parameter configurations
					}
					
					// Error
				}

				return first ?? b;
			}

			var mr = b as MemberSymbol;
			return mr == null ? b : TryPretendMethodExecution_(mr, args);
		}

		AbstractType TryPretendMethodExecution_(MemberSymbol mr, AbstractType[] execargs = null)
		{
			if (!(mr.Definition is DMethod))
				return mr;

			Dictionary<DVariable, AbstractType> args;
			if(!FunctionEvaluation.AssignCallArgumentsToIC<AbstractType>(mr, execargs, null, out args, ctxt))
				return null;

			if((ctxt.Options & ResolutionOptions.DontResolveBaseTypes) != 0)
				return mr;

			return mr.Base;
		}

		void GetRawCallOverloads(ResolutionContext ctxt, IExpression callForeExpression,
			out AbstractType[] baseExpression,
			out TemplateInstanceExpression tix)
		{
			tix = null;

			if (callForeExpression is PostfixExpression_Access)
			{
				var pac = (PostfixExpression_Access)callForeExpression;
				tix = pac.AccessExpression as TemplateInstanceExpression;

				baseExpression = Evaluation.EvalPostfixAccessExpression(this, ctxt, pac, null, false, false);
			}
			else
			{
				// Explicitly don't resolve the methods' return types - it'll be done after filtering to e.g. resolve template types to the deduced one
				var optBackup = ctxt.CurrentContext.ContextDependentOptions;
				ctxt.CurrentContext.ContextDependentOptions |= ResolutionOptions.DontResolveBaseTypes;

				if (callForeExpression is TokenExpression)
					baseExpression = ExpressionTypeEvaluation.GetResolvedConstructorOverloads((TokenExpression)callForeExpression, ctxt);
				else 
				{
					tix = callForeExpression as TemplateInstanceExpression;

					if (callForeExpression is IntermediateIdType)
						baseExpression = ExpressionTypeEvaluation.GetOverloads(callForeExpression as IntermediateIdType, ctxt, null, false);
					else
						baseExpression = new[] { callForeExpression != null ? AbstractType.Get(callForeExpression.Accept(this)) : null };
				}

				ctxt.CurrentContext.ContextDependentOptions = optBackup;
			}
		}

		public static AbstractType[] GetUnfilteredMethodOverloads(IExpression foreExpression, ResolutionContext ctxt, IExpression supExpression = null)
		{
			IEnumerable<AbstractType> overloads;

			if (foreExpression is IntermediateIdType)
				overloads = GetOverloads(foreExpression as IntermediateIdType, ctxt, null, !(foreExpression is IdentifierExpression));
			else if (foreExpression is PostfixExpression_Access)
				overloads = GetAccessedOverloads(foreExpression as PostfixExpression_Access, ctxt, null, false);
			else if (foreExpression is TokenExpression)
				overloads = GetResolvedConstructorOverloads((TokenExpression)foreExpression, ctxt);
			else
				overloads = AmbiguousType.TryDissolve(EvaluateType(foreExpression, ctxt, false));

			var l = new List<AbstractType>();
			bool staticOnly = true;

			if(overloads != null)
				foreach (var amb in overloads)
					foreach(var ov in AmbiguousType.TryDissolve(amb))
						GetUnfilteredMethodOverloads_Helper(foreExpression, ctxt, supExpression, l, ref staticOnly, ov);

			return l.ToArray();
		}

		private static void GetUnfilteredMethodOverloads_Helper(IExpression foreExpression, ResolutionContext ctxt, IExpression supExpression, List<AbstractType> l, ref bool staticOnly, AbstractType ov)
		{
			var t = ov;
			if (ov is MemberSymbol)
			{
				var ms = ov as MemberSymbol;
				if (ms.Definition is Dom.DMethod)
				{
					l.Add(ms);
					return;
				}

				staticOnly = false;
				t = ms.Base;
			}

			if (t is TemplateIntermediateType)
			{
				var tit = t as TemplateIntermediateType;

				var m = TypeDeclarationResolver.HandleNodeMatches(
					GetOpCalls(tit, staticOnly), ctxt,
					null, supExpression ?? foreExpression);

				/*
				 * On structs, there must be a default () constructor all the time.
				 * If there are (other) constructors in structs, the explicit member initializer constructor is not
				 * provided anymore. This will be handled in the GetConstructors() method.
				 * If there are opCall overloads, canCreateeExplicitStructCtor overrides the ctor existence check in GetConstructors()
				 * and enforces that the explicit ctor will not be generated.
				 * An opCall overload with no parameters supersedes the default ctor.
				 */
				var canCreateExplicitStructCtor = m == null || m.Length == 0;

				if (!canCreateExplicitStructCtor)
					l.AddRange(m);

				m = TypeDeclarationResolver.HandleNodeMatches(
					GetConstructors(tit, canCreateExplicitStructCtor), ctxt,
					null, supExpression ?? foreExpression);

				if (m != null && m.Length != 0)
					l.AddRange(m);
			}
			else
				l.Add(ov);
		}

		public static AbstractType[] GetAccessedOverloads(PostfixExpression_Access acc, ResolutionContext ctxt,
			ISemantic resultBase = null, bool DeducePostfixTemplateParams = true)
		{
			return Evaluation.EvalPostfixAccessExpression<AbstractType>(new ExpressionTypeEvaluation(ctxt), ctxt, acc, resultBase, DeducePostfixTemplateParams);
		}

		public static AbstractType[] GetResolvedConstructorOverloads(TokenExpression tk, ResolutionContext ctxt)
		{
			if (tk.Token == DTokens.This || tk.Token == DTokens.Super)
			{
				var classRef = EvaluateType(tk, ctxt) as TemplateIntermediateType;

				if (classRef != null)
					return D_Parser.Resolver.TypeResolution.TypeDeclarationResolver.HandleNodeMatches(GetConstructors(classRef), ctxt, classRef, tk);
			}
			return null;
		}

		/// <summary>
		/// Returns all constructors from the given class or struct.
		/// If no explicit constructor given, an artificial implicit constructor method stub will be created.
		/// </summary>
		public static IEnumerable<DMethod> GetConstructors(TemplateIntermediateType ct, bool canCreateExplicitStructCtor = true)
		{
			bool foundExplicitCtor = false;

			// Simply get all constructors that have the ctor id assigned. Makin' it faster ;)
			var ch = ct.Definition[DMethod.ConstructorIdentifier];
			if (ch != null)
				foreach (var m in ch)
				{
					// Not to forget: 'this' aliases are also possible - so keep checking for m being a genuine ctor
					var dm = m as DMethod;
					if (dm != null && dm.SpecialType == DMethod.MethodType.Constructor)
					{
						yield return dm;
						foundExplicitCtor = true;
					}
				}

			var isStruct = ct is StructType;
			if (!foundExplicitCtor || isStruct)
			{
				// Check if there is an opCall that has no parameters.
				// Only if no exists, it's allowed to make a default parameter.
				bool canMakeDefaultCtor = true;
				foreach (var opCall in GetOpCalls(ct, true))
					if (opCall.Parameters.Count == 0)
					{
						canMakeDefaultCtor = false;
						break;
					}

				if (canMakeDefaultCtor)
					yield return new DMethod(DMethod.MethodType.Constructor) { Name = DMethod.ConstructorIdentifier, Parent = ct.Definition, Description = "Default constructor for " + ct.Name };

				// If struct, there's also a ctor that has all struct members as parameters.
				// Only, if there are no explicit ctors nor opCalls
				if (isStruct && !foundExplicitCtor && canCreateExplicitStructCtor)
				{
					var dm= new DMethod(DMethod.MethodType.Constructor)
					{
						Name = DMethod.ConstructorIdentifier,
						Parent = ct.Definition,
						Description = "Default constructor for struct " + ct.Name
					};

					foreach (var member in ct.Definition)
					{
						var dv = member as DVariable;
						if (dv != null &&
							!dv.IsStatic &&
							!dv.IsAlias &&
							!dv.IsConst) //TODO dunno if public-ness of items is required..
							dm.Parameters.Add(dv);
					}

					yield return dm;
				}
			}
		}

		public static IEnumerable<DMethod> GetOpCalls(TemplateIntermediateType t, bool staticOnly)
		{
			var opCall = t.Definition["opCall"];
			if (opCall != null)
				foreach (var call in opCall)
				{
					var dm = call as DMethod;
					if (dm != null && (!staticOnly || dm.IsStatic))
						yield return dm;
				}
		}

		#endregion

		#region Infix (op-based) expressions
		AbstractType OpExpressionType(OperatorBasedExpression x)
		{
			var t = x.LeftOperand != null ? x.LeftOperand.Accept(this) : null;

			if (t != null)
				return t;

			return x.RightOperand != null ? x.RightOperand.Accept(this) : null;
		}

		public AbstractType Visit(AssignExpression x)
		{
			return OpExpressionType(x);
		}

		public AbstractType Visit(ConditionalExpression x)
		{
			return x.TrueCaseExpression != null ? x.TrueCaseExpression.Accept(this) : (x.FalseCaseExpression != null ? x.FalseCaseExpression.Accept(this) : null);
		}

		public AbstractType Visit(OrOrExpression x)
		{
			return new PrimitiveType(DTokens.Bool);
		}

		public AbstractType Visit(AndAndExpression x)
		{
			return new PrimitiveType(DTokens.Bool);
		}

		public AbstractType Visit(XorExpression x)
		{
			return OpExpressionType(x);
		}

		public AbstractType Visit(OrExpression x)
		{
			return OpExpressionType(x);
		}

		public AbstractType Visit(AndExpression x)
		{
			return OpExpressionType(x);
		}

		public AbstractType Visit(EqualExpression x)
		{
			return new PrimitiveType(DTokens.Bool);
		}

		public AbstractType Visit(IdentityExpression x)
		{
			return new PrimitiveType(DTokens.Bool);
		}

		public AbstractType Visit(RelExpression x)
		{
			return new PrimitiveType(DTokens.Bool);
		}

		public AbstractType Visit(InExpression x)
		{
			return x.RightOperand != null ? x.RightOperand.Accept(this) : null;
		}

		public AbstractType Visit(ShiftExpression x)
		{
			return OpExpressionType(x);
		}

		public AbstractType Visit(AddExpression x)
		{
			return OpExpressionType(x);
		}

		public AbstractType Visit(MulExpression x)
		{
			return OpExpressionType(x);
		}

		public AbstractType Visit(CatExpression x)
		{
			return OpExpressionType(x);
		}

		public AbstractType Visit(PowExpression x)
		{
			return OpExpressionType(x);
		}
		#endregion

		#region Prefix (unary) experssions
		public AbstractType Visit(CastExpression ce)
		{
			AbstractType castedType;

			if (ce.Type != null)
				castedType = TypeDeclarationResolver.ResolveSingle(ce.Type, ctxt);
			else if (ce.UnaryExpression != null)
			{
				castedType = AbstractType.Get(ce.UnaryExpression.Accept(this));

				if (castedType != null && ce.CastParamTokens != null && ce.CastParamTokens.Length > 0)
				{
					//TODO: Wrap resolved type with member function attributes
				}
			}
			else
				castedType = null;

			return castedType;
		}

		public AbstractType Visit(UnaryExpression_Cat x) // ~b;
		{
			return x.UnaryExpression.Accept(this);
		}

		public AbstractType Visit(UnaryExpression_Increment x)
		{
			return x.UnaryExpression.Accept(this);
		}

		public AbstractType Visit(UnaryExpression_Decrement x)
		{
			return x.UnaryExpression.Accept(this);
		}

		public AbstractType Visit(UnaryExpression_Add x)
		{
			return x.UnaryExpression.Accept(this);
		}

		public AbstractType Visit(UnaryExpression_Sub x)
		{
			return x.UnaryExpression.Accept(this);
		}

		public AbstractType Visit(UnaryExpression_Not x)
		{
			return x.UnaryExpression.Accept(this);
		}

		public AbstractType Visit(UnaryExpression_Mul x)
		{
			return x.UnaryExpression.Accept(this);
		}

		public AbstractType Visit(UnaryExpression_And x)
		{
			return new PointerType(x.UnaryExpression.Accept(this));
		}

		public AbstractType Visit(DeleteExpression x)
		{
			return null;
		}

		public AbstractType Visit(UnaryExpression_Type x)
		{
			var uat = x as UnaryExpression_Type;

			if (uat.Type == null)
				return null;

			return TypeDeclarationResolver.ResolveSingle(new IdentifierDeclaration(uat.AccessIdentifierHash) { EndLocation = uat.EndLocation, InnerDeclaration = uat.Type }, ctxt);
		}

		public AbstractType Visit(NewExpression nex)
		{
			// http://www.d-programming-language.org/expression.html#NewExpression

			var possibleTypes = TypeDeclarationResolver.ResolveSingle(nex.Type, ctxt, !(nex.Type is IdentifierDeclaration));

			var ctors = new Dictionary<DMethod, TemplateIntermediateType>();
			
			if (possibleTypes == null)
				return null;

			foreach (var t in AmbiguousType.TryDissolve(possibleTypes))
			{
				var ct = t as TemplateIntermediateType;
				if (ct != null &&
					!ct.Definition.ContainsAttribute(DTokens.Abstract))
					foreach (var ctor in GetConstructors(ct)){
						// Omit all ctors that won't return the adequate 
						if (ct.Modifier != 0)
						{
							if (!ctor.ContainsAttribute(ct.Modifier, DTokens.Pure))
								continue;						
						}
						else if(ctor.Attributes != null && ctor.Attributes.Count != 0)
						{
							bool skip = false;
							foreach (var attr in ctor.Attributes)
							{
								var mod = attr as Modifier;
								if (mod != null)
								{
									switch (mod.Token)
									{
										case DTokens.Const:
										case DTokens.Immutable:
										case DTokens.Shared:
										case DTokens.Nothrow: // ?
										// not DTokens.Pure due to some mystical reasons
											skip = true;
											break;
									}
									
									if(skip)
										break;
								}
							}
							if (skip)
								continue;
						}
						ctors.Add(ctor, ct);
					}
				else if (t is AssocArrayType)
				{
					t.NonStaticAccess = true;
					return AmbiguousType.Get(possibleTypes);
				}
			}

			if (ctors.Count == 0)
				return new UnknownType(nex);

			// HACK: Return the base types immediately
			if (TryReturnMethodReturnType)
			{
				var ret = ctors.First().Value; // AmbiguousType.Get(ctors.Values);
				if (ret != null)
					ret.NonStaticAccess = true;
				return ret;
			}

			MemberSymbol finalCtor = null;

			//TODO: Determine argument types and filter out ctor overloads.
			var kvFirst = ctors.First();
			finalCtor = new MemberSymbol(kvFirst.Key, kvFirst.Value);



			if (finalCtor != null)
				return TryPretendMethodExecution(finalCtor, nex);

			var resolvedCtors = new List<AbstractType>();

			foreach(var kv in ctors)
				resolvedCtors.Add(new MemberSymbol(kv.Key, kv.Value));

			return TryPretendMethodExecution(AmbiguousType.Get(resolvedCtors), nex);
		}
		#endregion

		#region Postfix expressions
		AbstractType EvalForeExpression(PostfixExpression ex)
		{
			var foreExpr = ex.PostfixForeExpression != null ? ex.PostfixForeExpression.Accept(this) : null;

			if (foreExpr == null)
				ctxt.LogError(new NothingFoundError(ex.PostfixForeExpression));

			return foreExpr;
		}

		public AbstractType Visit(PostfixExpression_Access ex)
		{
			return TryPretendMethodExecution(AmbiguousType.Get(Evaluation.EvalPostfixAccessExpression(this, ctxt, ex)));
		}

		public AbstractType Visit(PostfixExpression_Increment x)
		{
			return EvalForeExpression(x);
		}

		public AbstractType Visit(PostfixExpression_Decrement x)
		{
			return EvalForeExpression(x);
		}

		public static readonly int OpIndexIdHash = "opIndex".GetHashCode();

		public AbstractType Visit(PostfixExpression_ArrayAccess x)
		{
			var foreExpression = EvalForeExpression(x);

			if (x.Arguments == null || x.Arguments.Length == 0)
				return SliceArray (x, foreExpression, null);

			for(int arg_i = 0; foreExpression != null && arg_i < x.Arguments.Length; arg_i++) {
				var arg = x.Arguments [arg_i];

				// myArray[0]; myArray[0..5];
				foreExpression = DResolver.StripMemberSymbols (foreExpression);

				if (foreExpression == null)
					break;

				if (arg is PostfixExpression_ArrayAccess.SliceArgument)
					foreExpression = SliceArray (x, foreExpression, arg as PostfixExpression_ArrayAccess.SliceArgument);
				else
					foreExpression = AccessArrayAtIndex (x, foreExpression, arg,ref arg_i);
			}

			return foreExpression;
		}

		public AbstractType AccessArrayAtIndex(PostfixExpression_ArrayAccess x, AbstractType foreExpression, PostfixExpression_ArrayAccess.IndexArgument ix,ref int arg_i)
		{
			if (foreExpression is TemplateIntermediateType)
			{
				//TODO: Wtf is this?

				//TODO: Proper resolution of alias this declarations
				var tit = foreExpression as TemplateIntermediateType;
				var ch = tit.Definition[DVariable.AliasThisIdentifierHash];
				if (ch != null)
				{
					foreach (DVariable aliasThis in ch)
					{
						foreExpression = DResolver.StripMemberSymbols(TypeDeclarationResolver.HandleNodeMatch(aliasThis, ctxt, foreExpression));
						if (foreExpression != null)
							return foreExpression; // HACK: Just omit other alias this' to have a quick run-through
					}
				}

				foreExpression = tit;
			}

			var udt = foreExpression as UserDefinedType;

			if (udt != null) {
				ctxt.CurrentContext.IntroduceTemplateParameterTypes (udt);

				//Search opIndex overloads and try to match them to the given indexing arguments.
				var overloads = TypeDeclarationResolver.ResolveFurtherTypeIdentifier (OpIndexIdHash, foreExpression, ctxt, x, false);
				if (overloads != null && overloads.Length > 0) {
					var indexArgs = new List<AbstractType> ();
					if (x.Arguments != null)
						for (int k = arg_i; k < x.Arguments.Length; k++)
							indexArgs.Add (x.Arguments [k].Expression.Accept (this)); // TODO: Treat slices properly..somehow

					overloads = TemplateInstanceHandler.DeduceParamsAndFilterOverloads (overloads, indexArgs, true, ctxt);
					ctxt.CurrentContext.RemoveParamTypesFromPreferredLocals (udt);
					foreExpression = TryPretendMethodExecution (AmbiguousType.Get (overloads), x, indexArgs.Count != 0 ? indexArgs.ToArray () : null);
					arg_i += indexArgs.Count; //TODO: Only increment by the amount of actually used args for filtering out the respective method overload.
					return foreExpression;
				} else
					ctxt.CurrentContext.RemoveParamTypesFromPreferredLocals (udt);
			}

			if (foreExpression is AssocArrayType)
			{
				var ar = foreExpression as AssocArrayType;
				/*
				 * myType_Array[0] -- returns TypeResult myType
				 * return the value type of a given array result
				 */
				//TODO: Handle opIndex overloads
				if (ar.ValueType != null)
					ar.ValueType.NonStaticAccess = true;

				return new ArrayAccessSymbol(x, ar.ValueType);
			}
			/*
			 * int* a = new int[10];
			 * 
			 * a[0] = 12;
			 */
			else if (foreExpression is PointerType)
			{
				var b = (foreExpression as PointerType).Base;
				if (b != null)
					b.NonStaticAccess = true;
				return b;
			}
			//return new ArrayAccessSymbol(x,((PointerType)foreExpression).Base);

			else if (foreExpression is DTuple)
			{
				var tt = foreExpression as DTuple;

				var idx = Evaluation.EvaluateValue(ix.Expression, ctxt) as PrimitiveValue;

				if (tt.Items == null)
				{
					ctxt.LogError(tt, "No items in Type tuple");
				}
				else if (idx == null || !DTokens.IsBasicType_Integral(idx.BaseTypeToken))
				{
					ctxt.LogError(ix.Expression, "Index expression must evaluate to integer value");
				}
				else if (idx.Value > (decimal)Int32.MaxValue ||
						 (int)idx.Value >= tt.Items.Length || idx.Value < 0m)
				{
					ctxt.LogError(ix.Expression, "Index number must be a value between 0 and " + tt.Items.Length);
				}
				else
				{
					return AbstractType.Get(tt.Items[(int)idx.Value]);
				}
			}

			ctxt.LogError(x, "No matching base type for indexing operation");
			return null;
		}

		public static readonly int OpSliceIdHash = "opSlice".GetHashCode();

		AbstractType SliceArray(PostfixExpression_ArrayAccess x, AbstractType foreExpression, PostfixExpression_ArrayAccess.SliceArgument sl)
		{
			var udt = DResolver.StripMemberSymbols(foreExpression) as UserDefinedType;

			if (udt == null)
				return foreExpression;

			// TODO: Make suitable for multi-dimensional access
			AbstractType[] sliceArgs;

			if (sl != null)
				sliceArgs = new[] { sl.LowerBoundExpression.Accept (this), sl.UpperBoundExpression.Accept (this) };
			else
				sliceArgs = null;

			ctxt.CurrentContext.IntroduceTemplateParameterTypes(udt);

			var overloads = TypeDeclarationResolver.ResolveFurtherTypeIdentifier(OpSliceIdHash, foreExpression, ctxt, x, false);

			overloads = TemplateInstanceHandler.DeduceParamsAndFilterOverloads(overloads, sliceArgs, true, ctxt);

			ctxt.CurrentContext.RemoveParamTypesFromPreferredLocals(udt);
			return TryPretendMethodExecution(AmbiguousType.Get(overloads), x, sliceArgs) ?? foreExpression;
		}
		#endregion

		#region Identifier primitives
		public AbstractType Visit(TemplateInstanceExpression tix)
		{
			return TryPretendMethodExecution(AmbiguousType.Get(GetOverloads(tix, ctxt)));
		}

		public AbstractType Visit(IdentifierExpression id)
		{
			if (id.IsIdentifier)
				return TryPretendMethodExecution(AmbiguousType.Get(GetOverloads(id, ctxt)));

			byte tt;
			switch (id.Format)
			{
				case Parser.LiteralFormat.CharLiteral:
					var tk = id.Subformat == LiteralSubformat.Utf32 ? DTokens.Dchar :
						id.Subformat == LiteralSubformat.Utf16 ? DTokens.Wchar :
						DTokens.Char;

					return new PrimitiveType(tk, 0) { NonStaticAccess = true };

				case LiteralFormat.FloatingPoint | LiteralFormat.Scalar:
					var im = id.Subformat.HasFlag(LiteralSubformat.Imaginary);

					tt = im ? DTokens.Idouble : DTokens.Double;

					if (id.Subformat.HasFlag(LiteralSubformat.Float))
						tt = im ? DTokens.Ifloat : DTokens.Float;
					else if (id.Subformat.HasFlag(LiteralSubformat.Real))
						tt = im ? DTokens.Ireal : DTokens.Real;

					return new PrimitiveType(tt, 0) { NonStaticAccess = true };

				case LiteralFormat.Scalar:
					var unsigned = id.Subformat.HasFlag(LiteralSubformat.Unsigned);

					if (id.Subformat.HasFlag(LiteralSubformat.Long))
						tt = unsigned ? DTokens.Ulong : DTokens.Long;
					else
						tt = unsigned ? DTokens.Uint : DTokens.Int;

					return new PrimitiveType(tt, 0) { NonStaticAccess = true };

				case Parser.LiteralFormat.StringLiteral:
				case Parser.LiteralFormat.VerbatimStringLiteral:
					var str = GetStringType(id.Subformat);
					str.NonStaticAccess = true;
					return str;
				default:
					return null;
			}
		}

		/// <summary>
		/// Resolves an identifier and returns the definition + its base type.
		/// Does not deduce any template parameters or nor filters out unfitting template specifications!
		/// </summary>
		static AbstractType[] ResolveIdentifier(int idHash, ResolutionContext ctxt, ISyntaxRegion idObject, bool ModuleScope = false)
		{
			var loc = idObject is ISyntaxRegion ? ((ISyntaxRegion)idObject).Location : ctxt.CurrentContext.Caret;

			IDisposable disp = null;

			if (ModuleScope)
				disp = ctxt.Push(ctxt.ScopedBlock.NodeRoot as DModule, true);

			// If there are symbols that must be preferred, take them instead of scanning the ast
			else
			{
				TemplateParameterSymbol dedTemplateParam;
				if (ctxt.GetTemplateParam(idHash, out dedTemplateParam))
					return new[] { dedTemplateParam };
			}

			List<AbstractType> res;

			//var t = ctxt.Cache.TryGetType(idObject);
			bool hasBaseValue = true;
			/*if (t != null)
			{
				res = new List<AbstractType>(t.Length);
				foreach (var t_ in t)
				{
					if(hasBaseValue)
						foreach(var t__ in AmbiguousType.TryDissolve(t_))
							if(!(hasBaseValue = (t__ is DerivedDataType) && (t__ as DerivedDataType).Base != null))
								break;
					res.Add(t_);
				}
			}*/

			if (hasBaseValue || (ctxt.Options & ResolutionOptions.DontResolveBaseClasses | ResolutionOptions.DontResolveBaseTypes) != 0) {
				res = NameScan.SearchAndResolve (ctxt, loc, idHash, idObject);
			} else
				res = null;

			if (disp != null)
			{
				disp.Dispose();
				disp = null;
			}

			if (res.Count != 0)
			{
				if (idObject is IdentifierExpression || idObject is IdentifierDeclaration)
					for (var i = res.Count; i > 0; )
						res[--i] = TypeDeclarationResolver.TryPostDeduceAliasDefinition(res[i], idObject, ctxt);
				return res.ToArray();
			}

			// Support some very basic static typing if no phobos is given atm
			if (idHash == Evaluation.stringTypeHash)
				res.Add(Evaluation.GetStringType (ctxt));
			else if(idHash == Evaluation.wstringTypeHash)
				res.Add(Evaluation.GetStringType (ctxt, LiteralSubformat.Utf16));
			else if(idHash == Evaluation.dstringTypeHash)
				res.Add(Evaluation.GetStringType (ctxt, LiteralSubformat.Utf32));

			return res.ToArray();
		}


		/// <summary>
		/// Resolves id and optionally filters out overloads by template deduction.
		/// </summary>
		public static AbstractType[] GetOverloads(IntermediateIdType id, ResolutionContext ctxt, AbstractType resultBases = null, bool deduceParameters = true)
		{
			#if TRACE
			Trace.WriteLine (string.Format("GetOverloads({0}):", id));
			Trace.Indent ();
			var sw = new Stopwatch ();
			#endif

			if (resultBases == null && id is ITypeDeclaration && (id as ITypeDeclaration).InnerDeclaration != null) {
				#if TRACE 
				Trace.WriteLine(string.Format("Resolve base type {0}", (id as ITypeDeclaration).InnerDeclaration));
				Trace.Indent();
				sw.Restart();
				#endif

				resultBases = TypeDeclarationResolver.ResolveSingle ((id as ITypeDeclaration).InnerDeclaration, ctxt);

				#if TRACE
				sw.Stop();
				Trace.Unindent();
				Trace.WriteLine(string.Format("Finished resolving base type {0} => {1}. {2} ms.", (id as ITypeDeclaration).InnerDeclaration, resultBases, sw.ElapsedMilliseconds));
				#endif
			}

			#if TRACE
			Trace.WriteLine (string.Format("Getting raw overloads of {0}", id));
			Trace.Indent();
			sw.Restart ();
			#endif

			AbstractType[] res;
			if (resultBases == null)
				res = ResolveIdentifier(id.IdHash, ctxt, id, id.ModuleScoped);
			else
				res = TypeDeclarationResolver.ResolveFurtherTypeIdentifier(id.IdHash, resultBases, ctxt, id);

			#if TRACE
			sw.Stop ();
			Trace.Unindent();
			Trace.WriteLine (string.Format("Finished getting raw overloads of {0}. {1}ms", id, sw.ElapsedMilliseconds));
			Trace.WriteLine("Deducing.");
			sw.Restart();
			#endif

			var f = DResolver.FilterOutByResultPriority(ctxt, res);

			if (f.Count > 0)
			{
				if ((ctxt.Options & ResolutionOptions.NoTemplateParameterDeduction) != 0 || !deduceParameters)
					res = f.ToArray();
				else if(id is TemplateInstanceExpression)
					res = TemplateInstanceHandler.DeduceParamsAndFilterOverloads(f, id as TemplateInstanceExpression, ctxt);
				else
					res = TemplateInstanceHandler.DeduceParamsAndFilterOverloads (f, null, false, ctxt);
			}

			#if TRACE
			sw.Stop();
			Trace.WriteLine(string.Format("Finished deduction. {0} ms. {1}", sw.ElapsedMilliseconds, AmbiguousType.Get(res)));
			Trace.Unindent();
			#endif

			return res;
		}
#endregion

		#region Primitive expressions
		public AbstractType Visit(Expression ex)
		{
			return ex.Expressions.Count == 0 ? null : ex.Expressions[ex.Expressions.Count - 1].Accept(this);
		}

		public AbstractType Visit(AnonymousClassExpression x)
		{
			return TypeDeclarationResolver.HandleNodeMatch(x.AnonymousClass, ctxt, typeBase: x);
		}

		public AbstractType Visit(TokenExpression x)
		{
			switch (x.Token)
			{
				// References current class scope
				case DTokens.This:
					var classDef = ctxt.ScopedBlock;

					while (!(classDef is DClassLike) && classDef != null)
						classDef = classDef.Parent as IBlockNode;

					if (classDef is DClassLike)
					{
						var res = TypeDeclarationResolver.HandleNodeMatch(classDef, ctxt, null, x);
						res.NonStaticAccess = true;
						return res;
					}

					//TODO: Throw
					return null;


				case DTokens.Super:
					// References super type of currently scoped class declaration

					classDef = ctxt.ScopedBlock;

					while (!(classDef is DClassLike) && classDef != null)
						classDef = classDef.Parent as IBlockNode;

					if (classDef is DClassLike)
					{
						var tr = DResolver.ResolveClassOrInterface(classDef as DClassLike, ctxt, x, true);

						if (tr.Base != null)
						{
							// Important: Overwrite type decl base with 'super' token
							tr.Base.NonStaticAccess = true;
							return tr.Base;
						}
					}

					//TODO: Throw
					return null;

				case DTokens.Null:
					return null;

				case DTokens.Dollar:
					return new PrimitiveType(DTokens.Int); // Really integer or every kind of iterator type?

				case DTokens.False:
				case DTokens.True:
					return new PrimitiveType(DTokens.Bool);

				case DTokens.__FILE__:
					return GetStringType();
				case DTokens.__LINE__:
					return new PrimitiveType(DTokens.Int);
				case DTokens.__MODULE__:
					return GetStringType();
				case DTokens.__FUNCTION__:
				//TODO
					return null;
				case DTokens.__PRETTY_FUNCTION__:
					return GetStringType();
				default:
					return null;
			}
		}

		public AbstractType Visit(TypeDeclarationExpression x)
		{
			// should be containing a typeof() only; static properties etc. are parsed as access expressions
			return TypeDeclarationResolver.ResolveSingle(x.Declaration, ctxt);
		}

		public AbstractType Visit(ArrayLiteralExpression arr)
		{
			if (arr.Elements != null && arr.Elements.Count > 0)
			{
				// Simply resolve the first element's type and take it as the array's value type
				var valueType = arr.Elements[0] != null ? AbstractType.Get(arr.Elements[0].Accept(this)) : null;

				return new ArrayType(valueType);
			}

			ctxt.LogError(arr, "Array literal must contain at least one element.");
			return null;
		}

		public AbstractType Visit(AssocArrayExpression aa)
		{
			if (aa.Elements != null && aa.Elements.Count > 0)
			{
				var firstElement = aa.Elements[0].Key;
				var firstElementValue = aa.Elements[0].Value;

				var keyType = firstElement != null ? AbstractType.Get(firstElement.Accept(this)) : null;
				var valueType = firstElementValue != null ? AbstractType.Get(firstElementValue.Accept(this)) : null;

				return new AssocArrayType(valueType, keyType);
			}

			return null;
		}

		public AbstractType Visit(FunctionLiteral x)
		{
			return new DelegateType(
				(ctxt.Options & ResolutionOptions.DontResolveBaseTypes | ResolutionOptions.ReturnMethodReferencesOnly) != 0 ? null : TypeDeclarationResolver.GetMethodReturnType(x.AnonymousMethod, ctxt),
				x,
				TypeResolution.TypeDeclarationResolver.HandleNodeMatches(x.AnonymousMethod.Parameters, ctxt));
		}

		public AbstractType Visit(AssertExpression x)
		{
			return new PrimitiveType(DTokens.Void, 0);
		}

		public AbstractType Visit(MixinExpression x)
		{
			var s = Evaluation.EvaluateMixinExpressionContent(ctxt, x);

			if (s == null)
			{
				EvalError(new InvalidStringException(x));
				return null;
			}

			// Parse it as an expression
			var ex = DParser.ParseAssignExpression(s);

			if (ex == null)
			{
				EvalError(new EvaluationException(x, "Invalid expression code given"));
				return null;
			}
			//TODO: Excessive caching
			// Evaluate the expression's type/value
			return ex.Accept(this);
		}

		public AbstractType Visit(ImportExpression x)
		{
			return Evaluation.GetStringType(ctxt);
		}

		public AbstractType Visit(TypeidExpression x)
		{
			//TODO: Split up into more detailed typeinfo objects (e.g. for arrays, pointers, classes etc.)

			return TypeDeclarationResolver.ResolveSingle(new IdentifierDeclaration("TypeInfo") { InnerDeclaration = new IdentifierDeclaration("object") }, ctxt);
		}

		public AbstractType Visit(IsExpression x)
		{
			return new PrimitiveType(DTokens.Bool);
		}

		public AbstractType Visit(SurroundingParenthesesExpression x)
		{
			return x.Expression.Accept(this);
		}

		public AbstractType Visit(VoidInitializer x)
		{
			return new PrimitiveType(DTokens.Void);
		}

		public AbstractType Visit(ArrayInitializer x)
		{
			return Visit((AssocArrayExpression)x);
		}

		public AbstractType Visit(StructInitializer x)
		{
			// TODO: Create struct node with initialized members etc.
			return null;
		}

		public AbstractType Visit(StructMemberInitializer structMemberInitializer)
		{
			//TODO
			return null;
		}
		#endregion

		#region Traits
		public AbstractType Visit(TraitsExpression te)
		{
			PostfixExpression_Access pfa;
			AbstractType t;
			ResolutionOptions optionsBackup;

			switch (te.Keyword)
			{
				case "":
				case null:
					return null;

				case "identifier":
					return GetStringType();

				case "getMember":
					pfa = prepareMemberTraitExpression(te, out t);

					if (pfa == null || t == null)
						break;

					var vs = Evaluation.EvalPostfixAccessExpression(this, ctxt, pfa, t);
					if (vs == null || vs.Length == 0)
						return null;
					return vs[0];


				case "getOverloads":
					optionsBackup = ctxt.ContextIndependentOptions;
					ctxt.ContextIndependentOptions = ResolutionOptions.IgnoreAllProtectionAttributes;

					pfa = prepareMemberTraitExpression(te, out t);

					if (pfa != null && t != null)
						vs = Evaluation.EvalPostfixAccessExpression(this, ctxt, pfa, t);
					else
						vs = null;

					ctxt.ContextIndependentOptions = optionsBackup;

					return new DTuple(vs);


				case "getProtection":
					return GetStringType();

				case "getVirtualFunctions":
					break;
				case "getVirtualMethods":
					break;
				case "parent":
					break;
				case "classInstanceSize":
					break;
				case "allMembers":
					break;
				case "derivedMembers":
					break;

				case "compiles":
						return new PrimitiveType(DTokens.Bool);
			}

			if (te.Keyword.StartsWith("is") || te.Keyword.StartsWith("has"))
				return new PrimitiveType(DTokens.Bool);

			return null;
		}

		PostfixExpression_Access prepareMemberTraitExpression(TraitsExpression te, out AbstractType t)
		{
			return prepareMemberTraitExpression(ctxt, te, out t);
		}

		/// <summary>
		/// Used when evaluating traits.
		/// Evaluates the first argument to <param name="t">t</param>, 
		/// takes the second traits argument, tries to evaluate it to a string, and puts it + the first arg into an postfix_access expression
		/// </summary>
		internal static PostfixExpression_Access prepareMemberTraitExpression(ResolutionContext ctxt, TraitsExpression te, out AbstractType t, AbstractSymbolValueProvider vp = null)
		{
			if (te.Arguments != null && te.Arguments.Length == 2)
			{
				var tEx = te.Arguments[0];
				t = DResolver.StripMemberSymbols(ResolveTraitArgument(ctxt, tEx));

				if (t == null)
					ctxt.LogError(te, "First argument didn't resolve to a type");
				else if (te.Arguments[1].AssignExpression != null)
				{
					var litEx = te.Arguments[1].AssignExpression;
					var v = vp != null ? Evaluation.EvaluateValue(litEx, vp) : Evaluation.EvaluateValue(litEx, ctxt);
					
					if (v is ArrayValue && (v as ArrayValue).IsString)
					{
						var av = v as ArrayValue;

						// Mock up a postfix_access expression to ensure static properties & ufcs methods are checked either
						return new PostfixExpression_Access
						{
							PostfixForeExpression = tEx.AssignExpression ?? new TypeDeclarationExpression(tEx.Type),
							AccessExpression = new IdentifierExpression(av.StringValue)
							{
								Location = litEx.Location,
								EndLocation = litEx.EndLocation
							},
							EndLocation = litEx.EndLocation
						};
					}
					else
						ctxt.LogError(litEx, "Second traits argument must evaluate to a string literal");
				}
				else
					ctxt.LogError(te, "Second traits argument must be an expression");
			}

			t = null;
			return null;
		}

		public static AbstractType ResolveTraitArgument(ResolutionContext ctxt, TraitsArgument arg)
		{
			if (arg.Type != null)
				return TypeDeclarationResolver.ResolveSingle(arg.Type, ctxt);
			else if (arg.AssignExpression != null)
				return EvaluateType(arg.AssignExpression, ctxt);
			else
				return null;
		}
		#endregion

		public AbstractType Visit(AsmRegisterExpression x)
		{
			// TODO
			return null;
		}

		public AbstractType Visit(UnaryExpression_SegmentBase x)
		{
			return x.UnaryExpression.Accept(this);
		}
	}
}
