using System;
using D_Parser.Dom.Expressions;
using D_Parser.Dom;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;
namespace D_Parser.Resolver.ExpressionSemantics
{
	public partial class Evaluation
	{
		public ISymbolValue Visit(TypeidExpression tid)
		{
			/*
			 * Depending on what's given as argument, it's needed to find out what kind of TypeInfo_ class to return
			 * AND to fill it with all required information.
			 * 
			 * http://dlang.org/phobos/object.html#TypeInfo
			 */
			throw new NotImplementedException("TypeInfo creation not supported yet");
		}
	}
}
