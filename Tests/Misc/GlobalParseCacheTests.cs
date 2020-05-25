using System;
using System.IO;
using System.Linq;
using D_Parser.Dom;
using D_Parser.Misc;
using D_Parser.Parser;
using NUnit.Framework;

namespace Tests.Misc
{
	[TestFixture]
	public class GlobalParseCacheTests
	{
		[Test]
		public void ParseDirectory()
		{
			var tempDirectory = Path.Combine(Path.GetTempPath(), "dparser_test");
			try
			{
				Directory.CreateDirectory(tempDirectory);
				var tempModulePath = Path.Combine(tempDirectory, "modA.d");

				File.WriteAllText(tempModulePath, @"module modA; void bar();");
				var stats = GlobalParseCache.BeginAddOrUpdatePaths(tempDirectory)[0];
				Assert.IsTrue(stats.WaitForCompletion(10000));

				var module = GlobalParseCache.GetModule(tempDirectory, "modA");
				Assert.AreEqual(1, module.Children["bar"].Count());

				File.Delete(tempModulePath);
				File.WriteAllText(tempModulePath, @"module modA; void baz();");
				stats = GlobalParseCache.BeginAddOrUpdatePaths(tempDirectory)[0];
				Assert.IsTrue(stats.WaitForCompletion(10000));

				module = GlobalParseCache.GetModule(tempDirectory, "modA");
				Assert.AreEqual(0, module.Children["bar"].Count());
				Assert.AreEqual(1, module.Children["baz"].Count());
			}
			finally
			{
				Directory.Delete(tempDirectory, true);
				Assert.IsTrue(GlobalParseCache.RemoveRoot(tempDirectory));
			}
		}

		[Test]
		public void ParseDirectory_UpdateManually()
		{
			var tempDirectory = Path.Combine(Path.GetTempPath(), "dparser_test");
			try
			{
				Directory.CreateDirectory(tempDirectory);
				var tempModulePath = Path.Combine(tempDirectory, "modA.d");

				File.WriteAllText(tempModulePath, @"module modA; void bar();");
				var stats = GlobalParseCache.BeginAddOrUpdatePaths(tempDirectory)[0];
				Assert.IsTrue(stats.WaitForCompletion(10000));

				var module = GlobalParseCache.GetModule(tempDirectory, "modA");
				Assert.AreEqual(1, module.Children["bar"].Count());

				var moduleToPatch = DParser.ParseString(@"module modA; void baz();");
				moduleToPatch.FileName = tempModulePath;
				GlobalParseCache.AddOrUpdateModule(moduleToPatch);

				module = GlobalParseCache.GetModule(tempDirectory, "modA");
				Assert.AreEqual(0, module.Children["bar"].Count());
				Assert.AreEqual(1, module.Children["baz"].Count());
			}
			finally
			{
				Directory.Delete(tempDirectory, true);
				Assert.IsTrue(GlobalParseCache.RemoveRoot(tempDirectory));
			}
		}

		[Test]
		public void ParseEmptyDirectoryList()
		{
			int callbackInvokeCount = 0;
			var stats = GlobalParseCache.BeginAddOrUpdatePaths(Enumerable.Empty<string>(), false,
				ea => callbackInvokeCount++);
			Assert.AreEqual(0, stats.Count);
			Assert.AreEqual(1, callbackInvokeCount);
		}

		[Test]
		public void PackageModuleEnumeration()
		{
			var tempDirectory = Path.Combine(Path.GetTempPath(), "dparser_test");
			var subDirectory = Path.Combine(tempDirectory, "sub");
			try
			{
				Directory.CreateDirectory(tempDirectory);
				var tempModulePath = Path.Combine(tempDirectory, "modB.d");
				Directory.CreateDirectory(subDirectory);
				var tempModuleCPath = Path.Combine(subDirectory, "modC.d");

				File.WriteAllText(tempModulePath, @"module modB; void bar();");
				File.WriteAllText(tempModuleCPath, @"module sub.modC; void keks();");
				var stats = GlobalParseCache.BeginAddOrUpdatePaths(tempDirectory)[0];
				Assert.IsTrue(stats.WaitForCompletion(10000));

				Assert.AreEqual("modB", GlobalParseCache.GetModule(tempModulePath).ModuleName);
				Assert.AreEqual("sub.modC", GlobalParseCache.GetModule(tempDirectory, "sub.modC", out var pack).ModuleName);
				Assert.AreEqual("sub", pack.Name);

				var packages = GlobalParseCache.EnumPackagesRecursively(true, tempDirectory);
				Assert.AreEqual(2, packages.Count);
				Assert.IsInstanceOf<RootPackage>(packages[0]);
				Assert.AreEqual("sub", packages[1].Name);

				{
					var modules = GlobalParseCache.EnumModulesRecursively(tempDirectory);
					Assert.AreEqual(2, modules.Count);
					Assert.AreEqual("modB", modules[0].ModuleName);
					Assert.AreEqual("sub.modC", modules[1].ModuleName);
				}

				Assert.IsTrue(GlobalParseCache.RemoveModule(tempDirectory, "modB"));
				{
					var modules = GlobalParseCache.EnumModulesRecursively(tempDirectory);
					Assert.AreEqual(1, modules.Count);
					Assert.AreEqual("sub.modC", modules[0].ModuleName);
				}
			}
			finally
			{
				Directory.Delete(tempDirectory, true);
				Assert.IsTrue(GlobalParseCache.RemoveRoot(tempDirectory));
			}
		}
	}
}