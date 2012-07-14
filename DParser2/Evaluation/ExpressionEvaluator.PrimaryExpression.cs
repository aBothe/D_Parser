using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Dom;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Dom.Statements;
using D_Parser.Resolver.Templates;
using System.IO;

namespace D_Parser.Evaluation
{
	public partial class ExpressionEvaluator
	{
		public ISymbolValue Evaluate(PrimaryExpression x)
		{
			if (x is TemplateInstanceExpression)
				return EvalId(x);
			else if (x is IdentifierExpression)
				return Evaluate((IdentifierExpression)x);
			else if (x is TokenExpression)
			{
				var tkx = (TokenExpression)x;

				switch (tkx.Token)
				{
					case DTokens.This:
						if (Const) throw new NoConstException(x);
						//TODO; Non-constant!
						break;
					case DTokens.Super:
						if (Const) throw new NoConstException(x);
						//TODO; Non-const!
						break;
					case DTokens.Null:
						if (Const) throw new NoConstException(x);
						//TODO; Non-const!
						break;
					//return new PrimitiveValue(ExpressionValueType.Class, null, x);
					case DTokens.Dollar:
						// It's only allowed if the evaluation stack contains an array value
						if (vp.CurrentArrayLength != -1)
							return new PrimitiveValue(DTokens.Int, vp.CurrentArrayLength, x);

						throw new EvaluationException(x, "Dollar not allowed here!");

					case DTokens.True:
						return new PrimitiveValue(DTokens.Bool, true, x);
					case DTokens.False:
						return new PrimitiveValue(DTokens.Bool, false, x);
					case DTokens.__FILE__:
						break;
					/*return new PrimitiveValue(ExpressionValueType.String, 
						ctxt==null?"":((IAbstractSyntaxTree)ctxt.ScopedBlock.NodeRoot).FileName,x);*/
					case DTokens.__LINE__:
						return new PrimitiveValue(DTokens.Int, x.Location.Line, x);
				}
			}
			else if (x is TypeDeclarationExpression)
			{
				//TODO: Handle static properties like .length, .sizeof etc.
			}

			return null;
		}
	}
}
