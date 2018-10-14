using System;
using System.Collections.Generic;
using System.Linq;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Dom;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public partial class Evaluation
	{
		public ISymbolValue Visit(NewExpression nex)
		{
			return TryDoCTFEOrGetValueRefs(ExpressionTypeEvaluation.EvaluateType(nex, ctxt), nex);
		}

		public ISymbolValue Visit(CastExpression ce)
		{
			var toCast = ce.UnaryExpression != null ? ce.UnaryExpression.Accept (this) : null;
			var targetType = ce.Type != null ? TypeDeclarationResolver.ResolveSingle(ce.Type, ctxt) : null;

			var pv = toCast as PrimitiveValue;
			var pt = targetType as PrimitiveType;
			if (pv != null && pt != null) {
				//TODO: Truncate value bytes if required and/or treat Value/ImaginaryPart in any way!
				return new PrimitiveValue(pt.TypeToken, pv.Value, pv.ImaginaryPart, pt.Modifiers);
			}

			// TODO: Convert actual object
			return null;
		}

		public ISymbolValue Visit(UnaryExpression_Cat x) // ~b;
		{
			//TODO
			return x.UnaryExpression.Accept(this);
		}

		public ISymbolValue Visit(UnaryExpression_Increment x)
		{//TODO
			return x.UnaryExpression.Accept(this);
		}

		public ISymbolValue Visit(UnaryExpression_Decrement x)
		{//TODO
			return x.UnaryExpression.Accept(this);
		}

		public ISymbolValue Visit(UnaryExpression_Add x)
		{//TODO
			return x.UnaryExpression.Accept(this);
		}

		public ISymbolValue Visit(UnaryExpression_Sub x)
		{
			var v = x.UnaryExpression.Accept(this);

			if(v is VariableValue)
				v = EvaluateVariableValue(v as VariableValue);

			if (v is PrimitiveValue)
			{
				var pv = (PrimitiveValue)v;

				return new PrimitiveValue(pv.BaseTypeToken, -pv.Value, -pv.ImaginaryPart, pv.Modifiers);
			}

			return v;
		}

		public ISymbolValue Visit(UnaryExpression_Not x)
		{
			var v = x.UnaryExpression.Accept(this);
			
			if(v is VariableValue)
					v = EvaluateVariableValue(v as VariableValue);
				var pv = v as PrimitiveValue;
				if(pv == null){
					EvalError(x.UnaryExpression, "Expression must be a primitive value",v);
					return null;
				}
				
				return new PrimitiveValue(!IsFalsy(pv));
		}

		public ISymbolValue Visit(UnaryExpression_Mul x)
		{
			return x.UnaryExpression.Accept(this);
		}

		public ISymbolValue Visit(UnaryExpression_And x)
		{
			var ptrBase=x.UnaryExpression.Accept(this);

			// Create a new pointer
			// 
			return null;
		}

		public ISymbolValue Visit(DeleteExpression x)
		{
			// Reset the content of the variable
			return null;
		}

		public ISymbolValue Visit(UnaryExpression_Type x)
		{
			var uat = x as UnaryExpression_Type;

			if (uat.Type == null)
				return null;

			var types = TypeDeclarationResolver.ResolveSingle(uat.Type, ctxt);

			// First off, try to resolve static properties
			var statProp = StaticProperties.TryEvalPropertyValue(ctxt, evaluationState,
				new TypeValue(types), uat.AccessIdentifierHash);

			if (statProp != null)
				return statProp;

			//TODO

			return null;
		}
	}
}
