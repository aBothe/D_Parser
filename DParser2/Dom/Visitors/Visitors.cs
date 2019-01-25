using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;

namespace D_Parser.Dom
{
	public interface IVisitor { }
	public interface IVisitor<out R> { }
	public interface IVisitor<out R, ParameterType> { }

	public interface DVisitor : 
		NodeVisitor,
		MetaDeclarationVisitor,
		TemplateParameterVisitor,
		IStatementVisitor,
		ExpressionVisitor,
		TypeDeclarationVisitor
	{}

	public interface DVisitor<out R> : 
		NodeVisitor<R>,
		MetaDeclarationVisitor<R>,
		TemplateParameterVisitor<R>,
		StatementVisitor<R>,
		ExpressionVisitor<R>,
		TypeDeclarationVisitor<R>
	{}

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
		R VisitDVariable(DVariable n);
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
		void Visit(TemplateTypeParameter tp);
		void Visit(TemplateThisParameter tp);
		void Visit(TemplateValueParameter tp);
		void Visit(TemplateAliasParameter tp);
		void Visit(TemplateTupleParameter tp);
	}

	public interface TemplateParameterVisitor<out R> : IVisitor<R>
	{
		R VisitTemplateParameter (TemplateParameter tp);
		R Visit(TemplateTypeParameter tp);
		R Visit(TemplateThisParameter tp);
		R Visit(TemplateValueParameter tp);
		R Visit(TemplateAliasParameter tp);
		R Visit(TemplateTupleParameter tp);
	}

	public interface ITemplateParameterVisitor<out R, ParameterType> : IVisitor<R, ParameterType>
	{
		R VisitTemplateParameter(TemplateParameter tp, ParameterType parameter);
		R Visit(TemplateTypeParameter tp, ParameterType parameter);
		R Visit(TemplateThisParameter tp, ParameterType parameter);
		R Visit(TemplateValueParameter tp, ParameterType parameter);
		R Visit(TemplateAliasParameter tp, ParameterType parameter);
		R Visit(TemplateTupleParameter tp, ParameterType parameter);
	}

	public interface IStatementVisitor : IVisitor
	{
		void Visit(ModuleStatement s);
		void Visit(ImportStatement s);
		void VisitImport (ImportStatement.Import s);
		void VisitImport (ImportStatement.ImportBinding s);
		void VisitImport (ImportStatement.ImportBindings s);
		void Visit(BlockStatement s);
		void Visit(LabeledStatement s);
		void Visit(IfStatement s);
		void Visit(WhileStatement s);
		void Visit(ForStatement s);
		void Visit(ForeachStatement s);
		void Visit(SwitchStatement s);
		void Visit(SwitchStatement.CaseStatement s);
		void Visit(SwitchStatement.DefaultStatement s);
		void Visit(ContinueStatement s);
		void Visit(BreakStatement s);
		void Visit(ReturnStatement s);
		void Visit(GotoStatement s);
		void Visit(WithStatement s);
		void Visit(SynchronizedStatement s);
		void Visit(TryStatement s);
		void Visit(TryStatement.CatchStatement s);
		void Visit(TryStatement.FinallyStatement s);
		void Visit(ThrowStatement s);
		void Visit(ScopeGuardStatement s);
		void VisitAsmStatement(AsmStatement s);
		void VisitAsmInstructionStatement(AsmInstructionStatement s);
		void VisitAsmRawDataStatement(AsmRawDataStatement s);
		void VisitAsmAlignStatement(AsmAlignStatement s);
		void Visit(PragmaStatement s);
		void Visit(StatementCondition s);
		void Visit(VolatileStatement s);
		void Visit(ExpressionStatement s);
		void Visit(ContractStatement s);
		void Visit(DeclarationStatement s);
		void Visit(TemplateMixin s);
		void Visit(DebugSpecification s);
		void Visit(VersionSpecification s);
		void Visit(StaticAssertStatement s);
        void Visit(StaticForeachStatement s);
        void VisitMixinStatement(MixinStatement s);
	}

	public interface StatementVisitor<out R> : IVisitor<R>
	{
		R Visit(ModuleStatement s);
		R Visit(ImportStatement s);
		R VisitImport (ImportStatement.Import s);
		R VisitImport (ImportStatement.ImportBinding s);
		R VisitImport (ImportStatement.ImportBindings s);
		R Visit(BlockStatement s);
		R Visit(LabeledStatement s);
		R Visit(IfStatement s);
		R Visit(WhileStatement s);
		R Visit(ForStatement s);
		R Visit(ForeachStatement s);
		R Visit(SwitchStatement s);
		R Visit(SwitchStatement.CaseStatement s);
		R Visit(SwitchStatement.DefaultStatement s);
		R Visit(ContinueStatement s);
		R Visit(BreakStatement s);
		R Visit(ReturnStatement s);
		R Visit(GotoStatement s);
		R Visit(WithStatement s);
		R Visit(SynchronizedStatement s);
		R Visit(TryStatement s);
		R Visit(TryStatement.CatchStatement s);
		R Visit(TryStatement.FinallyStatement s);
		R Visit(ThrowStatement s);
		R Visit(ScopeGuardStatement s);
		R VisitAsmStatement(AsmStatement s);
		R VisitAsmInstructionStatement(AsmInstructionStatement s);
		R VisitAsmRawDataStatement(AsmRawDataStatement s);
		R VisitAsmAlignStatement(AsmAlignStatement s);
		R Visit(PragmaStatement s);
		R Visit(StatementCondition s);
		R Visit(VolatileStatement s);
		R Visit(ExpressionStatement s);
		R Visit(ContractStatement s);
		R Visit(DeclarationStatement s);
		R Visit(TemplateMixin s);
		R Visit(DebugSpecification s);
		R Visit(VersionSpecification s);
		R Visit(StaticAssertStatement s);
        R Visit(StaticForeachStatement s);
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
		void VisitScalarConstantExpression(ScalarConstantExpression x);
		void VisitStringLiteralExpression(StringLiteralExpression x);
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
		void Visit(StructMemberInitializer x);

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
		R VisitPostfixExpression_Methodcall(PostfixExpression_MethodCall x);
		R Visit(PostfixExpression_ArrayAccess x);

		R Visit(TemplateInstanceExpression x);
		R VisitScalarConstantExpression(ScalarConstantExpression x);
		R VisitStringLiteralExpression(StringLiteralExpression x);
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
		R Visit(StructMemberInitializer x);

		R Visit(AsmRegisterExpression x);
		R Visit(UnaryExpression_SegmentBase x);
	}

	public interface TypeDeclarationVisitor : IVisitor
	{
		void Visit(IdentifierDeclaration td);
		void Visit(DTokenDeclaration td);
		void Visit(ArrayDecl td);
		void Visit(DelegateDeclaration td);
		void Visit(PointerDecl td);
		void Visit(MemberFunctionAttributeDecl td);
		void Visit(TypeOfDeclaration td);
		void Visit(TraitsDeclaration td);
		void Visit(VectorDeclaration td);
		void Visit(VarArgDecl td);

		void Visit(TemplateInstanceExpression td);
	}

	public interface TypeDeclarationVisitor<out R> : IVisitor<R>
	{
		R Visit(IdentifierDeclaration td);
		R Visit(DTokenDeclaration td);
		R Visit(ArrayDecl td);
		R Visit(DelegateDeclaration td);
		R Visit(PointerDecl td);
		R Visit(MemberFunctionAttributeDecl td);
		R Visit(TypeOfDeclaration td);
		R Visit(TraitsDeclaration td);
		R Visit(VectorDeclaration td);
		R Visit(VarArgDecl td);
		R Visit(TemplateInstanceExpression td);
	}

	public interface ITypeDeclarationVisitor<out R, ParameterType> : IVisitor<R, ParameterType>
	{
		R Visit(IdentifierDeclaration td, ParameterType parameter);
		R Visit(DTokenDeclaration td, ParameterType parameter);
		R Visit(ArrayDecl td, ParameterType parameter);
		R Visit(DelegateDeclaration td, ParameterType parameter);
		R Visit(PointerDecl td, ParameterType parameter);
		R Visit(MemberFunctionAttributeDecl td, ParameterType parameter);
		R Visit(TypeOfDeclaration td, ParameterType parameter);
		R Visit(TraitsDeclaration td, ParameterType parameter);
		R Visit(VectorDeclaration td, ParameterType parameter);
		R Visit(VarArgDecl td, ParameterType parameter);
		R Visit(TemplateInstanceExpression td, ParameterType parameter);
	}
}
