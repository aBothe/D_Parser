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
            if (whiteList != null)
            {
                remainingWhiteList = new List<INode>(whiteList);
                this.whiteList = new List<INode>(whiteList);
            }

            if (blackList != null)
                this.blackList = new List<INode>(blackList);
        }

        public List<INode> remainingWhiteList;
        public List<INode> whiteList;
        public List<INode> blackList;
        public string suggestedItem;

        #region ICompletionDataGenerator implementation

        public void SetSuggestedItem(string item)
        {
            suggestedItem = item;
        }

        public void AddCodeGeneratingNodeItem(INode node, string codeToGenerate)
        {
        }

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

        public void AddTextItem(string Text, string Description)
        {
        }

        public void AddIconItem(string iconName, string text, string description)
        {
        }

        public readonly List<INode> addedItems = new List<INode>();

        public void Add(INode n)
        {
            if (blackList != null && blackList.Contains(n))
                Assert.Fail();

            if (whiteList != null && whiteList.Contains(n) && !remainingWhiteList.Remove(n))
                Assert.Fail(n + " occurred at least twice!");

            addedItems.Add(n);
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

        public bool HasRemainingItems => remainingWhiteList != null && remainingWhiteList.Count > 0;


        public void NotifyTimeout()
        {
            throw new OperationCanceledException();
        }

        public ISyntaxRegion TriggerSyntaxRegion { get; set; }
    }
}