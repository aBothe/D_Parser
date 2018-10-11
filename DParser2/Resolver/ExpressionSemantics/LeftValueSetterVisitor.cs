using System.Collections.Generic;

namespace D_Parser.Resolver.ExpressionSemantics
{
	class LeftValueSetterVisitor : ISymbolValueVisitor
	{
		private readonly StatefulEvaluationContext state;
		private readonly ISymbolValue valueToSet;

		public LeftValueSetterVisitor(StatefulEvaluationContext state, ISymbolValue valueToSet)
		{
			this.state = state;
			this.valueToSet = valueToSet;
		}

		public void VisitVariableValue(VariableValue v)
		{
			if(v is ArrayPointer arrayPointer)
				SetArrayPointerValue(arrayPointer);
			else if(v is AssocArrayPointer assocArrayPointer)
				SetAssocArrayPointer(assocArrayPointer);
			else
				state.SetLocalValue(v.Variable, valueToSet);
		}

		void SetArrayPointerValue(ArrayPointer ap)
		{
			var oldV = state.GetLocalValue(ap.Variable);

			if (oldV is ArrayValue)
			{
				var av = (ArrayValue)oldV;

				//TODO: Immutability checks

				if (av.IsString)
				{

				}
				else
				{
					var at = av.RepresentedType as ArrayType;
					var newElements = new ISymbolValue[av.Elements.Length + (ap.ItemNumber<0 ? 1:0)];
					av.Elements.CopyTo(newElements, 0);

					if (!ResultComparer.IsImplicitlyConvertible(valueToSet.RepresentedType, at.ValueType)){
						state.LogError(null,valueToSet.ToCode() + " must be implicitly convertible to the array's value type!", valueToSet);
						return;
					}

					// Add..
					if (ap.ItemNumber < 0)
						av.Elements[av.Elements.Length - 1] = valueToSet;
					else // or set the new value
						av.Elements[ap.ItemNumber] = valueToSet;

					state.SetLocalValue(ap.Variable, new ArrayValue(at, newElements));
				}
			}
			else{
				state.LogError(null,"Type of accessed item must be an array", oldV);
			}
		}

		void SetAssocArrayPointer(AssocArrayPointer assocArrayPointer)
		{
			var oldV = state.GetLocalValue(assocArrayPointer.Variable);

			if (oldV is AssociativeArrayValue)
			{
				if (assocArrayPointer.Key != null)
				{
					var aa = (AssociativeArrayValue)oldV;

					int itemToReplace = -1;

					for (int i = 0; i < aa.Elements.Count; i++)
						if (SymbolValueComparer.IsEqual(aa.Elements[i].Key, assocArrayPointer.Key))
						{
							itemToReplace = i;
							break;
						}

					// If we haven't found a matching key, add it to the array
					var newElements = new KeyValuePair<ISymbolValue, ISymbolValue>[aa.Elements.Count + (itemToReplace == -1 ? 1 : 0)];
					aa.Elements.CopyTo(newElements, 0);

					if (itemToReplace != -1)
						newElements[itemToReplace] = new KeyValuePair<ISymbolValue, ISymbolValue>(newElements[itemToReplace].Key, valueToSet);
					else
						newElements[newElements.Length - 1] = new KeyValuePair<ISymbolValue, ISymbolValue>(assocArrayPointer.Key, valueToSet);

					// Finally, make a new associative array containing the new elements
					state.SetLocalValue(assocArrayPointer.Variable,
						new AssociativeArrayValue(aa.RepresentedType as AssocArrayType, newElements));
				}
				else{
					state.LogError(null,"Key expression must not be null", assocArrayPointer.Key);
				}
			}
			else{
				state.LogError(null,"Type of accessed item must be an associative array", oldV);
			}
		}

		public void VisitErrorValue(ErrorValue v) => throw new System.NotImplementedException();
		public void VisitPrimitiveValue(PrimitiveValue v) => throw new System.NotImplementedException();
		public void VisitVoidValue(VoidValue v) => throw new System.NotImplementedException();
		public void VisitArrayValue(ArrayValue v) => throw new System.NotImplementedException();
		public void VisitAssociativeArrayValue(AssociativeArrayValue v) => throw new System.NotImplementedException();
		public void VisitDelegateValue(DelegateValue v) => throw new System.NotImplementedException();
		public void VisitNullValue(NullValue v) => throw new System.NotImplementedException();
		public void VisitTypeOverloadValue(InternalOverloadValue v) => throw new System.NotImplementedException();
		public void VisitTypeValue(TypeValue v) => throw new System.NotImplementedException();
	}
}