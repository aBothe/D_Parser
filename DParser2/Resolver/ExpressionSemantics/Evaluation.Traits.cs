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
			switch(te.Keyword)
			{
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
			}
			
			if(te.Keyword.StartsWith("is"))
			{
				if(eval)
				{
					bool ret = false;
					
					if(te.Arguments != null)
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
						}
						
						if(!ret)
							break;
					}
					
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
	}
}
