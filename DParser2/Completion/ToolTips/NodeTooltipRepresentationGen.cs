//
// NodeTooltipRepresentationGen.cs
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
using D_Parser.Dom;
using D_Parser.Parser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace D_Parser.Completion.ToolTips
{
	[Flags]
	public enum FormatFlags
	{
		None = 0,
		Color = 1 << 0,
		Underline = 1 << 1,
		Bold = 1 << 2,
		Italic = 1 << 3
	}

	public partial class NodeTooltipRepresentationGen
	{
		#region Tooltip Body creation
		/// <summary>
		/// 
		/// </summary>
		/// <param name="summary">The overall summary for the given node</param>
		/// <param name="categories">Keys: Category name, Values: Categories' contents</param>
		public void GenToolTipBody(DNode n, out string summary, out Dictionary<string, string> categories)
		{
			categories = null;
			summary = null;

			var desc = n.Description;
			if (!string.IsNullOrWhiteSpace(desc))
			{
				categories = new Dictionary<string, string>();

				var match = ddocSectionRegex.Match(desc);

				if (!match.Success)
				{
					summary = HandleSummary(desc);
					return;
				}

				if (match.Index > 0)
					summary = HandleSummary (desc.Substring (0, match.Index - 1));

				int k;
				while ((k = match.Index + match.Length) < desc.Length)
				{
					var nextMatch = ddocSectionRegex.Match(desc, k);
					if (nextMatch.Success)
					{
						AssignToCategories(categories, match.Groups["cat"].Value, desc.Substring(k, nextMatch.Index - k));
						match = nextMatch;
					}
					else
						break;
				}

				// Handle last match
				AssignToCategories(categories, match.Groups["cat"].Value, desc.Substring(k));
			}
		}

		private string HandleSummary(string desc)
		{
			var firstParagraphMatch = summaryFirstParagraphFilter.Match (desc);

			if (firstParagraphMatch.Success)
				desc = DDocToMarkup (desc.Substring (0, firstParagraphMatch.Index - 1));
			else
				desc = DDocToMarkup (desc);

			if (string.IsNullOrWhiteSpace (desc))
				return null;

			return desc.Trim();
		}

		private void AssignToCategories(Dictionary<string, string> cats, string catName, string rawContent)
		{
			var n = catName.ToLower(System.Globalization.CultureInfo.InvariantCulture);

			// Don't show any documentation except parameter & return value description -- It's a tooltip, not a full-blown viewer!
			if (n.StartsWith("param"))
			{
				cats[catName] = HandleParamsCode(DDocToMarkup(rawContent));
			}
			else if (n.StartsWith("returns"))
			{
				rawContent = rawContent.Trim();
				// n.StartsWith ("example") ? HandleExampleCode (DDocToMarkup(rawContent)) : 
				cats[catName] = DDocToMarkup(rawContent);
			}
		}

		static readonly Regex summaryFirstParagraphFilter = new Regex(@"\n\s*\n",
			RegexOptions.Compiled | RegexOptions.Multiline);

		static readonly System.Text.RegularExpressions.Regex paramsSectionRegex = new Regex(
			@"^\s*(?<name>[\w_]+)\s*=\s*(?<desc>(.|\n(?!\s*[\w_]+\s*=))*)\s*",
			RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.ExplicitCapture);

		string HandleParamsCode(string rawContent)
		{
			var sb = new StringBuilder();

			foreach (Match match in paramsSectionRegex.Matches(rawContent))
			{
				if (!match.Success)
					continue;

				AppendFormat(match.Groups["name"].Value, sb, FormatFlags.Italic | FormatFlags.Bold);
				sb.Append(' ').AppendLine(match.Groups["desc"].Value);
			}

			return sb.ToString();
		}

		/*
		const char ExampleCodeInit = '-';

		string HandleExampleCode (string categoryContent)
		{
			int i = categoryContent.IndexOf (ExampleCodeInit);
			if (i >= 0) {
				while (i < categoryContent.Length && categoryContent [i] == ExampleCodeInit)
					i++;
			} else
				i = 0;

			int lastI = categoryContent.LastIndexOf (ExampleCodeInit);
			if (lastI < i) {
				lastI = categoryContent.Length - 1;
			} else {
				while (lastI > i && categoryContent [lastI] == ExampleCodeInit)
					lastI--;
			}

			return DCodeToMarkup (categoryContent.Substring (i, lastI - i));
		}*/
		private static readonly Regex ddocSectionRegex = new Regex(@"^\s*(?<cat>[\w][\w\d_]*):",
			RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.ExplicitCapture);

		string DDocToMarkup(string ddoc)
		{
			if (ddoc == null)
				return string.Empty;

			var sb = new StringBuilder(ddoc.Length);
			int i = 0, len = 0;
			while (i < ddoc.Length)
			{
				var k = i + len;

				DDocParser.FindNextMacro(ddoc, i + len, out i, out len, out var macroName, out var parameters);

				if (i < 0)
				{
					i = k;
					break;
				}

				while (k < i)
					sb.Append(ddoc[k++]);

				if (macroName == "BR")
				{
					sb.AppendLine();
					continue;
				}

				var firstParam = parameters?["$0"];

				if (firstParam != null)
				{
					var firstParamMarkup = DDocToMarkup(firstParam);
					//TODO: Have proper macro infrastructure
					switch (macroName)
					{
						case "I":
								AppendFormat(firstParamMarkup, sb, FormatFlags.Italic);
							break;
						case "U":
								AppendFormat(firstParamMarkup, sb, FormatFlags.Underline);
							break;
						case "B":
								AppendFormat(firstParamMarkup, sb, FormatFlags.Bold);
							break;
						case "D_CODE":
						case "D":
								sb.Append(DCodeToMarkup(firstParamMarkup));
							break;
						case "RED":
								AppendFormat(firstParamMarkup, sb, FormatFlags.Color, 1.0);
							break;
						case "BLUE":
								AppendFormat(firstParamMarkup, sb, FormatFlags.Color, 0, 0, 1.0);
							break;
						case "GREEN":
								AppendFormat(firstParamMarkup, sb, FormatFlags.Color, 0, 1, 0);
							break;
						case "YELLOW":
								AppendFormat(firstParamMarkup, sb, FormatFlags.Color, 1, 1, 0);
							break;
						case "BLACK":
								AppendFormat(firstParamMarkup, sb, FormatFlags.Color);
							break;
						case "WHITE":
								AppendFormat(firstParamMarkup, sb, FormatFlags.Color, 1, 1, 1);
							break;
						default:
							sb.Append(firstParamMarkup);
							break;
					}
				}
			}

			while (i < ddoc.Length)
				sb.Append(ddoc[i++]);

			return sb.ToString();
		}
		#endregion

		protected virtual void AppendFormat(string content, StringBuilder sb, FormatFlags flags,
			double r = 0.0, double g = 0.0, double b = 0.0)
		{
			if (flags == FormatFlags.None)
			{
				sb.Append(content);
				return;
			}

			sb.Append("<span");

			if ((flags & FormatFlags.Bold) != 0)
				sb.Append(" weight='bold'");
			if ((flags & FormatFlags.Italic) != 0)
				sb.Append(" font_style='italic'");
			if ((flags & FormatFlags.Underline) != 0)
				sb.Append(" underline='single'");
			if ((flags & FormatFlags.Color) != 0)
			{
				sb.Append($" color='#{(int) (r * 255.0):x2}{(int) (g * 255.0):x2}{(int) (b * 255.0):x2}'");
			}

			sb.Append('>').Append(content).Append("</span>");
		}

		protected virtual string DCodeToMarkup(string code)
		{
			return code;
		}
	}
}
