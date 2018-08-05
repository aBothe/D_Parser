using D_Parser.Dom;
using D_Parser.Parser;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace D_Parser.Completion.Providers
{
	class MethodOverrideCompletionProvider : AbstractCompletionProvider
	{
		//TODO: Filter out already implemented methods
		readonly DNode begunNode;

		public MethodOverrideCompletionProvider(DNode begunNode, ICompletionDataGenerator gen)
			: base(gen)
		{
			this.begunNode = begunNode;
		}

		protected override void BuildCompletionDataInternal(IEditorData Editor, char enteredChar)
		{
			DClassLike dc;
			var intermediateBlock = begunNode.Parent as DBlockNode; // Skip the incremental parse block
			if (intermediateBlock != null && intermediateBlock.Location.IsEmpty)
				dc = intermediateBlock.Parent as DClassLike;
			else
				dc = begunNode.Parent as DClassLike;

			if (dc == null || dc.ClassType != DTokens.Class)
				return;

			TemplateIntermediateType classType = null;
			var ctxt = ResolutionContext.Create (Editor, true);

			CodeCompletion.DoTimeoutableCompletionTask(null, ctxt, ()=>
				classType =	DResolver.ResolveClassOrInterface(dc, ctxt,	null) as TemplateIntermediateType, Editor.CancelToken);

			if (classType == null)
				return;

			var typesToScan = new List<TemplateIntermediateType>();
			IterateThroughBaseClassesInterfaces(typesToScan, classType);

			foreach (var t in typesToScan)
			{
				foreach (var n in t.Definition)
				{
					var dm = n as DMethod;
					if (dm == null ||
						dm.ContainsAnyAttribute(DTokens.Final, DTokens.Private, DTokens.Static))
						continue; //TODO: Other attributes?

					CompletionDataGenerator.AddCodeGeneratingNodeItem(dm, GenerateOverridingMethodStub(dm, begunNode, !(t is InterfaceType)));
				}
			}
		}

		static void IterateThroughBaseClassesInterfaces(List<TemplateIntermediateType> l, TemplateIntermediateType tit)
		{
			if (tit == null)
				return;

			var @base = tit.Base as TemplateIntermediateType;
			if (@base != null)
			{
				if (!l.Contains(@base))
					l.Add(@base);
				IterateThroughBaseClassesInterfaces(l, @base);
			}

			if (tit.BaseInterfaces != null)
				foreach (var I in tit.BaseInterfaces)
				{
					if (!l.Contains(I))
						l.Add(I);
					IterateThroughBaseClassesInterfaces(l, I);
				}
		}

		static string GenerateOverridingMethodStub(DMethod dm, DNode begunNode, bool generateExecuteSuperFunctionStmt = true)
		{
			var sb = new StringBuilder();

			// Append missing attributes
			var remainingAttributes = new List<DAttribute>(dm.Attributes);
			if (begunNode != null && begunNode.Attributes != null)
			{
				foreach (var attr in begunNode.Attributes)
				{
					var mod = attr as Modifier;
					if (mod == null)
						continue;

					foreach (var remAttr in remainingAttributes)
					{
						var remMod = remAttr as Modifier;
						if (remMod == null)
							continue;

						if (mod.Token == remMod.Token)
						{
							remainingAttributes.Remove(remAttr);
							break;
						}
					}
				}
			}

			foreach (var attr in remainingAttributes) {
				// Don't take 'abstract' into new method
				if (attr is Modifier && (attr as Modifier).Token == DTokens.Abstract)
					continue;

				if (attr.Location < dm.NameLocation)
					sb.Append (attr.ToString ()).Append (' ');
			}

			// Type

			if (dm.Type != null)
				sb.Append(dm.Type.ToString()).Append(' ');


			// Name

			sb.Append(dm.Name);

			// Template Parameters

			if (dm.TemplateParameters != null && dm.TemplateParameters.Length != 0)
			{
				sb.Append('(');
				foreach (var tp in dm.TemplateParameters)
					sb.Append(tp.ToString()).Append(',');
				if (sb[sb.Length - 1] == ',')
					sb.Length--;
				sb.Append(')');
			}

			// Parameters

			sb.Append('(');
			foreach (var p in dm.Parameters)
				sb.Append((p is AbstractNode ? (p as AbstractNode).ToString(false) : p.ToString())).Append(',');
			if (sb[sb.Length - 1] == ',')
				sb.Length--;
			sb.Append(") ");

			// Post-param attributes

			foreach (var attr in remainingAttributes)
				if (attr.Location > dm.NameLocation)
					sb.Append(attr.ToString()).Append(' ');

			// Return stub
			sb.AppendLine("{");

			if (generateExecuteSuperFunctionStmt)
			{
				if (dm.Type == null || !(dm.Type is DTokenDeclaration && (dm.Type as DTokenDeclaration).Token == DTokens.Void))
					sb.Append("return ");

				sb.Append("super.").Append(dm.Name);

				if (dm.TemplateParameters != null && dm.TemplateParameters.Length != 0)
				{
					sb.Append("!(");
					foreach (var tp in dm.TemplateParameters)
						sb.Append(tp.Name).Append(',');
					if (sb[sb.Length - 1] == ',')
						sb.Length--;
					sb.Append(')');
				}

				if (dm.Parameters.Count != 0) // super.foo will also call the base overload;
				{
					sb.Append('(');
					foreach (var p in dm.Parameters)
						sb.Append(p.Name).Append(',');
					if (sb[sb.Length - 1] == ',')
						sb.Length--;
					sb.Append(')');
				}

				sb.AppendLine(";");
			}
			else
				sb.AppendLine();

			sb.AppendLine("}");

			return sb.ToString();
		}
	}
}
