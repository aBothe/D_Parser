//
// ParamInsightVisitor.cs
//
// Author:
//       Alexander Bothe <info@alexanderbothe.com>
//
// Copyright (c) 2013 Alexander Bothe
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using System.Collections.Generic;

namespace D_Parser.Completion
{
	public class ParamInsightVisitor : DefaultDepthFirstVisitor
	{
		Stack<IExpression> CallExpressionStack = new Stack<IExpression>();
		/// <summary>
		/// Can be null, PostfixExpression_MethodCall, TemplateInstanceExpression, NewExpression
		/// </summary>
		public IExpression LastCallExpression;

		IExpression peek
		{
			get{ return CallExpressionStack.Count != 0 ? CallExpressionStack.Peek () : null; }
		}

		public override void Visit (DTokenDeclaration td)
		{
			if (td.Token == DTokens.Incomplete && peek is TemplateInstanceExpression)
				LastCallExpression = peek;
		}

		public override void Visit (TokenExpression x)
		{
			if (x.Token == DTokens.Incomplete)
				LastCallExpression = peek;
		}

		public override void Visit (NewExpression x)
		{
			CallExpressionStack.Push (x);
			base.Visit (x);
			CallExpressionStack.Pop ();
		}

		public override void Visit (PostfixExpression_MethodCall x)
		{
			CallExpressionStack.Push (x);
			base.Visit (x);
			CallExpressionStack.Pop ();
		}

		public override void Visit (TemplateInstanceExpression x)
		{
			CallExpressionStack.Push (x);
			base.Visit (x);
			CallExpressionStack.Pop ();
		}
	}
}

