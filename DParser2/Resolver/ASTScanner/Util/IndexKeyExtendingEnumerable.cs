using System.Collections;
using System.Collections.Generic;
using D_Parser.Resolver.ExpressionSemantics;

namespace D_Parser.Resolver.ASTScanner.Util
{
	class IndexKeyExtendingEnumerable : IEnumerable<KeyValuePair<ISymbolValue, ISymbolValue>>
	{
		readonly IEnumerable<ISymbolValue> values;

		public IndexKeyExtendingEnumerable(IEnumerable<ISymbolValue> values)
		{
			this.values = values;
		}

		class IndexKeyExtendingEnumerator : IEnumerator<KeyValuePair<ISymbolValue, ISymbolValue>>
		{
			readonly IEnumerator<ISymbolValue> values;
			int index;

			public IndexKeyExtendingEnumerator(IEnumerator<ISymbolValue> values)
			{
				this.values = values;
				Reset();
			}

			public KeyValuePair<ISymbolValue, ISymbolValue> Current { get; private set; }
			object IEnumerator.Current => Current;

			public void Dispose() { values.Dispose(); }

			public bool MoveNext()
			{
				if (!values.MoveNext())
					return false;

				Current = new KeyValuePair<ISymbolValue, ISymbolValue>(new PrimitiveValue(index), values.Current);
				index++;
				return true;
			}

			public void Reset()
			{
				values.Reset();
				Current = new KeyValuePair<ISymbolValue, ISymbolValue>();
			}
		}

		public IEnumerator<KeyValuePair<ISymbolValue, ISymbolValue>> GetEnumerator()
			=> new IndexKeyExtendingEnumerator(values.GetEnumerator());
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
}
