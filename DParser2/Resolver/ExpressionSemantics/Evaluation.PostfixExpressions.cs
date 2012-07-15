using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public partial class Evaluation
	{
		ISemantic E(PostfixExpression ex)
		{
			if (ex is PostfixExpression_MethodCall)
				return E(ex as PostfixExpression_MethodCall, ctxt);

			var baseExpression = DResolver.StripAliasSymbol(E(ex.PostfixForeExpression) as AbstractType);

			if (baseExpression == null ||
				ex is PostfixExpression_Increment || // myInt++ is still of type 'int'
				ex is PostfixExpression_Decrement)
				return baseExpression;

			if (ex is PostfixExpression_Access)
			{
				var r= E(ex as PostfixExpression_Access, ctxt, baseExpression);
				ctxt.CheckForSingleResult(r, ex);
				return r!=null && r.Length != 0 ? r[0] : null;
			}

			// myArray[0]; myArray[0..5];
			var arrayBaseType = DResolver.StripMemberSymbols(baseExpression);

			if (ex is PostfixExpression_Index)
			{
				if (arrayBaseType is AssocArrayType)
				{
					var ar = (AssocArrayType)arrayBaseType;
					/*
					 * myType_Array[0] -- returns TypeResult myType
					 * return the value type of a given array result
					 */
					//TODO: Handle opIndex overloads

					return ar.ValueType;
				}
				/*
				 * int* a = new int[10];
				 * 
				 * a[0] = 12;
				 */
				else if (arrayBaseType is PointerType)
					return ((PointerType)arrayBaseType).Base;
			}
			else if (ex is PostfixExpression_Slice) // Still of the array's type.
				return arrayBaseType;

			return null;
		}

		ISemantic E(PostfixExpression_MethodCall call, bool returnBaseTypeOnly=true)
		{
			// Deduce template parameters later on
			AbstractType[] baseExpression = null;
			TemplateInstanceExpression tix = null;

			// Explicitly don't resolve the methods' return types - it'll be done after filtering to e.g. resolve template types to the deduced one
			var optBackup = ctxt.CurrentContext.ContextDependentOptions;
			ctxt.CurrentContext.ContextDependentOptions = ResolutionOptions.DontResolveBaseTypes;

			if (call.PostfixForeExpression is PostfixExpression_Access)
			{
				var pac = (PostfixExpression_Access)call.PostfixForeExpression;
				if (pac.AccessExpression is TemplateInstanceExpression)
					tix = (TemplateInstanceExpression)pac.AccessExpression;

				baseExpression = TypeDeclarationResolver.Convert(E(pac, null, call));
			}
			else if (call.PostfixForeExpression is TemplateInstanceExpression)
			{
				tix = (TemplateInstanceExpression)call.PostfixForeExpression;
				baseExpression = TypeDeclarationResolver.Convert(GetOverloads(tix, null, false));
			}
			else
				baseExpression = new[] { E(call.PostfixForeExpression) as AbstractType };

			ctxt.CurrentContext.ContextDependentOptions = optBackup;

			var methodOverloads = new List<AbstractType>();

			#region Search possible methods, opCalls or delegates that could be called
			bool requireStaticItems = true; //TODO: What if there's an opCall and a foreign method at the same time? - and then this variable would be bullshit
			IEnumerable<AbstractType> scanResults = DResolver.StripAliasSymbols(baseExpression);
			var nextResults = new List<AbstractType>();

			while (scanResults != null)
			{
				foreach (var b in scanResults)
				{
					if (b is MemberSymbol)
					{
						var mr = (MemberSymbol)b;

						if (mr.Definition is DMethod)
						{
							methodOverloads.Add(mr);
						}

						/*
						 * If mr.Node is not a method, so e.g. if it's a variable
						 * pointing to a delegate
						 * 
						 * class Foo
						 * {
						 *	string opCall() {  return "asdf";  }
						 * }
						 * 
						 * Foo f=new Foo();
						 * f(); -- calls opCall, opCall is not static
						 */
						else if (mr != null)
						{
							nextResults.Add(mr.Base);

							requireStaticItems = false;
						}
					}
					else if (b is DelegateType)
					{
						var dg = (DelegateType)b;

						/*
						 * int a = delegate(x) { return x*2; } (12); // a is 24 after execution
						 * auto dg=delegate(x) {return x*3;};
						 * int b = dg(4);
						 */

						if (dg.IsFunctionLiteral)
							methodOverloads.Add(dg);
					}
					else if (b is ClassType)
					{
						/*
						 * auto a = MyStruct(); -- opCall-Overloads can be used
						 */
						var classDef = ((ClassType)b).Definition;

						if (classDef == null)
							continue;

						foreach (var i in classDef)
							if (i.Name == "opCall" && i is DMethod && (!requireStaticItems || (i as DNode).IsStatic))
								methodOverloads.Add(TypeDeclarationResolver.HandleNodeMatch(i, ctxt, b, call) as MemberSymbol);
					}
					/*
					 * Every struct can contain a default ctor:
					 * 
					 * struct S { int a; bool b; }
					 * 
					 * auto s = S(1,true); -- ok
					 * auto s2= new S(2,false); -- error, no constructor found!
					 */
					else if (b is StructType && methodOverloads.Count == 0)
					{
						//TODO: Deduce parameters
						return b;
					}
				}

				scanResults = nextResults.Count == 0 ? null : nextResults.ToArray();
				nextResults.Clear();
			}
			#endregion

			if (methodOverloads.Count == 0)
				return null;

			// Get all arguments' types
			var callArgumentTypes = new List<AbstractType>();
			if (call.Arguments != null)
				foreach (var arg in call.Arguments)
					callArgumentTypes.Add(E(arg) as AbstractType);

			#region Deduce template parameters and filter out unmatching overloads
			// UFCS argument assignment will be done per-overload and in the EvalAndFilterOverloads method!

			// First add optionally given template params
			// http://dlang.org/template.html#function-templates
			var resolvedCallArguments = tix == null ?
				new List<ISemantic>() :
				TemplateInstanceHandler.PreResolveTemplateArgs(tix, ctxt);

			// Then add the arguments' types
			resolvedCallArguments.AddRange(callArgumentTypes);

			var templateParamFilteredOverloads= TemplateInstanceHandler.EvalAndFilterOverloads(
				methodOverloads,
				resolvedCallArguments.Count > 0 ? resolvedCallArguments.ToArray() : null,
				true, ctxt);
			#endregion

			#region Filter by parameter-argument comparison
			var argTypeFilteredOverloads = new List<AbstractType>();

			foreach (var ov in templateParamFilteredOverloads)
			{
				if (ov is MemberSymbol)
				{
					var ms = (MemberSymbol)ov;
					var dm = ms.Definition as DMethod;
					bool add = false;

					if (dm != null)
					{
						ctxt.CurrentContext.IntroduceTemplateParameterTypes(ms);

						add = false;

						if (callArgumentTypes.Count == 0 && dm.Parameters.Count == 0)
							add=true;
						else
							for (int i=0; i< dm.Parameters.Count; i++)
							{
								var paramType = TypeDeclarationResolver.ResolveSingle(dm.Parameters[i].Type, ctxt);
								
								// TODO: Expression tuples & variable argument lengths
								if (i >= callArgumentTypes.Count ||
									!ResultComparer.IsImplicitlyConvertible(callArgumentTypes[i], paramType, ctxt))
									continue;

								add = true;
							}

						if (add)
						{
							var bt=TypeDeclarationResolver.GetMethodReturnType(dm, ctxt);

							if (returnBaseTypeOnly)
								argTypeFilteredOverloads.Add(bt);
							else
								argTypeFilteredOverloads.Add(new MemberSymbol(dm, bt, ms.DeclarationOrExpressionBase, ms.DeducedTypes));
						}

						ctxt.CurrentContext.RemoveParamTypesFromPreferredLocals(ms);
					}
				}
				else if(ov is DelegateType)
				{
					var dg = (DelegateType)ov;
					var bt = TypeDeclarationResolver.GetMethodReturnType(dg, ctxt);

					//TODO: Param-Arg check
					if (returnBaseTypeOnly)
						argTypeFilteredOverloads.Add(bt);
					else
						argTypeFilteredOverloads.Add(new DelegateType(bt, dg.DeclarationOrExpressionBase as FunctionLiteral, dg.Parameters));
				}
			}

			ctxt.CheckForSingleResult(argTypeFilteredOverloads.ToArray(), call);

			return argTypeFilteredOverloads!=null && argTypeFilteredOverloads.Count !=0 ? argTypeFilteredOverloads[0] : null;
			#endregion
		}

		ISemantic[] E(PostfixExpression_Access acc,
			AbstractType resultBase = null,
			IExpression supExpression=null)
		{
			if (acc == null)
				return null;

			var baseExpression = resultBase ?? E(acc.PostfixForeExpression) as AbstractType;

			if (acc.AccessExpression is TemplateInstanceExpression)
			{
				// Do not deduce and filter if superior expression is a method call since call arguments' types also count as template arguments!
				var res=E((TemplateInstanceExpression)acc.AccessExpression, ctxt, new[]{baseExpression}, 
					!(supExpression is PostfixExpression_MethodCall));

				// Try to resolve ufcs(?)
				return res ?? UFCSResolver.TryResolveUFCS(baseExpression, acc, ctxt);
			}
			else if (acc.AccessExpression is NewExpression)
			{
				/*
				 * This can be both a normal new-Expression as well as an anonymous class declaration!
				 */
				//TODO!
			}
			else if (acc.AccessExpression is IdentifierExpression)
			{
				var id = ((IdentifierExpression)acc.AccessExpression).Value as string;
				
				/*
				 * 1) First off, try to resolve the identifier as it was a type declaration's identifer list part.
				 * 2) Static properties
				 * 3) UFCS
				 */

				// 1)
				var results = TypeDeclarationResolver.ResolveFurtherTypeIdentifier(id, new[]{baseExpression}, ctxt, acc);

				if (results != null)
					return results;

				// 2)
				var staticTypeProperty = StaticPropertyResolver.TryResolveStaticProperties(baseExpression, id, ctxt);

				if (staticTypeProperty != null)
					return new[] { staticTypeProperty };

				// 3)
				var ufcsResult = UFCSResolver.TryResolveUFCS(baseExpression, acc, ctxt);

				if (ufcsResult != null)
					return ufcsResult;
			}
			else
				return new[]{ baseExpression };

			return null;
		}
	}
}
