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
		/// <summary>
		/// Scans through all container expressions recursively and returns the one that's nearest to 'Where'.
		/// Will return 'e' if nothing found or if there wasn't anything to scan
		/// </summary>
		public static IExpression SearchExpressionDeeply(IExpression e, CodeLocation Where)
		{
			if (e is PostfixExpression_MethodCall || e is NewExpression)
			{
				IExpression[] args = null;
				var mc = e as PostfixExpression_MethodCall;
				if (mc != null)
				{
					if (mc.PostfixForeExpression != null && Where >= mc.PostfixForeExpression.Location && Where <= mc.PostfixForeExpression.EndLocation)
					{
						var foreExpr = SearchExpressionDeeply(mc.PostfixForeExpression, Where);
						if (foreExpr == mc.PostfixForeExpression)
							return mc;
					}
					args = mc.Arguments;
				}
				var nex = e as NewExpression;
				if (nex != null)
				{
					if (nex.Type != null && Where >= nex.Type.Location && Where <= nex.Type.EndLocation)
						return nex;
					args = nex.Arguments;
				}
					
				// An INVALID token expression is assumed as a safe indicator for handling the last expression in an expression chain!
				if (args != null && args.Length != 0)
				{
					var arg = SearchExpressionDeeply(args[args.Length - 1], Where);
					if (arg != null && (!(arg is TokenExpression) || (arg as TokenExpression).Token != DTokens.INVALID))
						return arg;
				}
				return e;
			}
			else if (e is TemplateInstanceExpression)
			{
				var tix = e as TemplateInstanceExpression;
				TokenExpression tex;
				if(tix.Arguments == null || tix.Arguments.Length == 0 || ((tex = tix.Arguments[tix.Arguments.Length-1] as TokenExpression) != null && tex.Token == DTokens.INVALID))
					return e;
			}

			var pfe = e as PostfixExpression;
			if (pfe != null && pfe.PostfixForeExpression != null && 
				Where >= pfe.PostfixForeExpression.Location && Where <= pfe.PostfixForeExpression.EndLocation)
				return SearchExpressionDeeply(pfe.PostfixForeExpression, Where);

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
					if (se != null && ((Where >= se.Location && Where <= se.EndLocation) || se.EndLocation.Line < 0))
					{
						/*
						 * a.b -- take the entire access expression instead of b only in order to be able to resolve it correctly
						 */
						var pfa = e as PostfixExpression_Access;
						if (pfa != null && pfa.AccessExpression == se)
						{/*
							var tix = pfa.AccessExpression as TemplateInstanceExpression;
							if(tix != null)
							{
								if(Where >= tix.Identifier.Location && Where <= tix.Identifier.EndLocation)
									continue;
							}*/
							continue;
						}
						
						se = SearchExpressionDeeply(se, Where);

						e = se;
						foundOne = true;
						break;
					}
				}

				if (!foundOne)
					break;
			}

			return e;
		}
	}
}
