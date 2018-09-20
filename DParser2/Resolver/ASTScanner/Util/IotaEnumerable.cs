using System.Collections;
using System.Collections.Generic;
using D_Parser.Resolver.ExpressionSemantics;

namespace D_Parser.Resolver.ASTScanner.Util
{
	class IotaEnumerable : IEnumerable<KeyValuePair<ISymbolValue, ISymbolValue>>
	{
		readonly PrimitiveType primitiveType;
		readonly decimal lower, upper;

		public IotaEnumerable(PrimitiveType primitiveType, decimal lower, decimal upper)
		{
			this.primitiveType = primitiveType;
			this.lower = lower;
			this.upper = upper;
		}

		class IotaEnumerator : IEnumerator<KeyValuePair<ISymbolValue, ISymbolValue>>
		{
			readonly decimal lower, upper;
			readonly PrimitiveType primitiveType;

			decimal currentIteration;


			public IotaEnumerator(PrimitiveType primitiveType, decimal lower, decimal upper)
			{
				this.lower = lower;
				this.primitiveType = primitiveType;
				this.upper = upper;

				Reset();
			}

			public KeyValuePair<ISymbolValue, ISymbolValue> Current { get; private set; }
			object IEnumerator.Current => Current;

			public void Dispose() { }

			public bool MoveNext()
			{
				if (currentIteration > upper)
					return false;

				var v = new PrimitiveValue(currentIteration, primitiveType);
				Current = new KeyValuePair<ISymbolValue, ISymbolValue>(v, v);
				currentIteration++;
				return true;
			}

			public void Reset()
			{
				currentIteration = lower;
				Current = new KeyValuePair<ISymbolValue, ISymbolValue>();
			}
		}

		public IEnumerator<KeyValuePair<ISymbolValue, ISymbolValue>> GetEnumerator() => new IotaEnumerator(primitiveType, lower, upper);
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
}
