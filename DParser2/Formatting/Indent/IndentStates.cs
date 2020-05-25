//
// IndentStates.cs
//
// Author:
//       Alexander Bothe <info@alexanderbothe.com>,
//		 Matej Miklečić <matej.miklecic@gmail.com>
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
using System;

namespace D_Parser.Formatting.Indent
{
#if IGNORE
	/// <summary>
	///     The base class for all indentation states. 
	///     Each state defines the logic for indentation based on chars that
	///     are pushed to it.
	/// </summary>
	abstract class IndentState : ICloneable
	{
		#region Properties

		/// <summary>
		///     The indentation engine using this state.
		/// </summary>
		public readonly DIndentEngine Engine;

		/// <summary>
		///     The parent state. 
		///     This state can use the indentation levels of its parent.
		///     When this state exits, the engine returns to the parent.
		/// </summary>
		public IndentState Parent;

		/// <summary>
		///     The indentation of the current line.
		///     This is set when the state is created and will be changed to
		///     <see cref="NextLineIndent"/> when the <see cref="CSharpIndentEngine.newLineChar"/> 
		///     is pushed.
		/// </summary>
		public Indent ThisLineIndent;

		/// <summary>
		///     The indentation of the next line.
		///     This is set when the state is created and can change depending
		///     on the pushed chars.
		/// </summary>
		public Indent NextLineIndent;

		#endregion

		#region Constructors

		protected IndentState()
		{
		}

		/// <summary>
		///     Creates a new indentation state that is a copy of the given
		///     prototype.
		/// </summary>
		/// <param name="prototype">
		///     The prototype state.
		/// </param>
		/// <param name="engine">
		///     The engine of the new state.
		/// </param>
		protected IndentState(IndentState prototype, DIndentEngine engine)
		{
			Engine = engine;
			Parent = prototype.Parent != null ? prototype.Parent.Clone(engine) : null;

			ThisLineIndent = prototype.ThisLineIndent.Clone();
			NextLineIndent = prototype.NextLineIndent.Clone();
		}

		#endregion

		#region IClonable

		object ICloneable.Clone()
		{
			return Clone(Engine);
		}

		public abstract IndentState Clone(DIndentEngine engine);

		#endregion

		#region Methods

		internal void Initialize (DIndentEngine engine, IndentState parent = null)
		{
			Parent = parent;
			Engine = engine;

			InitializeState();
		}

		/// <summary>
		///     Initializes the state:
		///       - sets the default indentation levels.
		/// </summary>
		/// <remarks>
		///     Each state can override this method if it needs a different
		///     logic for setting up the default indentations.
		/// </remarks>
		public virtual void InitializeState()
		{
			ThisLineIndent = new Indent(Engine.textEditorOptions);
			NextLineIndent = ThisLineIndent.Clone();
		}

		/// <summary>
		///     Actions performed when this state exits.
		/// </summary>
		public virtual void OnExit()
		{
			if (Parent != null)
			{
				// if a state exits on the newline character, it has to push
				// it back to its parent (and so on recursively if the parent 
				// state also exits). Otherwise, the parent state wouldn't
				// know that the engine isn't on the same line anymore.
				if (Engine.currentChar == Engine.newLineChar)
				{
					Parent.Push(Engine.newLineChar);
				}

				// when a state exits the engine stays on the same line and this
				// state has to override the Parent.ThisLineIndent.
				Parent.ThisLineIndent = ThisLineIndent.Clone();
			}
		}

		/// <summary>
		///     Changes the current state of the <see cref="CSharpIndentEngine"/> using the current
		///     state as the parent for the new one.
		/// </summary>
		/// <typeparam name="T">
		///     The type of the new state. Must be assignable from <see cref="IndentState"/>.
		/// </typeparam>
		public void ChangeState<T>()
			where T : IndentState, new ()
		{
			var t = new T();
			t.Initialize(Engine, Engine.currentState);
			Engine.currentState = t;
		}

		/// <summary>
		///     Exits this state by setting the current state of the
		///     <see cref="CSharpIndentEngine"/> to this state's parent.
		/// </summary>
		public void ExitState()
		{
			OnExit();
			Engine.currentState = Engine.currentState.Parent ?? new GlobalBodyState(Engine);
		}

		/// <summary>
		///     Common logic behind the push method.
		///     Each state can override this method and implement its own logic.
		/// </summary>
		/// <param name="ch">
		///     The current character that's being pushed.
		/// </param>
		public virtual void Push(char ch)
		{
			// replace ThisLineIndent with NextLineIndent if the newLineChar is pushed
			if (ch == Engine.newLineChar)
			{
				var delta = Engine.textEditorOptions.ContinuationIndent;
				while (NextLineIndent.CurIndent - ThisLineIndent.CurIndent > delta &&
					NextLineIndent.PopIf(IndentType.Continuation)) ;
				ThisLineIndent = NextLineIndent.Clone();
			}
		}

		/// <summary>
		///     When derived, checks if the given sequence of chars form
		///     a valid keyword or variable name, depending on the state.
		/// </summary>
		/// <param name="keyword">
		///     A possible keyword.
		/// </param>
		public virtual void CheckKeyword(byte keyword)
		{ }

		/// <summary>
		///     When derived, checks if the given sequence of chars form
		///     a valid keyword or variable name, depending on the state.
		/// </summary>
		/// <param name="keyword">
		///     A possible keyword.
		/// </param>
		/// <remarks>
		///     This method should be called from <see cref="Push(char)"/>.
		///     It is left to derived classes to call this method because of
		///     performance issues.
		/// </remarks>
		public virtual void CheckKeywordOnPush(byte keyword)
		{ }

		#endregion
	}
#endif
}

