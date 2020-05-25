using D_Parser.Dom;
using D_Parser.Resolver;
using D_Parser.Resolver.TypeResolution;

namespace D_Parser.Completion.Providers
{
    public class VariableNameSuggestionCompletionProvider : AbstractCompletionProvider
    {
        private readonly DNode _node;
        
        public VariableNameSuggestionCompletionProvider(ICompletionDataGenerator completionDataGenerator, DNode node)
            : base(completionDataGenerator)
        {
            _node = node;
        }

        protected override void BuildCompletionDataInternal(IEditorData editor, char enteredChar)
        {
            var ctxt = ResolutionContext.Create(editor, true);
            var type = TypeDeclarationResolver.ResolveSingle(_node.Type, ctxt);
            while (type is TemplateParameterSymbol tps)
                type = tps.Base;
            if (type is TemplateIntermediateType tit && !string.IsNullOrEmpty(tit.Definition.Name))
            {
                var name = tit.Definition.Name;
                var camelCasedName = char.ToLowerInvariant(name[0]) + name.Substring(1);
                CompletionDataGenerator.SetSuggestedItem(camelCasedName);
                CompletionDataGenerator.AddTextItem(camelCasedName, string.Empty);
            }
        }
    }
}