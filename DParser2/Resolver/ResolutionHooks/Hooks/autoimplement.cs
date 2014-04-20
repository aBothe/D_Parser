//
// autoimplement.cs
//
// Author:
//       Alexander Bothe <info@alexanderbothe.com>
//
// Copyright (c) 2014 Alexander Bothe
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
using System;
using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Resolver.ResolutionHooks
{
	class autoimplement : IHook
	{
		public AbstractType TryDeduce (DSymbol ds, IEnumerable<ISemantic> templateArguments, ref INode returnedNode)
		{
			var cls= ds as ClassType;
			if (cls == null || templateArguments == null)
				return null;

			var en = templateArguments.GetEnumerator ();
			if (!en.MoveNext ())
				return null;

			returnedNode = cls.Definition;
			var baseClass = DResolver.StripMemberSymbols(AbstractType.Get(en.Current)) as TemplateIntermediateType;

			if (baseClass == null)
				return ds;

			return new ClassType(cls.Definition, ds.DeclarationOrExpressionBase, baseClass as ClassType, baseClass is InterfaceType ? new[] {baseClass as InterfaceType} : null, ds.DeducedTypes);
		}

		public string HookedSymbol {
			get {
				return "std.typecons.AutoImplement";
			}
		}

		public bool SupersedesMultipleOverloads {
			get {
				return true;
			}
		}
	}
}

