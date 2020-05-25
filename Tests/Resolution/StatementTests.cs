﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.ExpressionSemantics;
using NUnit.Framework;

namespace Tests.Resolution
{
	[TestFixture]
	public class StatementTests : ResolutionTestHelper
	{
		[Test]
		public void ForeachIteratorType()
		{
			var ctxt = CreateCtxt("A", @"module A;
void foo() { 
foreach(c; cl) 
	c.a;
}

class Cl{ int a; }
Cl** cl;
");
			var A = ctxt.MainPackage()["A"];
			var foo = A["foo"].First() as DMethod;
			var c_a = ((foo.Body.First() as ForeachStatement).ScopedStatement as ExpressionStatement).Expression;
			ctxt.CurrentContext.Set(foo, c_a.Location);

			AbstractType t;

			t = ExpressionTypeEvaluation.EvaluateType(c_a, ctxt);
			Assert.IsInstanceOf<MemberSymbol>(t);
			Assert.IsInstanceOf<PrimitiveType>((t as DerivedDataType).Base);
		}

		[Test]
		public void ForeachIteratorType_BackFrontMembers()
		{
			var ctxt = CreateDefCtxt(@"module A;
struct SomeType {}
class FrontTier(EntryType) {
	void front(int abc);
	EntryType front() { };
}

struct BackTier {
	string back();
	void back(string asd);
}

FrontTier!SomeType frontier;
BackTier backtier;

void foo() {
	foreach(e; frontier) {
		e;
	}
}

void bar() {
	foreach_reverse(e; backtier) {
		e;
	}
}");

			{
				var foo = N<DMethod>(ctxt, "A.foo");
				var e_statement = S(foo, 0, 0, 0) as ExpressionStatement;
				var t = ExpressionTypeEvaluation.EvaluateType(e_statement.Expression, ctxt);

				Assert.IsInstanceOf<MemberSymbol>(t);
				var ms = t as MemberSymbol;
				Assert.IsInstanceOf<TemplateParameterSymbol>(ms.Base);
				var tps = ms.Base as TemplateParameterSymbol;
				Assert.IsInstanceOf<StructType>(tps.Base);
			}

			{
				var bar = N<DMethod>(ctxt, "A.bar");
				var e_statement = S(bar, 0, 0, 0) as ExpressionStatement;
				var t = ExpressionTypeEvaluation.EvaluateType(e_statement.Expression, ctxt);

				Assert.IsInstanceOf<MemberSymbol>(t);
				var ms = t as MemberSymbol;
				Assert.IsInstanceOf<ArrayType>(ms.Base);
				var at = ms.Base as ArrayType;
				Assert.IsTrue(at.IsString);
			}
		}

		[Test]
		public void ForeachIteratorType_OpApply()
		{
			var ctxt = CreateDefCtxt(@"module A;
class Foo { int opApply(scope int delegate(ref uint) dg); }
class Bar(T) { int opApplyReverse(scope int delegate(ref T) dg); }

Foo frontier;
Bar!string backtier;

void foo() {
	foreach(e; frontier) {
		e;
	}
}

void bar() {
	foreach_reverse(e; backtier) {
		e;
	}
}");

			{
				var foo = N<DMethod>(ctxt, "A.foo");
				var e_statement = S(foo, 0, 0, 0) as ExpressionStatement;
				var t = ExpressionTypeEvaluation.EvaluateType(e_statement.Expression, ctxt);

				Assert.IsInstanceOf<MemberSymbol>(t);
				var ms = t as MemberSymbol;
				Assert.IsInstanceOf<PrimitiveType>(ms.Base);
			}

			{
				var bar = N<DMethod>(ctxt, "A.bar");
				var e_statement = S(bar, 0, 0, 0) as ExpressionStatement;
				var t = ExpressionTypeEvaluation.EvaluateType(e_statement.Expression, ctxt);

				Assert.IsInstanceOf<MemberSymbol>(t);
				var ms = t as MemberSymbol;
				Assert.IsInstanceOf<TemplateParameterSymbol>(ms.Base);
				var tps = ms.Base as TemplateParameterSymbol;
				Assert.IsInstanceOf<ArrayType>(tps.Base);
				var at = tps.Base as ArrayType;
				Assert.IsTrue(at.IsString);
			}
		}

		[Test]
		public void TryCatch()
		{
			var ctxt = CreateCtxt("A", @"module A;
import exc;
void main(){
try{}
catch(MyException ex){
ex;
}", @"module exc; class MyException { int msg; }");
			var A = ctxt.MainPackage()["A"];
			var main = A["main"].First() as DMethod;
			var tryStmt = main.Body.SubStatements.ElementAt(0) as TryStatement;
			var catchStmt = tryStmt.Catches[0];

			var exStmt = (catchStmt.ScopedStatement as BlockStatement).SubStatements.ElementAt(0) as ExpressionStatement;
			ctxt.Push(main, exStmt.Location);
			var t = ExpressionTypeEvaluation.EvaluateType(exStmt.Expression, ctxt);

			Assert.IsInstanceOf<MemberSymbol>(t);
			t = (t as MemberSymbol).Base;
			Assert.IsInstanceOf<ClassType>(t);
		}

		[Test]
		public void TryCatch_ImplicitExVarType()
		{
			var ctxt = CreateCtxt("A", @"module A;
import exc;
void main(){
try{}
catch(ex){
ex;
}");
			var A = ctxt.MainPackage()["A"];
			var main = A["main"].First() as DMethod;
			var tryStmt = main.Body.SubStatements.ElementAt(0) as TryStatement;
			var catchStmt = tryStmt.Catches[0];

			var exStmt = (catchStmt.ScopedStatement as BlockStatement).SubStatements.ElementAt(0) as ExpressionStatement;
			ctxt.Push(main, exStmt.Location);
			var t = ExpressionTypeEvaluation.EvaluateType(exStmt.Expression, ctxt);

			Assert.IsInstanceOf<MemberSymbol>(t);
			t = (t as MemberSymbol).Base;
			Assert.IsInstanceOf<ClassType>(t);
			var ct = t as ClassType;
			Assert.AreEqual("Exception", ct.Definition.Name);
		}

		[Test]
		public void WithStmt()
		{
			var ctxt = CreateCtxt("A", @"module A;
class C(T) { int c; T tc; }
class B(D) {
int a;
D da;

void afoo(){
int local;
C!(char[]) mc;
with(mc){
	x;
}
}
}
");
			var C = N<DClassLike>(ctxt, "A.C");
			var C_c = N<DVariable>(C, "c");
			var C_tc = N<DVariable>(C, "tc");

			var B = N<DClassLike>(ctxt, "A.B");
			var B_a = N<DVariable>(B, "a");
			var B_da = N<DVariable>(B, "da");

			var afoo = N<DMethod>(B, "afoo");
			var local = (S(afoo, 0) as DeclarationStatement).Declarations[0] as DVariable;
			var xstmt = S(afoo, 2, 0, 0);

			ctxt.CurrentContext.Set(afoo, xstmt.Location);

			IExpression x;
			AbstractType t;

			x = DParser.ParseExpression("tc");
			(x as IdentifierExpression).Location = xstmt.Location;
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.IsTrue(C_tc.IsDefinedIn(t));
			Assert.IsInstanceOf<TemplateParameterSymbol>((t as DerivedDataType).Base);
			Assert.IsInstanceOf<ArrayType>(((t as DerivedDataType).Base as DerivedDataType).Base);

			x = DParser.ParseExpression("c");
			(x as IdentifierExpression).Location = xstmt.Location;
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.IsTrue(C_c.IsDefinedIn(t));
			Assert.IsInstanceOf<PrimitiveType>((t as DerivedDataType).Base);

			x = DParser.ParseExpression("da");
			(x as IdentifierExpression).Location = xstmt.Location;
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.IsTrue(B_da.IsDefinedIn(t));

			x = DParser.ParseExpression("a");
			(x as IdentifierExpression).Location = xstmt.Location;
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.IsTrue(B_a.IsDefinedIn(t));

			x = DParser.ParseExpression("local");
			(x as IdentifierExpression).Location = xstmt.Location;
			t = ExpressionTypeEvaluation.EvaluateType(x, ctxt);

			Assert.IsTrue(local.IsDefinedIn(t));
		}

	}
}
