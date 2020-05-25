using System;
using System.Collections.Generic;
using D_Parser.Completion;
using D_Parser.Dom;
using NUnit.Framework;

namespace Tests.Completion
{
    class TestCompletionDataGen : ICompletionDataGenerator
    {
        public TestCompletionDataGen(INode[] whiteList, INode[] blackList)
        {
            _remainingWhiteList = new List<INode>(whiteList ?? new INode[0]);
            _whiteList = new List<INode>(whiteList ?? new INode[0]);
            _blackList = new List<INode>(blackList ?? new INode[0]);
        }

        private readonly List<INode> _remainingWhiteList;
        private readonly List<INode> _whiteList;
        private readonly List<INode> _blackList;
        public readonly List<string> AddedTextItems = new List<string>();
        public string suggestedItem;

        #region ICompletionDataGenerator implementation

        public void SetSuggestedItem(string item)
        {
            suggestedItem = item;
        }

        public void AddCodeGeneratingNodeItem(INode node, string codeToGenerate)
        {
        }

        public bool IsEmpty => AddedTextItems.Count == 0 && suggestedItem == null && Tokens.Count == 0 &&
                               Attributes.Count == 0 && AddedItems.Count == 0 && Packages.Count == 0;

        public readonly List<byte> Tokens = new List<byte>();

        public void Add(byte Token)
        {
            Tokens.Add(Token);
        }

        public readonly List<string> Attributes = new List<string>();

        public void AddPropertyAttribute(string AttributeText)
        {
            Attributes.Add(AttributeText);
        }

        public void AddTextItem(string text, string Description)
        {
            AddedTextItems.Add(text);
        }

        public void AddIconItem(string iconName, string text, string description)
        {
        }

        public readonly List<INode> AddedItems = new List<INode>();

        public void Add(INode n)
        {
            if (_blackList.Contains(n))
                Assert.Fail();

            if (_whiteList.Contains(n) && !_remainingWhiteList.Remove(n))
                Assert.Fail(n + " occurred at least twice!");

            AddedItems.Add(n);
        }

        public void AddModule(DModule module, string nameOverride = null)
        {
            this.Add(module);
        }

        public readonly List<string> Packages = new List<string>();

        public void AddPackage(string packageName)
        {
            Packages.Add(packageName);
        }

        #endregion

        public bool HasRemainingItems => _remainingWhiteList.Count > 0;


        public void NotifyTimeout()
        {
            throw new OperationCanceledException();
        }

        public ISyntaxRegion TriggerSyntaxRegion { get; set; }
    }
}