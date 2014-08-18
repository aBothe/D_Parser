using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace D_Parser.Misc
{
	/// <summary>
	/// Helper class for retrieving all predefined version identifiers depending on e.g.
	/// the currently used OS, CPU-specific properties and further flags.
	/// For details, see http://dlang.org/version.html, "Predefined Versions"
	/// </summary>
	public static class VersionIdEvaluation
	{
		static string[] minimalConfiguration;

		// Stolen from http://stackoverflow.com/questions/10138040/how-to-detect-properly-windows-linux-mac-operating-systems
		public enum Platform
		{
			None,
			Unkown,
			Windows,
			Linux,
			Mac
		}
		static Platform _os = Platform.None;

		public static Platform OS
		{
			get
			{
				if (_os != Platform.None)
					return _os;

				switch (Environment.OSVersion.Platform)
				{
					case PlatformID.Unix:
						// Well, there are chances MacOSX is reported as Unix instead of MacOSX.
						// Instead of platform check, we'll do a feature checks (Mac specific root folders)
						if (Directory.Exists("/Applications")
							& Directory.Exists("/System")
							& Directory.Exists("/Users")
							& Directory.Exists("/Volumes"))
							_os = Platform.Mac;
						else
							_os = Platform.Linux;
						break;
					case PlatformID.MacOSX:
						_os = Platform.Mac;
						break;
					default:
						_os = Platform.Windows;
						break;
				}

				return _os;
			}
		}

		public static string[] GetOSAndCPUVersions()
		{
			if(minimalConfiguration != null)
				return minimalConfiguration;

			var l = new List<string>();

			l.Add("all");
			l.Add ("assert");

			// OS
			bool is64BitOS = Environment.Is64BitOperatingSystem;

			switch (OS)
			{
				case Platform.Windows:
					l.Add("Windows");
					l.Add(is64BitOS ? "Win64" : "Win32");
					break;
				case Platform.Linux:
					l.Add ("Posix");
					l.Add("linux");
					break;
				case Platform.Mac:
					l.Add("OSX");
						l.Add("darwin");
						l.Add ("Posix");
					break;
			}
			
			//TODO: Execute uname to retrieve further info of the Posix-OS
			// http://www.computerhope.com/unix/uuname.htm
			
			// CPU information
			var cpuArch = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");
			switch (cpuArch)
			{
				case "X86":
					x86:
					l.Add("D_InlineAsm_X86");
					l.Add("X86");
					break;
				case "AMD64":
					x64:
					l.Add("D_InlineAsm_X86_64");
					l.Add("X86_64");
					break;
				case "IA64":
					l.Add("IA64");
					break;

				default:
					if (string.IsNullOrWhiteSpace (cpuArch)) {
						if (is64BitOS)
							goto x64;
						else
							goto x86;
					}
					break;
			}
			//TODO: Other architectures...
			
			if(BitConverter.IsLittleEndian)
				l.Add("LittleEndian");
			else
				l.Add("BigEndian");

			return minimalConfiguration = l.ToArray();
		}

		static readonly Regex versionRegex = new Regex ("version=(?<n>\\w+)", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

		/// <summary>
		/// See class description.
		/// </summary>
		/// <returns>
		/// The version identifiers.
		/// </returns>
		/// <param name="compilerId">The compiler-specific version identifier which is e.g. DigitalMars for dmd1/dmd2</param>
		/// <param name="finalCompilerCommandLine">
		/// Used for extracting additional information like "-cov" that implies D_Coverage or "-m64" that
		/// implies D
		/// </param>
		/// <param name="isD1">If false, D_Version2 will be defined</param>
		public static string[] GetVersionIds(string compilerId,string finalCompilerCommandLine, bool unittests, bool isD1 = false)
		{
			var l = new List<string>();

			l.AddRange(GetOSAndCPUVersions());

			// Compiler id
			if(!string.IsNullOrEmpty(compilerId))
				l.Add(compilerId);

			// D specific info

			if(finalCompilerCommandLine.Contains("-cov"))
				l.Add("D_Coverage");
			if(finalCompilerCommandLine.Contains("-D"))
				l.Add("D_Ddoc");
				
			if (finalCompilerCommandLine.Contains("-m64"))
				l.Add("D_LP64");
			else
				l.Add("D_X32");

			// D_HardFloat, D_SoftFloat -- how to determine this?
			l.Add("D_HardFloat");
			//l.Add("D_SoftFloat");

			if(finalCompilerCommandLine.Contains("-fPIC"))
				l.Add("D_PIC");

			l.Add("D_SIMD");

			if(!isD1)
				l.Add("D_Version2");

			if(finalCompilerCommandLine.Contains("-noboundscheck"))
				l.Add("D_NoBOundsChecks");
			if(finalCompilerCommandLine.Contains("-unittest") || unittests)
				l.Add("unittest");

			foreach (Match m in versionRegex.Matches(finalCompilerCommandLine)) {
				var ver = m.Groups ["n"].Value; 
				if (!string.IsNullOrEmpty (ver) && !l.Contains (ver))
					l.Add (ver);
			}

			return l.ToArray();
		}
	}
}

