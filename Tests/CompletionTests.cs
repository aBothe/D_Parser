using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using D_Parser;
using D_Parser.Completion;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Misc;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.Templates;
using D_Parser.Resolver.TypeResolution;
using NUnit.Framework;
using System.IO;

namespace Tests
{
	/// <summary>
	/// Description of CompletionTests.
	/// </summary>
	public class CompletionTests
	{
		[Test]
		public void ForStatement()
		{
			var code = @"module A; // 1
void main(){
	for(
		int
		i= // 5
		0;
		i<100;
		i++)
		
	{ // 10
	}";
			var mod = DParser.ParseString(code);
			var main = mod["main"][0] as DMethod;
			
			var l = new CodeLocation(1,4);
			var off = DocumentHelper.LocationToOffset(code,l);
			ParserTrackerVariables ptr;
			var parsedBlock = CtrlSpaceCompletionProvider.FindCurrentCaretContext(code,main, off,l,out ptr);
			
			/*
			 * a) Test if completion popup would open in each code situation
			 * b) Test in which case iteration variables can be found.
			 */
			
			//TODO
		}

		[Test]
		public void MemberCompletion()
		{
			bool expectNodeName;

			object lpo;

			lpo = GetLastParsedObject ("std.templ!(hello).", out expectNodeName);
			Assert.That (lpo, Is.TypeOf (typeof(PostfixExpression_Access)));
			Assert.That ((lpo as PostfixExpression_Access).PostfixForeExpression, Is.TypeOf(typeof(PostfixExpression_Access)));
			Assert.That (expectNodeName, Is.False);

			lpo = GetLastParsedObject ("templ!(hello.", out expectNodeName);
			Assert.That (lpo, Is.TypeOf (typeof(PostfixExpression_Access)));
			Assert.That (((lpo as PostfixExpression_Access).PostfixForeExpression as IdentifierExpression).Value, Is.EqualTo ("hello"));
			Assert.That (expectNodeName, Is.False);

			lpo = GetLastParsedObject ("templ!(hello).", out expectNodeName);
			Assert.That (lpo, Is.TypeOf (typeof(PostfixExpression_Access)));
			Assert.That (((lpo as PostfixExpression_Access).PostfixForeExpression as TemplateInstanceExpression).TemplateId, Is.EqualTo ("templ"));
			Assert.That (expectNodeName, Is.False);

			Assert.That (GetLastParsedObject("std.templ!(hello.", out expectNodeName), 
			             Is.TypeOf (typeof(PostfixExpression_Access)));
			Assert.That (expectNodeName, Is.False);

			Assert.That (GetLastParsedObject("b = B.", out expectNodeName), 
			             Is.TypeOf (typeof(PostfixExpression_Access)));
			Assert.That (expectNodeName, Is.False);

			Assert.That (GetLastParsedObject("b = (B.", out expectNodeName), 
			             Is.TypeOf (typeof(PostfixExpression_Access)));
			Assert.That (expectNodeName, Is.False);
		}

		static object GetLastParsedObject(string code, out bool expectNodeId)
		{
			using (var sr = new StringReader(code)) {
				var parser = DParser.Create (sr);
				parser.Step ();
				parser.Statement ();

				expectNodeId = parser.TrackerVariables.ExpectingNodeName;
				return parser.TrackerVariables.LastParsedObject;
			}
		}
	}
}
