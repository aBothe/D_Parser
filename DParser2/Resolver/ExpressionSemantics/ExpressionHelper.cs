using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public class ExpressionHelper
	{
		public static bool IsParamRelatedExpression(IExpression subEx)
		{
			var pfa = subEx as PostfixExpression_Access;
			return subEx is PostfixExpression_MethodCall ||
							subEx is TemplateInstanceExpression ||
							subEx is NewExpression ||
							(pfa != null &&
								(pfa.AccessExpression is NewExpression || pfa.AccessExpression is TemplateInstanceExpression));
		}

		/// <summary>
		/// Scans through all container expressions recursively and returns the one that's nearest to 'Where'.
		/// Will return 'e' if nothing found or if there wasn't anything to scan
		/// </summary>
		public static IExpression SearchExpressionDeeply(IExpression e, CodeLocation Where, bool WatchForParamSensitiveExpressions = false)
		{
			IExpression lastParamSensExpr = e;

			if (e != null && e.EndLocation.Line < 0 && (e is PostfixExpression_MethodCall || e is NewExpression))
			{
				var args = e is PostfixExpression_MethodCall ? 
					(e as PostfixExpression_MethodCall).Arguments :
					(e as NewExpression).Arguments;
				
				// A (-1;-1) is assumed as a safe indicator for handling the last expression in an expression chain!
				if(args!=null && args.Length != 0){
					var arg = SearchExpressionDeeply(args[args.Length-1], Where, WatchForParamSensitiveExpressions);
						
					if(!WatchForParamSensitiveExpressions || IsParamRelatedExpression(arg))
						return arg;
				}
				return e;
			}

			while (e is ContainerExpression)
			{
				var currentContainer = e as ContainerExpression;

				if (!(e.Location <= Where || e.EndLocation >= Where))
					break;

				var subExpressions = currentContainer.SubExpressions;

				if (subExpressions == null || subExpressions.Length < 1)
					break;
				bool foundOne = false;
				foreach (var se_ in subExpressions)
				{
					var se = se_;
					if (se != null && (Where >= se.Location && Where <= se.EndLocation) || se.EndLocation.Line < 0)
					{
						/*
						 * a.b -- take the entire access expression instead of b only in order to be able to resolve it correctly
						 */
						var pfa = e as PostfixExpression_Access;
						if (pfa != null && pfa.AccessExpression == se)
						{
							var tix = pfa.AccessExpression as TemplateInstanceExpression;
							if(tix != null)
							{
								if(Where >= tix.Identifier.Location && Where <= tix.Identifier.EndLocation)
									continue;
							}
							continue;
						}
						
						se = SearchExpressionDeeply(se, Where, WatchForParamSensitiveExpressions);

						if (IsParamRelatedExpression(se))
							lastParamSensExpr = se;
						e = se;
						foundOne = true;
						break;
					}
				}

				if (!foundOne)
					break;
			}

			return WatchForParamSensitiveExpressions ? lastParamSensExpr : e;
		}

		public static IExpression SearchForMethodCallsOrTemplateInstances(IStatement Statement, CodeLocation Caret)
		{
			IExpression curExpression = null;
			INode curDeclaration = null;

			/*
			 * Step 1: Step down the statement hierarchy to find the stmt that's most next to Caret
			 * Note: As long we haven't found any fitting elements, go on searching
			 */
			while (Statement != null && curExpression == null && curDeclaration == null)
			{
				if (Statement is IExpressionContainingStatement)
				{
					var exprs = (Statement as IExpressionContainingStatement).SubExpressions;

					if (exprs != null && exprs.Length > 0)
						foreach (var expr in exprs)
							if (expr != null && Caret >= expr.Location && (Caret <= expr.EndLocation || expr.EndLocation.IsEmpty))
							{
								curExpression = expr;
								break;
							}
				}

				if (Statement is IDeclarationContainingStatement)
				{
					var decls = (Statement as IDeclarationContainingStatement).Declarations;

					if (decls != null && decls.Length > 0)
						foreach (var decl in decls)
							if (decl != null && Caret >= decl.Location && Caret <= decl.EndLocation)
							{
								curDeclaration = decl;
								break;
							}
				}

				if (Statement is StatementContainingStatement)
				{
					var stmts = (Statement as StatementContainingStatement).SubStatements;

					bool foundDeeperStmt = false;

					if (stmts != null && stmts.Length > 0)
						foreach (var stmt in stmts)
							if (stmt != null && Caret >= stmt.Location && Caret <= stmt.EndLocation)
							{
								foundDeeperStmt = true;
								Statement = stmt;
								break;
							}

					if (foundDeeperStmt)
						continue;
				}

				break;
			}

			if (curDeclaration == null && curExpression == null)
				return null;


			/*
			 * Step 2: If a declaration was found, check for its inner elements
			 */
			if (curDeclaration != null)
			{
				if (curDeclaration is DVariable)
				{
					var dv = curDeclaration as DVariable;

					if (dv.Initializer != null && Caret >= dv.Initializer.Location && Caret <= dv.Initializer.EndLocation)
						curExpression = dv.Initializer;
				}

				//TODO: Watch the node's type! Over there, there also can be template instances..
			}

			return curExpression == null ? null : SearchExpressionDeeply(curExpression, Caret, true);
		}
	}
}
