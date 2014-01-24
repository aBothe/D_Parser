//
// GotoLabelCompletionProvider.cs
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
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using D_Parser.Dom;

namespace D_Parser.Completion.Providers
{
	class GotoLabelCompletionProvider : AbstractCompletionProvider
	{
		readonly AbstractStatement gs;

		public GotoLabelCompletionProvider (AbstractStatement gs,ICompletionDataGenerator gen) : base(gen)
		{
			this.gs = gs;
		}

		class LabelVisitor : DefaultDepthFirstVisitor
		{
			public ICompletionDataGenerator gen;

			public LabelVisitor(ICompletionDataGenerator gen)
			{
				this.gen = gen;
			}

			public override void Visit (LabeledStatement s)
			{
				if(s.IdentifierHash != 0)
					gen.AddTextItem (s.Identifier, "Jump label");
			}
		}

		protected override void BuildCompletionDataInternal (IEditorData ed, char enteredChar)
		{
			var gen = CompletionDataGenerator;

			IStatement stmt = gs;
			do{
				stmt = stmt.Parent;
				if (stmt is SwitchStatement) {
					gen.Add (DTokens.Case);
					gen.Add (DTokens.Default);
					break;
				}
			}
			while(stmt != null && stmt.Parent != null);

			if(stmt != null)
				stmt.Accept (new LabelVisitor (gen));
		}
	}
}

