using System;
using System.IO;
using System.Linq;
using System.Threading;
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
				GlobalParseCache.BeginAddOrUpdatePaths(tempDirectory);
				Assert.That(GlobalParseCache.WaitForFinish(10000));

				var module = GlobalParseCache.GetModule(tempDirectory, "modA");
				Assert.That(module.Children["bar"].Count(), Is.EqualTo(1));

				File.Delete(tempModulePath);
				File.WriteAllText(tempModulePath, @"module modA; void baz();");
				GlobalParseCache.BeginAddOrUpdatePaths(tempDirectory);
				Assert.That(GlobalParseCache.WaitForFinish(10000));

				module = GlobalParseCache.GetModule(tempDirectory, "modA");
				Assert.That(module.Children["bar"].Count(), Is.EqualTo(0));
				Assert.That(module.Children["baz"].Count(), Is.EqualTo(1));
			}
			finally
			{
				Directory.Delete(tempDirectory, true);
				Assert.That(GlobalParseCache.RemoveRoot(tempDirectory));
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
				GlobalParseCache.BeginAddOrUpdatePaths(tempDirectory);
				Assert.That(GlobalParseCache.WaitForFinish(10000));

				var module = GlobalParseCache.GetModule(tempDirectory, "modA");
				Assert.That(module.Children["bar"].Count(), Is.EqualTo(1));

				var moduleToPatch = DParser.ParseString(@"module modA; void baz();");
				moduleToPatch.FileName = tempModulePath;
				GlobalParseCache.AddOrUpdateModule(moduleToPatch);

				module = GlobalParseCache.GetModule(tempDirectory, "modA");
				Assert.That(module.Children["bar"].Count(), Is.EqualTo(0));
				Assert.That(module.Children["baz"].Count(), Is.EqualTo(1));
			}
			finally
			{
				Directory.Delete(tempDirectory, true);
				Assert.That(GlobalParseCache.RemoveRoot(tempDirectory));
			}
		}

		[Test]
		public void ParseEmptyDirectoryList()
		{
			int callbackInvokeCount = 0;
			GlobalParseCache.BeginAddOrUpdatePaths(Enumerable.Empty<string>(), false,
				ea => callbackInvokeCount++);
			Assert.That(GlobalParseCache.WaitForFinish(10000));
			Assert.That(callbackInvokeCount, Is.EqualTo(1));
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
				GlobalParseCache.BeginAddOrUpdatePaths(tempDirectory);
				Assert.That(GlobalParseCache.WaitForFinish(10000));

				Assert.That(GlobalParseCache.GetModule(tempModulePath).ModuleName, Is.EqualTo("modB"));
				Assert.That(GlobalParseCache.GetModule(tempDirectory, "sub.modC", out var pack).ModuleName,
					Is.EqualTo("sub.modC"));
				Assert.That(pack.Name, Is.EqualTo("sub"));

				var packages = GlobalParseCache.EnumPackagesRecursively(true, tempDirectory);
				Assert.That(packages.Count, Is.EqualTo(2));
				Assert.That(packages[0], Is.TypeOf(typeof(RootPackage)));
				Assert.That(packages[1].Name, Is.EqualTo("sub"));

				{
					var modules = GlobalParseCache.EnumModulesRecursively(tempDirectory);
					Assert.That(modules.Count, Is.EqualTo(2));
					Assert.That(modules[0].ModuleName, Is.EqualTo("modB"));
					Assert.That(modules[1].ModuleName, Is.EqualTo("sub.modC"));
				}

				Assert.That(GlobalParseCache.RemoveModule(tempDirectory, "modB"));
				{
					var modules = GlobalParseCache.EnumModulesRecursively(tempDirectory);
					Assert.That(modules.Count, Is.EqualTo(1));
					Assert.That(modules[0].ModuleName, Is.EqualTo("sub.modC"));
				}
			}
			finally
			{
				Directory.Delete(tempDirectory, true);
				Assert.That(GlobalParseCache.RemoveRoot(tempDirectory));
			}
		}
	}
}