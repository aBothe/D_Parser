using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Resolver.ExpressionSemantics;

namespace D_Parser.Resolver.Model
{
	public class ComplexValue : ISymbolValue
	{
		public ComplexValue(AbstractType type)
		{
			RepresentedType = type;
		}

		private readonly Dictionary<DVariable, ISymbolValue> _properties = new Dictionary<DVariable, ISymbolValue>();

		public ISymbolValue GetPropertyValue(DVariable field)
		{
			return _properties[field];
		}

		public void SetPropertyValue(DVariable field, ISymbolValue value)
		{
			_properties[field] = value;
		}

		public AbstractType RepresentedType { get; }

		public bool Equals(ISymbolValue other)
		{
			throw new System.NotImplementedException();
		}

		public string ToCode()
		{
			throw new System.NotImplementedException();
		}

		public void Accept(ISymbolValueVisitor vis)
		{
			throw new System.NotImplementedException();
		}

		public R Accept<R>(ISymbolValueVisitor<R> v)
		{
			throw new System.NotImplementedException();
		}
	}
}