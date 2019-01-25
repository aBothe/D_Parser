using D_Parser.Resolver;
using System;
using System.Collections.Generic;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Resolver.ExpressionSemantics;

namespace D_Parser.Dom.Visitors
{
	public class AstElementHashingVisitor : DVisitor<long>, IResolvedTypeVisitor<long>, ISymbolValueVisitor<long>
	{
		#region Properties
		const long prime = 31;
		[ThreadStatic]
		static AstElementHashingVisitor instance;
		public static AstElementHashingVisitor Instance
		{
			get{
				if (instance == null)
					instance = new AstElementHashingVisitor ();

				return instance;
			}
		}

		readonly List<AbstractType> AbstractTypesBeingVisited = new List<AbstractType>();
		#endregion

		#region Hashing fundamentals
		static void HashEnum<T>(ref long h, long prime, IEnumerable<T> l, Func<T, long> visPred, bool mindOrder = false)
		{
			long len = 0;
			if (l != null) {
				foreach (var i in l) {
					if (mindOrder)
						Hash (ref h, prime, len);
					Hash (ref h, prime, i != null ? visPred(i) : 0);
					len++;
				}
			}
			Hash (ref h, prime, len);
		}

		void HashEnum(ref long h, long prime, IEnumerable<IExpression> l, bool mindOrder = false)		{			HashEnum (ref h, prime, l, (o) => o.Accept(Instance), mindOrder);	}
		void HashEnum(ref long h, long prime, IEnumerable<INode> l, bool mindOrder = false)				{			HashEnum (ref h, prime, l, (o) => o.Accept(Instance), mindOrder);	}
		void HashEnum(ref long h, long prime, IEnumerable<DAttribute> l, bool mindOrder = false)		{			HashEnum (ref h, prime, l, (o) => o.Accept(Instance), mindOrder);	}
		void HashEnum(ref long h, long prime, IEnumerable<IStatement> l, bool mindOrder = false)		{			HashEnum (ref h, prime, l, (o) => o.Accept(Instance), mindOrder);	}
		void HashEnum(ref long h, long prime, IEnumerable<TemplateParameter> l, bool mindOrder = false)	{			HashEnum (ref h, prime, l, (o) => o.Accept(Instance), mindOrder);	}
		void HashEnum(ref long h, long prime, IEnumerable<ITypeDeclaration> l, bool mindOrder = false)	{			HashEnum (ref h, prime, l, (o) => o.Accept(Instance), mindOrder);	}

		void HashEnum(ref long h, long prime, IEnumerable<AbstractType> l, bool mindOrder = false)	{	HashEnum (ref h, prime, l, (o) => o.Accept(Instance), mindOrder);	}
		void HashEnum(ref long h, long prime, IEnumerable<ISymbolValue> l, bool mindOrder = false)	{	HashEnum (ref h, prime, l, (o) => o.Accept(Instance), mindOrder);	}

		static void Hash(ref long h, long prime, long v)	{	h = unchecked(prime * h + v);	}

		static 
		void Hash(ref long h, long prime, bool o)			{		Hash (ref h, prime, o ? 3L : 0);						}
		static
		void Hash(ref long h, long prime, object o)			{		Hash (ref h, prime, (o != null ? o.GetHashCode() : 0));	}
		void Hash(ref long h, long prime, TemplateParameter o)	{	Hash (ref h, prime, (o != null ? o.Accept(this) : 0));	}
		void Hash(ref long h, long prime, IStatement o)		{		Hash (ref h, prime, (o != null ? o.Accept(this) : 0));	}
		void Hash(ref long h, long prime, INode o)			{		Hash (ref h, prime, (o != null ? o.Accept(this) : 0));	}
		void Hash(ref long h, long prime, ITypeDeclaration o){		Hash (ref h, prime, (o != null ? o.Accept(this) : 0));	}
		void Hash(ref long h, long prime, IExpression o)	{		Hash (ref h, prime, (o != null ? o.Accept(this) : 0));	}
		void Hash(ref long h, long prime, ISymbolValue o)	{		Hash (ref h, prime, (o != null ? o.Accept(this) : 0));	}
		void Hash(ref long h, long prime, AbstractType o)	{
			if (o == null || AbstractTypesBeingVisited.Contains(o)) {
				Hash (ref h, prime, 0);
				return;
			}

			AbstractTypesBeingVisited.Add (o);
			Hash (ref h, prime, o.Accept(this));
			AbstractTypesBeingVisited.Remove (o);
		}
		#endregion

		#region Nodes
		long VisitAbstractNode(AbstractNode n)
		{
			long h = 1;

			// h = prime * h + n.Location.GetHashCode (); // Ignore physical occurrence
			Hash (ref h, prime, n.Type);
			Hash (ref h, prime, n.NameHash);
			Hash (ref h, prime, DNode.GetNodePath (n, false).GetHashCode ());

			return h;
		}

		long VisitDNode(DNode n)
		{
			long h = VisitAbstractNode(n);

			HashEnum (ref h, prime, n.TemplateParameters);
			Hash (ref h, prime, n.TemplateConstraint);
			HashEnum (ref h, prime, n.Attributes);

			return h;
		}				

		public long Visit(DEnumValue n)
		{
			return unchecked(100003 * VisitDVariable(n as DVariable));
		}

		public long VisitDVariable(DVariable n)
		{
			long h = 100019 * VisitDNode (n);

			Hash (ref h, prime, n.Initializer);
			Hash (ref h, prime, n.IsAlias);
			Hash (ref h, prime, n.IsAliasThis);

			return h;
		}

		public long Visit(DMethod n)
		{
			long h = VisitDNode (n);
			const long prime = 1000037;

			Hash (ref h, prime, (long)n.SpecialType);
			HashEnum (ref h, prime, n.Parameters);

			// Don't care about its definition yet

			return h;
		}

		long VisitBlockNode(DBlockNode n)
		{
			var h = VisitDNode (n);
			const long prime = 1000117;

			HashEnum (ref h, prime, n.Children);
			HashEnum (ref h, prime, n.StaticStatements);

			return h;
		}

		public long Visit(DClassLike n)
		{
			var h = VisitBlockNode (n);
			const long prime = 1000039;

			Hash(ref h, prime, n.IsAnonymousClass);
			HashEnum (ref h, prime, n.BaseClasses);
			Hash (ref h, prime, n.ClassType);

			return h;
		}

		public long Visit(DEnum n)
		{
			return unchecked(1000081 * VisitBlockNode(n));
		}

		public long Visit(DModule n)
		{
			var h = VisitBlockNode (n);
			const long prime = 1000099;

			Hash (ref h, prime, n.ModuleName);
			Hash (ref h, prime, n.FileName);
			Hash (ref h, prime, n.OptionalModuleStatement);

			return h;
		}

		public long Visit(DBlockNode n)
		{
			return VisitBlockNode(n);
		}

		public long Visit(TemplateParameter.Node n)
		{
			// 1000121
			return n.TemplateParameter.Accept(this);
		}

		public long Visit(NamedTemplateMixinNode n)
		{
			return unchecked(1000133 * n.Mixin.Accept(this));
		}

		public long Visit(EponymousTemplate ep)
		{
			return unchecked(1000231 * VisitDVariable((DVariable)ep));
		}

		public long Visit(ModuleAliasNode n)
		{
			long h = Visit ((ImportSymbolNode)n);
			const long prime = 1000249;

			Hash(ref h, prime, n.Import != null ? n.Import.Accept (this) : 0);

			return h;
		}

		public long Visit(ImportSymbolNode n)
		{
			long h = VisitDVariable ((DVariable)n);
			const long prime = 1000253;

			Hash(ref h, prime, n.ImportStatement != null ? n.ImportStatement.Accept (this) : 0);

			return h;
		}

		public long Visit(ImportSymbolAlias n)
		{
			long h = Visit ((ImportSymbolNode)n);
			const long prime = 1000273;

			Hash(ref h, prime, n.ImportBinding != null ? n.ImportBinding.Accept (this) : 0);

			return h;
		}
		#endregion

		#region Attributes
		public long VisitAttribute(Modifier attr)
		{
			const long prime = 1000151;
			long h = 1;

			Hash (ref h, prime, attr.Token);
			Hash (ref h, prime, attr.LiteralContent);

			return h;
		}

		public long VisitAttribute(DeprecatedAttribute a)
		{
			return 1000159;
		}

		public long VisitAttribute(PragmaAttribute attr)
		{
			return unchecked(1000171 * VisitAttribute((Modifier)attr));
		}

		public long VisitAttribute(BuiltInAtAttribute a)
		{
			return 1000183 * (long)a.Kind;
		}

		public long VisitAttribute(UserDeclarationAttribute a)
		{
			const long prime = 1000187;
			long r = 1;

			HashEnum (ref r, prime, a.AttributeExpression);

			return r;
		}

		public long VisitAttribute(VersionCondition a)
		{
			return 1000193 * ((long)a.VersionNumber + (long)a.VersionIdHash);
		}

		public long VisitAttribute(DebugCondition a)
		{
			return 1000199 * ((long)a.DebugLevel + (long)a.DebugIdHash);
		}

		public long VisitAttribute(StaticIfCondition a)
		{
			const long prime = 1000211;
			long h = 1;

			Hash (ref h, prime, a.Expression);

			return h;
		}

		public long VisitAttribute(NegatedDeclarationCondition a)
		{
			return unchecked(1000213 * a.FirstCondition.Accept(this));
		}
		#endregion

		#region Statements
		public long Visit(ModuleStatement moduleStatement)
		{
			return 1000289;
		}

		public long Visit(ImportStatement importStatement)
		{
			return 1000291;
		}

		public long VisitImport(ImportStatement.Import import)
		{
			return 1000303;
		}

		public long VisitImport(ImportStatement.ImportBinding importBinding)
		{
			return 1000313;
		}

		public long VisitImport(ImportStatement.ImportBindings bindings)
		{
			return 1000333;
		}

		public long Visit(Statements.BlockStatement blockStatement)
		{
			return 1000357;
		}

		public long Visit(Statements.LabeledStatement labeledStatement)
		{
			return 1000367;
		}

		public long Visit(Statements.IfStatement ifStatement)
		{
			return 1000381;
		}

		public long Visit(Statements.WhileStatement whileStatement)
		{
			return 1000393;
		}

		public long Visit(Statements.ForStatement forStatement)
		{
			return 1000397;
		}

		public long Visit(Statements.ForeachStatement foreachStatement)
		{
			return 1000403;
		}

		public long Visit(Statements.SwitchStatement switchStatement)
		{
			return 1000409;
		}

		public long Visit(Statements.SwitchStatement.CaseStatement caseStatement)
		{
			return 1000423;
		}

		public long Visit(Statements.SwitchStatement.DefaultStatement defaultStatement)
		{
			return 1000427;
		}

		public long Visit(Statements.ContinueStatement continueStatement)
		{
			return 1000429;
		}

		public long Visit(Statements.BreakStatement breakStatement)
		{
			return 1000453;
		}

		public long Visit(Statements.ReturnStatement returnStatement)
		{
			return 1000457;
		}

		public long Visit(Statements.GotoStatement gotoStatement)
		{
			return 1000507;
		}

		public long Visit(Statements.WithStatement withStatement)
		{
			return 1000537;
		}

		public long Visit(Statements.SynchronizedStatement synchronizedStatement)
		{
			return 1000541;
		}

		public long Visit(Statements.TryStatement tryStatement)
		{
			return 1000547;
		}

		public long Visit(Statements.TryStatement.CatchStatement catchStatement)
		{
			return 1000577;
		}

		public long Visit(Statements.TryStatement.FinallyStatement finallyStatement)
		{
			return 1000579;
		}

		public long Visit(Statements.ThrowStatement throwStatement)
		{
			return 1000589;
		}

		public long Visit(Statements.ScopeGuardStatement scopeGuardStatement)
		{
			return 1000609;
		}

		public long VisitAsmStatement(Statements.AsmStatement asmStatement)
		{
			return 1000619;
		}

		public long VisitAsmInstructionStatement(AsmInstructionStatement instrStatement)
		{
			return 1000621;
		}

		public long VisitAsmRawDataStatement(AsmRawDataStatement dataStatement)
		{
			return 1000639;
		}

		public long VisitAsmAlignStatement(AsmAlignStatement alignStatement)
		{
			return 1000651;
		}

		public long Visit(Statements.PragmaStatement pragmaStatement)
		{
			return 1000667;
		}

        // return 1000669;

		public long Visit(Statements.StatementCondition condition)
		{
			return 1000679;
		}

		public long Visit(Statements.VolatileStatement volatileStatement)
		{
			return 1000691;
		}

		public long Visit(Statements.ExpressionStatement expressionStatement)
		{
			return 1000697;
		}

		public long Visit(Statements.DeclarationStatement declarationStatement)
		{
			return 1000721;
		}

		public long Visit(Statements.TemplateMixin templateMixin)
		{
			return 1000723;
		}

		public long Visit(Statements.DebugSpecification versionSpecification)
		{
			return 1000763;
		}

		public long Visit(Statements.VersionSpecification versionSpecification)
		{
			return 1000777;
		}

		public long Visit(Statements.StaticAssertStatement s)
		{
			return 1000793;
		}

        public long Visit(Statements.StaticForeachStatement s)
        {
            return 1002341; // higher than any other prime in this file
        }

        public long VisitMixinStatement(Statements.MixinStatement s)
		{
			return 1000829;
		}
		#endregion

		#region Expressions
		public long Visit(Expressions.Expression x)
		{
			const long prime = 1000847;
			long h = prime;
			HashEnum (ref h, prime, x.Expressions, true);
			return h;
		}

		long VisitOpBasedExpression(OperatorBasedExpression op, long prime)
		{
			long hashCode = 1;

			Hash (ref hashCode, prime, op.LeftOperand);
			Hash (ref hashCode, prime, op.RightOperand);
			Hash (ref hashCode, prime, op.OperatorToken);

			return hashCode;
		}

		public long Visit(Expressions.AssignExpression x)
		{
			return VisitOpBasedExpression (x, 1000849);
		}

		public long Visit(Expressions.ConditionalExpression x)
		{
			long h = 1;
			const long prime = 1000859;

			Hash (ref h, prime, x.OrOrExpression);
			Hash (ref h, prime, x.TrueCaseExpression);
			Hash (ref h, prime, x.FalseCaseExpression);

			return h;
		}

		public long Visit(Expressions.OrOrExpression x)
		{
			return VisitOpBasedExpression(x, 1000861);
		}

		public long Visit(Expressions.AndAndExpression x)
		{
			return VisitOpBasedExpression(x, 1000889);
		}

		public long Visit(Expressions.XorExpression x)
		{
			return VisitOpBasedExpression(x, 1000907);
		}

		public long Visit(Expressions.OrExpression x)
		{
			return VisitOpBasedExpression(x, 1000919);
		}

		public long Visit(Expressions.AndExpression x)
		{
			return VisitOpBasedExpression(x, 1000921);
		}

		public long Visit(Expressions.EqualExpression x)
		{
			return VisitOpBasedExpression(x, 1000931);
		}

		public long Visit(Expressions.IdentityExpression x)
		{
			const long prime = 1000969;
			var h = VisitOpBasedExpression (x, prime);

			Hash (ref h, prime, x.Not);

			return h;
		}

		public long Visit(Expressions.RelExpression x)
		{
			return VisitOpBasedExpression(x, 1000973);
		}

		public long Visit(Expressions.InExpression x)
		{
			const long prime = 1000981;
			var h = VisitOpBasedExpression (x, prime);

			Hash (ref h, prime, x.Not ? 2 : 1);

			return h;
		}

		public long Visit(Expressions.ShiftExpression x)
		{
			return VisitOpBasedExpression(x, 1000999);
		}

		public long Visit(Expressions.AddExpression x)
		{
			return VisitOpBasedExpression(x, 1001003);
		}

		public long Visit(Expressions.MulExpression x)
		{
			return VisitOpBasedExpression(x, 1001017);
		}

		public long Visit(Expressions.CatExpression x)
		{
			return VisitOpBasedExpression(x, 1001023);
		}

		public long Visit(Expressions.PowExpression x)
		{
			return VisitOpBasedExpression(x, 1001027);
		}

		public long Visit(Expressions.UnaryExpression_And x)
		{
			return VisitSimpleUnaryExpression(x, 1001041);
		}

		public long Visit(Expressions.UnaryExpression_Increment x)
		{
			return VisitSimpleUnaryExpression(x, 1001069);
		}

		public long Visit(Expressions.UnaryExpression_Decrement x)
		{
			return VisitSimpleUnaryExpression(x, 1001081);
		}

		public long Visit(Expressions.UnaryExpression_Mul x)
		{
			return VisitSimpleUnaryExpression(x, 1001087);
		}

		public long Visit(Expressions.UnaryExpression_Add x)
		{
			return VisitSimpleUnaryExpression(x, 1001089);
		}

		public long Visit(Expressions.UnaryExpression_Sub x)
		{
			return VisitSimpleUnaryExpression(x, 1001093);
		}

		public long Visit(Expressions.UnaryExpression_Not x)
		{
			return VisitSimpleUnaryExpression(x, 1001107);
		}

		public long Visit(Expressions.UnaryExpression_Cat x)
		{
			return VisitSimpleUnaryExpression(x, 1001123);
		}

		public long Visit(Expressions.UnaryExpression_Type x)
		{
			long h = 1;
			const long prime = 1001153;

			Hash (ref h, prime, x.Type);
			Hash (ref h, prime, x.AccessIdentifierHash);

			return h;
		}

		public long Visit(Expressions.NewExpression x)
		{
			long h = 1;
			const long prime = 1001159;

			Hash (ref h, prime, x.Type);
			HashEnum (ref h, prime, x.Arguments);
			HashEnum (ref h, prime, x.NewArguments);

			return h;
		}

		public long Visit(Expressions.AnonymousClassExpression x)
		{
			long h = 1;
			const long prime = 1001173;

			HashEnum (ref h, prime, x.ClassArguments);
			HashEnum (ref h, prime, x.NewArguments);
			Hash (ref h, prime, x.AnonymousClass);

			return h;
		}

		public long Visit(Expressions.DeleteExpression x)
		{
			return VisitSimpleUnaryExpression(x, 1001177);
		}

		public long Visit(Expressions.CastExpression x)
		{
			const long prime = 1001191;
			long h = 1;

			Hash (ref h, prime, x.Type);
			Hash (ref h, prime, x.UnaryExpression);

			unchecked{
				h *= prime;
				if (x.CastParamTokens != null && x.CastParamTokens.Length != 0) {
					h += x.CastParamTokens.Length;
					foreach (var tk in x.CastParamTokens) {
						h = prime * h + tk;
					}
				}
			}

			return h;
		}

		long VisitPostfixExpression (Expressions.PostfixExpression x, long prime){
			long h = 1;
			Hash (ref h, prime, x.PostfixForeExpression);
			return h;
		}

		public long Visit(Expressions.PostfixExpression_Access x)
		{
			const long prime = 1001197;
			var h = VisitPostfixExpression (x, prime);

			Hash (ref h, prime, x.AccessExpression);

			return h;
		}

		public long Visit(Expressions.PostfixExpression_Increment x)
		{
			return VisitPostfixExpression(x, 1001219);
		}

		public long Visit(Expressions.PostfixExpression_Decrement x)
		{
			return VisitPostfixExpression(x, 1001237);
		}

		public long VisitPostfixExpression_Methodcall(Expressions.PostfixExpression_MethodCall x)
		{
			const long prime = 1001267;
			var h = VisitPostfixExpression (x, prime);

			HashEnum (ref h, prime, x.Arguments, true);

			return h;
		}

		public long Visit(Expressions.PostfixExpression_ArrayAccess x)
		{
			const long prime = 1001279;
			var h = VisitPostfixExpression (x, prime);

			h *= prime;
			if (x.Arguments != null && x.Arguments.Length != 0) {
				h += x.Arguments.Length;

				foreach (var arg in x.Arguments) {
					Hash (ref h, prime, arg.Expression);
					if (arg is PostfixExpression_ArrayAccess.SliceArgument)
						Hash (ref h, prime, (arg as PostfixExpression_ArrayAccess.SliceArgument).UpperBoundExpression);
				}
			}

			return h;
		}

		//TODO: return 1001291; got freed due to obsoletion of postfixexpression_slice

		public long Visit(Expressions.TemplateInstanceExpression x)
		{
			const long prime = 1001303;
			var h = VisitTypeDeclaration (x, prime);

			Hash (ref h, prime, x.Identifier);
			Hash (ref h, prime, x.TemplateIdHash);
			HashEnum (ref h, prime, x.Arguments, true);

			return h;
		}

		public long VisitScalarConstantExpression(Expressions.ScalarConstantExpression x)
		{
			const long prime = 1002341;
			long h = 1;

			Hash(ref h, prime, x.EscapeStringHash);
			Hash(ref h, prime, x.Format);
			Hash(ref h, prime, x.Subformat);
			Hash(ref h, prime, x.Value);

			return h;
		}

		public long VisitStringLiteralExpression(Expressions.StringLiteralExpression x)
		{
			const long prime = 1002343;
			long h = 1;

			Hash(ref h, prime, x.Format);
			Hash(ref h, prime, x.Subformat);
			Hash(ref h, prime, x.Value);

			return h;
		}

		public long Visit(Expressions.IdentifierExpression x)
		{
			long h = 1;
			const long prime = 1001311;

			Hash (ref h, prime, x.IdHash);

			return h;
		}

		public long Visit(Expressions.TokenExpression x)
		{
			return 1001321 + (long)x.Token >> 8;
		}

		public long Visit(Expressions.TypeDeclarationExpression x)
		{
			return 1001323 + x.Declaration.Accept(this);
		}

		public long Visit(Expressions.ArrayLiteralExpression x)
		{
			const long prime = 1001327;
			long h = prime;

			HashEnum (ref h, prime, x.Elements, true);

			return h;
		}

		public long Visit(Expressions.AssocArrayExpression x)
		{
			return 1001347;
		}

		public long Visit(Expressions.FunctionLiteral x)
		{
			return 1001353;
		}

		public long Visit(Expressions.AssertExpression x)
		{
			return 1001369;
		}

		public long Visit(Expressions.MixinExpression x)
		{
			return 1001381;
		}

		public long Visit(Expressions.ImportExpression x)
		{
			return 1001387;
		}

		public long Visit(Expressions.TypeidExpression x)
		{
			return 1001389;
		}

		public long Visit(Expressions.IsExpression x)
		{
			const long prime = 1001401;
			long h = 1;

			Hash (ref h, prime, x.TestedType);
			Hash (ref h, prime, x.TypeAliasIdentifierHash);
			Hash (ref h, prime, x.EqualityTest);
			Hash (ref h, prime, x.TypeSpecialization);
			Hash (ref h, prime, x.TypeSpecializationToken);
			HashEnum (ref h, prime, x.TemplateParameterList);

			return h;
		}

		public long Visit(Expressions.TraitsExpression x)
		{
			return 1001411;
		}

		public long Visit(Expressions.SurroundingParenthesesExpression x)
		{
			return 1001431;
		}

		public long Visit(Expressions.VoidInitializer x)
		{
			return 1001447;
		}

		public long Visit(Expressions.ArrayInitializer x)
		{
			return 1001459;
		}

		public long Visit(Expressions.StructInitializer x)
		{
			return 1001467;
		}

		public long Visit(Expressions.StructMemberInitializer structMemberInitializer)
		{
			return 1001491;
		}

		public long Visit(Expressions.AsmRegisterExpression x)
		{
			return 1001501 * x.Register.GetHashCode();
		}

		long VisitSimpleUnaryExpression(SimpleUnaryExpression x, long prime)
		{
			long h = 1;
			Hash (ref h, prime, x.UnaryExpression);
			Hash (ref h, prime, x.ForeToken);
			return h;
		}

		public long Visit(Expressions.UnaryExpression_SegmentBase x)
		{
			//TODO
			return 1001527;
		}
		#endregion

		#region TypeDeclarations
		long VisitTypeDeclaration(ITypeDeclaration td, long prime)
		{
			long h = 1;
			Hash (ref h, prime, td.InnerDeclaration);
			return h;
		}

		public long Visit(IdentifierDeclaration td)
		{
			const long prime = 1001531;
			var h = VisitTypeDeclaration (td, prime);

			Hash (ref h, prime, td.ModuleScoped);
			Hash (ref h, prime, td.IdHash);

			return h;
		}

		public long Visit(DTokenDeclaration td)
		{
			const long prime = 1001549;
			var h = VisitTypeDeclaration (td, prime);

			Hash (ref h, prime, (long)td.Token);

			return h;
		}

		public long Visit(ArrayDecl td)
		{
			const long prime = 1001551;
			var h = VisitTypeDeclaration (td, prime);

			Hash (ref h, prime, td.KeyType);
			Hash (ref h, prime, td.KeyExpression);

			return h;
		}

		public long Visit(DelegateDeclaration td)
		{
			const long prime = 1001563;
			var h = VisitTypeDeclaration (td, prime);

			Hash (ref h, prime, td.IsFunction);
			HashEnum (ref h, prime, td.Parameters, true);
			HashEnum (ref h, prime, td.Modifiers);

			return h;
		}

		public long Visit(PointerDecl td)
		{
			return VisitTypeDeclaration(td, 1001569);
		}

		public long Visit(MemberFunctionAttributeDecl td)
		{
			const long prime = 1001587;
			var h = VisitTypeDeclaration (td, prime);

			Hash (ref h, prime, td.Modifier);
			Hash (ref h, prime, td.InnerType);

			return h;
		}

		public long Visit(TypeOfDeclaration td)
		{
			const long prime = 1001593;
			var h = VisitTypeDeclaration(td, prime);

			Hash(ref h, prime, td.Expression);

			return h;
		}

		public long Visit(TraitsDeclaration td)
		{
			const long prime = 1001593;
			var h = VisitTypeDeclaration(td, prime);

			Hash(ref h, prime, td.Expression);

			return h;
		}

		public long Visit(VectorDeclaration td)
		{
			const long prime = 1001621;
			var h = VisitTypeDeclaration (td, prime);

			Hash (ref h, prime, td.Id);
			Hash (ref h, prime, td.IdDeclaration);

			return h;
		}

		public long Visit(VarArgDecl td)
		{
			return VisitTypeDeclaration(td, 1001629);
		}

		// const long prime = 1001639;
		#endregion

		#region Resolved Types
		static long VisitAbstractType (AbstractType t, long prime){
			long h = 1;

			Hash (ref h, prime, t.NonStaticAccess);
			HashEnum (ref h, prime, t.Modifiers, (modifier) => modifier);

			return h;
		}


		public long VisitPrimitiveType(PrimitiveType t)
		{
			const long prime = 1001659;
			var h = VisitAbstractType(t, prime);

			Hash (ref h, prime, t.TypeToken);

			return h;
		}



		long VisitDerivedType(DerivedDataType t, long prime)
		{
			var h = VisitAbstractType (t, prime);

			Hash (ref h, prime, t.Base);

			return h;
		}

		public long VisitPointerType(PointerType t)
		{
			return VisitDerivedType (t, 1001669);
		}

		public long VisitArrayType(ArrayType t)
		{
			const long prime = 1001683;
			var h = VisitAssocArrayType (t);

			Hash (ref h, prime, t.FixedLength);

			return h;
		}

		public long VisitAssocArrayType(AssocArrayType t)
		{
			const long prime = 1001687;
			var h = VisitDerivedType (t, prime);

			Hash (ref h, prime, t.KeyType);

			return h;
		}

		public long VisitDelegateCallSymbol(DelegateCallSymbol t)
		{
			const long prime = 1001713;
			var h = VisitDerivedType (t, prime);

			Hash (ref h, prime, t.Delegate);

			return h;
		}

		public long VisitDelegateType(DelegateType t)
		{
			const long prime = 1001723;
			var h = VisitDerivedType (t, prime);

			Hash (ref h, prime, t.IsFunction);
			HashEnum (ref h, prime, t.Parameters);

			return h;
		}

		public long VisitAliasedType(AliasedType t)
		{
			return VisitDSymbol(t, 1001743);
		}

		long VisitDSymbol(DSymbol t, long prime)
		{
			var h = VisitDerivedType (t, prime);

			Hash (ref h, prime, t.Definition);
			HashEnum (ref h, prime, t.DeducedTypes);

			return h;
		}

		long VisitUserDefinedType(UserDefinedType udt, long prime)
		{
			return VisitDSymbol (udt, prime);
		}

		long VisitTemplateIntermediaryType(TemplateIntermediateType tit, long prime)
		{
			var h = VisitUserDefinedType (tit, prime);

			HashEnum (ref h, prime, tit.BaseInterfaces);

			return h;
		}

		public long VisitEnumType(EnumType t)
		{
			return VisitUserDefinedType (t, 1001783);
		}

		public long VisitStructType(StructType t)
		{
			return VisitTemplateIntermediaryType (t, 1001797);
		}

		public long VisitUnionType(UnionType t)
		{
			return VisitTemplateIntermediaryType (t, 1001801);
		}

		public long VisitClassType(ClassType t)
		{
			return VisitTemplateIntermediaryType (t, 1001807);
		}

		public long VisitInterfaceType(InterfaceType t)
		{
			return VisitTemplateIntermediaryType (t, 1001809);
		}

		public long VisitTemplateType(TemplateType t)
		{
			return VisitTemplateIntermediaryType (t, 1001821);
		}

		public long VisitMixinTemplateType(MixinTemplateType t)
		{
			return VisitTemplateIntermediaryType (t, 1001831);
		}

		public long VisitEponymousTemplateType(EponymousTemplateType t)
		{
			return VisitUserDefinedType (t, 1001839);
		}

		public long VisitStaticProperty(StaticProperty t)
		{
			return VisitMemberSymbol(t, 1001911);
		}

		public long VisitMemberSymbol(MemberSymbol t)
		{
			return VisitDSymbol (t, 1001933);
		}

		long VisitMemberSymbol(MemberSymbol t, long prime)
		{
			return VisitDSymbol (t, prime);
		}

		public long VisitTemplateParameterSymbol(TemplateParameterSymbol tps)
		{
			const long prime = 1001941;
			var h = VisitMemberSymbol (tps, prime);

			Hash (ref h, prime, tps.Parameter);
			Hash (ref h, prime, tps.ParameterValue);
			Hash (ref h, prime, tps.IsKnowinglyUndetermined);

			return h;
		}

		public long VisitArrayAccessSymbol(ArrayAccessSymbol t)
		{
			return VisitDerivedType(t, 1001947);
		}

		public long VisitModuleSymbol(ModuleSymbol t)
		{
			return VisitDSymbol(t, 1001953);
		}

		public long VisitPackageSymbol(PackageSymbol t)
		{
			const long prime = 1001977;
			var h = VisitAbstractType (t, prime);

			Hash (ref h, prime, t.Package.Path);

			return h;
		}

		public long VisitDTuple(DTuple t)
		{
			const long prime = 1001981;
			var h = VisitAbstractType (t, prime);

			HashEnum (ref h, prime, t.Items, 
				(o) => o is ISymbolValue ? (o as ISymbolValue).Accept(Instance) : o is AbstractType ? (o as AbstractType).Accept(Instance) : 0, true);

			return h;
		}

		public long VisitUnknownType(UnknownType t)
		{
			return VisitAbstractType(t, 1001983);
		}

		public long VisitAmbigousType(AmbiguousType t)
		{
			const long prime = 1001989;
			var h = VisitAbstractType (t, prime);

			HashEnum (ref h, prime, t.Overloads);

			return h;
		}
		#endregion

		#region Metablocks
		public long VisitMetaDeclarationBlock(MetaDeclarationBlock m)
		{
			return 1002017;
		}

		public long VisitAttributeMetaDeclarationBlock(AttributeMetaDeclarationBlock m)
		{
			return 1002049;
		}

		public long VisitAttributeMetaDeclarationSection(AttributeMetaDeclarationSection m)
		{
			return 1002061;
		}

		public long VisitElseMetaDeclarationBlock(ElseMetaDeclarationBlock m)
		{
			return 1002073;
		}

		public long VisitElseMetaDeclarationSection(ElseMetaDeclarationSection m)
		{
			return 1002151;
		}

		public long VisitElseMetaDeclaration(ElseMetaDeclaration m)
		{
			return 1002077;
		}

		public long VisitAttributeMetaDeclaration(AttributeMetaDeclaration m)
		{
			return 1002083;
		}
		#endregion

		#region Template parameters
		public long VisitTemplateParameter(TemplateParameter tp)
		{
			const long prime = 1002091;
			long h = 1;

			Hash (ref h, prime, tp.NameHash);

			return h;
		}

		public long Visit(TemplateTypeParameter tp)
		{
			const long prime = 1002101;
			var h = VisitTemplateParameter (tp);

			Hash (ref h, prime, tp.Specialization);
			Hash (ref h, prime, tp.Default);

			return h;
		}

		public long Visit(TemplateThisParameter tp)
		{
			const long prime = 1002109;
			var h = VisitTemplateParameter (tp);

			Hash (ref h, prime, tp.FollowParameter);

			return h;
		}

		public long Visit(TemplateValueParameter tp)
		{
			const long prime = 1002121;
			var h = VisitTemplateParameter (tp);

			Hash (ref h, prime, tp.DefaultExpression);
			Hash (ref h, prime, tp.SpecializationExpression);
			Hash (ref h, prime, tp.Type);

			return h;
		}

		public long Visit(TemplateAliasParameter tp)
		{
			const long prime = 1002143;
			var h = VisitTemplateParameter (tp);

			Hash (ref h, prime, tp.SpecializationType);
			Hash (ref h, prime, tp.DefaultType);

			return h;
		}

		public long Visit(TemplateTupleParameter tp)
		{
			return 1002149 * VisitTemplateParameter(tp);
		}
		#endregion

		#region SymbolValues

		public long VisitErrorValue (ErrorValue v)
		{
			const long prime = 1002173;
			return prime;
		}

		long VisitExpressionValue(ExpressionValue v, long prime)
		{
			long h = 1;
			Hash (ref h, prime, v.RepresentedType);
			return h;
		}

		public long VisitPrimitiveValue (PrimitiveValue v)
		{
			const long prime = 1002191;
			var h = VisitExpressionValue (v, prime);

			Hash (ref h, prime, v.Value);
			Hash (ref h, prime, v.ImaginaryPart);

			return h;
		}

		public long VisitVoidValue (VoidValue v)
		{
			return VisitExpressionValue (v, 1002227);
		}

		public long VisitArrayValue (ArrayValue v)
		{
			const long prime = 1002241;
			var h = VisitExpressionValue (v, prime);

			Hash (ref h, prime, v.StringValue);
			Hash (ref h, prime, v.StringFormat);
			HashEnum (ref h, prime, v.Elements);

			return h;
		}

		public long VisitAssociativeArrayValue (AssociativeArrayValue v)
		{
			const long prime = 1002247;
			var h = VisitExpressionValue (v, prime);

			HashEnum (ref h, prime, v.Elements, (kv) => 
				((kv.Key != null ? kv.Key.Accept(Instance) : 0) ^ 
					(kv.Value != null ? kv.Value.Accept(Instance) : 0))
			);

			return h;
		}

		public long VisitDelegateValue (DelegateValue v)
		{
			const long prime = 1002257;
			var h = VisitExpressionValue (v, prime);

			Hash (ref h, prime, v.Definition);
			Hash (ref h, prime, v.IsFunction);

			return h;
		}

		public long VisitNullValue (NullValue v)
		{
			return VisitExpressionValue (v, 1002259);
		}

		public long VisitTypeOverloadValue (InternalOverloadValue v)
		{
			const long prime = 1002263;
			var h = VisitExpressionValue (v, prime);

			HashEnum (ref h, prime, v.Overloads);

			return h;
		}

		long VisitReferenceValue(ReferenceValue v, long prime)
		{
			return VisitExpressionValue(v, prime); 
		}

		public long VisitVariableValue (VariableValue v)
		{
			return VisitReferenceValue(v, 1002289);
		}

		public long VisitTypeValue (TypeValue v)
		{
			return VisitExpressionValue (v, 1002299);
		}

		// 1002341 for ScalarConstantExpression
		// 1002343 for StringLiteralExpression

		#endregion
	}
}
