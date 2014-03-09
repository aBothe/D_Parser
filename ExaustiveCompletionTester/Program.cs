using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;

namespace ExaustiveCompletionTester
{
	public static class Program
	{
		public static readonly ConcurrentQueue<FileProcessingData> completedFiles = new ConcurrentQueue<FileProcessingData>();
		public static readonly ConcurrentQueue<FileProcessingData> filesToProcess = new ConcurrentQueue<FileProcessingData>();
		public static readonly ConcurrentQueue<FileProcessingData> startedFiles = new ConcurrentQueue<FileProcessingData>();
		public static readonly HashSet<string> TriggeredExceptionLocations = new HashSet<string>();
		public static FileProcessingData[] activeData;
		public static volatile int liveWorkerCount = 0;
		const string ExceptionsDirectory = ".\\Exceptions";

		public static void Main (string[] args)
		{
			if (Directory.Exists(ExceptionsDirectory))
				Directory.Delete(ExceptionsDirectory, true);
			foreach (var v in Directory.EnumerateFileSystemEntries(Config.PhobosPath))
				ProcessPath(v);

			var workerCount = Environment.ProcessorCount - 1;
			if (workerCount == 0)
				workerCount = 1;
			liveWorkerCount = workerCount;
			activeData = new FileProcessingData[workerCount];
			for (int i = 0; i < workerCount; i++)
				new Thread(workerMain).Start(i);

			FileProcessingData curFile = null;
			while (liveWorkerCount > 0)
			{
				while (startedFiles.TryDequeue(out curFile))
					WriteAt(curFile.FileID, 1, "Processing " + curFile.ShortFilePath);

				while (completedFiles.TryDequeue(out curFile))
				{
					WriteFromLeft(curFile.FileID, "100%)");
					if (curFile.ExceptionsTriggered.Count > 0)
					{
						if (!Directory.Exists(ExceptionsDirectory))
							Directory.CreateDirectory(ExceptionsDirectory);
						for (int i = 0; i < curFile.ExceptionsTriggered.Count; i++)
						{
							var excI = curFile.ExceptionsTriggered[i];
							var fLin = excI.Item2.Substring(0, (excI.Item2 + "\n").IndexOf('\n'));
							if (!TriggeredExceptionLocations.Contains(fLin))
							{
								TriggeredExceptionLocations.Add(fLin);
								File.WriteAllText(ExceptionsDirectory + "\\" + curFile.ShortFilePath.Replace('\\', '_') + "-" + i.ToString() + ".txt", curFile.str.Substring(0, excI.Item1));
								File.WriteAllText(ExceptionsDirectory + "\\" + curFile.ShortFilePath.Replace('\\', '_') + "-" + i.ToString() + ".trace.txt", excI.Item2);
							}
						}
					}
				}

				foreach (var v in activeData)
				{
					if (v != null && v.lengthString != null)
					{
						WriteFromLeft(v.FileID, String.Format("Thread {0} {1}/{2} ({3}%)", v.WorkerID, v.i.ToString().PadLeft(v.lengthString.Length, ' '), v.lengthString, ((int)((v.i / (double)v.FileLength) * 100)).ToString().PadLeft(3, ' ')));
					}
				}

				Thread.Sleep(100);
			}
		}

		public static void WriteFromLeft(int line, string str)
		{
			Console.CursorTop = line - 1;
			Console.CursorLeft = Console.BufferWidth - str.Length;
			Console.Write(str);
		}

		public static void WriteAt(int line, int column, string str)
		{
			Console.CursorTop = line - 1;
			Console.CursorLeft = column - 1;
			Console.Write(str);
		}

		public static void workerMain(object threadIDObj)
		{
			var workerID = (int)threadIDObj;
			FileProcessingData curWorkingData = null;
			while (filesToProcess.TryDequeue(out curWorkingData))
			{
				curWorkingData.WorkerID = workerID;
				activeData[workerID] = curWorkingData;
				startedFiles.Enqueue(curWorkingData);
				curWorkingData.Process();
				activeData[workerID] = null;
				completedFiles.Enqueue(curWorkingData);
			}
			liveWorkerCount--;
		}

		public static void ProcessPath(string path)
		{
			if (File.Exists(path))
			{
				if (Path.GetExtension(path) == ".d" || Path.GetExtension(path) == ".di")
				{
					filesToProcess.Enqueue(new FileProcessingData(path, filesToProcess.Count + 1));
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
