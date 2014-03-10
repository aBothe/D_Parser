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
		public int FileID { get; private set; }
		public int FileLength { get; private set; }
		public string str { get; private set; }
		public int WorkerID { get; set; }
		public int i = 0;
		public string lengthString = null;
		public List<Tuple<int, string>> ExceptionsTriggered = new List<Tuple<int, string>>();

		public FileProcessingData(string filePath, int fileID)
		{
			this.FullFilePath = filePath;
			this.ShortFilePath = filePath.Substring(Config.PhobosPath.Length);
			this.FileID = fileID;
		}

		public void Process()
		{
			str = File.ReadAllText(FullFilePath);
			this.FileLength = str.Length;
			this.lengthString = this.FileLength.ToString();
			int line = 1;
			int lineStart = 0;

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
						else if (str[i] == '"')
						{
							// Basic string
							i++;
							while (i < str.Length && str[i] != '"')
							{
								if (str[i] == '\\')
									i++;
								i++;
							}
							i++;
							break;
						}
						else if (str[i] == '/' && i + 1 < str.Length)
						{
							if (str[i + 1] == '/')
							{
								// Line Comment
								i += 2;
								while (i < str.Length && str[i] != '\n')
									i++;
								break;
							}
							else if (str[i + 1] == '*')
							{
								// Block Comment
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
							else if (str[i + 1] == '+')
							{
								// Nesting Block Comment
								i += 2;
								int nestDepth = 1;
								while (i < str.Length)
								{
									if (str[i] == '+' && i + 1 < str.Length && str[i + 1] == '/')
									{
										i++;
										nestDepth--;
										if (nestDepth == 0)
										{
											i++;
											break;
										}
									}
									else if (str[i] == '/' && i + 1 < str.Length && str[i + 1] == '+')
									{
										i++;
										nestDepth++;
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
					ExceptionsTriggered.Add(new Tuple<int, string>(i, e.Message + "\r\nStack Trace:\r\n" + e.StackTrace));
				}

				i++;
			}
		}
	}
}

