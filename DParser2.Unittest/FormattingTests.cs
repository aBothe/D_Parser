using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using D_Parser.Formatting;
using D_Parser.Dom;
using System.Diagnostics;

namespace D_Parser.Unittest
{
	[TestClass]
	public class FormattingTests
	{
		[TestMethod]
		public void TestFormatter()
		{
			TestLastLine("", 0);

			TestLastLine(@"import
std", 1);
			TestLastLine(@"import std;
", 0);
			TestLastLine(@"import
", 1);
			TestLastLine(@"import std;
import std;
", 0);
			TestLastLine(@"import std.stdio,
	std.conv;",1);

			TestLastLine(@"import std;
import
", 1);
			TestLastLine(@"import std;
import
	std;", 1);

			TestLastLine(@"
class A
{
	void foo()
	{
	}
	", 1);

			TestLastLine(@"class A {
	void foo(
		)", 2);

			TestLastLine(@"class A {
	void foo()
	{", 1);

			TestLastLine(@"class A {
	void foo()
	{
	}", 1);

			TestLastLine(@"
class A
{
	private:
		int a;
		", 2);

			TestLastLine(@"
class A
{
	private:
		int a;
		int b;
}", 0);

			TestLastLine(@"
class A
{
	private:
		int a;
	public:
		", 2);

			TestLastLine(@"foo();", 0, true);

			TestLastLine(@"foo();
", 0, true);

			TestLastLine(@"foo(
);", 1, true);
			TestLastLine(@"foo(
	a.lol", 1, true);
			TestLastLine(@"foo(
	a,
	b", 1, true);
			TestLastLine(@"foo(a,
	b);", 1, true);

			TestLastLine(@"foo(
	a)", 1, true);
			TestLastLine(@"foo(
	b())", 1, true);
			TestLastLine(@"foo(
", 1, true);
			TestLastLine(@"foo(
)", 1, true);

			TestLastLine(@"void foo(asdf())
{", 0);
			TestLastLine(@"foo(asdf())
", 1, true);
			TestLastLine(@"foo(b=asdf())
", 1, true);
			TestLastLine(@"foo(asdf)
", 1, true);
			TestLastLine(@"writeln(34,
	joLol);", 1, true);
			TestLastLine(@"writeln,
	lolSecondExpression();", 1, true);
			TestLastLine(@"std.stdio.
	writeln(a)", 1, true);
			TestLastLine(@"std.stdio.
	writeln(a);", 1);
			TestLastLine(@"writeln(
	a());", 1);
			TestLastLine(@"writeln(""Hello Word!""
	);", 1);
			TestLastLine(@"writeln(
	(162*2)%315==9);", 1);

			TestLastLine(@"void foo()
{
	asdf;
}
", 0);
			TestLastLine(@"void foo()
{
	asdf();", 1);
			TestLastLine(@"void foo()
{
	asdf();
	", 1);
			TestLastLine(@"void foo()
{
	asdf;
}", 0);
			TestLastLine(@"void foo()
{
	asdf;
	asdf;}
", 0);
			TestLastLine(@"void foo()
{
	asdf;
	asdf;}", 1);
			TestLastLine(@"void foo(){
	a;
	bar();}", 1);
			TestLastLine(@"void foo(){
lol(); ger++;
} yeah;", 0);
			TestLastLine(@"void foo(){
	lol(); } foo();", 1);
			TestLastLine(@"void foo(){
	lol(); } foo();
", 0);
			TestLastLine(@"void foo(){
	asdf();
	if(true){
		whynot();
		bar(); }} fooGer();", 2);


			TestLastLine(@"void foo()
	if(..)", 1);
			TestLastLine(@"void foo()
	if(..)
in", 0);
			TestLastLine(@"void foo()
in", 0);
			TestLastLine(@"void foo()
out", 0);
			TestLastLine(@"void foo()
out(result)", 0);
			TestLastLine(@"void foo()
	if(true)
out(result)", 0);
			TestLastLine(@"void foo()
out(result){", 0);
			TestLastLine(@"void foo()
body", 0);
			TestLastLine(@"void foo(in
", 1);
			TestLastLine(@"void foo(out
", 1);



			TestLastLine(@"void foo()
{
	b(
		{
			nestedFoo();", 3);

			TestLastLine(@"void foo()
{
	b({
		nestedFoo();
	});
	", 1);

			TestLastLine(@"void foo()
{
	bar({asdfCall();});", 1);

			TestLastLine(@"class A:B
{", 0);
			TestLastLine(@"class A:
", 1);
			TestLastLine(@"class A:B
", 1);


			TestLastLine(@"enum A
{", 0);
			TestLastLine(@"enum A
{
", 1);
			TestLastLine(@"enum A
{
	a,
	", 1);
			TestLastLine(@"enum A
{
a= A+
B", 2);
			TestLastLine(@"enum A
{
a,
b=A+B,
c", 1);
			TestLastLine(@"enum A
{
a,
b=A+B,
c,
", 1);
			TestLastLine(@"enum A
{
a,
b=A*B,
c,
d,", 1);
			TestLastLine(@"enum A
{
a,
b
,c", 1);
			TestLastLine(@"enum A
{
a
b,
c,
}", 0);

			TestLastLine(@"enum A
{
	a,b
}

void foo()
{
	", 1);


			TestLastLine(@"static if(a)
{", 0);

			TestLastLine(@"static if(a)
	a;", 1);
			TestLastLine(@"static if(asdf)
", 1);
			TestLastLine(@"static if(asdf())
", 1);
			TestLastLine(@"static if(asdf()==b)
", 1);
			TestLastLine(@"static if(
", 1);


			TestLastLine(@"switch(a)
{
	case:", 1);
			TestLastLine(@"switch(a)
{
	case 3:
		lol;", 2);
			TestLastLine(@"switch(a)
{
	case 3:
		lol;
		", 2);

			TestLastLine(@"switch(a)
{
	case 3:
		lol;
}", 0);

			TestLastLine(@"switch(a)
{
	case 3:
		lol;
}
", 0);
			TestLastLine(@"switch(a)
{
	case 3:
		lol;
		asdf;", 2);
			TestLastLine(@"switch(a)
{
	case 3:
		lol;
	case 4:
		asdf;", 2);
			TestLastLine(@"switch(a)
{
	case 3:
		lol;
	default:
	case 4:
		asdf;", 2);


			TestLastLine(@"private:
	", 1);

			TestLastLine(@"version(Windows):
	", 1);
			TestLastLine(@"version(D):", 0);

			TestLastLine(@"
private void foo()
{
	a;
}", 0);

			TestLastLine(@"
private void foo()
{
	a;
}", 0);

			TestLastLine(@"
private:
	void foo()
	{", 1);

			TestLastLine(@"
void main(string[] args)
{
    if(true)
    {
		for(int i=0; i<5;i++)
		{
			i = i % 4;
			if(i == 3)
			{
				i++;
				", 4);

			TestLastLine(@"
void main(string[] args)
{
    if(true)
    {
		for(int i=0; i<5;i++)
		{
			i = i % 4;
			if(i == 3)
			{
				i++;
			}", 3);

			TestLine(@"
void main(string[] args)
{
    if(true)
    {
		for(int i=0; i<5;i++)
		{
			i = i % 4;
			if(i == 3)
			{
				i++;
			}
		}
	}
}", 12, 3);

			TestLastLine(@"
void main(string[] args)
{
    if(true)
    {
		for(int i=0; i<5;i++)
		{
			i = i % 4;
			if(i == 3)
			{
				i++;
			}}", 3);

			TestLastLine(@"
void main(string[] args)
{
    if(true)
    {
		for(int i=0; i<5;i++)
		{
			i = i % 4;
			if(i == 3)
			{
				i++;
				;;", 4);

			TestLine(@"import std.stdio;
void main(string[] args)
{
	writeln();
	
}
", 5, 1);
		}


		void TestLastLine(string code, int targetIndent, bool isStmt = false)
		{
			var newInd = GetLastLineIndent(code);
			Assert.AreEqual(targetIndent, newInd, "[Additional Content]\n" + code);
		}

		void TestLine(string code, int line, int targetIndent)
		{
			var newInd = GetLineIndent(code, new CodeLocation(0, line));
			Assert.AreEqual(targetIndent, newInd, code);
		}


		static int GetLineIndent(string code, CodeLocation caret)
		{
			var ast = D_Parser.Parser.DParser.ParseString(code);

			return IndentationCalculator.CalculateForward(ast, caret);
			/*
			var fmt = new DFormatter();

			var cb = fmt.CalculateIndentation(code, line);

			return cb != null ? cb.GetLineIndentation(line) : 0;*/
		}

		static int GetLastLineIndent(string code)
		{
			var l = DocumentHelper.OffsetToLocation(code, code.Length);
			return GetLineIndent(code, l);
		}
	}
}
