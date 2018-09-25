using System;
using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Resolver.Templates;

namespace D_Parser.Resolver.TypeResolution
{
	class ClassInterfaceResolver
	{
		static readonly int ObjectNameHash = "Object".GetHashCode();

		static ClassType ResolveObjectClass(ResolutionContext ctxt)
		{
			using (ctxt.Push(ctxt.ScopedBlock == null ? null : ctxt.ScopedBlock.NodeRoot)) //TODO: understand why we're passing null
				return TypeDeclarationResolver.ResolveSingle(new IdentifierDeclaration(ObjectNameHash), ctxt, false) as ClassType;
		}

		[ThreadStatic]
		static int bcStack = 0;
		[ThreadStatic]
		static List<ISyntaxRegion> parsedClassInstanceDecls;
		/// <summary>
		/// Takes the class passed via the tr, and resolves its base class and/or implemented interfaces.
		/// Also usable for enums.
		///
		/// Never returns null. Instead, the original 'tr' object will be returned if no base class was resolved.
		/// Will clone 'tr', whereas the new object will contain the base class.
		/// </summary>
		public static TemplateIntermediateType ResolveClassOrInterface(
			DClassLike dc,
			ResolutionContext ctxt,
			ISyntaxRegion instanceDeclaration,
			bool ResolveFirstBaseIdOnly = false,
			IEnumerable<TemplateParameterSymbol> extraDeducedTemplateParams = null)
		{
			if (parsedClassInstanceDecls == null)
				parsedClassInstanceDecls = new List<ISyntaxRegion>();

			switch (dc.ClassType)
			{
				case DTokens.Class:
				case DTokens.Interface:
					break;
				default:
					if (dc.BaseClasses.Count != 0)
						ctxt.LogError(dc, "Only classes and interfaces may inherit from other classes/interfaces");
					return null;
			}

			bool isClass = dc.ClassType == DTokens.Class;

			if (bcStack > 6 || (instanceDeclaration != null && parsedClassInstanceDecls.Contains(instanceDeclaration)))
			{
				return isClass ? new ClassType(dc, null) as TemplateIntermediateType : new InterfaceType(dc);
			}

			if (instanceDeclaration != null)
				parsedClassInstanceDecls.Add(instanceDeclaration);
			bcStack++;

			try
			{
				var deducedTypes = ResolveInstantiationTemplateArguments(dc, ctxt, instanceDeclaration);
				if (deducedTypes == null)
					return null;

				if (extraDeducedTemplateParams != null)
					foreach (var tps in extraDeducedTemplateParams)
						deducedTypes[tps.Parameter] = tps;

				TemplateIntermediateType baseClass;
				List<InterfaceType> interfaces;
				ResolveBaseClasses(dc, ctxt, instanceDeclaration, ResolveFirstBaseIdOnly, deducedTypes, out baseClass, out interfaces);

				if (isClass)
					return new ClassType(dc, baseClass, interfaces.Count == 0 ? null : interfaces.ToArray(), deducedTypes);

				return new InterfaceType(dc, interfaces.Count == 0 ? null : interfaces.ToArray(), deducedTypes);
			}
			finally
			{
				parsedClassInstanceDecls.Remove(instanceDeclaration);
				bcStack--;
			}
		}

		private static DeducedTypeDictionary ResolveInstantiationTemplateArguments(DClassLike dc, ResolutionContext ctxt, ISyntaxRegion instanceDeclaration)
		{
			var deducedTypes = new DeducedTypeDictionary(dc);
			var tix = instanceDeclaration as TemplateInstanceExpression;
			if (tix != null && (ctxt.Options & ResolutionOptions.NoTemplateParameterDeduction) == 0)
			{
				// Pop a context frame as we still need to resolve the template instance expression args in the place where the expression occurs, not the instantiated class' location
				var backup = ctxt.CurrentContext;
				ctxt.Pop();

				if (ctxt.CurrentContext == null)
					ctxt.Push(backup);

				var givenTemplateArguments = TemplateInstanceHandler.PreResolveTemplateArgs(new[] { dc }, tix, ctxt);

				if (ctxt.CurrentContext != backup)
				{
					foreach (var kv in ctxt.CurrentContext.DeducedTemplateParameters)
					{
						backup.DeducedTemplateParameters[kv.Key] = kv.Value;
						deducedTypes[kv.Key] = kv.Value;
					}
					ctxt.Push(backup);
				}

				if (!TemplateInstanceHandler.DeduceParams(givenTemplateArguments, false, ctxt, null, dc, deducedTypes))
					return null;
			}

			return deducedTypes;
		}

		private static void ResolveBaseClasses(DClassLike dc,
			ResolutionContext ctxt,
			ISyntaxRegion instanceDeclaration,
			bool ResolveFirstBaseIdOnly,
			DeducedTypeDictionary deducedTypes,
			out TemplateIntermediateType baseClass,
			out List<InterfaceType> interfaces)
		{
			interfaces = new List<InterfaceType>();

			if(dc.BaseClasses == null || dc.BaseClasses.Count == 0)
			{
				baseClass = dc.NameHash != ObjectNameHash ? ResolveObjectClass(ctxt) : null;
				return;
			}

			baseClass = null;

			var back = ctxt.ScopedBlock;
			using (ctxt.Push(dc.Parent))
			{
				var pop = back != ctxt.ScopedBlock;

				ctxt.CurrentContext.DeducedTemplateParameters.Add(deducedTypes);

				for (int i = 0; i < (ResolveFirstBaseIdOnly ? 1 : dc.BaseClasses.Count); i++)
				{
					ResolveBaseClassOrInterface(dc.BaseClasses[i], i == 0, dc, ctxt, ref baseClass, ref interfaces);
				}

				if (!pop)
					ctxt.CurrentContext.DeducedTemplateParameters.Remove(deducedTypes); // May be backup old tps?
			}
		}

		private static void ResolveBaseClassOrInterface(ITypeDeclaration type,
			bool isFirstBase,
			DClassLike dc,
			ResolutionContext ctxt,
			ref TemplateIntermediateType baseClass,
			ref List<InterfaceType> interfaces)
		{
			// If there's an explicit 'Object' inheritance, also return the pre-resolved object class
			if (type is IdentifierDeclaration &&
				(type as IdentifierDeclaration).IdHash == ObjectNameHash)
			{
				if (baseClass != null)
					ctxt.LogError(new ResolutionError(dc, "Class must not have two base classes"));
				else if (!isFirstBase)
					ctxt.LogError(new ResolutionError(dc, "The base class name must preceed base interfaces"));
				else
					baseClass = ResolveObjectClass(ctxt);

				return;
			}

			if (type == null || (type is IdentifierDeclaration && (type as IdentifierDeclaration).IdHash == dc.NameHash) || dc.NodeRoot == dc)
			{
				ctxt.LogError(new ResolutionError(dc, "A class cannot inherit from itself"));
				return;
			}

			var r = DResolver.StripMemberSymbols(TypeDeclarationResolver.ResolveSingle(type, ctxt));

			if (r is ClassType || r is TemplateType)
			{
				if (dc.ClassType != DTokens.Class)
					ctxt.LogError(new ResolutionError(type, "An interface cannot inherit from non-interfaces"));
				else if (isFirstBase)
					baseClass = r as TemplateIntermediateType;
				else
					ctxt.LogError(new ResolutionError(dc, "The base " + (r is ClassType ? "class" : "template") + " name must preceed base interfaces"));
			}
			else if (r is InterfaceType)
			{
				interfaces.Add(r as InterfaceType);

				if (dc.ClassType == DTokens.Class && dc.NameHash != ObjectNameHash && baseClass == null)
					baseClass = ResolveObjectClass(ctxt);
			}
			else
			{
				ctxt.LogError(new ResolutionError(type, "Resolved class is neither a class nor an interface"));
			}
		}
	}
}
