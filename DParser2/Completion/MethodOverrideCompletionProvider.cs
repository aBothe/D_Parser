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

		public MethodOverrideCompletionProvider(DNode begunNode,ICompletionDataGenerator gen)
			: base(gen)
		{
			this.begunNode = begunNode;
		}

		protected override void BuildCompletionDataInternal(IEditorData Editor, char enteredChar)
		{
			var dc = begunNode.Parent as DClassLike;

			if (dc == null || dc.ClassType != DTokens.Class)
				return;

			var classType = DResolver.ResolveClassOrInterface(dc, ResolutionContext.Create(Editor), null) as TemplateIntermediateType;

			if (classType == null)
				return;

			while ((classType = classType.Base as TemplateIntermediateType) != null)
			{
				foreach (var n in classType.Definition)
				{
					var dm = n as DMethod;
					if (dm == null ||
						dm.ContainsAttribute(DTokens.Override, DTokens.Final, DTokens.Private, DTokens.Static))
						continue; //TODO: Other attributes?

					CompletionDataGenerator.AddCodeGeneratingNodeItem(dm, GenerateOverridingMethodStub(dm, begunNode));
				}
			}
		}

		static string GenerateOverridingMethodStub(DMethod dm, DNode begunNode = null)
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

			foreach (var attr in remainingAttributes)
				if(attr.Location < dm.NameLocation)
					sb.Append(attr.ToString()).Append(' ');

			// Type

			if (dm.Type != null)
				sb.Append(dm.Type.ToString()).Append(' ');


			// Name

			sb.Append(dm.Name);

			// Template Parameters

			if (dm.TemplateParameters != null && dm.TemplateParameters.Length != 0)
			{
				sb.Append('(');
				foreach(var tp in dm.TemplateParameters)
					sb.Append(tp.ToString()).Append(',');
				if (sb[sb.Length - 1] == ',')
					sb.Length--;
				sb.Append(')');
			}

			// Parameters

			sb.Append('(');
			foreach (var p in dm.Parameters)
				sb.Append ((p is AbstractNode ? (p as AbstractNode).ToString(false) : p.ToString())).Append(',');
			if (sb[sb.Length - 1] == ',')
				sb.Length--;
			sb.Append(") ");

			// Post-param attributes

			foreach (var attr in remainingAttributes)
				if (attr.Location > dm.NameLocation)
					sb.Append(attr.ToString()).Append(' ');

			// Return stub

			sb.AppendLine("{");

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

			sb.AppendLine(";").AppendLine("}");

			return sb.ToString();
		}
	}
}
