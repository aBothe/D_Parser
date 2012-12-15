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
				case "hasMember":
					if(!eval)
						return new PrimitiveType(DTokens.Bool, 0);
					
					bool ret = false;
					var optionsBackup = ctxt.ContextIndependentOptions;
					ctxt.ContextIndependentOptions = ResolutionOptions.IgnoreAllProtectionAttributes;
					
					if(te.Arguments != null && te.Arguments.Length == 2)
					{
						var tEx = te.Arguments[0];
						var t = DResolver.StripMemberSymbols(E(tEx,te));
						
						if(te.Arguments[1].AssignExpression != null)
						{
							var litEx = te.Arguments[1].AssignExpression;
							var v = E(litEx);
							
							if(v is ArrayValue && (v as ArrayValue).IsString)
							{
								var av = v as ArrayValue;
								
								// Mock up a postfix_access expression to ensure static properties & ufcs methods are checked either
								var pfa = new PostfixExpression_Access{ 
									PostfixForeExpression = tEx.AssignExpression ?? new TypeDeclarationExpression(tEx.Type),
									AccessExpression = new IdentifierExpression(av.StringValue) {
										Location = litEx.Location, 
										EndLocation = litEx.EndLocation},
									EndLocation = litEx.EndLocation};
								ignoreErrors = true;
								eval = false;
								ret = E(pfa, t, false) != null;
								eval = true;
								ignoreErrors = false;
							}
							else
								EvalError(te.Arguments[1].AssignExpression, "Second traits argument must evaluate to a string literal", v);
						}
						else
							EvalError(te, "Second traits argument must be an expression");
					}
					
					ctxt.ContextIndependentOptions = optionsBackup;
					return new PrimitiveValue(ret, te);
					
				case "identifier":
					break;
				case "getMember":
					break;
				case "getOverloads":
					break;
				case "getProtection":
					break;
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
					break;
				case "compiles":
					break;
			}
			
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
									ret = !dm.ContainsAttribute(includeFinalNonOverridingMethods ? 0 : DTokens.Final, DTokens.Static);
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
