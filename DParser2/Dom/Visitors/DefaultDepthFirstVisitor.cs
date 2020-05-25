using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;

namespace D_Parser.Dom
{
	public abstract class DefaultDepthFirstVisitor : DVisitor
	{
		#region Nodes
		public virtual void VisitChildren(IBlockNode block)
		{
			foreach (var n in block)
				n.Accept(this);
		}

		/// <summary>
		/// Calls VisitDNode already.
		/// </summary>
		public virtual void VisitBlock(DBlockNode block)
		{
			VisitChildren(block);
			VisitDNode(block);

			if (block.StaticStatements.Count != 0)
				foreach (var s in block.StaticStatements)
					s.Accept(this);

			if (block.MetaBlocks.Count != 0)
				foreach (var mb in block.MetaBlocks)
					mb.Accept(this);
		}

		public virtual void Visit(DEnumValue n)
		{
			Visit(n as DVariable);
		}

		public virtual void Visit(DVariable n)
		{
			n.Initializer?.Accept(this);
			VisitDNode(n);
		}

		public virtual void Visit(EponymousTemplate ep)
		{
			Visit(ep as DVariable);
		}

		public virtual void Visit(DMethod n)
		{
			VisitDNode(n);

			foreach (var par in n.Parameters)
				par.Accept(this);

			foreach (var t in n.Contracts)
				t.Accept(this);

			n.Body?.Accept(this);
		}

		public virtual void Visit(DClassLike n)
		{
			VisitBlock(n);

			foreach (var bc in n.BaseClasses)
				bc.Accept(this);
		}

		public virtual void Visit(DEnum n)
		{
			VisitBlock(n);
		}

		public virtual void Visit(DModule n)
		{
			VisitBlock(n);
		}

		public virtual void Visit(TemplateParameter.Node n)
		{
			VisitDNode(n);

			n.TemplateParameter.Accept(this);
		}
		
		public virtual void Visit(NamedTemplateMixinNode n)
		{
			//VisitDNode(n);

			n.Mixin?.Accept(this);
		}

		public virtual void Visit(ModuleAliasNode n)
		{
			
		}

		public virtual void Visit(ImportSymbolNode n)
		{

		}

		public virtual void Visit(ImportSymbolAlias n)
		{

		}

		public virtual void VisitDNode(DNode n)
		{
			if (n.TemplateParameters != null)
				foreach (var tp in n.TemplateParameters)
					tp.Accept(this);

			n.TemplateConstraint?.Accept(this);

			if (n.Attributes != null && n.Attributes.Count != 0)
				foreach (var attr in n.Attributes)
					attr.Accept(this);

			n.Type?.Accept(this);
		}

		public virtual void VisitAttribute(Modifier attribute) { }

		public virtual void VisitAttribute(PragmaAttribute pragma)
		{
			if (pragma.Arguments != null && pragma.Arguments.Length != 0)
				foreach (var arg in pragma.Arguments)
					arg.Accept(this);
		}

		public virtual void VisitAttribute(VersionCondition vis)
		{
			
		}

		public virtual void VisitAttribute(DebugCondition debugCondition)
		{
			
		}
		
		public virtual void VisitAttribute(DeprecatedAttribute a)
		{
			
		}
		
		public virtual void VisitAttribute(BuiltInAtAttribute a)
		{
			
		}
		
		public virtual void VisitAttribute(UserDeclarationAttribute a)
		{
			if(a.AttributeExpression != null && a.AttributeExpression.Length != 0)
				foreach(var x in a.AttributeExpression)
				{
					x?.Accept(this);
				}
		}
		
		public virtual void VisitAttribute(StaticIfCondition a)
		{
			a.Expression?.Accept(this);
		}
		
		public virtual void VisitAttribute(NegatedDeclarationCondition a)
		{
			a.FirstCondition.Accept(this);
		}
		#endregion

		#region Statements
		public virtual void VisitSubStatements(StatementContainingStatement stmtContainer)
		{
			var ss = stmtContainer.SubStatements;
			if (ss != null)
				foreach (IStatement substatement in ss)
				{
					substatement?.Accept(this);
				}
		}

		/// <summary>
		/// Visit abstract stmt
		/// </summary>
		public virtual void VisitChildren(StatementContainingStatement stmtContainer)
		{
			VisitSubStatements(stmtContainer);
			VisitAbstractStmt(stmtContainer);
		}

		public virtual void Visit(ModuleStatement s)
		{
			VisitAbstractStmt(s);
			s.ModuleName?.Accept(this);
		}

		public virtual void Visit(ImportStatement s)
		{
			if (s.Attributes != null)
				foreach (var attr in s.Attributes)
				{
					attr?.Accept (this);
				}

			if (s.Imports != null)
				foreach (var imp in s.Imports)
				{
					imp?.Accept (this);
				}

			s.ImportBindList?.Accept (this);

			VisitAbstractStmt(s);
		}

		public virtual void VisitImport (ImportStatement.Import i)
		{
			i.ModuleAlias?.Accept (this);
			i.ModuleIdentifier?.Accept (this);
		}

		public virtual void VisitImport (ImportStatement.ImportBinding i)
		{
			i.Alias?.Accept (this);
			i.Symbol?.Accept (this);
		}

		public virtual void VisitImport (ImportStatement.ImportBindings i)
		{
			i.Module?.Accept (this);

			if(i.SelectedSymbols != null)
				foreach (var imp in i.SelectedSymbols)
				{
					imp?.Accept (this);
				}
		}

		public virtual void Visit(BlockStatement s)
		{
			VisitChildren(s);
		}

		public virtual void Visit(LabeledStatement s)
		{
			VisitAbstractStmt(s);
		}

		public virtual void Visit(IfStatement s)
		{
			VisitChildren(s);

			s.IfCondition?.Accept(this);

			//TODO: Are the declarations also in the statements?
			s.IfVariable?.Accept(this);
		}

		public virtual void Visit(WhileStatement s)
		{
			VisitChildren(s);

			s.Condition?.Accept(this);
		}

		public virtual void Visit(ForStatement s)
		{
			// Also visits 'Initialize'
			VisitChildren(s);

			s.Test?.Accept(this);
			s.Increment?.Accept(this);
		}

		public virtual void Visit(ForeachStatement s)
		{
			VisitChildren (s);

			if (s.ForeachTypeList != null)
				foreach (var t in s.ForeachTypeList)
				{
					t?.Accept(this);
				}

			s.Aggregate?.Accept(this);

			s.UpperAggregate?.Accept(this);
		}

		public virtual void Visit(SwitchStatement s)
		{
			VisitChildren(s);

			s.SwitchExpression?.Accept(this);
		}

		public virtual void Visit(SwitchStatement.CaseStatement s)
		{
			VisitChildren(s);

			s.ArgumentList?.Accept(this);
			s.LastExpression?.Accept(this);
		}

		public virtual void Visit(SwitchStatement.DefaultStatement s)
		{
			VisitChildren(s);
		}

		public virtual void Visit(ContinueStatement s)
		{
			VisitAbstractStmt(s);
		}

		public virtual void Visit(BreakStatement s)
		{
			VisitAbstractStmt(s);
		}

		public virtual void Visit(ReturnStatement s)
		{
			VisitAbstractStmt(s);
			s.ReturnExpression?.Accept(this);
		}

		public virtual void Visit(GotoStatement s)
		{
			VisitAbstractStmt(s);
			s.CaseExpression?.Accept(this);
		}

		public virtual void Visit(WithStatement s)
		{
			VisitChildren(s);

			s.WithExpression?.Accept(this);
			s.WithSymbol?.Accept(this);
		}

		public virtual void Visit(SynchronizedStatement s)
		{
			VisitChildren(s);

			s.SyncExpression?.Accept(this);
		}

		public virtual void Visit(TryStatement s)
		{
			VisitChildren(s);
		}

		public virtual void Visit(TryStatement.CatchStatement s)
		{
			VisitChildren(s);

			s.CatchParameter?.Accept(this);
		}

		public virtual void Visit(Statements.TryStatement.FinallyStatement s)
		{
			VisitChildren(s);
		}

		public virtual void Visit(Statements.ThrowStatement s)
		{
			VisitAbstractStmt(s);

			s.ThrowExpression?.Accept(this);
		}

		public virtual void Visit(Statements.ScopeGuardStatement s)
		{
			VisitChildren(s);
		}

		public virtual void VisitAsmStatement(AsmStatement s) { VisitChildren(s); }
		public virtual void VisitAsmRawDataStatement(AsmRawDataStatement s) { VisitAbstractStmt(s); }
		public virtual void VisitAsmInstructionStatement(AsmInstructionStatement s) 
		{
			VisitAbstractStmt(s);
			if (s.Arguments != null)
			{
				foreach (var a in s.Arguments)
					a.Accept(this);
			}
		}
		public virtual void VisitAsmAlignStatement(AsmAlignStatement s) 
		{
			VisitAbstractStmt(s);

			s.ValueExpression?.Accept(this);
		}

		public virtual void Visit(Statements.PragmaStatement s)
		{
			VisitChildren(s);

			s.Pragma.Accept(this);
		}

		public virtual void Visit(Statements.StaticAssertStatement s)
		{
			VisitAbstractStmt(s);

			s.AssertedExpression?.Accept(this);
			s.Message?.Accept(this);
		}

		public virtual void Visit(StatementCondition s)
		{
			if(s!=null)
			{
				s.Condition?.Accept (this);
				VisitChildren(s);
			}
		}

        public virtual void Visit(StaticForeachStatement s)
        {
            Visit(s as ForeachStatement);
        }
       
        public virtual void Visit(Statements.VolatileStatement s)
		{
			VisitChildren(s);
		}

		public virtual void Visit(Statements.ExpressionStatement s)
		{
			VisitAbstractStmt(s);

			s.Expression.Accept(this);
		}

		public virtual void Visit(Statements.ContractStatement s)
		{
			VisitAbstractStmt(s);

			s.Condition?.Accept(this);
			s.Message?.Accept(this);
			s.ScopedStatement?.Accept(this);

			s.OutResultVariable?.Accept(this);
		}

		public virtual void Visit(Statements.DeclarationStatement s)
		{
			VisitAbstractStmt(s);

			if (s.Declarations != null)
				foreach (var decl in s.Declarations)
				{
					decl?.Accept(this);
				}
		}

		public virtual void Visit(Statements.TemplateMixin s)
		{
			VisitAbstractStmt(s);

			s.Qualifier?.Accept(this);
		}

		public virtual void Visit(Statements.VersionSpecification s)
		{
			VisitAbstractStmt(s);
		}

		public virtual void Visit(Statements.DebugSpecification s)
		{
			VisitAbstractStmt(s);
		}
		
		public virtual void VisitMixinStatement(MixinStatement s)
		{
			VisitAbstractStmt (s);
			if(s.Attributes!=null && s.Attributes.Length != 0)
				foreach(var attr in s.Attributes)
				{
					attr?.Accept(this);
				}

			s.MixinExpression?.Accept(this);
		}

		public virtual void VisitAbstractStmt(AbstractStatement stmt)
		{
			
		}
		#endregion

		#region Expressions
		public virtual void VisitChildren(ContainerExpression x)
		{
			if(x.SubExpressions != null)
				foreach (var sx in x.SubExpressions)
				{
					sx?.Accept(this);
				}
		}

		public virtual void VisitOpBasedExpression(OperatorBasedExpression ox)
		{
			VisitChildren(ox);
		}

		public virtual void Visit(Expression x)
		{
			VisitChildren(x);
		}

		public virtual void Visit(Expressions.AssignExpression x)
		{
			VisitOpBasedExpression(x);
		}

		public virtual void Visit(Expressions.ConditionalExpression x)
		{
			VisitChildren(x);
		}

		public virtual void Visit(Expressions.OrOrExpression x)
		{
			VisitOpBasedExpression(x);
		}

		public virtual void Visit(Expressions.AndAndExpression x)
		{
			VisitOpBasedExpression(x);
		}

		public virtual void Visit(Expressions.XorExpression x)
		{
			VisitOpBasedExpression(x);
		}

		public virtual void Visit(Expressions.OrExpression x)
		{
			VisitOpBasedExpression(x);
		}

		public virtual void Visit(Expressions.AndExpression x)
		{
			VisitOpBasedExpression(x);
		}

		public virtual void Visit(Expressions.EqualExpression x)
		{
			VisitOpBasedExpression(x);
		}

		public virtual void Visit(Expressions.IdentityExpression x)
		{
			VisitOpBasedExpression(x);
		}

		public virtual void Visit(Expressions.RelExpression x)
		{
			VisitOpBasedExpression(x);
		}

		public virtual void Visit(Expressions.InExpression x)
		{
			VisitOpBasedExpression(x);
		}

		public virtual void Visit(Expressions.ShiftExpression x)
		{
			VisitOpBasedExpression(x);
		}

		public virtual void Visit(Expressions.AddExpression x)
		{
			VisitOpBasedExpression(x);
		}

		public virtual void Visit(Expressions.MulExpression x)
		{
			VisitOpBasedExpression(x);
		}

		public virtual void Visit(Expressions.CatExpression x)
		{
			VisitOpBasedExpression(x);
		}

		public virtual void Visit(Expressions.PowExpression x)
		{
			VisitOpBasedExpression(x);
		}

		public virtual void Visit(Expressions.UnaryExpression_And x)
		{
			VisitChildren(x);
		}

		public virtual void Visit(Expressions.UnaryExpression_Increment x)
		{
			VisitChildren(x);
		}

		public virtual void Visit(Expressions.UnaryExpression_Decrement x)
		{
			VisitChildren(x);
		}

		public virtual void Visit(Expressions.UnaryExpression_Mul x)
		{
			VisitChildren(x);
		}

		public virtual void Visit(Expressions.UnaryExpression_Add x)
		{
			VisitChildren(x);
		}

		public virtual void Visit(Expressions.UnaryExpression_Sub x)
		{
			VisitChildren(x);
		}

		public virtual void Visit(Expressions.UnaryExpression_Not x)
		{
			VisitChildren(x);
		}

		public virtual void Visit(Expressions.UnaryExpression_Cat x)
		{
			VisitChildren(x);
		}

		public virtual void Visit(Expressions.UnaryExpression_Type x)
		{
			x.Type?.Accept(this);
		}

		public virtual void Visit(Expressions.NewExpression x)
		{
			VisitChildren(x);
			if (x?.Type != null && !(x.Type is IExpression))
				x.Type.Accept (this);
		}

		public virtual void Visit(Expressions.AnonymousClassExpression x)
		{
			VisitChildren(x);

			x.AnonymousClass?.Accept(this);
		}

		public virtual void Visit(Expressions.DeleteExpression x)
		{
			VisitChildren(x);
		}

		public virtual void Visit(Expressions.CastExpression x)
		{
			x.UnaryExpression?.Accept(this);

			x.Type?.Accept(this);
		}

		public virtual void VisitPostfixExpression(PostfixExpression x)
		{
			x.PostfixForeExpression?.Accept(this);
		}

		public virtual void Visit(Expressions.PostfixExpression_Access x)
		{
			VisitPostfixExpression(x);

			x.AccessExpression?.Accept(this);
		}

		public virtual void Visit(Expressions.PostfixExpression_Increment x)
		{
			VisitPostfixExpression(x);
		}

		public virtual void Visit(Expressions.PostfixExpression_Decrement x)
		{
			VisitPostfixExpression(x);
		}

		public virtual void Visit(Expressions.PostfixExpression_MethodCall x)
		{
			VisitPostfixExpression(x);

			if (x.ArgumentCount != 0)
				foreach (var arg in x.Arguments)
				{
					arg?.Accept (this);
				}
		}

		public virtual void Visit(Expressions.PostfixExpression_ArrayAccess x)
		{
			VisitPostfixExpression(x);

			if (x.Arguments != null)
				foreach (var arg in x.Arguments)
					if (arg != null) {
						arg.Expression.Accept (this);
						(arg as PostfixExpression_ArrayAccess.SliceArgument)?.UpperBoundExpression.Accept (this);
					}
		}

		public virtual void Visit(TemplateInstanceExpression x)
		{
			x.Identifier?.Accept(this);

			if (x.Arguments != null)
				foreach (var arg in x.Arguments)
				{
					arg?.Accept(this);
				}
		}

		public virtual void VisitScalarConstantExpression(Expressions.ScalarConstantExpression x) { }
		public void VisitStringLiteralExpression(StringLiteralExpression x) { }
		public virtual void Visit(Expressions.IdentifierExpression x) { }
		public virtual void Visit(Expressions.TokenExpression x) { }

		public virtual void Visit(Expressions.TypeDeclarationExpression x)
		{
			x.Declaration?.Accept(this);
		}

		public virtual void Visit(Expressions.ArrayLiteralExpression x)
		{
			if(x.Elements != null)
				foreach (var e in x.Elements)
				{
					e?.Accept(this);
				}
		}

		public virtual void Visit(Expressions.AssocArrayExpression x)
		{
			if(x.Elements != null)
				foreach (var kv in x.Elements)
				{
					kv.Key?.Accept(this);
					kv.Value?.Accept(this);
				}
		}

		public virtual void Visit(Expressions.FunctionLiteral x)
		{
			x.AnonymousMethod.Accept(this);
		}

		public virtual void Visit(Expressions.AssertExpression x)
		{
			VisitChildren(x);
		}

		public virtual void Visit(Expressions.MixinExpression x)
		{
			x.AssignExpression?.Accept(this);
		}

		public virtual void Visit(Expressions.ImportExpression x)
		{
			x.AssignExpression?.Accept(this);
		}

		public virtual void Visit(Expressions.TypeidExpression x)
		{
			if (x.Type != null)
				x.Type.Accept(this);
			else
			{
				x.Expression?.Accept(this);
			}
		}

		public virtual void Visit(Expressions.IsExpression x)
		{
			x.TestedType?.Accept(this);

			// Do not visit the artificial param..it's not existing

			x.TypeSpecialization?.Accept(this);

			if (x.TemplateParameterList != null)
				foreach (var p in x.TemplateParameterList)
				{
					p?.Accept(this);
				}
		}

		public virtual void Visit(Expressions.TraitsExpression x)
		{
			if (x.Arguments != null)
				foreach (var arg in x.Arguments)
					if(arg != null)
						Visit(arg);
		}

		public virtual void Visit(TraitsArgument arg)
		{
			arg.Type?.Accept(this);
			arg.AssignExpression?.Accept(this);
		}

		public virtual void Visit(Expressions.SurroundingParenthesesExpression x)
		{
			x.Expression?.Accept(this);
		}

		public virtual void Visit(Expressions.VoidInitializer x)
		{
			
		}

		public virtual void Visit(Expressions.ArrayInitializer x)
		{
			Visit((AssocArrayExpression)x);
		}

		public virtual void Visit(Expressions.StructInitializer x)
		{
			if (x.MemberInitializers != null)
				foreach (var i in x.MemberInitializers)
				{
					i?.Accept(this);
				}
		}

		public virtual void Visit(StructMemberInitializer init)
		{
			init.Value?.Accept(this);
		}

		public virtual void Visit(AsmRegisterExpression x)
		{

		}

		public virtual void Visit(UnaryExpression_SegmentBase x)
		{
			x.RegisterExpression?.Accept(this);
			x.UnaryExpression?.Accept(this);
		}
		#endregion

		#region Decls
		public virtual void VisitInner(ITypeDeclaration td)
		{
			td.InnerDeclaration?.Accept(this);
		}

		public virtual void Visit(IdentifierDeclaration td)
		{
			VisitInner(td);
		}

		public virtual void Visit(DTokenDeclaration td)
		{
			VisitInner(td);
		}

		public virtual void Visit(ArrayDecl td)
		{
			VisitInner(td);

			td.KeyType?.Accept(this);

			td.KeyExpression?.Accept(this);

			// ValueType == InnerDeclaration
		}

		public virtual void Visit(DelegateDeclaration td)
		{
			VisitInner(td);
			// ReturnType == InnerDeclaration

			if (td.Modifiers != null && td.Modifiers.Length != 0)
				foreach (var attr in td.Modifiers)
				{
					attr?.Accept(this);
				}

			foreach (var p in td.Parameters)
			{
				p?.Accept(this);
			}
		}

		public virtual void Visit(PointerDecl td)
		{
			VisitInner(td);
		}

		public virtual void Visit(MemberFunctionAttributeDecl td)
		{
			VisitInner(td);

			td.InnerType?.Accept(this);
		}

		public virtual void Visit(TypeOfDeclaration td)
		{
			VisitInner(td);

			td.Expression?.Accept(this);
		}

		public virtual void Visit(TraitsDeclaration td)
		{
			VisitInner(td);

			td.Expression?.Accept(this);
		}

		public virtual void Visit(VectorDeclaration td)
		{
			VisitInner(td);

			td.IdDeclaration?.Accept(this);
			td.Id?.Accept(this);
		}

		public virtual void Visit(VarArgDecl td)
		{
			VisitInner(td);
		}
		#endregion

		#region Meta decl blocks
		public virtual void VisitIMetaBlock(IMetaDeclarationBlock block)
		{

		}

		public virtual void VisitMetaDeclarationBlock(MetaDeclarationBlock m)
		{
			VisitIMetaBlock(m);
		}

		public virtual void VisitAttributeMetaDeclarationBlock(AttributeMetaDeclarationBlock m)
		{
			VisitAttributeMetaDeclaration(m);
			VisitIMetaBlock(m);
		}

		public virtual void VisitAttributeMetaDeclarationSection(AttributeMetaDeclarationSection m)
		{
			VisitAttributeMetaDeclaration(m);
		}

		public virtual void VisitElseMetaDeclarationBlock(ElseMetaDeclarationBlock m)
		{
			VisitElseMetaDeclaration(m);
			VisitIMetaBlock(m);
		}

		public virtual void VisitElseMetaDeclaration(ElseMetaDeclaration m)
		{
			
		}

		public virtual void VisitElseMetaDeclarationSection(ElseMetaDeclarationSection m) {
			VisitElseMetaDeclaration(m);
		}

		public virtual void VisitAttributeMetaDeclaration(AttributeMetaDeclaration md)
		{
			if (md.AttributeOrCondition != null)
				foreach (var attr in md.AttributeOrCondition)
				{
					attr?.Accept(this);
				}

			md.OptionalElseBlock?.Accept(this);
		}
		#endregion

		#region Template parameters
		public virtual void VisitTemplateParameter(TemplateParameter tp) {
		}

		public virtual void Visit(TemplateTypeParameter p)
		{
			VisitTemplateParameter (p);

			p.Specialization?.Accept(this);

			p.Default?.Accept(this);
		}

		public virtual void Visit(TemplateThisParameter p)
		{
			VisitTemplateParameter (p);
			p.FollowParameter?.Accept(this);
		}

		public virtual void Visit(TemplateValueParameter p)
		{
			VisitTemplateParameter (p);
			p.Type?.Accept(this);

			p.SpecializationExpression?.Accept(this);
			p.DefaultExpression?.Accept(this);
		}

		public virtual void Visit(TemplateAliasParameter p)
		{
			Visit((TemplateValueParameter)p);

			p.SpecializationType?.Accept(this);
			p.DefaultType?.Accept(this);
		}

		public virtual void Visit(TemplateTupleParameter p)
		{
			VisitTemplateParameter (p);
		}
		#endregion
	}
}
