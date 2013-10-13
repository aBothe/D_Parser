using System;
using System.Collections.Generic;
using System.Text;

using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public partial class Evaluation
	{		
		ISemantic E(TraitsExpression te)
		{
			switch(te.Keyword)
			{
				case "":
				case null:
					return null;
					
				case "hasMember":
					if(!eval)
						return new PrimitiveType(DTokens.Bool, 0);
					
					bool ret = false;
					var optionsBackup = ctxt.ContextIndependentOptions;
					ctxt.ContextIndependentOptions = ResolutionOptions.IgnoreAllProtectionAttributes;
					
					AbstractType t;
					var pfa = prepareMemberTraitExpression(te, out t);
					
					if(pfa != null && t != null)
					{
						ignoreErrors = true;
						eval = false;
						ret = E(pfa, t, false) != null;
						eval = true;
						ignoreErrors = false;
					}
					ctxt.ContextIndependentOptions = optionsBackup;
					return new PrimitiveValue(ret, te);
					
					
				case "identifier":
					if(!eval)
						return GetStringType();
					
					if(te.Arguments!=null && te.Arguments.Length == 1)
						return new ArrayValue(GetStringType(), te.Arguments[0].ToString());
					break;
					
					
				case "getMember":
					pfa = prepareMemberTraitExpression(te, out t);
					
					if(pfa == null ||t == null)
						break;
					
					var vs = E(pfa,t);
					if(vs == null || vs.Length == 0)
						return null;
					return vs[0];
					
					
				case "getOverloads":
					optionsBackup = ctxt.ContextIndependentOptions;
					ctxt.ContextIndependentOptions = ResolutionOptions.IgnoreAllProtectionAttributes;
					
					pfa = prepareMemberTraitExpression(te, out t);
					
					if(pfa != null  && t != null)
					{
						var evalBak = eval;
						eval = false;
						vs = E(pfa,t);
						eval = evalBak;
					}
					else
						vs = null;
					
					ctxt.ContextIndependentOptions = optionsBackup;
					
					return eval ? new TypeValue(new DTuple(te, vs)) as ISemantic : new DTuple(te, vs);
					
					
				case "getProtection":
					if(!eval)
						return GetStringType();
					
					optionsBackup = ctxt.ContextIndependentOptions;
					ctxt.ContextIndependentOptions = ResolutionOptions.IgnoreAllProtectionAttributes;
					
					var prot = "public";
					
					if(te.Arguments == null || te.Arguments.Length != 1 || te.Arguments[0] == null)
						EvalError(te, "First trait argument must be a symbol identifier");
					else
					{
						t = E(te.Arguments[0], te);
						
						if(t is DSymbol)
						{
							var dn = (t as DSymbol).Definition;
							
							if(dn.ContainsAttribute(DTokens.Private))
								prot = "private";
							else if(dn.ContainsAttribute(DTokens.Protected))
								prot = "protected";
							else if(dn.ContainsAttribute(DTokens.Package))
								prot = "package";
							else if(dn.ContainsAttribute(DTokens.Export))
								prot = "export";
						}
						else
							EvalError(te, "First argument must evaluate to an existing code symbol");
					}
					
					ctxt.ContextIndependentOptions = optionsBackup;
					return new ArrayValue(GetStringType(), prot);					
					
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
					if(!eval)
						return new PrimitiveType(DTokens.Bool);
					
					ret = false;
					
					if(te.Arguments == null || te.Arguments.Length < 2)
					{
						EvalError(te, "isSame requires two arguments to compare");
					}
					else
					{
						t = E(te.Arguments[0], te);
						
						if(t != null)
						{
							var t2 = E(te.Arguments[1], te);
							
							if(t2 != null)
								ret = Resolver.ResultComparer.IsEqual(t,t2);
						}
					}
					
					return new PrimitiveValue(ret, te);
					
				case "compiles":
					if(!eval)
						return new PrimitiveType(DTokens.Bool);
					
					ret = false;
					
					if(te.Arguments != null){
						foreach(var arg in te.Arguments)
						{
							ret = E(arg, te) != null;
							
							if(!ret)
								break;
						}
					}
						
					return new PrimitiveValue(ret, te);
			}
			
			#region isXYZ-traits
			if(te.Keyword.StartsWith("is"))
			{
				if(eval)
				{
					var optionsBackup = ctxt.ContextIndependentOptions;
					ctxt.ContextIndependentOptions = ResolutionOptions.IgnoreAllProtectionAttributes;
					bool ret = false;
					
					if(te.Arguments != null)
					foreach(var arg in te.Arguments)
					{
						var t = E(arg,te);
						
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
									ret = !dm.ContainsAttribute(includeFinalNonOverridingMethods ? (byte)0 : DTokens.Final, DTokens.Static);
								}
								break;
							case "isAbstractFunction":
								ms = t as MemberSymbol;

								ret = ms!=null && 
									ms.Definition is DMethod &&
									ms.Definition.ContainsAttribute(DTokens.Abstract);
								break;
							case "isFinalFunction":
								ms = t as MemberSymbol;
								
								if( ms!=null && ms.Definition is DMethod)
								{
									ret = ms.Definition.ContainsAttribute(DTokens.Abstract) ||
										(ms.Definition.Parent is DClassLike && (ms.Definition.Parent as DClassLike).ContainsAttribute(DTokens.Final));
								}
								break;
							case "isStaticFunction":
								ms = t as MemberSymbol;

								ret = ms!=null && ms.Definition is DMethod && ms.Definition.IsStatic;
								break;
								
							case "isRef":
								ret = t is MemberSymbol && (t as MemberSymbol).Definition.ContainsAttribute(DTokens.Ref);
								break;
							case "isOut":
								ret = t is MemberSymbol && (t as MemberSymbol).Definition.ContainsAttribute(DTokens.Out);
								break;
							case "isLazy":
								ret = t is MemberSymbol && (t as MemberSymbol).Definition.ContainsAttribute(DTokens.Lazy);
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
										DTokens.BasicTypes_Integral[pt.TypeToken] || 
										DTokens.BasicTypes_FloatingPoint[pt.TypeToken]);
									break;
								case "isFloating":
									pt = t as PrimitiveType;
									ret = pt != null && DTokens.BasicTypes_FloatingPoint[pt.TypeToken];
									break;
								case "isIntegral":
									pt = t as PrimitiveType;
									ret = pt != null && DTokens.BasicTypes_Integral[pt.TypeToken];
									break;
								case "isScalar":
									pt = t as PrimitiveType;
									ret = pt != null && DTokens.BasicTypes[pt.TypeToken];
									break;
								case "isUnsigned":
									pt = t as PrimitiveType;
									ret = pt != null && DTokens.BasicTypes_Unsigned[pt.TypeToken];
									break;
									
								case "isAbstractClass":
									ret = t is ClassType && (t as ClassType).Definition.ContainsAttribute(DTokens.Abstract);
									break;
								case "isFinalClass":
									ret = t is ClassType && (t as ClassType).Definition.ContainsAttribute(DTokens.Final);
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
					return new PrimitiveValue(ret, te);
				}
				else
					return new PrimitiveType(DTokens.Bool, 0, te);
			}
			else
			{
				if(eval)
					EvalError(te, "Illegal trait token");
				return null;
			}
			#endregion
		}
		
		/// <summary>
		/// Used when evaluating traits.
		/// Evaluates the first argument to <param name="t">t</param>, 
		/// takes the second traits argument, tries to evaluate it to a string, and puts it + the first arg into an postfix_access expression
		/// </summary>
		PostfixExpression_Access prepareMemberTraitExpression(TraitsExpression te,out AbstractType t)
		{
			if(te.Arguments != null && te.Arguments.Length == 2)
			{
				var tEx = te.Arguments[0];
				t = DResolver.StripMemberSymbols(E(tEx,te));
				
				if(t == null)
					EvalError(te, "First argument didn't resolve to a type");
				else if(te.Arguments[1].AssignExpression != null)
				{
					var litEx = te.Arguments[1].AssignExpression;
					var eval_Backup = eval;
					eval = true;
					var v = E(litEx);
					eval = eval_Backup;
					
					if(v is ArrayValue && (v as ArrayValue).IsString)
					{
						var av = v as ArrayValue;
						
						// Mock up a postfix_access expression to ensure static properties & ufcs methods are checked either
						return new PostfixExpression_Access{ 
							PostfixForeExpression = tEx.AssignExpression ?? new TypeDeclarationExpression(tEx.Type),
							AccessExpression = new IdentifierExpression(av.StringValue) {
								Location = litEx.Location, 
								EndLocation = litEx.EndLocation},
							EndLocation = litEx.EndLocation
						};
					}
					else
						EvalError(te.Arguments[1].AssignExpression, "Second traits argument must evaluate to a string literal", v);
				}
				else
					EvalError(te, "Second traits argument must be an expression");
			}
			
			t = null;
			return null;
		}
		
		AbstractType E(TraitsArgument arg, TraitsExpression te)
		{
			if(arg.Type != null)
			{
				return TypeDeclarationResolver.ResolveSingle(arg.Type, ctxt);
			}
			else if(arg.AssignExpression != null)
			{
				return DResolver.StripAliasSymbol(EvaluateType(arg.AssignExpression, ctxt));
			}
			else
			{
				EvalError(te, "Argument must be a type or an expression!");
				return null;
			}
		}
	}
}
