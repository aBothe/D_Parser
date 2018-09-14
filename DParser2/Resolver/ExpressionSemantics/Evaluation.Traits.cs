using System;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public partial class Evaluation
	{


		public ISymbolValue Visit(TraitsExpression te)
		{
			switch(te.Keyword)
			{
				case "":
				case null:
					return null;
					
				case "hasMember":
					bool ret = false;
					var optionsBackup = ctxt.ContextIndependentOptions;
					ctxt.ContextIndependentOptions = ResolutionOptions.IgnoreAllProtectionAttributes;
					
					AbstractType t;
					var pfa = ExpressionTypeEvaluation.prepareMemberTraitExpression(ctxt, te, out t, ValueProvider);
					
					if(pfa != null && t != null)
					{
						t.NonStaticAccess = true;
						ignoreErrors = true;
						var res = ExpressionTypeEvaluation.EvaluateType(pfa, ctxt, false);
						ret = res != null;
						ignoreErrors = false;
					}
					ctxt.ContextIndependentOptions = optionsBackup;
					return new PrimitiveValue(ret);
					
					
				case "identifier":
					if(te.Arguments!=null && te.Arguments.Length == 1)
						return new ArrayValue(GetStringLiteralType(), te.Arguments[0].ToString());
					break;
					
					
				case "getMember":
					pfa = ExpressionTypeEvaluation.prepareMemberTraitExpression(ctxt, te, out t, ValueProvider);
					
					if(pfa == null ||t == null)
						break;
					
					var vs = EvalPostfixAccessExpression(this, ctxt, pfa,t, ValueProvider:ValueProvider);
					if(vs == null || vs.Count == 0)
						return null;
					return vs[0];
					
					
				case "getOverloads":
					optionsBackup = ctxt.ContextIndependentOptions;
					ctxt.ContextIndependentOptions = ResolutionOptions.IgnoreAllProtectionAttributes;

					pfa = ExpressionTypeEvaluation.prepareMemberTraitExpression(ctxt, te, out t, ValueProvider);

					if (pfa != null && t != null)
						vs = EvalPostfixAccessExpression(this, ctxt, pfa, t);
					else
						vs = null;
					
					ctxt.ContextIndependentOptions = optionsBackup;
					
					return new TypeValue(new DTuple(vs));
					
					
				case "getProtection":
					optionsBackup = ctxt.ContextIndependentOptions;
					ctxt.ContextIndependentOptions = ResolutionOptions.IgnoreAllProtectionAttributes;
					
					var prot = "public";
					
					if(te.Arguments == null || te.Arguments.Length != 1 || te.Arguments[0] == null)
						EvalError(te, "First trait argument must be a symbol identifier");
					else
					{
						t = ExpressionTypeEvaluation.ResolveTraitArgument(ctxt,te.Arguments[0]);
						
						if(t is DSymbol)
						{
							var dn = (t as DSymbol).Definition;
							
							if(dn.ContainsAnyAttribute(DTokens.Private))
								prot = "private";
							else if(dn.ContainsAnyAttribute(DTokens.Protected))
								prot = "protected";
							else if(dn.ContainsAnyAttribute(DTokens.Package))
								prot = "package";
							else if(dn.ContainsAnyAttribute(DTokens.Export))
								prot = "export";
						}
						else
							EvalError(te, "First argument must evaluate to an existing code symbol");
					}
					
					ctxt.ContextIndependentOptions = optionsBackup;
					return new ArrayValue(GetStringLiteralType(), prot);
					
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
					
				case "isSame":
					ret = false;
					
					if(te.Arguments == null || te.Arguments.Length < 2)
					{
						EvalError(te, "isSame requires two arguments to compare");
					}
					else
					{
						t = ExpressionTypeEvaluation.ResolveTraitArgument(ctxt, te.Arguments[0]);
						
						if(t != null)
						{
							var t2 = ExpressionTypeEvaluation.ResolveTraitArgument(ctxt, te.Arguments[1]);
							
							if(t2 != null)
								ret = Resolver.ResultComparer.IsEqual(t,t2);
						}
					}
					
					return new PrimitiveValue(ret);
					
				case "compiles":
					ret = false;
					
					if(te.Arguments != null){
						foreach(var arg in te.Arguments)
						{
							ret = arg == null || ExpressionTypeEvaluation.ResolveTraitArgument(ctxt, arg) != null;
							
							if(!ret)
								break;
						}
					}
						
					return new PrimitiveValue(ret);
			}
			
			#region isXYZ-traits
			if(te.Keyword.StartsWith("is"))
			{
				var optionsBackup = ctxt.ContextIndependentOptions;
				ctxt.ContextIndependentOptions = ResolutionOptions.IgnoreAllProtectionAttributes;
				ctxt.CurrentContext.ContextDependentOptions |= ResolutionOptions.ReturnMethodReferencesOnly;
				bool ret = false;
					
				if(te.Arguments != null)
				foreach(var arg in te.Arguments)
				{
					var t = ExpressionTypeEvaluation.ResolveTraitArgument(ctxt,arg);
						
					bool tested = true;
						
					switch(te.Keyword)
					{
						case "isVirtualFunction":
						case "isVirtualMethod":
							var ms = t as MemberSymbol;
							if(ms==null || !(ms.Definition is DMethod))
								break;
								
							var dm = ms.Definition as DMethod;
							var dc = dm.Parent as DClassLike;
							if(dc != null && dc.ClassType != DTokens.Struct)
							{
								bool includeFinalNonOverridingMethods = te.Keyword == "isVirtualFunction";
								ret = !dm.ContainsAnyAttribute(includeFinalNonOverridingMethods ? (byte)0 : DTokens.Final, DTokens.Static);
							}
							break;
						case "isAbstractFunction":
							ms = t as MemberSymbol;

							ret = ms!=null && 
								ms.Definition is DMethod &&
								ms.Definition.ContainsAnyAttribute(DTokens.Abstract);
							break;
						case "isFinalFunction":
							ms = t as MemberSymbol;
								
							if( ms!=null && ms.Definition is DMethod)
							{
								ret = ms.Definition.ContainsAnyAttribute(DTokens.Abstract) ||
									(ms.Definition.Parent is DClassLike && (ms.Definition.Parent as DClassLike).ContainsAnyAttribute(DTokens.Final));
							}
							break;
						case "isStaticFunction":
							ms = t as MemberSymbol;

							ret = ms!=null && ms.Definition is DMethod && ms.Definition.IsStatic;
							break;
								
						case "isRef":
							ret = t is MemberSymbol && (t as MemberSymbol).Definition.ContainsAnyAttribute(DTokens.Ref);
							break;
						case "isOut":
							ret = t is MemberSymbol && (t as MemberSymbol).Definition.ContainsAnyAttribute(DTokens.Out);
							break;
						case "isLazy":
							ret = t is MemberSymbol && (t as MemberSymbol).Definition.ContainsAnyAttribute(DTokens.Lazy);
							break;
						default:
							tested = false;
							break;
					}
						
					t = DResolver.StripMemberSymbols(t);
						
					if(!tested)
						switch(te.Keyword)
						{
							case "isArithmetic":
								var pt = t as PrimitiveType;
								ret = pt != null && (
									DTokensSemanticHelpers.IsBasicType_Integral(pt.TypeToken) ||
									DTokensSemanticHelpers.IsBasicType_FloatingPoint(pt.TypeToken));
								break;
							case "isFloating":
								pt = t as PrimitiveType;
								ret = pt != null && DTokensSemanticHelpers.IsBasicType_FloatingPoint(pt.TypeToken);
								break;
							case "isIntegral":
								pt = t as PrimitiveType;
								ret = pt != null && DTokensSemanticHelpers.IsBasicType_Integral(pt.TypeToken);
								break;
							case "isScalar":
								pt = t as PrimitiveType;
								ret = pt != null && DTokensSemanticHelpers.IsBasicType(pt.TypeToken);
								break;
							case "isUnsigned":
								pt = t as PrimitiveType;
								ret = pt != null && DTokensSemanticHelpers.IsBasicType_Unsigned(pt.TypeToken);
								break;
									
							case "isAbstractClass":
								ret = t is ClassType && (t as ClassType).Definition.ContainsAnyAttribute(DTokens.Abstract);
								break;
							case "isFinalClass":
								ret = t is ClassType && (t as ClassType).Definition.ContainsAnyAttribute(DTokens.Final);
								break;
								
							case "isAssociativeArray":
								ret = t is AssocArrayType && !(t is ArrayType);
								break;
							case "isStaticArray":
								ret = t is ArrayType && (t as ArrayType).IsStaticArray;
								break;
						}
						
					if(!ret)
						break;
				}
					
				ctxt.ContextIndependentOptions = optionsBackup;
				return new PrimitiveValue(ret);
			}
			else
			{
				EvalError(te, "Illegal trait token");
				return null;
			}
			#endregion
		}
	}
}
