using System;
using System.IO;
using Tests;
using D_Parser.Completion;
using System.Collections.Generic;

namespace ExaustiveCompletionTester
{
	public sealed class FileProcessingData
	{
		public string FullFilePath { get; private set; }
		public string ShortFilePath { get; private set; }
		public int i = 0;
		public int lastPercent = 0;
		public List<Tuple<int, string>> ExceptionsTriggered = new List<Tuple<int, string>>();

		public FileProcessingData(string filePath)
		{
			this.FullFilePath = filePath;
			this.ShortFilePath = filePath.Substring(Config.PhobosPath.Length);
		}

		public void Process()
		{
			Console.WriteLine("Processing " + ShortFilePath);
			string str = File.ReadAllText(FullFilePath);
			int line = 1;
			int lineStart = 0;
			string strLen = str.Length.ToString();

			while (i <= str.Length)
			{
				if (i < str.Length)
				{
					while (i < str.Length)
					{
						if (str[i] == '\n')
						{
							line++;
							lineStart = i + 1;
						}
						else if (str[i] == '/' && i + 1 < str.Length)
						{
							if (str[i + 1] == '/')
							{
								i += 2;
								while (i < str.Length && str[i] != '\n')
									i++;
								break;
							}
							else if (str[i + 1] == '*')
							{
								i += 2;
								while (i < str.Length)
								{
									if (str[i] == '*' && i + 1 < str.Length && str[i + 1] == '/')
									{
										i += 2;
										break;
									}
									else if (str[i] == '\n')
									{
										line++;
										lineStart = i + 1;
									}
									i++;
								}
								break;
							}
						}
						if (!Char.IsLetter(str[i]) && str[i] != '_')
							break;
						i++;
					}
				}

				var tStr = str.Substring(0, i);
				var ed = CompletionTests.GenEditorData(line, i - lineStart, tStr);
				var g = new SpecializedDataGen();
				try
				{
					CodeCompletion.GenerateCompletionData(ed, g, 'a', true);
				}
				catch (Exception e)
				{
					ExceptionsTriggered.Add(new Tuple<int, string>(i, e.StackTrace));
				}

				i++;
				if ((int)((i / (double)str.Length) * 100) > lastPercent)
				{
					lastPercent = (int)((i / (double)str.Length) * 100);
					Console.WriteLine("{0}/{1} ({2}%)", i.ToString().PadLeft(strLen.Length, ' '), strLen, lastPercent.ToString().PadLeft(3, ' '));
				}
			}

			if (ExceptionsTriggered.Count > 0)
			{
				Console.WriteLine("Triggered {0} exceptions in {1}", ExceptionsTriggered.Count, ShortFilePath);
				foreach (var v in ExceptionsTriggered)
				{
					Console.WriteLine("{" + str.Substring(0, v.Item1) + "}:{" + v.Item2 + "}");
				}
			}
		}
	}
}

