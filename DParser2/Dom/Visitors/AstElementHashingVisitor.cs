using D_Parser.Resolver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Misc;
using D_Parser.Resolver.ExpressionSemantics;

namespace D_Parser.Dom.Visitors
{
	public class AstElementHashingVisitor : DVisitor<long>, IResolvedTypeVisitor<long>
	{
		const long prime = 31;
		public static readonly AstElementHashingVisitor Instance = new AstElementHashingVisitor();

		public static long Hash(ISemantic s)
		{
			return Instance.Accept(s);
		}

		public static long Hash(ISyntaxRegion sr)
		{
			return Instance.Accept(sr);
		}

		private AstElementHashingVisitor() { }

		public long Accept(ISyntaxRegion sr)
		{
			if (sr is INode)
				return (sr as INode).Accept(this);
			if (sr is IExpression)
			{
				return (long)sr.ToString().GetHashCode(); // Temporary bypass
				//return (sr as IExpression).Accept(this);
			}
			if (sr is IStatement)
				return (sr as IStatement).Accept(this);
			if (sr is ITypeDeclaration)
				return (sr as ITypeDeclaration).Accept(this);
			if (sr is DAttribute)
				return (sr as DAttribute).Accept(this);
			if (sr is TemplateParameter)
				return (sr as TemplateParameter).Accept (this);

			throw new InvalidOperationException();
		}

		public long Accept(ISemantic s)
		{
			if (s is AbstractType)
				return (s as AbstractType).Accept(this);
			if (s is ISymbolValue)
			{
				return (long)s.ToCode().GetHashCode(); // Temp. bypass
			}

			throw new InvalidOperationException();
		}

		long VisitAbstractNode(AbstractNode n)
		{
			unchecked{
				long h = 1;

				// h = prime * h + n.Location.GetHashCode (); // Ignore physical occurrence
				h = prime * h + (n.Type != null ? n.Type.Accept (this) : 0);
				h = prime * h + (n.NameHash ^ (n.NameHash>>32));
				h = prime * h + DNode.GetNodePath (n, false).GetHashCode ();

				return h;
			}
		}

		void HashEnum<T>(ref long h, IEnumerable<T> l, bool mindOrder = false) where T:ISyntaxRegion
		{
			unchecked{
				h *= prime;
				if (l != null) {
					int len = 0;
					foreach (var i in l) {
						if (mindOrder)
							h = prime * h + len;
						h = prime * h + Accept (i);
						len++;
					}

					h = prime * h + len;
				}
			}
		}

		long VisitDNode(DNode n)
		{
			long h = VisitAbstractNode(n);

			unchecked{
				HashEnum (ref h, n.TemplateParameters);
				h = prime * h + (n.TemplateConstraint != null ? n.TemplateConstraint.Accept (this) : 0);
				HashEnum (ref h, n.Attributes);
			}

			return h;
		}
				

		public long Visit(DEnumValue n)
		{
			return unchecked(100003 * Visit(n as DVariable));
		}

		public long Visit(DVariable n)
		{
			long h = 100019 * VisitDNode (n);

			unchecked{
				h = prime * h + (n.Initializer != null ? n.Initializer.Accept (this) : 0);
				h = prime * h + (n.IsAlias ? 1 : 0);
				h = prime * h + (n.IsAliasThis ? 1 : 0);
			}

			return h;
		}

		public long Visit(DMethod n)
		{
			long h = VisitDNode (n);
			const long prime = 1000037;

			unchecked{
				h = prime * h + (long)n.SpecialType;
				HashEnum (ref h, n.Parameters);

				// Don't care about its definition yet
			}

			return h;
		}

		long VisitBlockNode(DBlockNode n)
		{
			var h = VisitDNode (n);
			const long prime = 1000117;

			unchecked{
				HashEnum (ref h, n.Children);
				HashEnum (ref h, n.StaticStatements);
			}

			return h;
		}

		public long Visit(DClassLike n)
		{
			var h = VisitBlockNode (n);
			const long prime = 1000039;

			unchecked{
				h = prime * h + (n.IsAnonymousClass ? 1 : 0);
				HashEnum (ref h, n.BaseClasses);
				h = prime * h + n.ClassType;
			}

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

			unchecked{
				h *= prime;
				if (n.ModuleName != null)
					h += n.ModuleName.GetHashCode ();
				h *= prime;
				if (n.FileName != null)
					h += n.FileName.GetHashCode ();

				h = prime * h + (n.OptionalModuleStatement != null ? n.OptionalModuleStatement.Accept (this) : 0);
			}

			return h;
		}

		public long Visit(DBlockNode n)
		{
			return VisitBlockNode(n);
		}

		public long Visit(TemplateParameter.Node n)
		{
			return unchecked(1000121 * n.TemplateParameter.Accept(this));
		}

		public long Visit(NamedTemplateMixinNode n)
		{
			return unchecked(1000133 * n.Mixin.Accept(this));
		}

		public long VisitAttribute(Modifier attr)
		{
			return unchecked(1000151 * attr.Token * (attr.ContentHash == 0 ? (attr.LiteralContent != null ? attr.LiteralContent.GetHashCode() : 1) : attr.ContentHash));
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
			long r = 1000187;

			HashEnum (ref r, a.AttributeExpression);

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
			return 1000211 + (a.Expression != null ? a.Expression.Accept(this) : 0);
		}

		public long VisitAttribute(NegatedDeclarationCondition a)
		{
			return unchecked(1000213 * Accept(a.FirstCondition));
		}

		public long Visit(EponymousTemplate ep)
		{
			return unchecked(1000231 * Visit((DVariable)ep));
		}

		public long Visit(ModuleAliasNode n)
		{
			long h = Visit ((ImportSymbolNode)n);
			const long prime = 1000249;

			unchecked{
				h = prime * h + (n.Import != null ? n.Import.Accept (this) : 0);			
			}

			return h;
		}

		public long Visit(ImportSymbolNode n)
		{
			long h = Visit ((DVariable)n);
			const long prime = 1000253;

			unchecked{
				h = prime * h + (n.ImportStatement != null ? n.ImportStatement.Accept (this) : 0);			
			}

			return h;
		}

		public long Visit(ImportSymbolAlias n)
		{
			long h = Visit ((ImportSymbolNode)n);
			const long prime = 1000273;

			unchecked{
				h = prime * h + (n.ImportBinding != null ? n.ImportBinding.Accept (this) : 0);			
			}

			return h;
		}

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

		public long Visit(Statements.AsmStatement asmStatement)
		{
			return 1000619;
		}

		public long Visit(Statements.AsmStatement.InstructionStatement instrStatement)
		{
			return 1000621;
		}

		public long Visit(Statements.AsmStatement.RawDataStatement dataStatement)
		{
			return 1000639;
		}

		public long Visit(Statements.AsmStatement.AlignStatement alignStatement)
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

		public long VisitMixinStatement(Statements.MixinStatement s)
		{
			return 1000829;
		}

		public long Visit(Expressions.Expression x)
		{
			return 1000847;
		}

		long VisitOpBasedExpression(OperatorBasedExpression op)
		{
			long hashCode = prime;
			unchecked
			{
				if (op.LeftOperand != null)
					hashCode += prime * op.LeftOperand.Accept(this);
				hashCode += prime * hashCode + op.OperatorToken;
				hashCode = prime * hashCode + (op.RightOperand != null ? op.RightOperand.Accept (this) : 0);
			}
			return hashCode;
		}

		public long Visit(Expressions.AssignExpression x)
		{
			return 1000849;
		}

		public long Visit(Expressions.ConditionalExpression x)
		{
			return 1000859;
		}

		public long Visit(Expressions.OrOrExpression x)
		{
			return 1000861;
		}

		public long Visit(Expressions.AndAndExpression x)
		{
			return 1000889;
		}

		public long Visit(Expressions.XorExpression x)
		{
			return 1000907;
		}

		public long Visit(Expressions.OrExpression x)
		{
			return 1000919;
		}

		public long Visit(Expressions.AndExpression x)
		{
			return 1000921;
		}

		public long Visit(Expressions.EqualExpression x)
		{
			return 1000931;
		}

		public long Visit(Expressions.IdentityExpression x)
		{
			return 1000969;
		}

		public long Visit(Expressions.RelExpression x)
		{
			return 1000973;
		}

		public long Visit(Expressions.InExpression x)
		{
			return 1000981 + (x.Not ? 1 : 0);
		}

		public long Visit(Expressions.ShiftExpression x)
		{
			return 1000999;
		}

		public long Visit(Expressions.AddExpression x)
		{
			return 1001003;
		}

		public long Visit(Expressions.MulExpression x)
		{
			return 1001017;
		}

		public long Visit(Expressions.CatExpression x)
		{
			return 1001023;
		}

		public long Visit(Expressions.PowExpression x)
		{
			return 1001027;
		}

		public long Visit(Expressions.UnaryExpression_And x)
		{
			return 1001041;
		}

		public long Visit(Expressions.UnaryExpression_Increment x)
		{
			return 1001069;
		}

		public long Visit(Expressions.UnaryExpression_Decrement x)
		{
			return 1001081;
		}

		public long Visit(Expressions.UnaryExpression_Mul x)
		{
			return 1001087;
		}

		public long Visit(Expressions.UnaryExpression_Add x)
		{
			return 1001089;
		}

		public long Visit(Expressions.UnaryExpression_Sub x)
		{
			return 1001093;
		}

		public long Visit(Expressions.UnaryExpression_Not x)
		{
			return 1001107;
		}

		public long Visit(Expressions.UnaryExpression_Cat x)
		{
			return 1001123;
		}

		public long Visit(Expressions.UnaryExpression_Type x)
		{
			return 1001153;
		}

		public long Visit(Expressions.NewExpression x)
		{
			return 1001159;
		}

		public long Visit(Expressions.AnonymousClassExpression x)
		{
			return 1001173;
		}

		public long Visit(Expressions.DeleteExpression x)
		{
			return 1001177;
		}

		public long Visit(Expressions.CastExpression x)
		{
			return 1001191;
		}

		public long Visit(Expressions.PostfixExpression_Access x)
		{
			return 1001197;
		}

		public long Visit(Expressions.PostfixExpression_Increment x)
		{
			return 1001219;
		}

		public long Visit(Expressions.PostfixExpression_Decrement x)
		{
			return 1001237;
		}

		public long Visit(Expressions.PostfixExpression_MethodCall x)
		{
			return 1001267;
		}

		public long Visit(Expressions.PostfixExpression_ArrayAccess x)
		{
			return 1001279;
		}

		//TODO: return 1001291; got freed due to obsoletion of postfixexpression_slice

		public long Visit(Expressions.TemplateInstanceExpression x)
		{
			long h = x.InnerDeclaration != null ? x.InnerDeclaration.Accept(this) : 1;
			const long prime = 1001303;

			unchecked
			{
				h = prime * h + (x.Identifier != null ? x.Identifier.GetHashCode () : 0);
				h = prime * h + x.TemplateIdHash;
				HashEnum (ref h, x.Arguments, true);
			}

			return h;
		}

		public long Visit(Expressions.IdentifierExpression x)
		{
			long h = 1;
			const long prime = 1001311;

			unchecked{
				h = prime * h + (x.IsIdentifier ? 1 : 0);
				h = prime * h + x.ValueStringHash;
				h = prime * h + (x.Value != null ? x.Value.GetHashCode () : 0);
			}

			return h;
		}

		public long Visit(Expressions.TokenExpression x)
		{
			return 1001321 + (long)x.Token >> 8;
		}

		public long Visit(Expressions.TypeDeclarationExpression x)
		{
			return 1001323;
		}

		public long Visit(Expressions.ArrayLiteralExpression x)
		{
			return 1001327;
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
			return 1001401;
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
			return 1001501;
		}

		public long Visit(Expressions.UnaryExpression_SegmentBase x)
		{
			return 1001527;
		}

		public long Visit(IdentifierDeclaration identifierDeclaration)
		{
			long h = 1;
			const long prime = 1001531;

			unchecked{
				h = prime * h + (identifierDeclaration.ModuleScoped ? 2 : 1);
				h = prime * h + identifierDeclaration.IdHash;
			}

			return h;
		}

		public long Visit(DTokenDeclaration dTokenDeclaration)
		{
			return 1001549 + dTokenDeclaration.Token;
		}

		public long Visit(ArrayDecl arrayDecl)
		{
			return 1001551;
		}

		public long Visit(DelegateDeclaration delegateDeclaration)
		{
			return 1001563;
		}

		public long Visit(PointerDecl pointerDecl)
		{
			return 1001569;
		}

		public long Visit(MemberFunctionAttributeDecl memberFunctionAttributeDecl)
		{
			return 1001587;
		}

		public long Visit(TypeOfDeclaration typeOfDeclaration)
		{
			return 1001593;
		}

		public long Visit(VectorDeclaration vectorDeclaration)
		{
			return 1001621;
		}

		public long Visit(VarArgDecl varArgDecl)
		{
			return 1001629;
		}

		public long Visit(ITemplateParameterDeclaration iTemplateParameterDeclaration)
		{
			return 1001639;
		}

		public long VisitPrimitiveType(PrimitiveType t)
		{
			return (long)(1001659 << 8) + Primes.PrimeNumbers[t.TypeToken];
		}

		public long VisitPointerType(PointerType t)
		{
			return 1001669;
		}

		public long VisitArrayType(ArrayType t)
		{
			return 1001683;
		}

		public long VisitAssocArrayType(AssocArrayType t)
		{
			return 1001687;
		}

		public long VisitDelegateCallSymbol(DelegateCallSymbol t)
		{
			return 1001713;
		}

		public long VisitDelegateType(DelegateType t)
		{
			return 1001723;
		}

		public long VisitAliasedType(AliasedType t)
		{
			return 1001743;
		}

		public long VisitEnumType(EnumType t)
		{
			return 1001783;
		}

		public long VisitStructType(StructType t)
		{
			return 1001797;
		}

		public long VisitUnionType(UnionType t)
		{
			return 1001801;
		}

		public long VisitClassType(ClassType t)
		{
			return 1001807;
		}

		public long VisitInterfaceType(InterfaceType t)
		{
			return 1001809;
		}

		public long VisitTemplateType(TemplateType t)
		{
			return 1001821;
		}

		public long VisitMixinTemplateType(MixinTemplateType t)
		{
			return 1001831;
		}

		public long VisitEponymousTemplateType(EponymousTemplateType t)
		{
			return 1001839;
		}

		public long VisitStaticProperty(StaticProperty t)
		{
			return 1001911;
		}

		public long VisitMemberSymbol(MemberSymbol t)
		{
			return 1001933;
		}

		public long VisitTemplateParameterSymbol(TemplateParameterSymbol tps)
		{
			return 1001941 * ((long)tps.Parameter.GetHashCode() +
							(tps.Base != null ? (long)tps.Base.ToCode(false).GetHashCode() : 0) +
							(tps.ParameterValue != null ? (long)tps.ParameterValue.ToCode().GetHashCode() : 0));
		}

		public long VisitArrayAccessSymbol(ArrayAccessSymbol t)
		{
			return 1001947;
		}

		public long VisitModuleSymbol(ModuleSymbol t)
		{
			return 1001953;
		}

		public long VisitPackageSymbol(PackageSymbol t)
		{
			return 1001977;
		}

		public long VisitDTuple(DTuple t)
		{
			return 1001981;
		}

		public long VisitUnknownType(UnknownType t)
		{
			return 1001983;
		}

		public long VisitAmbigousType(AmbiguousType t)
		{
			return 1001989;
		}

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

		public long VisitTemplateParameter(TemplateParameter tp)
		{
			return 1002091;
		}

		public long Visit(TemplateTypeParameter templateTypeParameter)
		{
			return 1002101;
		}

		public long Visit(TemplateThisParameter templateThisParameter)
		{
			return 1002109;
		}

		public long Visit(TemplateValueParameter p)
		{
			return 1002121 + p.Representation.Accept(this);
		}

		public long Visit(TemplateAliasParameter templateAliasParameter)
		{
			return 1002143;
		}

		public long Visit(TemplateTupleParameter templateTupleParameter)
		{
			return 1002149;
		}

		// next prime would be 1002173
	}
}
