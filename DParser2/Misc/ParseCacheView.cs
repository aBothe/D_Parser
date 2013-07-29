//
// IParseCache.cs
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
using D_Parser.Resolver;
using D_Parser.Parser;
using System.Collections.Concurrent;
using System.Threading;

namespace D_Parser.Misc
{
	public class ParseCacheView : IEnumerable<RootPackage>
	{
		protected List<RootPackage> packs;

		static protected readonly AbstractType defaultSizeT = new AliasedType(
			new DVariable{ Name = "size_t", Type = new DTokenDeclaration(DTokens.Uint)}, new PrimitiveType(DTokens.Uint), null);

		DClassLike objectClass;
		AbstractType sizet;
		ClassType objectType;

		public ParseCacheView(IEnumerable<string> basePaths)
		{
			if (basePaths == null)
				throw new ArgumentNullException ("basePaths");
			packs = new List<RootPackage> ();
			Add (basePaths);
		}

		public ParseCacheView(IEnumerable<RootPackage> roots)
		{
			if (roots == null)
				throw new ArgumentNullException ("roots");
			this.packs = new List<RootPackage> (roots);
		}

		public void Add(RootPackage pack)
		{
			if(pack!=null)
				packs.Add (pack);
		}

		public void Add(IEnumerable<string> roots)
		{
			RootPackage rp;
			foreach (var r in roots)
				if((rp=GlobalParseCache.GetRootPackage (r))!=null)
					packs.Add (rp);
		}

		public virtual DClassLike ObjectClass {
			get {
				if (objectClass != null)
					return objectClass;

				foreach (var root in this)
					if ((objectClass = root.ObjectClass) != null)
						break;

				return objectClass;
			}
		}

		public virtual AbstractType SizeT {
			get {
				if (sizet != null)
					return sizet;

				foreach (var root in this)
					if ((sizet = root.SizeT) != null)
						break;

				if (sizet == null)
					sizet = defaultSizeT;

				return sizet;
			}
		}

		public virtual ClassType ObjectClassResult {
			get {
				if (objectType != null)
					return objectType;

				foreach (var root in this)
					if ((objectType = root.ObjectClassResult) != null)
						break;

				return objectType;
			}
		}

		public int Count
		{
			get{return packs.Count;}
		}

		public RootPackage this[int i]
		{
			get{
				return packs[i];
			}
		}

		public IEnumerator<RootPackage> GetEnumerator ()
		{
			return packs.GetEnumerator ();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator ()
		{
			return GetEnumerator();
		}

		public IEnumerable<ModulePackage> LookupPackage(string packName)
		{
			foreach (var root in this)
				if(root != null)
					yield return root.GetSubPackage (packName);
		}

		public IEnumerable<DModule> LookupModuleName(string moduleName)
		{
			DModule m;
			foreach (var root in this)
				if(root != null && (m = root.GetSubModule(moduleName)) != null)
					yield return m;
		}
	}
}

