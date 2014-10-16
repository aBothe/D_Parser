using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;

namespace D_Parser.Refactoring
{
	public class SortImportsRefactoring
	{
		public static void SortImports(DBlockNode scope, ITextDocument doc, bool separatePackageRoots = false)
		{
			foreach (var kv in CalculateImportsToSort(scope))
				ResortImports(kv.Value, doc, kv.Key, separatePackageRoots);
		}

		static void ResortImports(List<ImportStatement> importsToSort, ITextDocument editor, List<DAttribute> attributesNotToWrite, bool separatePackageRoots)
		{
			if (importsToSort.Count < 2)
				return;

			int firstOffset = int.MaxValue;
			string indent = "";

			// Remove all of them from the document; Memorize where the first import was
			for (int i = importsToSort.Count - 1; i >= 0; i--)
			{
				var ss = importsToSort[i];
				var ssLocation = ss.Location;
				var ssEndLocation = ss.EndLocation;

				DAttribute attr;
				if (ss.Attributes != null && ss.Attributes.Length > 0)
				{
					attr = ss.Attributes.FirstOrDefault((e) => !attributesNotToWrite.Contains(e));
					if (attr != null && attr.Location < ssLocation)
						ssLocation = attr.Location;

					attr = ss.Attributes.LastOrDefault((e) => !attributesNotToWrite.Contains(e));
					if (attr != null && attr.EndLocation > ssEndLocation)
						ssEndLocation = attr.EndLocation;
				}

				var l1 = editor.LocationToOffset(ssLocation.Line, ssLocation.Column);
				var l2 = editor.LocationToOffset(ssEndLocation.Line, ssEndLocation.Column);
				var n = editor.Length - 1;

				// Remove indents and trailing semicolon.
				for (char c; l1 > 0 && ((c = editor.GetCharAt(l1 - 1)) == ' ' || c == '\t'); l1--) ;
				for (char c; l2 < n && ((c = editor.GetCharAt(l2 + 1)) == ' ' || c == '\t' || c == ';'); l2++) ;
				for (char c; l2 < n && ((c = editor.GetCharAt(l2 + 1)) == '\n' || c == '\r'); l2++) ;

				l1 = Math.Max(0, l1);
				l2 = Math.Max(0, l2);

				firstOffset = Math.Min(l1, firstOffset);
				indent = editor.GetLineIndent(editor.OffsetToLineNumber(firstOffset));
				editor.Remove(l1, l2 - l1);
			}

			// Sort
			importsToSort.Sort(new ImportComparer());

			// Write all imports beneath each other.
			var eol = editor.EolMarker;
			var sb = new StringBuilder();
			ITypeDeclaration prevId = null;

			foreach (var i in importsToSort)
			{
				sb.Append(indent);

				if (i.Attributes != null)
				{
					foreach (var attr in i.Attributes)
					{
						if (attributesNotToWrite.Contains(attr))
							continue;

						sb.Append(attr.ToString()).Append(' ');
					}
				}

				sb.Append(i.ToCode(false)).Append(";").Append(eol);

				if (separatePackageRoots)
				{
					var iid = ImportComparer.ExtractPrimaryId(i);
					if (prevId != null && iid != null &&
						(iid.InnerDeclaration ?? iid).ToString(true) != (prevId.InnerDeclaration ?? prevId).ToString(true))
						sb.Append(eol);

					prevId = iid;
				}
			}

			editor.Insert(firstOffset, sb.ToString());
		}
		
		/// <param name="scope"></param>
		/// <returns>A sorted list of imports that have their mutual attributes enlisted as the key.</returns>
		static List<KeyValuePair<List<DAttribute>, List<ImportStatement>>> CalculateImportsToSort(DBlockNode scope)
		{
			// Get the first scope that contains import statements
			var importsToSort = new List<ImportStatement> ();

			var allSharedAttrs = new List<DAttribute>();
			foreach (var mb in scope.MetaBlocks)
			{
				var mba = mb as AttributeMetaDeclaration;
				if (mba == null)
					continue;
				else if(mba is AttributeMetaDeclarationSection)
					allSharedAttrs.AddRange(mba.AttributeOrCondition);
				else if (mba is AttributeMetaDeclarationBlock)
					allSharedAttrs.AddRange(mba.AttributeOrCondition);
				//TODO: Else-attributes
			}

			foreach (var ss in scope.StaticStatements) {
				var iss = ss as ImportStatement;
				if (iss != null)
				{
					importsToSort.Add(iss);
				}
			}

			if (importsToSort.Count < 2)
				return new List<KeyValuePair<List<DAttribute>,List<ImportStatement>>>();

			// Split imports into groups divided by shared attributes
			var impDict = new Dictionary<List<DAttribute>, List<ImportStatement>> ();
			var noAttrImports = new List<ImportStatement> ();

			foreach(var ii in importsToSort) {
				if (ii.Attributes == null || ii.Attributes.Length == 0) {
					noAttrImports.Add (ii);
					continue;
				}

				var sharedAttrs = new List<DAttribute>(ii.Attributes.Intersect(allSharedAttrs));
				if (sharedAttrs.Count == 0)
					continue;

				bool hasAdded = false;

				foreach (var kv in impDict) {
					if (kv.Key.Count > sharedAttrs.Count ||
						kv.Key.Any((e) => !sharedAttrs.Contains(e)))
						continue;

					if (kv.Key.Count == sharedAttrs.Count)
					{
						if (!kv.Value.Contains (ii))
							kv.Value.Add (ii);
						hasAdded = true;
						break;
					}

					kv.Value.Remove (ii);
				}

				if (!hasAdded)
					impDict.Add (sharedAttrs, new List<ImportStatement>{ ii });
			}

			impDict.Add(new List<DAttribute>(), noAttrImports);

			// Sort impDict entries by their at last occurring shared attribute
			var impDictList = impDict.ToList();
			impDictList.Sort(new ImportDictComparer());

			return impDictList;
		}

		class ImportComparer : IComparer<ImportStatement>
		{
			public static ITypeDeclaration ExtractPrimaryId(ImportStatement i)
			{
				if (i.Imports.Count != 0)
					return i.Imports[0].ModuleIdentifier;

				if (i.ImportBindList != null)
					return i.ImportBindList.Module.ModuleIdentifier;

				return null;
			}

			public int Compare(ImportStatement x, ImportStatement y)
			{
				if (x == y)
					return 0;
				var sx = ExtractPrimaryId(x);
				if (sx == null)
					return 0;
				var sy = ExtractPrimaryId(y);
				if (sy == null)
					return 0;
				return sx.ToString(true).CompareTo(sy.ToString(true));
			}
		}

		class ImportDictComparer : IComparer<KeyValuePair<List<DAttribute>, List<ImportStatement>>>
		{
			public int Compare(KeyValuePair<List<DAttribute>, List<ImportStatement>> x, KeyValuePair<List<DAttribute>, List<ImportStatement>> y)
			{
				if (x.Key == y.Key)
					return 0;

				var l1 = CodeLocation.Empty;
				var l2 = CodeLocation.Empty;

				foreach (var attr in x.Key)
					if (attr.Location > l1)
						l1 = attr.Location;

				foreach (var attr in x.Key)
					if (attr.Location > l1)
						l1 = attr.Location;

				if (l1 > l2)
					return 1;
				if (l1 == l2)
					return 0;
				return -1;
			}
		}
	}
}
