using System;
using System.Collections.Generic;

namespace D_Parser.Misc
{
	/// <summary>
	/// Helper class for retrieving all predefined version identifiers depending on e.g.
	/// the currently used OS, CPU-specific properties and further flags.
	/// For details, see http://dlang.org/version.html, "Predefined Versions"
	/// </summary>
	public class VersionIdEvaluation
	{
		static string[] minimalConfiguration;

		public static string[] GetOSAndCPUVersions()
		{
			if(minimalConfiguration != null)
				return minimalConfiguration;

			var l = new List<string>();

			l.Add("all");

			// OS
			bool isWin = Environment.OSVersion.Platform.HasFlag(PlatformID.Win32NT);
			bool is64BitOS = Environment.Is64BitOperatingSystem;
			
			if(isWin)
			{
				l.Add("Windows");
				l.Add(is64BitOS ? "Win64" : "Win32");
			}
			else{
				switch(Environment.OSVersion.Platform)
				{
					case PlatformID.MacOSX:
						l.Add("OSX");
						l.Add("darwin");
						break;
					case PlatformID.Unix:
						break;
				}
			}
			//TODO: Execute uname to retrieve further info of the Posix-OS
			// http://www.computerhope.com/unix/uuname.htm
			
			// CPU information
			var cpuArch = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");
			if(cpuArch=="X86")
				l.Add("X86");
			else if(cpuArch=="AMD64")
				l.Add("X86_64");
			else if(cpuArch=="IA64")
				l.Add("IA64");
			//TODO: Other architectures...
			
			if(BitConverter.IsLittleEndian)
				l.Add("LittleEndian");
			else
				l.Add("BigEndian");

			return minimalConfiguration = l.ToArray();
		}

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

			if(false /* TODO: Determine x64-version of dmd etc. */)
				l.Add("D_InlineAsm_X86_64");
			else
				l.Add("D_InlineAsm_X86");

			if(finalCompilerCommandLine.Contains("-m64"))
				l.Add("D_LP64");
			else
				l.Add("D_X32");

			// D_HardFloat, D_SoftFloat -- how to determine this?
			l.Add("D_HardFloat");
			l.Add("D_SoftFloat");

			if(finalCompilerCommandLine.Contains("-fPIC"))
				l.Add("D_PIC");

			l.Add("D_SIMD");

			if(!isD1)
				l.Add("D_Version2");

			if(finalCompilerCommandLine.Contains("-noboundscheck"))
				l.Add("D_NoBOundsChecks");
			if(finalCompilerCommandLine.Contains("-unittest") || unittests)
				l.Add("unittest");

			return l.ToArray();
		}
	}
}

