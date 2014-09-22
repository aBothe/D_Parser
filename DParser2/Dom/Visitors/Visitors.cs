using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;

namespace D_Parser.Dom
{
	public interface IVisitor { }
	public interface IVisitor<out R> { }

	public interface DVisitor : 
		NodeVisitor, 
		MetaDeclarationVisitor,
		TemplateParameterVisitor,
		StatementVisitor, 
		ExpressionVisitor, 
		TypeDeclarationVisitor
	{
		
	}

	public interface DVisitor<out R> : 
		NodeVisitor<R>,
		MetaDeclarationVisitor<R>,
		TemplateParameterVisitor<R>,
		StatementVisitor<R>, 
		ExpressionVisitor<R>, 
		TypeDeclarationVisitor<R>
	{

	}

	public interface NodeVisitor : IVisitor
	{
		void Visit(DEnumValue n);
		void Visit(DVariable n);
		void Visit(DMethod n);
		void Visit(DClassLike n);
		void Visit(DEnum n);
		void Visit(DModule n);
		void VisitBlock(DBlockNode n);
		void Visit(TemplateParameter.Node n);
		void Visit(NamedTemplateMixinNode n);

		void VisitAttribute(Modifier a);
		void VisitAttribute(DeprecatedAttribute a);
		void VisitAttribute(PragmaAttribute a);
		void VisitAttribute(BuiltInAtAttribute a);
		void VisitAttribute(UserDeclarationAttribute a);
		
		void VisitAttribute(VersionCondition a);
		void VisitAttribute(DebugCondition a);
		void VisitAttribute(StaticIfCondition a);
		void VisitAttribute(NegatedDeclarationCondition a);

		void Visit(EponymousTemplate n);
		void Visit(ModuleAliasNode n);
		void Visit(ImportSymbolNode n);
		void Visit(ImportSymbolAlias n);
	}

	public interface NodeVisitor<out R> : IVisitor<R>
	{
		R Visit(DEnumValue n);
		R Visit(DVariable n);
		R Visit(DMethod n);
		R Visit(DClassLike n);
		R Visit(DEnum n);
		R Visit(DModule n);
		R Visit(DBlockNode n);
		R Visit(TemplateParameter.Node n);
		R Visit(NamedTemplateMixinNode n);

		R VisitAttribute(Modifier a);
		R VisitAttribute(DeprecatedAttribute a);
		R VisitAttribute(PragmaAttribute attr);
		R VisitAttribute(BuiltInAtAttribute a);
		R VisitAttribute(UserDeclarationAttribute a);
		
		R VisitAttribute(VersionCondition a);
		R VisitAttribute(DebugCondition a);
		R VisitAttribute(StaticIfCondition a);
		R VisitAttribute(NegatedDeclarationCondition a);

		R Visit(EponymousTemplate n);
		R Visit(ModuleAliasNode n);
		R Visit(ImportSymbolNode n);
		R Visit(ImportSymbolAlias n);
	}

	public interface MetaDeclarationVisitor : IVisitor
	{
		void VisitMetaDeclarationBlock(MetaDeclarationBlock m);
		void VisitAttributeMetaDeclarationBlock(AttributeMetaDeclarationBlock m);
		void VisitAttributeMetaDeclarationSection(AttributeMetaDeclarationSection m);
		void VisitElseMetaDeclarationBlock(ElseMetaDeclarationBlock m);
		void VisitElseMetaDeclarationSection(ElseMetaDeclarationSection m);
		void VisitElseMetaDeclaration(ElseMetaDeclaration m);
		void VisitAttributeMetaDeclaration(AttributeMetaDeclaration m);
	}
	
	public interface MetaDeclarationVisitor<out R> : IVisitor
	{
		R VisitMetaDeclarationBlock(MetaDeclarationBlock m);
		R VisitAttributeMetaDeclarationBlock(AttributeMetaDeclarationBlock m);
		R VisitAttributeMetaDeclarationSection(AttributeMetaDeclarationSection m);
		R VisitElseMetaDeclarationBlock(ElseMetaDeclarationBlock m);
		R VisitElseMetaDeclarationSection(ElseMetaDeclarationSection m);
		R VisitElseMetaDeclaration(ElseMetaDeclaration m);
		R VisitAttributeMetaDeclaration(AttributeMetaDeclaration m);
	}

	public interface TemplateParameterVisitor : IVisitor
	{
		void VisitTemplateParameter (TemplateParameter tp);
		void Visit(TemplateTypeParameter templateTypeParameter);
		void Visit(TemplateThisParameter templateThisParameter);
		void Visit(TemplateValueParameter templateValueParameter);
		void Visit(TemplateAliasParameter templateAliasParameter);
		void Visit(TemplateTupleParameter templateTupleParameter);
	}

	public interface TemplateParameterVisitor<out R> : IVisitor<R>
	{
		R VisitTemplateParameter (TemplateParameter tp);
		R Visit(TemplateTypeParameter templateTypeParameter);
		R Visit(TemplateThisParameter templateThisParameter);
		R Visit(TemplateValueParameter templateValueParameter);
		R Visit(TemplateAliasParameter templateAliasParameter);
		R Visit(TemplateTupleParameter templateTupleParameter);
	}

	public interface StatementVisitor : IVisitor
	{
		void Visit(ModuleStatement moduleStatement);
		void Visit(ImportStatement importStatement);
		void VisitImport (ImportStatement.Import import);
		void VisitImport (ImportStatement.ImportBinding importBinding);
		void VisitImport (ImportStatement.ImportBindings bindings);
		void Visit(BlockStatement blockStatement);
		void Visit(LabeledStatement labeledStatement);
		void Visit(IfStatement ifStatement);
		void Visit(WhileStatement whileStatement);
		void Visit(ForStatement forStatement);
		void Visit(ForeachStatement foreachStatement);
		void Visit(SwitchStatement switchStatement);
		void Visit(SwitchStatement.CaseStatement caseStatement);
		void Visit(SwitchStatement.DefaultStatement defaultStatement);
		void Visit(ContinueStatement continueStatement);
		void Visit(BreakStatement breakStatement);
		void Visit(ReturnStatement returnStatement);
		void Visit(GotoStatement gotoStatement);
		void Visit(WithStatement withStatement);
		void Visit(SynchronizedStatement synchronizedStatement);
		void Visit(TryStatement tryStatement);
		void Visit(TryStatement.CatchStatement catchStatement);
		void Visit(TryStatement.FinallyStatement finallyStatement);
		void Visit(ThrowStatement throwStatement);
		void Visit(ScopeGuardStatement scopeGuardStatement);
		void Visit(AsmStatement asmStatement);
		void Visit(AsmStatement.InstructionStatement instrStatement);
		void Visit(AsmStatement.RawDataStatement dataStatement);
		void Visit(AsmStatement.AlignStatement alignStatement);
		void Visit(PragmaStatement pragmaStatement);
		void Visit(StatementCondition condition);
		void Visit(VolatileStatement volatileStatement);
		void Visit(ExpressionStatement expressionStatement);
		void Visit(DeclarationStatement declarationStatement);
		void Visit(TemplateMixin templateMixin);
		void Visit(DebugSpecification versionSpecification);
		void Visit(VersionSpecification versionSpecification);
		void Visit(StaticAssertStatement s);
		void VisitMixinStatement(MixinStatement s);
	}

	public interface StatementVisitor<out R> : IVisitor<R>
	{
		R Visit(ModuleStatement moduleStatement);
		R Visit(ImportStatement importStatement);
		R VisitImport (ImportStatement.Import import);
		R VisitImport (ImportStatement.ImportBinding importBinding);
		R VisitImport (ImportStatement.ImportBindings bindings);
		R Visit(BlockStatement blockStatement);
		R Visit(LabeledStatement labeledStatement);
		R Visit(IfStatement ifStatement);
		R Visit(WhileStatement whileStatement);
		R Visit(ForStatement forStatement);
		R Visit(ForeachStatement foreachStatement);
		R Visit(SwitchStatement switchStatement);
		R Visit(SwitchStatement.CaseStatement caseStatement);
		R Visit(SwitchStatement.DefaultStatement defaultStatement);
		R Visit(ContinueStatement continueStatement);
		R Visit(BreakStatement breakStatement);
		R Visit(ReturnStatement returnStatement);
		R Visit(GotoStatement gotoStatement);
		R Visit(WithStatement withStatement);
		R Visit(SynchronizedStatement synchronizedStatement);
		R Visit(TryStatement tryStatement);
		R Visit(TryStatement.CatchStatement catchStatement);
		R Visit(TryStatement.FinallyStatement finallyStatement);
		R Visit(ThrowStatement throwStatement);
		R Visit(ScopeGuardStatement scopeGuardStatement);
		R Visit(AsmStatement asmStatement);
		R Visit(AsmStatement.InstructionStatement instrStatement);
		R Visit(AsmStatement.RawDataStatement dataStatement);
		R Visit(AsmStatement.AlignStatement alignStatement);
		R Visit(PragmaStatement pragmaStatement);
		R Visit(StatementCondition condition);
		R Visit(VolatileStatement volatileStatement);
		R Visit(ExpressionStatement expressionStatement);
		R Visit(DeclarationStatement declarationStatement);
		R Visit(TemplateMixin templateMixin);
		R Visit(DebugSpecification versionSpecification);
		R Visit(VersionSpecification versionSpecification);
		R Visit(StaticAssertStatement s);
		R VisitMixinStatement(MixinStatement s);
	}

	public interface ExpressionVisitor : IVisitor
	{
		void Visit(Expression x);
		void Visit(AssignExpression x);
		void Visit(ConditionalExpression x);
		void Visit(OrOrExpression x);
		void Visit(AndAndExpression x);
		void Visit(XorExpression x);
		void Visit(OrExpression x);
		void Visit(AndExpression x);
		void Visit(EqualExpression x);
		void Visit(IdentityExpression x);
		void Visit(RelExpression x);
		void Visit(InExpression x);
		void Visit(ShiftExpression x);
		void Visit(AddExpression x);
		void Visit(MulExpression x);
		void Visit(CatExpression x);
		void Visit(PowExpression x);

		void Visit(UnaryExpression_And x);
		void Visit(UnaryExpression_Increment x);
		void Visit(UnaryExpression_Decrement x);
		void Visit(UnaryExpression_Mul x);
		void Visit(UnaryExpression_Add x);
		void Visit(UnaryExpression_Sub x);
		void Visit(UnaryExpression_Not x);
		void Visit(UnaryExpression_Cat x);
		void Visit(UnaryExpression_Type x);

		void Visit(NewExpression x);
		void Visit(AnonymousClassExpression x);
		void Visit(DeleteExpression x);
		void Visit(CastExpression x);

		void Visit(PostfixExpression_Access x);
		void Visit(PostfixExpression_Increment x);
		void Visit(PostfixExpression_Decrement x);
		void Visit(PostfixExpression_MethodCall x);
		void Visit(PostfixExpression_ArrayAccess x);

		void Visit(TemplateInstanceExpression x);
		void Visit(IdentifierExpression x);
		void Visit(TokenExpression x);
		void Visit(TypeDeclarationExpression x);
		void Visit(ArrayLiteralExpression x);
		void Visit(AssocArrayExpression x);
		void Visit(FunctionLiteral x);
		void Visit(AssertExpression x);
		void Visit(MixinExpression x);
		void Visit(ImportExpression x);
		void Visit(TypeidExpression x);
		void Visit(IsExpression x);
		void Visit(TraitsExpression x);
		void Visit(SurroundingParenthesesExpression x);

		void Visit(VoidInitializer x);
		void Visit(ArrayInitializer x);
		void Visit(StructInitializer x);
		void Visit(StructMemberInitializer structMemberInitializer);

		void Visit(AsmRegisterExpression x);
		void Visit(UnaryExpression_SegmentBase x);
	}

	public interface ExpressionVisitor<out R> : IVisitor<R>
	{
		R Visit(Expression x);
		R Visit(AssignExpression x);
		R Visit(ConditionalExpression x);
		R Visit(OrOrExpression x);
		R Visit(AndAndExpression x);
		R Visit(XorExpression x);
		R Visit(OrExpression x);
		R Visit(AndExpression x);
		R Visit(EqualExpression x);
		R Visit(IdentityExpression x);
		R Visit(RelExpression x);
		R Visit(InExpression x);
		R Visit(ShiftExpression x);
		R Visit(AddExpression x);
		R Visit(MulExpression x);
		R Visit(CatExpression x);
		R Visit(PowExpression x);

		R Visit(UnaryExpression_And x);
		R Visit(UnaryExpression_Increment x);
		R Visit(UnaryExpression_Decrement x);
		R Visit(UnaryExpression_Mul x);
		R Visit(UnaryExpression_Add x);
		R Visit(UnaryExpression_Sub x);
		R Visit(UnaryExpression_Not x);
		R Visit(UnaryExpression_Cat x);
		R Visit(UnaryExpression_Type x);

		R Visit(NewExpression x);
		R Visit(AnonymousClassExpression x);
		R Visit(DeleteExpression x);
		R Visit(CastExpression x);

		R Visit(PostfixExpression_Access x);
		R Visit(PostfixExpression_Increment x);
		R Visit(PostfixExpression_Decrement x);
		R Visit(PostfixExpression_MethodCall x);
		R Visit(PostfixExpression_ArrayAccess x);

		R Visit(TemplateInstanceExpression x);
		R Visit(IdentifierExpression x);
		R Visit(TokenExpression x);
		R Visit(TypeDeclarationExpression x);
		R Visit(ArrayLiteralExpression x);
		R Visit(AssocArrayExpression x);
		R Visit(FunctionLiteral x);
		R Visit(AssertExpression x);
		R Visit(MixinExpression x);
		R Visit(ImportExpression x);
		R Visit(TypeidExpression x);
		R Visit(IsExpression x);
		R Visit(TraitsExpression x);
		R Visit(SurroundingParenthesesExpression x);

		R Visit(VoidInitializer x);
		R Visit(ArrayInitializer x);
		R Visit(StructInitializer x);
		R Visit(StructMemberInitializer structMemberInitializer);

		R Visit(AsmRegisterExpression x);
		R Visit(UnaryExpression_SegmentBase x);
	}

	public interface TypeDeclarationVisitor : IVisitor
	{
		void Visit(IdentifierDeclaration identifierDeclaration);
		void Visit(DTokenDeclaration dTokenDeclaration);
		void Visit(ArrayDecl arrayDecl);
		void Visit(DelegateDeclaration delegateDeclaration);
		void Visit(PointerDecl pointerDecl);
		void Visit(MemberFunctionAttributeDecl memberFunctionAttributeDecl);
		void Visit(TypeOfDeclaration typeOfDeclaration);
		void Visit(VectorDeclaration vectorDeclaration);
		void Visit(VarArgDecl varArgDecl);

		void Visit(ITemplateParameterDeclaration iTemplateParameterDeclaration);

		void Visit(TemplateInstanceExpression templateInstanceExpression);
	}

	public interface TypeDeclarationVisitor<out R> : IVisitor<R>
	{
		R Visit(IdentifierDeclaration identifierDeclaration);
		R Visit(DTokenDeclaration dTokenDeclaration);
		R Visit(ArrayDecl arrayDecl);
		R Visit(DelegateDeclaration delegateDeclaration);
		R Visit(PointerDecl pointerDecl);
		R Visit(MemberFunctionAttributeDecl memberFunctionAttributeDecl);
		R Visit(TypeOfDeclaration typeOfDeclaration);
		R Visit(VectorDeclaration vectorDeclaration);
		R Visit(VarArgDecl varArgDecl);
		R Visit(ITemplateParameterDeclaration iTemplateParameterDeclaration);
		R Visit(TemplateInstanceExpression templateInstanceExpression);
	}
}
