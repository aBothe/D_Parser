using System;
using D_Parser;
using NUnit.Framework;

namespace Tests
{
	[TestFixture]
	public class IndentationTests
	{
		const int TabSize = 4;
		const string TabToSpaceRepresentation = "    ";

		[Test]
		public void Indentation1()
		{
			TestLastLine("", 0);

			TestLastLine(@"
tastyDeleg(
    {
        asd",2*TabSize);

			TestLastLine(@"
tastyDeleg( (uint a)
    {
        asd",2*TabSize);

			TestLastLine(@"import
	std", TabSize);
			TestLastLine(@"import std.stdio,
	std;", TabSize);
			TestLastLine(@"import std;
", 0);
			TestLastLine(@"import
	", TabSize);
			TestLastLine(@"import std;
import std;
", 0);
			TestLastLine(@"import std.stdio,
	std.conv;",TabSize);
			TestLastLine(@"import std;
import
", TabSize);
			TestLastLine(@"import std;
import
	std;", TabSize);

			TestLastLine(@"
class A
{
	void foo()
	{
	}
	", TabSize);

			TestLastLine(@"
class A
{
	private:
	int a;
	", TabSize);

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
	", TabSize);

			TestLastLine(@"foo();", 0);

			TestLastLine(@"foo();
", 0);

			TestLastLine(@"foo(
	);", TabSize);
			TestLastLine(@"foo(
	a.lol", TabSize);
			TestLastLine(@"foo(
	a,
	b", TabSize);
			TestLastLine(@"foo(a,
	b);", TabSize);

			TestLastLine(@"foo(
	a)", TabSize);
			TestLastLine(@"foo(
	b())", TabSize);
			TestLastLine(@"foo(
", TabSize);
			TestLastLine(@"foo(
)", TabSize);

			TestLastLine(@"foo(asdf())
{", 0);
			TestLastLine(@"foo(asdf())
", TabSize);
			TestLastLine(@"foo(asdf()=b)
", TabSize);
			TestLastLine(@"foo(asdf)
", TabSize);
			TestLastLine(@"writeln(34,
	joLol);", TabSize);

			TestLastLine(@"std.stdio.
	writeln(a)", TabSize);
			TestLastLine(@"std.stdio.
	writeln(a);", TabSize);
			TestLastLine(@"writeln(
	a());", TabSize);
			TestLastLine(@"writeln(""Hello Word!""
	);", TabSize);
			TestLastLine(@"writeln(
	(162*2)%315==9);", TabSize);

			TestLastLine(@"foo()
{
	asdf;
}
", 0);
			TestLastLine(@"foo()
{
	asdf();", TabSize);
			TestLastLine(@"foo()
{
	asdf();
	", TabSize);
			TestLastLine(@"foo()
{
	asdf;
}", 0);
			TestLastLine(@"foo()
{
	asdf;
	asdf;}
", 0);
			TestLastLine(@"foo()
{
	asdf;
	asdf;}", TabSize);
			TestLastLine(@"foo(){
	a;
	bar();}", TabSize);
			TestLastLine(@"foo(){
lol(); ger++;
} yeah;", 0);
			TestLastLine(@"foo(){
	lol(); } foo();", TabSize);
			TestLastLine(@"foo(){
	lol(); } foo();
", 0);
			TestLastLine(@"foo(){
	asdf();
	if(true){
		whynot();
		bar(); }} fooGer();", 2*TabSize);
		}

		[Ignore]
		[Test]
		public void IndentationTODO()
		{
			TestLastLine(@"writeln,
	lolSecondExpression();",1);

			TestLastLine(@"private {
	class A:
		", 2*TabSize);

			TestLastLine(@"enum A
{
	a= A+
		B", 2*TabSize);
		}

		[Test]
		public void SwitchIndentation()
		{
			TestLastLine(@"foo() {
	switch(a)
	{
		default:", 2*TabSize);
			TestLastLine(@"foo() {
	switch(a)
	{
		case asdf:", 2*TabSize);
			TestLastLine(@"foo() { 
	switch(a)
	{
		case 3:
			lol;", 3*TabSize);
			TestLastLine(@"switch(a)
{
	case 3:
		lol;
		", 2*TabSize);

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
		asdf;", 2*TabSize);
			TestLastLine(@"switch(a)
{
	case 3:
		lol;
	case 4:
		asdf;", 2*TabSize);
			TestLastLine(@"switch(a)
{
	case 3:
		lol;
	default:
	case 4:
		asdf;", 2*TabSize);
		}

		[Test]
		public void Indentation2()
		{
			TestLastLine(@"foo()
{
	b(
		{
			nestedFoo();", 3*TabSize);

			TestLastLine(@"foo()
{
	b({
		nestedFoo();
	});
	", TabSize);

			TestLastLine(@"foo()
{
	bar({asdfCall();});", TabSize);

			TestLastLine(@"class A:B
{", 0);
			TestLastLine(@"class A:B
", TabSize);


			TestLastLine(@"enum A
{", 0);
			TestLastLine(@"enum A
{
", TabSize);
			TestLastLine(@"enum A
{
	a,
	", TabSize);

			TestLastLine(@"enum A
{
a,
b=A+B,
c", TabSize);
			TestLastLine(@"enum A
{
a,
b=A+B,
c,
", TabSize);
			TestLastLine(@"enum A
{
a,
b=A*B,
c,
d,", TabSize);
			TestLastLine(@"enum A
{
a,
b
,c", TabSize);
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
	", TabSize);


			TestLastLine(@"if(a)
{", 0);

			TestLastLine(@"if(a)
	a;", TabSize);
			TestLastLine(@"if(asdf)
", TabSize);
			TestLastLine(@"if(asdf())
", TabSize);
			TestLastLine(@"if(asdf()==b)
", TabSize);
			TestLastLine(@"if(
", TabSize);





			TestLastLine(@"private:
", 0);
			TestLastLine(@"version(Windows):
", 0);
			TestLastLine(@"version(D):", 0);

			TestLastLine(@"
private foo()
{
	a;
}", 0);

			TestLastLine(@"
private foo()
{
	a;
}", 0);

			TestLastLine(@"
private:
foo()
{", 0);
			TestLastLine(@"
private:
foo()
{
	", TabSize);

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
				", 4*TabSize);

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
			}", 3*TabSize);

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
}", 12, 3*TabSize);

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
			}}", 3*TabSize);

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
				;;", 4*TabSize);

			TestLine(@"import std.stdio;
void main(string[] args)
{
	writeln();
	
}
", 5, TabSize);
		}

		[Ignore]
		[Test]
		public void TestIndenter()
		{
			TestLastLine(@"foo()
	if(..)", TabSize);
			TestLastLine(@"foo()
	if(..)
in", 0);
			TestLastLine(@"foo()
in", 0);
			TestLastLine(@"foo()
out", 0);
			TestLastLine(@"foo()
out(result)", 0);
			TestLastLine(@"foo()
	if(true)
out(result)", 0);
			TestLastLine(@"foo()
out(result){", 0);
			TestLastLine(@"foo()
body", 0);
			TestLastLine(@"void foo(in
", TabSize);
			TestLastLine(@"void foo(out
", TabSize);




		}

		/// <summary>
		/// https://github.com/aBothe/Mono-D/issues/576
		/// </summary>
		[Test]
		public void TestIssue576()
		{
			TestLastLine(@"void foo() {
    tastyDeleg(
        a(myArray[123,
                asd",TabSize*4);

			TestLastLine(@"void foo() {
    tastyDeleg(
               a(myArray[
                     asd",TabSize*4);

			TestLastLine(@"void foo() {
    tastyDeleg(
        a(
            asd",TabSize*3);

			TestLastLine(@"void foo() {
    tastyDeleg(
        a(1234,
            asd",TabSize*3);

	TestLastLine(@"void foo() {
    tastyDeleg(
        asd",TabSize*2);

			TestLastLine(@"void foo() {
    tastyDeleg(1234,
        asd",TabSize*2);

			TestLastLine(@"
tastyDeleg(
    asd",TabSize);
		}

		void TestLastLine(string code, int targetIndent)
		{
			var newInd = GetLastLineIndent(code);
			Assert.AreEqual(targetIndent , newInd, code);

			newInd = GetLastLineIndent(code, true, true);
			Assert.AreEqual(targetIndent, newInd, "[Additional Content]\n"+code);
		}

		void TestLine(string code, int line, int targetIndent)
		{
			var caret = DocumentHelper.OffsetToLocation(code, code.Length);
			var newInd = GetLineIndent(code, line);
			Assert.AreEqual(newInd, targetIndent, code);
		}


		static int GetLineIndent(string code, int line)
		{
			return D_Parser.Formatting.Indent.IndentEngineWrapper.CalculateIndent(code, line).Replace("\t",TabToSpaceRepresentation).Length;
		}

		static int GetLastLineIndent(string code, bool AddNewLines = false, bool AddSomeFinalCode = false)
		{
			var caret = DocumentHelper.OffsetToLocation(code, code.Length);

			if (AddNewLines)
				code += "\r\n\r\n";

			if (AddSomeFinalCode)
				code += "StaticFinalContent;";

			return GetLineIndent(code, caret.Line);
		}
	}
}
