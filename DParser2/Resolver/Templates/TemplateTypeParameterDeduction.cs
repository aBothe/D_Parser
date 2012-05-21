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

			return HandleDecl(p.Specialization,arg);
		}

		bool HandleDecl(ITypeDeclaration td, ResolveResult rr)
		{
			if (td is ArrayDecl)
				return HandleDecl((ArrayDecl)td, rr);
			else if (td is IdentifierDeclaration)
				return HandleDecl((IdentifierDeclaration)td, rr);
			return false;
		}

		bool HandleDecl(ArrayDecl ad, ResolveResult r)
		{
			if (r is ArrayResult)
			{
				var ar = (ArrayResult)r;

				return HandleDecl(ad.InnerDeclaration, ar.ResultBase);
			}

			return false;
		}

		bool HandleDecl(IdentifierDeclaration id, ResolveResult r)
		{
			// Bottom-level reached
			if (id.InnerDeclaration== null && Contains(id.Id))
			{
				// Associate template param with r
				return Set(id.Id, r);
			}

			return false;
		}
	}
}
