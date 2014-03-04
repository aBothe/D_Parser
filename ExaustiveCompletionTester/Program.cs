using System;
using System.IO;
using D_Parser.Completion;
using D_Parser.Dom;
using D_Parser;
using D_Parser.Parser;
using Tests;
using System.Collections.Generic;

namespace ExaustiveCompletionTester
{
	public static class Program
	{
		public static List<FileProcessingData> filesToProcess = new List<FileProcessingData>();

		public static void Main (string[] args)
		{
			const string PhobosPath = @"F:\D\Repo\phobos";
			foreach (var v in Directory.EnumerateFileSystemEntries(PhobosPath))
				ProcessPath(v);

			foreach (var v in filesToProcess)
				v.Process();
		}

		public static void ProcessPath(string path)
		{
			if (File.Exists(path))
			{
				if (Path.GetExtension(path) == ".d" || Path.GetExtension(path) == ".di")
				{
					filesToProcess.Add(new FileProcessingData(path));
				}
			}
			else if (Directory.Exists(path))
			{
				foreach (var v in Directory.EnumerateFileSystemEntries(path))
					ProcessPath(v);
			}
		}
	}
}
