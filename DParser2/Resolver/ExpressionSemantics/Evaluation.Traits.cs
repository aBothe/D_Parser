using System;
using System.Collections.Generic;
using System.Text;

using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public partial class Evaluation
	{
		ISemantic E(TraitsExpression te)
		{
			if(eval)
			{
				switch(te.Keyword)
				{
					case "isAbstractClass":
						if(te.Arguments == null)
							return new PrimitiveValue(false, te);
						
						foreach(var arg in te.Arguments)
						{
							AbstractType t;
							if(arg.Type != null)
							{
								t = TypeDeclarationResolver.ResolveSingle(arg.Type, ctxt);
							}
							else if(arg.AssignExpression != null)
							{
								t = DResolver.StripMemberSymbols(EvaluateType(arg.AssignExpression, ctxt));
							}
							else
							{
								EvalError(te, "Argument must be a type or an expression!");
								return new PrimitiveValue(false, te);
							}
						}
						break;
					case "isArithmetic":
						break;
					case "isArithmeticArray":
						break;
					case "isFinalClass":
						break;
					case "isFloating":
						break;
					case "isIntegral":
						break;
					case "isScalar":
						break;
					case "isStaticArray":
						break;
					case "isUnsigned":
						break;
					case "isVirtualFunction":
						break;
					case "isVirtualMethod":
						break;
					case "isAbstractFunction":
						break;
					case "isFinalFunction":
						break;
					case "isStaticFunction":
						break;
					case "isRef":
						break;
					case "isOut":
						break;
					case "isLazy":
						break;
					case "hasMember":
						break;
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
					default:
						EvalError(te, "Illegal trait token");
						return null;
				}
			}
			else
			{
				if(te.Keyword.StartsWith("is") || 
				   te.Keyword.StartsWith("has") ||
				   te.Keyword == "compiles")
				{
					return new PrimitiveType(DTokens.Bool, 0, te);
				}
			}
			// TODO: Return either bools, strings, array (pointers) to members or stuff
			return null;
		}
	}
}
