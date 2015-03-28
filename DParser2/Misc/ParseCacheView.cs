//
// ParseCacheView.cs
//
// Author:
//       Alexander Bothe <info@alexanderbothe.com>
//
// Copyright (c) 2015 Alexander Bothe
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
using D_Parser.Resolver;
using D_Parser.Parser;
namespace D_Parser.Misc
{
	public class LegacyParseCacheView : ParseCacheView
	{
		#region Properties
		readonly List<RootPackage> packs;
		#endregion

		#region Constructors
		public LegacyParseCacheView(IEnumerable<string> packageRoots)
		{
			this.packs = new List<RootPackage> ();
			Add (packageRoots);
		}

		public LegacyParseCacheView(IEnumerable<RootPackage> packages)
		{
			this.packs = new List<RootPackage> (packages);
		}
		#endregion

		public override IEnumerable<RootPackage> EnumRootPackagesSurroundingModule (DModule module)
		{
			return packs;
		}

		public void Add(RootPackage pack)
		{
			if(pack!=null && !packs.Contains(pack))
				packs.Add (pack);
		}

		public void Add(IEnumerable<string> roots)
		{
			RootPackage rp;
			foreach (var r in roots)
				if((rp=GlobalParseCache.GetRootPackage (r))!=null && !packs.Contains(rp))
					packs.Add (rp);
		}
	}

	public abstract class ParseCacheView
	{
		public abstract IEnumerable<RootPackage> EnumRootPackagesSurroundingModule(DModule module);
		public IEnumerable<RootPackage> EnumRootPackagesSurroundingModule(INode childNode)
		{
			return EnumRootPackagesSurroundingModule ((childNode != null ? childNode.NodeRoot : null) as DModule);
		}


		public IEnumerable<ModulePackage> LookupPackage(INode context, string packName)
		{
			foreach (var root in EnumRootPackagesSurroundingModule(context))
				if(root != null)
					yield return root.GetSubPackage (packName);
		}

		public IEnumerable<DModule> LookupModuleName(INode context, string moduleName)
		{
			DModule m;
			foreach (var root in EnumRootPackagesSurroundingModule(context))
				if(root != null && (m = root.GetSubModule(moduleName)) != null)
					yield return m;
		}
	}
}

