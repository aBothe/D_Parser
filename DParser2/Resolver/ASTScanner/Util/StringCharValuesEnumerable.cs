using System;
using System.Collections;
using System.Collections.Generic;
using D_Parser.Resolver.ExpressionSemantics;

namespace D_Parser.Resolver.ASTScanner.Util
{
	class StringCharValuesEnumerable : IEnumerable<ISymbolValue>
	{
		private readonly PrimitiveType charType;
		private readonly String enumeratee;

		public StringCharValuesEnumerable(PrimitiveType charType, String enumeratee)
		{
			this.charType = charType;
			this.enumeratee = enumeratee;
		}

		class ValuesEnumerator : IEnumerator<ISymbolValue>
		{
			private readonly PrimitiveType charType;
			private readonly String enumeratee;
			int index;

			public ValuesEnumerator(PrimitiveType charType, String enumeratee)
			{
				this.charType = charType;
				this.enumeratee = enumeratee;
				Reset();
			}

			public ISymbolValue Current { get; private set; }

			object IEnumerator.Current => Current;

			public void Dispose() { }

			public bool MoveNext()
			{
				if (index > enumeratee.Length)
					return false;

				Current = new PrimitiveValue(enumeratee[index], charType);
				index++;
				return true;
			}

			public void Reset()
			{
				index = 0;
				Current = null;
			}
		}

		public IEnumerator<ISymbolValue> GetEnumerator()
		{
			throw new NotImplementedException();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			throw new NotImplementedException();
		}
	}
}
