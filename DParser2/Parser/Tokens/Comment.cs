using System;

using D_Parser.Dom;

namespace D_Parser.Parser
{
	public sealed class Comment
	{
		[Flags]
		public enum Type : byte
		{
			Block = 1,
			SingleLine = 2,
			Documentation = 4
		}

		public readonly Type CommentType;
		public readonly string CommentText;
		public readonly CodeLocation StartPosition;
		public readonly CodeLocation EndPosition;

		/// <value>
		/// Is true, when the comment is at line start or only whitespaces
		/// between line and comment start.
		/// </value>
		public readonly bool CommentStartsLine;

		public Comment(Type commentType, string comment, bool commentStartsLine, CodeLocation startPosition, CodeLocation endPosition)
		{
			this.CommentType = commentType;
			this.CommentText = comment;
			this.CommentStartsLine = commentStartsLine;
			this.StartPosition = startPosition;
			this.EndPosition = endPosition;
		}
	}
}