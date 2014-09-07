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
	public class AstElementHashingVisitor : DVisitor<ulong>, IResolvedTypeVisitor<ulong>
	{
		public static readonly AstElementHashingVisitor Instance = new AstElementHashingVisitor();

		public static ulong Hash(ISemantic s)
		{
			return Instance.Accept(s);
		}

		public static ulong Hash(ISyntaxRegion sr)
		{
			return Instance.Accept(sr);
		}

		private AstElementHashingVisitor() { }

		public ulong Accept(ISyntaxRegion sr)
		{
			if (sr is INode)
				return (sr as INode).Accept(this);
			if (sr is IExpression)
			{
				return (ulong)sr.ToString().GetHashCode(); // Temporary bypass
				//return (sr as IExpression).Accept(this);
			}
			if (sr is IStatement)
				return (sr as IStatement).Accept(this);
			if (sr is ITypeDeclaration)
				return (sr as ITypeDeclaration).Accept(this);
			if (sr is DAttribute)
				return (sr as DAttribute).Accept(this);

			throw new InvalidOperationException();
		}

		public ulong Accept(ISemantic s)
		{
			if (s is AbstractType)
				return (s as AbstractType).Accept(this);
			if (s is ISymbolValue)
			{
				return (ulong)s.ToCode().GetHashCode(); // Temp. bypass
			}

			throw new InvalidOperationException();
		}

		public ulong Visit(DEnumValue dEnumValue)
		{
			return 1000003;
		}

		public ulong Visit(DVariable dVariable)
		{
			return 1000033;
		}

		public ulong Visit(DMethod dMethod)
		{
			return 1000037;
		}

		public ulong Visit(DClassLike dClassLike)
		{
			return 1000039;
		}

		public ulong Visit(DEnum dEnum)
		{
			return 1000081;
		}

		public ulong Visit(DModule dModule)
		{
			return 1000099;
		}

		public ulong Visit(DBlockNode dBlockNode)
		{
			return 1000117;
		}

		public ulong Visit(TemplateParameter.Node templateParameterNode)
		{
			return 1000121;
		}

		public ulong Visit(NamedTemplateMixinNode n)
		{
			return 1000133;
		}

		public ulong VisitAttribute(Modifier attr)
		{
			return 1000151 + Primes.PrimeNumbers[attr.Token] << 32;
		}

		public ulong VisitAttribute(DeprecatedAttribute a)
		{
			return 1000159;
		}

		public ulong VisitAttribute(PragmaAttribute attr)
		{
			return 1000171;
		}

		public ulong VisitAttribute(BuiltInAtAttribute a)
		{
			return 1000183 + Misc.Primes.PrimeNumbers[(int)a.Kind] << 32;
		}

		public ulong VisitAttribute(UserDeclarationAttribute a)
		{
			ulong r = 1000187;

			if(a.AttributeExpression != null)
				for(int i = 0; i < a.AttributeExpression.Length; i++)
					r += (Primes.PrimeNumbers[i] * Accept(a.AttributeExpression[i])) << 32;

			return r;
		}

		public ulong VisitAttribute(VersionCondition a)
		{
			return 1000193 + (a.VersionIdHash == 0 ? a.VersionNumber : (ulong)a.VersionIdHash) << 32;
		}

		public ulong VisitAttribute(DebugCondition a)
		{
			return 1000199 + (a.DebugIdHash == 0 ? a.DebugLevel : (ulong)a.DebugIdHash) << 32;
		}

		public ulong VisitAttribute(StaticIfCondition a)
		{
			return 1000211 + (a.Expression != null ? Accept(a.Expression) << 8 : 0);
		}

		public ulong VisitAttribute(NegatedDeclarationCondition a)
		{
			return 1000213 + Accept(a.FirstCondition) << 1;
		}

		public ulong Visit(EponymousTemplate ep)
		{
			return 1000231;
		}

		public ulong Visit(ModuleAliasNode moduleAliasNode)
		{
			return 1000249;
		}

		public ulong Visit(ImportSymbolNode importSymbolNode)
		{
			return 1000253;
		}

		public ulong Visit(ImportSymbolAlias importSymbolAlias)
		{
			return 1000273;
		}

		public ulong Visit(ModuleStatement moduleStatement)
		{
			return 1000289;
		}

		public ulong Visit(ImportStatement importStatement)
		{
			return 1000291;
		}

		public ulong VisitImport(ImportStatement.Import import)
		{
			return 1000303;
		}

		public ulong VisitImport(ImportStatement.ImportBinding importBinding)
		{
			return 1000313;
		}

		public ulong VisitImport(ImportStatement.ImportBindings bindings)
		{
			return 1000333;
		}

		public ulong Visit(Statements.BlockStatement blockStatement)
		{
			return 1000357;
		}

		public ulong Visit(Statements.LabeledStatement labeledStatement)
		{
			return 1000367;
		}

		public ulong Visit(Statements.IfStatement ifStatement)
		{
			return 1000381;
		}

		public ulong Visit(Statements.WhileStatement whileStatement)
		{
			return 1000393;
		}

		public ulong Visit(Statements.ForStatement forStatement)
		{
			return 1000397;
		}

		public ulong Visit(Statements.ForeachStatement foreachStatement)
		{
			return 1000403;
		}

		public ulong Visit(Statements.SwitchStatement switchStatement)
		{
			return 1000409;
		}

		public ulong Visit(Statements.SwitchStatement.CaseStatement caseStatement)
		{
			return 1000423;
		}

		public ulong Visit(Statements.SwitchStatement.DefaultStatement defaultStatement)
		{
			return 1000427;
		}

		public ulong Visit(Statements.ContinueStatement continueStatement)
		{
			return 1000429;
		}

		public ulong Visit(Statements.BreakStatement breakStatement)
		{
			return 1000453;
		}

		public ulong Visit(Statements.ReturnStatement returnStatement)
		{
			return 1000457;
		}

		public ulong Visit(Statements.GotoStatement gotoStatement)
		{
			return 1000507;
		}

		public ulong Visit(Statements.WithStatement withStatement)
		{
			return 1000537;
		}

		public ulong Visit(Statements.SynchronizedStatement synchronizedStatement)
		{
			return 1000541;
		}

		public ulong Visit(Statements.TryStatement tryStatement)
		{
			return 1000547;
		}

		public ulong Visit(Statements.TryStatement.CatchStatement catchStatement)
		{
			return 1000577;
		}

		public ulong Visit(Statements.TryStatement.FinallyStatement finallyStatement)
		{
			return 1000579;
		}

		public ulong Visit(Statements.ThrowStatement throwStatement)
		{
			return 1000589;
		}

		public ulong Visit(Statements.ScopeGuardStatement scopeGuardStatement)
		{
			return 1000609;
		}

		public ulong Visit(Statements.AsmStatement asmStatement)
		{
			return 1000619;
		}

		public ulong Visit(Statements.AsmStatement.InstructionStatement instrStatement)
		{
			return 1000621;
		}

		public ulong Visit(Statements.AsmStatement.RawDataStatement dataStatement)
		{
			return 1000639;
		}

		public ulong Visit(Statements.AsmStatement.AlignStatement alignStatement)
		{
			return 1000651;
		}

		public ulong Visit(Statements.PragmaStatement pragmaStatement)
		{
			return 1000667;
		}

		public ulong Visit(Statements.AssertStatement assertStatement)
		{
			return 1000669;
		}

		public ulong Visit(Statements.StatementCondition condition)
		{
			return 1000679;
		}

		public ulong Visit(Statements.VolatileStatement volatileStatement)
		{
			return 1000691;
		}

		public ulong Visit(Statements.ExpressionStatement expressionStatement)
		{
			return 1000697;
		}

		public ulong Visit(Statements.DeclarationStatement declarationStatement)
		{
			return 1000721;
		}

		public ulong Visit(Statements.TemplateMixin templateMixin)
		{
			return 1000723;
		}

		public ulong Visit(Statements.DebugSpecification versionSpecification)
		{
			return 1000763;
		}

		public ulong Visit(Statements.VersionSpecification versionSpecification)
		{
			return 1000777;
		}

		public ulong Visit(Statements.StaticAssertStatement s)
		{
			return 1000793;
		}

		public ulong VisitMixinStatement(Statements.MixinStatement s)
		{
			return 1000829;
		}

		public ulong Visit(Expressions.Expression x)
		{
			return 1000847;
		}

		ulong VisitOpBasedExpression(OperatorBasedExpression op)
		{
			ulong hashCode = 0uL;
			unchecked
			{
				if (op.LeftOperand != null)
					hashCode += 1000000007 * op.LeftOperand.Accept(this);
				if (op.RightOperand != null)
					hashCode += 1000000009 * op.RightOperand.Accept(this);
				hashCode += 1000000021 * (ulong)op.OperatorToken;
			}
			return hashCode;
		}

		public ulong Visit(Expressions.AssignExpression x)
		{
			return 1000849;
		}

		public ulong Visit(Expressions.ConditionalExpression x)
		{
			return 1000859;
		}

		public ulong Visit(Expressions.OrOrExpression x)
		{
			return 1000861;
		}

		public ulong Visit(Expressions.AndAndExpression x)
		{
			return 1000889;
		}

		public ulong Visit(Expressions.XorExpression x)
		{
			return 1000907;
		}

		public ulong Visit(Expressions.OrExpression x)
		{
			return 1000919;
		}

		public ulong Visit(Expressions.AndExpression x)
		{
			return 1000921;
		}

		public ulong Visit(Expressions.EqualExpression x)
		{
			return 1000931;
		}

		public ulong Visit(Expressions.IdentityExpression x)
		{
			return 1000969;
		}

		public ulong Visit(Expressions.RelExpression x)
		{
			return 1000973;
		}

		public ulong Visit(Expressions.InExpression x)
		{
			return 1000981 + (x.Not ? 1uL : 0uL);
		}

		public ulong Visit(Expressions.ShiftExpression x)
		{
			return 1000999;
		}

		public ulong Visit(Expressions.AddExpression x)
		{
			return 1001003;
		}

		public ulong Visit(Expressions.MulExpression x)
		{
			return 1001017;
		}

		public ulong Visit(Expressions.CatExpression x)
		{
			return 1001023;
		}

		public ulong Visit(Expressions.PowExpression x)
		{
			return 1001027;
		}

		public ulong Visit(Expressions.UnaryExpression_And x)
		{
			return 1001041;
		}

		public ulong Visit(Expressions.UnaryExpression_Increment x)
		{
			return 1001069;
		}

		public ulong Visit(Expressions.UnaryExpression_Decrement x)
		{
			return 1001081;
		}

		public ulong Visit(Expressions.UnaryExpression_Mul x)
		{
			return 1001087;
		}

		public ulong Visit(Expressions.UnaryExpression_Add x)
		{
			return 1001089;
		}

		public ulong Visit(Expressions.UnaryExpression_Sub x)
		{
			return 1001093;
		}

		public ulong Visit(Expressions.UnaryExpression_Not x)
		{
			return 1001107;
		}

		public ulong Visit(Expressions.UnaryExpression_Cat x)
		{
			return 1001123;
		}

		public ulong Visit(Expressions.UnaryExpression_Type x)
		{
			return 1001153;
		}

		public ulong Visit(Expressions.NewExpression x)
		{
			return 1001159;
		}

		public ulong Visit(Expressions.AnonymousClassExpression x)
		{
			return 1001173;
		}

		public ulong Visit(Expressions.DeleteExpression x)
		{
			return 1001177;
		}

		public ulong Visit(Expressions.CastExpression x)
		{
			return 1001191;
		}

		public ulong Visit(Expressions.PostfixExpression_Access x)
		{
			return 1001197;
		}

		public ulong Visit(Expressions.PostfixExpression_Increment x)
		{
			return 1001219;
		}

		public ulong Visit(Expressions.PostfixExpression_Decrement x)
		{
			return 1001237;
		}

		public ulong Visit(Expressions.PostfixExpression_MethodCall x)
		{
			return 1001267;
		}

		public ulong Visit(Expressions.PostfixExpression_Index x)
		{
			return 1001279;
		}

		public ulong Visit(Expressions.PostfixExpression_Slice x)
		{
			return 1001291;
		}

		public ulong Visit(Expressions.TemplateInstanceExpression x)
		{
			return 1001303;
		}

		public ulong Visit(Expressions.IdentifierExpression x)
		{
			return 1001311 + (x.IsIdentifier ? (ulong)x.ValueStringHash << 32 : (ulong)x.Value.GetHashCode());
		}

		public ulong Visit(Expressions.TokenExpression x)
		{
			return 1001321 + Primes.PrimeNumbers[x.Token] << 32;
		}

		public ulong Visit(Expressions.TypeDeclarationExpression x)
		{
			return 1001323;
		}

		public ulong Visit(Expressions.ArrayLiteralExpression x)
		{
			return 1001327;
		}

		public ulong Visit(Expressions.AssocArrayExpression x)
		{
			return 1001347;
		}

		public ulong Visit(Expressions.FunctionLiteral x)
		{
			return 1001353;
		}

		public ulong Visit(Expressions.AssertExpression x)
		{
			return 1001369;
		}

		public ulong Visit(Expressions.MixinExpression x)
		{
			return 1001381;
		}

		public ulong Visit(Expressions.ImportExpression x)
		{
			return 1001387;
		}

		public ulong Visit(Expressions.TypeidExpression x)
		{
			return 1001389;
		}

		public ulong Visit(Expressions.IsExpression x)
		{
			return 1001401;
		}

		public ulong Visit(Expressions.TraitsExpression x)
		{
			return 1001411;
		}

		public ulong Visit(Expressions.SurroundingParenthesesExpression x)
		{
			return 1001431;
		}

		public ulong Visit(Expressions.VoidInitializer x)
		{
			return 1001447;
		}

		public ulong Visit(Expressions.ArrayInitializer x)
		{
			return 1001459;
		}

		public ulong Visit(Expressions.StructInitializer x)
		{
			return 1001467;
		}

		public ulong Visit(Expressions.StructMemberInitializer structMemberInitializer)
		{
			return 1001491;
		}

		public ulong Visit(Expressions.AsmRegisterExpression x)
		{
			return 1001501;
		}

		public ulong Visit(Expressions.UnaryExpression_SegmentBase x)
		{
			return 1001527;
		}

		public ulong Visit(IdentifierDeclaration identifierDeclaration)
		{
			return 1001531;
		}

		public ulong Visit(DTokenDeclaration dTokenDeclaration)
		{
			return 1001549;
		}

		public ulong Visit(ArrayDecl arrayDecl)
		{
			return 1001551;
		}

		public ulong Visit(DelegateDeclaration delegateDeclaration)
		{
			return 1001563;
		}

		public ulong Visit(PointerDecl pointerDecl)
		{
			return 1001569;
		}

		public ulong Visit(MemberFunctionAttributeDecl memberFunctionAttributeDecl)
		{
			return 1001587;
		}

		public ulong Visit(TypeOfDeclaration typeOfDeclaration)
		{
			return 1001593;
		}

		public ulong Visit(VectorDeclaration vectorDeclaration)
		{
			return 1001621;
		}

		public ulong Visit(VarArgDecl varArgDecl)
		{
			return 1001629;
		}

		public ulong Visit(ITemplateParameterDeclaration iTemplateParameterDeclaration)
		{
			return 1001639;
		}

		public ulong VisitPrimitiveType(PrimitiveType t)
		{
			return 1001659;
		}

		public ulong VisitPointerType(PointerType t)
		{
			return 1001669;
		}

		public ulong VisitArrayType(ArrayType t)
		{
			return 1001683;
		}

		public ulong VisitAssocArrayType(AssocArrayType t)
		{
			return 1001687;
		}

		public ulong VisitDelegateCallSymbol(DelegateCallSymbol t)
		{
			return 1001713;
		}

		public ulong VisitDelegateType(DelegateType t)
		{
			return 1001723;
		}

		public ulong VisitAliasedType(AliasedType t)
		{
			return 1001743;
		}

		public ulong VisitEnumType(EnumType t)
		{
			return 1001783;
		}

		public ulong VisitStructType(StructType t)
		{
			return 1001797;
		}

		public ulong VisitUnionType(UnionType t)
		{
			return 1001801;
		}

		public ulong VisitClassType(ClassType t)
		{
			return 1001807;
		}

		public ulong VisitInterfaceType(InterfaceType t)
		{
			return 1001809;
		}

		public ulong VisitTemplateType(TemplateType t)
		{
			return 1001821;
		}

		public ulong VisitMixinTemplateType(MixinTemplateType t)
		{
			return 1001831;
		}

		public ulong VisitEponymousTemplateType(EponymousTemplateType t)
		{
			return 1001839;
		}

		public ulong VisitStaticProperty(StaticProperty t)
		{
			return 1001911;
		}

		public ulong VisitMemberSymbol(MemberSymbol t)
		{
			return 1001933;
		}

		public ulong VisitTemplateParameterSymbol(TemplateParameterSymbol t)
		{
			return 1001941;
		}

		public ulong VisitArrayAccessSymbol(ArrayAccessSymbol t)
		{
			return 1001947;
		}

		public ulong VisitModuleSymbol(ModuleSymbol t)
		{
			return 1001953;
		}

		public ulong VisitPackageSymbol(PackageSymbol t)
		{
			return 1001977;
		}

		public ulong VisitDTuple(DTuple t)
		{
			return 1001981;
		}

		public ulong VisitUnknownType(UnknownType t)
		{
			return 1001983;
		}

		public ulong VisitAmbigousType(AmbiguousType t)
		{
			return 1001989;
		}

		public ulong VisitMetaDeclarationBlock(MetaDeclarationBlock m)
		{
			return 1002017;
		}

		public ulong VisitAttributeMetaDeclarationBlock(AttributeMetaDeclarationBlock m)
		{
			return 1002049;
		}

		public ulong VisitAttributeMetaDeclarationSection(AttributeMetaDeclarationSection m)
		{
			return 1002061;
		}

		public ulong VisitElseMetaDeclarationBlock(ElseMetaDeclarationBlock m)
		{
			return 1002073;
		}

		public ulong VisitElseMetaDeclarationSection(ElseMetaDeclarationSection m)
		{
			return 1002151;
		}

		public ulong VisitElseMetaDeclaration(ElseMetaDeclaration m)
		{
			return 1002077;
		}

		public ulong VisitAttributeMetaDeclaration(AttributeMetaDeclaration m)
		{
			return 1002083;
		}

		public ulong VisitTemplateParameter(TemplateParameter tp)
		{
			return 1002091;
		}

		public ulong Visit(TemplateTypeParameter templateTypeParameter)
		{
			return 1002101;
		}

		public ulong Visit(TemplateThisParameter templateThisParameter)
		{
			return 1002109;
		}

		public ulong Visit(TemplateValueParameter templateValueParameter)
		{
			return 1002121;
		}

		public ulong Visit(TemplateAliasParameter templateAliasParameter)
		{
			return 1002143;
		}

		public ulong Visit(TemplateTupleParameter templateTupleParameter)
		{
			return 1002149;
		}

		// next prime would be 1002173
	}
}
