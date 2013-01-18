using System;
using System.Collections.Generic;
using D_Parser.Dom;

namespace D_Parser.Formatting
{
	public enum FormattingMode {
		OnTheFly,
		Intrusive
	}
	
	public class DFormattingVisitor : DefaultDepthFirstVisitor
	{
		sealed class TextReplaceAction
		{
			internal readonly int Offset;
			internal readonly int RemovalLength;
			internal readonly string NewText;
			internal TextReplaceAction DependsOn;

#if DEBUG
			internal readonly string StackTrace;
#endif

			public TextReplaceAction (int offset, int removalLength, string newText)
			{
				this.Offset = offset;
				this.RemovalLength = removalLength;
				this.NewText = newText ?? string.Empty;
				#if DEBUG
				this.StackTrace = Environment.StackTrace;
				#endif
			}
			
			public override bool Equals(object obj)
			{
				var other = obj as TextReplaceAction;
				if (other == null) {
					return false;
				}
				return this.Offset == other.Offset && this.RemovalLength == other.RemovalLength && this.NewText == other.NewText;
			}
			
			public override int GetHashCode()
			{
				return 0;
			}

			public override string ToString()
			{
				return string.Format("[TextReplaceAction: Offset={0}, RemovalLength={1}, NewText={2}]", Offset, RemovalLength, NewText);
			}
		}
		
		#region Properties
		DFormattingOptions policy;
		IDocumentAdapter document;
		List<TextReplaceAction> changes = new List<TextReplaceAction> ();
		FormattingIndentStack curIndent;
		readonly ITextEditorOptions options;
		
		public FormattingMode FormattingMode {
			get;
			set;
		}

		public bool HadErrors {
			get;
			set;
		}

		/*
		public DomRegion FormattingRegion {
			get;
			set;
		}*/
		#endregion
		
		#region Constructor / Init
		public DFormattingVisitor(DFormattingOptions policy, IDocumentAdapter document, ITextEditorOptions options = null)
		{
			if (policy == null) {
				throw new ArgumentNullException("policy");
			}
			if (document == null) {
				throw new ArgumentNullException("document");
			}
			this.policy = policy;
			this.document = document;
			this.options = options ?? TextEditorOptions.Default;
			curIndent = new FormattingIndentStack(this.options);
		}
		
		#endregion
	}
}
