using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Resolver;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Resolver.ASTScanner;

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

		DNode[] nodeStore;
		AbstractType[] resolvedTypes;
		public AbstractType[] ResolvedTypesOrMethods
		{
			get {
				return resolvedTypes;
			}
			set {
				resolvedTypes = value;
				// Avoid the loss of weakly referenced DSymbol definitions.
				var l = new List<DNode>();
				if (value != null)
					foreach (var t in value)
					{
						var ds = t as DSymbol;
						if(ds != null)
							l.Add(ds.Definition);
					}
				nodeStore = l.ToArray();
			}
		}
		public DNode[] ResolvedNodes
		{
			get { return nodeStore; }
		}

		public readonly Dictionary<IExpression, AbstractType> TemplateArguments = new Dictionary<IExpression, AbstractType>();
		/// <summary>
		/// Stores the already typed arguments (Expressions) + their resolved types.
		/// The value part will be null if nothing could get returned.
		/// </summary>
		public readonly Dictionary<IExpression, AbstractType> Arguments = new Dictionary<IExpression, AbstractType>();

		/// <summary>
		///	Identifies the currently called method overload. Is an index related to <see cref="ArgumentsResolutionResult.ResolvedTypesOrMethods"/>
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

	public class ParameterInsightResolution : ExpressionVisitor
	{
		public readonly ArgumentsResolutionResult res;
		public readonly IEditorData Editor;
		public readonly ResolutionContext ctxt;
		public readonly IBlockNode curScope;

		private ParameterInsightResolution(IEditorData ed, ResolutionContext c, ArgumentsResolutionResult r, IBlockNode cs) {
			Editor = ed;
			ctxt = c;
			res = r;
			curScope = cs;
		}

		/// <summary>
		/// Reparses the given method's fucntion body until the cursor position,
		/// searches the last occurring method call or template instantiation,
		/// counts its already typed arguments
		/// and returns a wrapper containing all the information.
		/// </summary>
		public static ArgumentsResolutionResult ResolveArgumentContext(IEditorData Editor)
		{
			IBlockNode curBlock = null;
			bool inNonCode;
			var sr = CodeCompletion.FindCurrentCaretContext(Editor, ref curBlock, out inNonCode);

			IExpression lastParamExpression = null;

			var paramInsightVis = new ParamInsightVisitor ();
			if (sr is INode)
				(sr as INode).Accept (paramInsightVis);
			else if (sr is IStatement)
				(sr as IStatement).Accept (paramInsightVis);
			else if (sr is IExpression)
				(sr as IExpression).Accept (paramInsightVis);

			lastParamExpression = paramInsightVis.LastCallExpression;

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

			var ctxt = ResolutionContext.Create(Editor, false);				

			CodeCompletion.DoTimeoutableCompletionTask(null, ctxt, () =>
			{
				ctxt.Push(Editor);

				ctxt.CurrentContext.ContextDependentOptions |= ResolutionOptions.DontResolveAliases;

				if (lastParamExpression != null)
					lastParamExpression.Accept(new ParameterInsightResolution(Editor, ctxt, res, curBlock));				
			});

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

			return res;
		}

		class CtorScan : NameScan
		{
			public CtorScan(ISyntaxRegion sr, ResolutionContext ctxt)
				: base(ctxt, DMethod.ConstructorIdentifierHash, sr)
			{

			}

			public static bool ScanForConstructors(NewExpression sr, IBlockNode scope, UserDefinedType udt, List<AbstractType> _ctors, out bool explicitCtorFound)
			{
				explicitCtorFound = false;
				var ct = new CtorScan(sr, new ResolutionContext(new Misc.ParseCacheView(new RootPackage[] {}), null, scope));
				ct.DeepScanClass(udt, new ItemCheckParameters(MemberFilter.Methods), false);

				_ctors.AddRange(ct.matches_types);

				var rawList = (udt.Definition as DClassLike)[DMethod.ConstructorIdentifierHash];
				if(rawList != null)
				{
					foreach(var n in rawList)
					{
						var dm = n as DMethod;
						if(dm == null || dm.IsStatic || dm.SpecialType != DMethod.MethodType.Constructor)
							continue;

						explicitCtorFound = true;
						break;
					}
				}

				return ct.matches_types.Count != 0;
			}

			protected override bool PreCheckItem (INode n)
			{
				var dm = n as DMethod;
				return dm != null && !dm.IsStatic && dm.SpecialType == DMethod.MethodType.Constructor && base.PreCheckItem(n);
			}
		}

		private static void HandleNewExpression_Ctor(NewExpression nex, IBlockNode curBlock, List<AbstractType> _ctors, AbstractType t)
		{
			var udt = t as TemplateIntermediateType;
			if (udt is ClassType || udt is StructType)
			{
				bool explicitCtorFound;
				
				if (!CtorScan.ScanForConstructors(nex, curBlock, udt, _ctors, out explicitCtorFound))
				{
					if (explicitCtorFound)
					{
						// TODO: Somehow inform the user that the current class can't be instantiated
					}
					else
					{
						// Introduce default constructor
						_ctors.Add(new MemberSymbol(new DMethod(DMethod.MethodType.Constructor)
						{
							Description = "Default constructor for " + udt.Name,
							Parent = udt.Definition
						}, udt));
					}
				}
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

		public void Visit(NewExpression nex)
		{
			res.MethodIdentifier = nex;
			CalculateCurrentArgument(nex, res, Editor.CaretLocation, ctxt);

			var type = TypeDeclarationResolver.ResolveSingle(nex.Type, ctxt);

			var _ctors = new List<AbstractType>();

			if (type is AmbiguousType)
				foreach (var t in (type as AmbiguousType).Overloads)
					HandleNewExpression_Ctor(nex, curScope, _ctors, t);
			else
				HandleNewExpression_Ctor(nex, curScope, _ctors, type);

			res.ResolvedTypesOrMethods = _ctors.ToArray();
		}

		public void Visit(PostfixExpression_MethodCall call)
		{
			res.IsMethodArguments = true;

			res.MethodIdentifier = call.PostfixForeExpression;
			res.ResolvedTypesOrMethods = ExpressionTypeEvaluation.GetUnfilteredMethodOverloads(call.PostfixForeExpression, ctxt, call);

			if (call.Arguments != null)
				res.CurrentlyTypedArgumentIndex = call.ArgumentCount;
		}

		public void Visit(PostfixExpression_ArrayAccess x)
		{//TODO for Slices: Omit opIndex overloads if it's obvious that we don't want them -- a[1.. |
			if (x.Arguments != null)
				res.CurrentlyTypedArgumentIndex = x.Arguments.Length;

			res.IsMethodArguments = true;
			res.ParsedExpression = x;

			var overloads = new List<AbstractType>();

			if (x.PostfixForeExpression == null)
				return;

			var b = ExpressionTypeEvaluation.EvaluateType(x.PostfixForeExpression, ctxt);

			var ov = TypeDeclarationResolver.ResolveFurtherTypeIdentifier(ExpressionTypeEvaluation.OpSliceIdHash, b, ctxt, x, false);
			if (ov != null)
				overloads.AddRange(ov);

			ov = TypeDeclarationResolver.ResolveFurtherTypeIdentifier(ExpressionTypeEvaluation.OpIndexIdHash, b, ctxt, x, false);
			if (ov != null)
				overloads.AddRange(ov);

			if (overloads.Count == 0)
			{
				b = DResolver.StripMemberSymbols(b);
				var toTypeDecl = new DTypeToTypeDeclVisitor();
				var aa = b as AssocArrayType;
				if (aa != null){
					var retType = aa.ValueType != null ? aa.ValueType.Accept(toTypeDecl) : null;
					var dm = new DMethod { 
						Name = "opIndex",
						Type = retType
					};
					dm.Parameters.Add(new DVariable { 
						Name = "index",
						Type = aa.KeyType != null ? aa.KeyType.Accept(toTypeDecl) : null 
					});
					overloads.Add(new MemberSymbol(dm, aa.ValueType));

					if ((aa is ArrayType) && !(aa as ArrayType).IsStaticArray)
					{
						dm = new DMethod
						{
							Name = "opSlice",
							Type = retType
						};
						overloads.Add(new MemberSymbol(dm, aa.ValueType));
					}
				}
				else if (b is PointerType)
				{
					b = (b as PointerType).Base;
					var dm = new DMethod
					{
						Name = "opIndex",
						Type = b != null ? b.Accept(toTypeDecl) : null
					};
					dm.Parameters.Add(new DVariable
					{
						Name = "index",
						Type = new IdentifierDeclaration("size_t")
					});
					overloads.Add(new MemberSymbol(dm, b));
				}
			}

			res.ResolvedTypesOrMethods = overloads.ToArray();
		}

		public void Visit(TemplateInstanceExpression tix)
		{
			res.IsTemplateInstanceArguments = true;

			res.MethodIdentifier = tix;
			ctxt.ContextIndependentOptions = ResolutionOptions.NoTemplateParameterDeduction;
			res.ResolvedTypesOrMethods = ExpressionTypeEvaluation.GetOverloads(tix, ctxt, null, false);

			if (tix.Arguments != null)
				res.CurrentlyTypedArgumentIndex = tix.Arguments.Length;
			else
				res.CurrentlyTypedArgumentIndex = 0;
		}

		#region unused
		public void Visit(Expression x)
		{
			
		}

		public void Visit(AssignExpression x)
		{
			
		}

		public void Visit(ConditionalExpression x)
		{
			
		}

		public void Visit(OrOrExpression x)
		{
			
		}

		public void Visit(AndAndExpression x)
		{
			
		}

		public void Visit(XorExpression x)
		{
			
		}

		public void Visit(OrExpression x)
		{
			
		}

		public void Visit(AndExpression x)
		{
			
		}

		public void Visit(EqualExpression x)
		{
			
		}

		public void Visit(IdentityExpression x)
		{
			
		}

		public void Visit(RelExpression x)
		{
			
		}

		public void Visit(InExpression x)
		{
			
		}

		public void Visit(ShiftExpression x)
		{
			
		}

		public void Visit(AddExpression x)
		{
			
		}

		public void Visit(MulExpression x)
		{
			
		}

		public void Visit(CatExpression x)
		{
			
		}

		public void Visit(PowExpression x)
		{
			
		}

		public void Visit(UnaryExpression_And x)
		{
			
		}

		public void Visit(UnaryExpression_Increment x)
		{
			
		}

		public void Visit(UnaryExpression_Decrement x)
		{
			
		}

		public void Visit(UnaryExpression_Mul x)
		{
			
		}

		public void Visit(UnaryExpression_Add x)
		{
			
		}

		public void Visit(UnaryExpression_Sub x)
		{
			
		}

		public void Visit(UnaryExpression_Not x)
		{
			
		}

		public void Visit(UnaryExpression_Cat x)
		{
			
		}

		public void Visit(UnaryExpression_Type x)
		{
			
		}

		public void Visit(AnonymousClassExpression x)
		{
			
		}

		public void Visit(DeleteExpression x)
		{
			
		}

		public void Visit(CastExpression x)
		{
			
		}

		public void Visit(PostfixExpression_Access x)
		{
			
		}

		public void Visit(PostfixExpression_Increment x)
		{
			
		}

		public void Visit(PostfixExpression_Decrement x)
		{
			
		}

		public void Visit(IdentifierExpression x)
		{
			
		}

		public void Visit(TokenExpression x)
		{
			
		}

		public void Visit(TypeDeclarationExpression x)
		{
			
		}

		public void Visit(ArrayLiteralExpression x)
		{
			
		}

		public void Visit(AssocArrayExpression x)
		{
			
		}

		public void Visit(FunctionLiteral x)
		{
			
		}

		public void Visit(AssertExpression x)
		{
			
		}

		public void Visit(MixinExpression x)
		{
			
		}

		public void Visit(ImportExpression x)
		{
			
		}

		public void Visit(TypeidExpression x)
		{
			
		}

		public void Visit(IsExpression x)
		{
			
		}

		public void Visit(TraitsExpression x)
		{
			
		}

		public void Visit(SurroundingParenthesesExpression x)
		{
			
		}

		public void Visit(VoidInitializer x)
		{
			
		}

		public void Visit(ArrayInitializer x)
		{
			
		}

		public void Visit(StructInitializer x)
		{
			
		}

		public void Visit(StructMemberInitializer structMemberInitializer)
		{
			
		}

		public void Visit(AsmRegisterExpression x)
		{
			
		}

		public void Visit(UnaryExpression_SegmentBase x)
		{

		}
		#endregion
	}
}
