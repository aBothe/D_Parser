using System;
using System.Collections.Generic;
using System.Linq;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Dom;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public partial struct Evaluation
	{
		public ISymbolValue Visit(NewExpression nex)
		{
			//TODO: Create virtual object and call the appropriate ctor, then return the object
			return TryDoCTFEOrGetValueRefs(ExpressionTypeEvaluation.EvaluateType(nex, ctxt, false), nex);
		}

		public ISymbolValue Visit(CastExpression ce)
		{
			var toCast = ce.UnaryExpression != null ? ce.UnaryExpression.Accept (this) : null;
			var targetType = ce.Type != null ? TypeDeclarationResolver.ResolveSingle(ce.Type, ctxt) : null;

			var pv = toCast as PrimitiveValue;
			var pt = targetType as PrimitiveType;
			if (pv != null && pt != null) {
				//TODO: Truncate value bytes if required and/or treat Value/ImaginaryPart in any way!
				return new PrimitiveValue(pt.TypeToken, pv.Value, pv.ImaginaryPart, pt.Modifier);
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
				v = EvaluateValue(v as VariableValue, ValueProvider);

			if (v is PrimitiveValue)
			{
				var pv = (PrimitiveValue)v;

				return new PrimitiveValue(pv.BaseTypeToken, -pv.Value, -pv.ImaginaryPart, pv.Modifier);
			}

			return v;
		}

		public ISymbolValue Visit(UnaryExpression_Not x)
		{
			var v = x.UnaryExpression.Accept(this);
			
			if(v is VariableValue)
					v = EvaluateValue(v as VariableValue, ValueProvider);
				var pv = v as PrimitiveValue;
				if(pv == null){
					EvalError(x.UnaryExpression, "Expression must be a primitive value",v);
					return null;
				}
				
				return new PrimitiveValue(!IsFalseZeroOrNull(pv));		
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
			var statProp = StaticProperties.TryEvalPropertyValue(ValueProvider, types, uat.AccessIdentifierHash);

			if (statProp != null)
				return statProp;

			//TODO

			return null;
		}
	}
}
