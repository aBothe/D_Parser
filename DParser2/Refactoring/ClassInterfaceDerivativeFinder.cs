//
// ClassInterfaceDerivativeFinder.cs
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
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Resolver;
using D_Parser.Resolver.ASTScanner;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Parser;

namespace D_Parser.Refactoring
{
	public class ClassInterfaceDerivativeFinder : AbstractVisitor
	{
		List<TemplateIntermediateType> results = new List<TemplateIntermediateType>();
		List<INode> alreadyResolvedClasses = new List<INode>();
		DClassLike typeNodeToFind;

		ClassInterfaceDerivativeFinder (ResolutionContext ctxt) : base(ctxt)
		{
		}

		public static IEnumerable<TemplateIntermediateType> SearchForClassDerivatives(TemplateIntermediateType t, ResolutionContext ctxt)
		{
			if (!(t is ClassType || t is InterfaceType))
				throw new ArgumentException ("t is expected to be a class or an interface, not " + (t != null ? t.ToString () : "null"));

			var f = new ClassInterfaceDerivativeFinder (ctxt);

			f.typeNodeToFind = t.Definition;
			var bt = t;
			while (bt != null) {
				f.alreadyResolvedClasses.Add (bt.Definition);
				bt = DResolver.StripMemberSymbols (bt.Base) as TemplateIntermediateType;
			}

			var filter = MemberFilter.Classes;
			if (t is InterfaceType) // -> Only interfaces can inherit interfaces. Interfaces cannot be subclasses of classes.
				filter |= MemberFilter.Interfaces;

			f.IterateThroughScopeLayers (t.Definition.Location, filter);

			return f.results; // return them.
		}

		protected override bool HandleItem (INode n)
		{
			// Find all class+interface definitions
			var dc = n as DClassLike;
			if (dc == null || 
				dc.BaseClasses == null || dc.BaseClasses.Count < 0 || 
				alreadyResolvedClasses.Contains(dc))
				return false;
				
			// resolve immediate base classes/interfaces; Rely on dc being either a class or an interface, nothing else.
			var t = DResolver.ResolveBaseClasses (dc.ClassType == DTokens.Class ? (UserDefinedType)new ClassType (dc, null, null) : new InterfaceType (dc, null),ctxt) as TemplateIntermediateType;
			alreadyResolvedClasses.Add (dc);

			// Look for classes/interfaces that match dc (check all dcs to have better performance on ambiguous lastresults),
			var bt = DResolver.StripMemberSymbols(t.Base) as TemplateIntermediateType;
			while (bt != null) {
				var def = bt.Definition;
				if (def == typeNodeToFind) {
					if(!results.Contains(t))
						results.Add (t);
				}
				if(!alreadyResolvedClasses.Contains(def))
					alreadyResolvedClasses.Add (def);
				bt = DResolver.StripMemberSymbols (bt.Base) as TemplateIntermediateType;
			}

			return false;
		}

		protected override bool HandleItem (PackageSymbol pack)
		{
			return false;
		}
	}
}

