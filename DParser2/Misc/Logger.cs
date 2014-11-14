using System;
using System.Collections.Generic;

namespace D_Parser.Misc
{
	public class Logger
	{
		public static readonly List<ILogger> Loggers = new List<ILogger>();

		static Logger()
		{
			Loggers.Add (new ConsoleLogger());
		}

		public static void Log(LogLevel lvl, string msg, Exception ex = null)
		{
			foreach (var l in Loggers)
				l.Log (lvl, msg, ex);
		}

		public static void LogError(string msg, Exception ex = null)
		{
			Log (LogLevel.Error, msg, ex);
		}

		public static void LogWarn(string msg, Exception ex = null)
		{
			Log (LogLevel.Warn, msg, ex);
		}
	}

	class ConsoleLogger : ILogger
	{
		public void Log (LogLevel lvl, string msg, Exception ex)
		{
			switch (lvl) {
			case LogLevel.Debug:
				Console.Write ("Debug: ");
				break;
			case LogLevel.Error:
				Console.Write ("Error: ");
				break;
			case LogLevel.Fatal:
				Console.Write ("Fatal error: ");
				break;
			case LogLevel.Info:
				Console.Write ("Info: ");
				break;
			case LogLevel.Warn:
				Console.Write ("Warning: ");
				break;
			}

			if (msg != null)
				Console.Write (msg);

			if (ex != null) {
				Console.WriteLine ();
				Console.WriteLine (ex.Message);
				Console.Write (ex.StackTrace);
			}

			Console.WriteLine ();
		}

		public LogLevel EnabledLogLevel {
			get {
				return LogLevel.Info;
			}
		}

		public string Name {
			get {
				return "Console";
			}
		}
	}

	public interface ILogger
	{
		LogLevel EnabledLogLevel { get;}
		string Name {get;}
		void Log(LogLevel lvl, string msg, Exception ex);
	}

	public enum LogLevel
	{
		Fatal = 1,
		Error,
		Warn = 4,
		Info = 8,
		Debug = 16
	}
}

