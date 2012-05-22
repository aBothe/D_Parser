using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom;
using D_Parser.Dom.Statements;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Resolver.Templates
{
	partial class TemplateParameterDeduction
	{
		public bool Handle(TemplateTypeParameter p, ResolveResult arg)
		{
			// if no argument given, try to handle default arguments
			if (arg == null)
			{
				if (p.Default == null)
					return false;
				else
				{
					IStatement stmt = null;
					ctxt.PushNewScope(DResolver.SearchBlockAt(ctxt.ScopedBlock.NodeRoot as IBlockNode, p.Default.Location, out stmt));
					ctxt.ScopedStatement = stmt;
					var defaultTypeRes = TypeDeclarationResolver.Resolve(p.Default, ctxt);
					bool b = false;
					if (defaultTypeRes != null)
						b = Set(p.Name, defaultTypeRes.First());
					ctxt.Pop();
					return b;
				}
			}

			// If no spezialization given, assign argument immediately
			if (p.Specialization == null)
				return Set(p.Name, arg);

			bool handleResult= HandleDecl(p.Specialization,arg);

			if (!handleResult)
				return false;

			// Apply the entire argument to parameter p if there hasn't been no explicit association yet
			if (!TargetDictionary.ContainsKey(p.Name) || TargetDictionary[p.Name] == null || TargetDictionary[p.Name].Count == 0)
				TargetDictionary[p.Name] = new List<ResolveResult>(new[]{arg});

			return true;
		}

		bool HandleDecl(ITypeDeclaration td, ResolveResult rr)
		{
			if (td is ArrayDecl)
				return HandleDecl((ArrayDecl)td, rr);
			else if (td is IdentifierDeclaration)
				return HandleDecl((IdentifierDeclaration)td, rr);
			else if (td is DTokenDeclaration)
				return HandleDecl((DTokenDeclaration)td, rr);
			else if (td is DelegateDeclaration)
				return HandleDecl((DelegateDeclaration)td, rr);
			else if (td is PointerDecl)
				return HandleDecl((PointerDecl)td,rr);
			return false;
		}

		bool HandleDecl(IdentifierDeclaration id, ResolveResult r)
		{
			// Bottom-level reached
			if (id.InnerDeclaration == null && Contains(id.Id) && !id.ModuleScoped)
			{
				// Associate template param with r
				return Set(id.Id, r);
			}

			/*
			 * If not stand-alone identifier or is not required as template param, resolve the id and compare it against r
			 */
			var _r = TypeDeclarationResolver.Resolve(id, ctxt);
			return _r == null || _r.Length == 0 || 
				ResultComparer.IsImplicitlyConvertible(_r[0], r);
		}

		bool HandleDecl(DTokenDeclaration tk, ResolveResult r)
		{
			if (r is StaticTypeResult && r.DeclarationOrExpressionBase is DTokenDeclaration)
				return tk.Token == ((DTokenDeclaration)r.DeclarationOrExpressionBase).Token;

			return false;
		}

		bool HandleDecl(ArrayDecl ad, ResolveResult r)
		{
			if (r is ArrayResult)
			{
				var ar = (ArrayResult)r;

				// Handle key type
				if((ad.KeyType != null || ad.KeyExpression!=null)&& (ar.KeyType == null || ar.KeyType.Length == 0))
					return false;
				bool result = false;

				if (ad.KeyExpression != null)
				{
					if (ar.ArrayDeclaration.KeyExpression != null)
						result = Evaluation.ExpressionEvaluator.IsEqual(ad.KeyExpression, ar.ArrayDeclaration.KeyExpression, ctxt);
				}
				else if(ad.KeyType!=null)
					result = HandleDecl(ad.KeyType, ar.KeyType[0]);

				if (!result)
					return false;

				// Handle inner type
				return HandleDecl(ad.InnerDeclaration, ar.ResultBase);
			}

			return false;
		}

		bool HandleDecl(DelegateDeclaration d, ResolveResult r)
		{
			if (r is DelegateResult)
			{
				var dr = (DelegateResult)r;

				// Delegate literals or other expressions are not allowed
				if(!dr.IsDelegateDeclaration)
					return false;

				var dr_decl = (DelegateDeclaration)dr.DeclarationOrExpressionBase;

				// Compare return types
				if(	d.IsFunction == dr_decl.IsFunction &&
					dr.ReturnType!=null && 
					dr.ReturnType.Length!=0 && 
					HandleDecl(d.ReturnType,dr.ReturnType[0]))
				{
					// If no delegate args expected, it's valid
					if ((d.Parameters == null || d.Parameters.Count == 0) &&
						dr_decl.Parameters == null || dr_decl.Parameters.Count == 0)
						return true;

					// If parameter counts unequal, return false
					else if (d.Parameters == null || dr_decl.Parameters == null || d.Parameters.Count != dr_decl.Parameters.Count)
						return false;

					// Compare & Evaluate each expected with given parameter
					var dr_paramEnum = dr_decl.Parameters.GetEnumerator();
					foreach (var p in d.Parameters)
					{
						// Compare attributes with each other
						if (p is DNode)
						{
							if (!(dr_paramEnum.Current is DNode))
								return false;

							var dn = (DNode)p;
							var dn_arg = (DNode)dr_paramEnum.Current;

							if ((dn.Attributes == null || dn.Attributes.Count == 0) &&
								(dn_arg.Attributes == null || dn_arg.Attributes.Count == 0))
								return true;

							else if (dn.Attributes == null || dn_arg.Attributes == null ||
								dn.Attributes.Count != dn_arg.Attributes.Count)
								return false;

							foreach (var attr in dn.Attributes)
							{
								if (attr.IsProperty ?
									!dn_arg.ContainsPropertyAttribute(attr.LiteralContent as string) :
									!dn_arg.ContainsAttribute(attr.Token))
									return false;
							}
						}

						// Compare types
						if (p.Type!=null && dr_paramEnum.MoveNext() && dr_paramEnum.Current.Type!=null)
						{
							var dr_resolvedParamType = TypeDeclarationResolver.Resolve(dr_paramEnum.Current.Type, ctxt);

							if (dr_resolvedParamType == null ||
								dr_resolvedParamType.Length == 0 ||
								!HandleDecl(p.Type, dr_resolvedParamType[0]))
								return false;
						}
						else
							return false;
					}
				}
			}

			return false;
		}

		bool HandleDecl(PointerDecl p, ResolveResult r)
		{
			if (r is StaticTypeResult && r.DeclarationOrExpressionBase is PointerDecl)
			{
				return HandleDecl(p.InnerDeclaration, r.ResultBase);
			}

			return false;
		}
	}
}
