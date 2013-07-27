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

namespace D_Parser.Misc
{
	public class ParseCacheView : IEnumerable<RootPackage>
	{
		protected IEnumerable<string> basePaths;
		protected List<RootPackage> packs = new List<RootPackage> ();
		bool initedPacks = false;

		static protected readonly AbstractType defaultSizeT = new AliasedType(
			new DVariable{ Name = "size_t", Type = new DTokenDeclaration(DTokens.Uint)}, new PrimitiveType(DTokens.Uint), null);

		DClassLike objectClass;
		AbstractType sizet;
		ClassType objectType;

		public ParseCacheView(IEnumerable<string> basePaths)
		{
			if (basePaths == null)
				throw new ArgumentNullException ("basePaths");
			this.basePaths = basePaths;
		}

		public ParseCacheView(IEnumerable<RootPackage> roots)
		{
			if (roots == null)
				throw new ArgumentNullException ("roots");
			this.packs = new List<RootPackage> (roots);
		}

		public void Add(RootPackage pack)
		{
			packs.Add (pack);
		}

		public void Add(IEnumerable<string> roots)
		{
			foreach (var r in roots)
				packs.Add (GlobalParseCache.GetRootPackage (r));
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

		void InitPacks()
		{
			if (!initedPacks && basePaths != null) {
				foreach (var p in basePaths)
					packs.Add (GlobalParseCache.GetRootPackage (p));
				initedPacks = true;
			}
		}

		public RootPackage this[int i]
		{
			get{
				InitPacks ();

				if (packs != null)
					return packs[i];
				return null;
			}
		}

		public IEnumerator<RootPackage> GetEnumerator ()
		{
			if (packs == null)
				InitPacks ();

			if (packs != null)
				return packs.GetEnumerator ();
			else
				return new RootPackage[0].GetEnumerator () as IEnumerator<RootPackage>;
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator ()
		{
			return GetEnumerator();
		}

		public IEnumerable<ModulePackage> LookupPackage(string packName)
		{
			foreach (var root in this)
				yield return root.GetSubPackage (packName);
		}

		public IEnumerable<DModule> LookupModuleName(string moduleName)
		{
			DModule m;
			foreach (var root in this)
				if((m = root.GetSubModule(moduleName)) != null)
					yield return m;
		}
	}
}

