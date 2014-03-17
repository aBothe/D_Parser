using System;
using System.IO;
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
			try
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
							switch(str[i])
							{
								case '\n':
									line++;
									lineStart = i + 1;
									i++;
									continue;
								case '"':
									// Basic string
									i++;
									while (i < str.Length && str[i] != '"')
									{
										if (str[i] == '\\')
											i++;
										i++;
									}
									i++;
									goto BreakLoop;
								case '/':
									if (i + 1 >= str.Length)
										goto default;
									if (str[i + 1] == '/')
									{
										// Line Comment
										i += 2;
										while (i < str.Length && str[i] != '\n')
											i++;
										goto BreakLoop;
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
										goto BreakLoop;
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
										goto BreakLoop;
									}
									goto default;
								case '0':
								case '1':
								case '2':
								case '3':
								case '4':
								case '5':
								case '6':
								case '7':
								case '8':
								case '9':
									i++;
									while (i < str.Length && IsDigit(str[i]))
										i++;
									goto BreakLoop;
								case 'a':
								case 'b':
								case 'c':
								case 'd':
								case 'e':
								case 'f':
								case 'g':
								case 'h':
								case 'i':
								case 'j':
								case 'k':
								case 'l':
								case 'm':
								case 'n':
								case 'o':
								case 'p':
								case 'q':
								case 'r':
								case 's':
								case 't':
								case 'u':
								case 'v':
								case 'w':
								case 'x':
								case 'y':
								case 'z':
								case 'A':
								case 'B':
								case 'C':
								case 'D':
								case 'E':
								case 'F':
								case 'G':
								case 'H':
								case 'I':
								case 'J':
								case 'K':
								case 'L':
								case 'M':
								case 'N':
								case 'O':
								case 'P':
								case 'Q':
								case 'R':
								case 'S':
								case 'T':
								case 'U':
								case 'V':
								case 'W':
								case 'X':
								case 'Y':
								case 'Z':
								case '_':
									i++;
									while (i < str.Length && (IsIdentifierChar(str[i]) || IsDigit(str[i])))
										i++;
									goto BreakLoop;
								default:
									goto BreakLoop;
							}
						BreakLoop:
							break;
						}
					}

					var tStr = str.Substring(0, i);
					var ed = CompletionFacilities.GenEditorData(line, i - lineStart, tStr);
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
			catch (Exception e)
			{
				if (!Directory.Exists(Program.TesterErrorsDirectory))
					Directory.CreateDirectory(Program.TesterErrorsDirectory);
				File.WriteAllText(Program.TesterErrorsDirectory + "\\" + ShortFilePath.Replace('\\', '_') + "-" + i.ToString() + ".txt", e.Message + "\r\nStack Trace:\r\n" + e.StackTrace);
			}
		}

		private static bool IsIdentifierChar(char c)
		{
			switch (c)
			{
				case 'a':
				case 'b':
				case 'c':
				case 'd':
				case 'e':
				case 'f':
				case 'g':
				case 'h':
				case 'i':
				case 'j':
				case 'k':
				case 'l':
				case 'm':
				case 'n':
				case 'o':
				case 'p':
				case 'q':
				case 'r':
				case 's':
				case 't':
				case 'u':
				case 'v':
				case 'w':
				case 'x':
				case 'y':
				case 'z':
				case 'A':
				case 'B':
				case 'C':
				case 'D':
				case 'E':
				case 'F':
				case 'G':
				case 'H':
				case 'I':
				case 'J':
				case 'K':
				case 'L':
				case 'M':
				case 'N':
				case 'O':
				case 'P':
				case 'Q':
				case 'R':
				case 'S':
				case 'T':
				case 'U':
				case 'V':
				case 'W':
				case 'X':
				case 'Y':
				case 'Z':
				case '_':
					return true;
				default:
					return false;
			}
		}

		private static bool IsDigit(char c)
		{
			switch (c)
			{
				case '0':
				case '1':
				case '2':
				case '3':
				case '4':
				case '5':
				case '6':
				case '7':
				case '8':
				case '9':
					return true;
				default:
					return false;
			}
		}
	}
}

