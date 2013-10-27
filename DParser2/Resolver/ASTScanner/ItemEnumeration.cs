//
// ItemEnumeration.cs
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
using System;
using System.Collections.Generic;
using D_Parser.Dom;

namespace D_Parser.Resolver.ASTScanner
{
	public class ItemEnumeration : AbstractVisitor
	{
		protected ItemEnumeration (ResolutionContext ctxt) : base (ctxt)
		{
		}

		public static List<INode> EnumScopedBlockChildren (ResolutionContext ctxt,MemberFilter VisibleMembers)
		{
			var en = new ItemEnumeration (ctxt);

			en.ScanBlock(ctxt.ScopedBlock, ctxt.ScopedBlock.EndLocation, VisibleMembers);

			return en.Nodes;
		}

		List<INode> Nodes = new List<INode> ();

		protected override bool HandleItem (INode n)
		{
			Nodes.Add (n);
			return false;
		}

		protected override bool HandleItems (IEnumerable<INode> nodes)
		{
			Nodes.AddRange (nodes);
			return false;
		}

		protected override bool HandleItem (PackageSymbol pack)
		{
			return false;
		}
	}
}

