using System;
using System.IO;
using System.Linq;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Misc;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.ExpressionSemantics;
using D_Parser.Resolver.TypeResolution;
using NUnit.Framework.Constraints;

namespace Tests
{
	public static class ResolutionContextExtensions
	{
		public static MutableRootPackage FirstPackage(this LegacyParseCacheView pcl)
		{
			return pcl.EnumRootPackagesSurroundingModule(null).First() as MutableRootPackage;
		}

		public static MutableRootPackage MainPackage(this ResolutionContext ctxt)
		{
			return (ctxt.ParseCache as LegacyParseCacheView).FirstPackage();
		}
	}

	class IsDefinition : NUnit.Framework.Constraints.Constraint
	{
		readonly INode n;
		object act;

		public IsDefinition(INode expectedDefinition) { n = expectedDefinition; }

		public override ConstraintResult ApplyTo<TActual>(TActual actual)
		{
			act = actual;
			return new ConstraintResult(this, actual, n == actual as INode || (actual is DSymbol && (actual as DSymbol).Definition == n));
		}
	}

	public class ResolutionTestHelper
	{
		public static DModule objMod = DParser.ParseString(@"module object;
						alias immutable(char)[] string;
						alias immutable(wchar)[] wstring;
						alias immutable(dchar)[] dstring;
						class Object { string toString(); }
						alias int size_t;
						class Exception { string msg; }");

		public static LegacyParseCacheView CreateCache(params string[] moduleCodes)
		{
			var r = new MutableRootPackage(objMod);

			foreach (var code in moduleCodes)
				r.AddModule(DParser.ParseString(code));

			return new LegacyParseCacheView(new[] { r });
		}

		public static ResolutionContext CreateDefCtxt(ParseCacheView pcl, IBlockNode scope, IStatement stmt = null)
		{
			CodeLocation loc = CodeLocation.Empty;

			if (stmt != null)
				loc = stmt.Location;
			else if (scope is DModule)
				loc = scope.EndLocation;
			else if (scope != null)
				loc = scope.Location;

			return CreateDefCtxt(pcl, scope, loc);
		}

		public static ResolutionContext CreateDefCtxt(ParseCacheView pcl, IBlockNode scope, CodeLocation caret)
		{
			var r = ResolutionContext.Create(pcl, new ConditionalCompilationFlags(new[] { "Windows", "all" }, 1, true, null, 0), scope, caret);
			r.CompletionOptions.CompletionTimeout = 0;
			r.CompletionOptions.DisableMixinAnalysis = false;
			return r;
		}

		public static ResolutionContext CreateDefCtxt(params string[] modules)
		{
			var pcl = CreateCache(modules);
			return CreateDefCtxt(pcl, pcl.FirstPackage().GetModules().First());
		}

		public static ResolutionContext CreateCtxt(string scopedModule, params string[] modules)
		{
			var pcl = CreateCache(modules);
			return CreateDefCtxt(pcl, pcl.FirstPackage()[scopedModule]);
		}

		public static ResolutionContext CreateDefCtxt(string scopedModule, out DModule mod, params string[] modules)
		{
			var pcl = CreateCache(modules);
			mod = pcl.FirstPackage()[scopedModule];

			return CreateDefCtxt(pcl, mod);
		}

		public static T N<T>(ResolutionContext ctxt, string path) where T : DNode
		{
			return GetChildNode<T>(ctxt, path);
		}

		public static T GetChildNode<T>(ResolutionContext ctxt, string path) where T : DNode
		{
			DModule mod = null;

			int dotIndex = -1;
			while ((dotIndex = path.IndexOf('.', dotIndex + 1)) > 0)
			{
				mod = ctxt.ParseCache.LookupModuleName(ctxt.ScopedBlock, path.Substring(0, dotIndex)).First();
				if (mod != null)
					break;
			}

			if (dotIndex == -1 && mod == null)
				return ctxt.ParseCache.LookupModuleName(ctxt.ScopedBlock, path).First() as T;

			if (mod == null)
				throw new ArgumentException("Invalid module name");

			return (T)GetChildNode(mod, path.Substring(dotIndex + 1));
		}

		public static T N<T>(IBlockNode parent, string path) where T : INode
		{
			return (T)GetChildNode(parent, path);
		}

		public static T GetChildNode<T>(IBlockNode parent, string path) where T : INode
		{
			return (T)GetChildNode(parent, path);
		}

		public static INode GetChildNode(IBlockNode parent, string path)
		{
			var childNameIndex = path.IndexOf(".");
			var childName = childNameIndex < 0 ? path : path.Substring(0, childNameIndex);

			var child = parent[childName].First();

			if (childNameIndex < 0)
				return child;

			if (!(child is IBlockNode))
				throw new ArgumentException("Invalid path");

			return GetChildNode(child as IBlockNode, path.Substring(childNameIndex + 1));
		}

		public static AbstractType[] R(string id, ResolutionContext ctxt)
		{
			return ExpressionTypeEvaluation.GetOverloads(new IdentifierExpression(id), ctxt, null, false) ?? new AbstractType[0];
		}

		public static AbstractType RS(string id, ResolutionContext ctxt)
		{
			return AmbiguousType.Get(ExpressionTypeEvaluation.GetOverloads(new IdentifierDeclaration(id), ctxt, null, false));
		}

		public static AbstractType RS(ITypeDeclaration id, ResolutionContext ctxt)
		{
			return TypeDeclarationResolver.ResolveSingle(id, ctxt);
		}

		public static IStatement S(DMethod dm, params int[] path)
		{
			IStatement stmt = null;
			StatementContainingStatement scs = dm.Body;

			foreach (var elementAt in path)
			{
				if (scs == null)
					throw new InvalidDataException();

				stmt = scs.SubStatements.ElementAt(elementAt);
				scs = stmt as StatementContainingStatement;
			}

			return stmt;
		}
	}
}
