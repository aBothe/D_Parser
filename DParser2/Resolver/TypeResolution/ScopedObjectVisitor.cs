using D_Parser.Dom;
using D_Parser.Dom.Expressions;

namespace D_Parser.Resolver.TypeResolution
{
	class ScopedObjectVisitor : DefaultDepthFirstVisitor
	{
		public ISyntaxRegion IdNearCaret;
		readonly CodeLocation caret;

		public ScopedObjectVisitor(CodeLocation caret)
		{
			this.caret = caret;
		}

		public override void Visit(PostfixExpression_MethodCall x)
		{
			base.Visit(x);
			if (IdNearCaret == x.PostfixForeExpression)
				IdNearCaret = x;
			else if (IdNearCaret == null)
				if (x.Location <= caret && (x.EndLocation >= caret || x.EndLocation.IsEmpty))
					IdNearCaret = x; // on parenthesis or within empty argument list
		}

		public override void Visit(PostfixExpression_Access x)
		{
			if (x.AccessExpression != null &&
				x.AccessExpression.Location <= caret &&
				x.AccessExpression.EndLocation >= caret) {
				x.AccessExpression.Accept (this);
				if(IdNearCaret == x.AccessExpression)
					IdNearCaret = x;
			}else
				base.Visit(x);
		}

		public override void Visit (TemplateInstanceExpression x)
		{
			if (x.Identifier.Location <= caret && x.Identifier.EndLocation >= caret)
				IdNearCaret = x;
			else
				base.Visit (x);
		}

		public override void Visit(IdentifierExpression x)
		{
			if (x.Location <= caret && x.EndLocation >= caret)
				IdNearCaret = x;
			else
				base.Visit(x);
		}

		public override void Visit(IdentifierDeclaration x)
		{
			if (x.Location <= caret && x.EndLocation >= caret)
				IdNearCaret = x;
			else
				base.Visit(x);
		}

		public override void VisitTemplateParameter(TemplateParameter tp)
		{
			var nl = tp.NameLocation;
			string name;
			if (tp.NameHash != 0 &&
				caret.Line == nl.Line &&
				caret.Column >= nl.Column &&
				(name = tp.Name) != null &&
				caret.Column <= nl.Column + name.Length)
				IdNearCaret = tp.Representation;
		}

		public override void VisitDNode(DNode n)
		{
			var nl = n.NameLocation;
			string name;	
			if (n.NameHash != 0 &&
				caret.Line == nl.Line &&
				caret.Column >= nl.Column &&
				(name = n.Name) != null &&
				caret.Column <= nl.Column + name.Length)
				IdNearCaret = n;
			else
				base.VisitDNode(n);
		}

		// Template parameters
	}
}
