using System;
using D_Parser.Misc;
using Tests;
using D_Parser.Dom;
using D_Parser.Resolver;
using System.Linq;
using D_Parser.Completion;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Resolver.TypeResolution;
using System.Diagnostics;

namespace TestTool
{
	public class BotanProfil
	{
		const string srcDir = "/home/lx/Desktop/dump/D/botan-master/source";
		static readonly string[] versions = new[]{"SHA2_32", "SHA2_64", "MD4", "MD5", "SHA1", "CRC24", "PBKDF1", "PBKDF2", "CTR_BE", "HMAC", 
			"EMSA1", "EMSA1_BSI", "EMSA_X931", "EMSA_PKCS1", "EMSA_PSSR", "EMSA_RAW", "EME_OAEP", "EME_PKCS1v15", "PBE_PKCSv20", 
			"Self_Tests", "ECB", "CBC", "XTS", "OFB", "CFB", 
			"AEAD_FILTER", "AEAD_CCM", "AEAD_EAX", "AEAD_OCB", "AEAD_GCM", "GCM_CLMUL", "AEAD_SIV", "RFC6979", "RSA", "RW", "DLIES", "DSA", "ECDSA", "ElGamal", "GOST_3410", 
			"Nyberg_Rueppel", "Diffie_Hellman", "ECDH", "AES", "Blowfish", "Camellia", "CAST", "Cascade", "DES", "GOST_28147", "IDEA", 
			"KASUMI", "LION", "MARS", "MISTY1", "NOEKEON", "RC2", "RC5", "RC6", "SAFER", "SEED", "Serpent", "TEA", "Twofish", "Threefish", 
			"XTEA", "Adler32",  "CRC32", "GOST_3411", "HAS_160", "Keccak", "MD2",  "RIPEMD_128", "RIPEMD_160", "SHA1_x86_64",
			"SHA2_64", "Skein_512", "Tiger", "Whirlpool", "ParallelHash", "Comb4P", "CBC_MAC", "CMAC", "SSL3_MAC", 
			"ANSI_X919_MAC", "RC4", "ChaCha", "Salsa20", "AES_NI", "AES_SSSE3", "Serpent_SIMD",  "SIMD_Scalar", "SIMD_SSE2",
			"Noekeon_SIMD", "XTEA_SIMD", "IDEA_SSE2", "SHA1_SSE2", "Engine_ASM", "Engine_AES_ISA", "Engine_SIMD",
			"Entropy_HRTimer", "Entropy_Rdrand", "Entropy_DevRand", "Entropy_EGD", "Entropy_UnixProc", "Entropy_CAPI", 
			"Entropy_Win32", "Entropy_ProcWalk", "X931_RNG", "HMAC_DRBG", "KDF1", "KDF2", "SSL_V3_PRF", "TLS_V10_PRF", "TLS_V12_PRF", "X942_PRF",
			"TLS", "X509", "PUBKEY", "FPE_FE1", "RFC3394", "PassHash9", "BCrypt", "SRP6", "TSS", "CryptoBox", 
			"CryptoBox_PSK", "ZLib"};

		public static void Run()
		{
			Parse ();

			var pcw = new LegacyParseCacheView (new[]{ srcDir });
			var ctxt = new ResolutionContext (pcw, new ConditionalCompilationFlags(versions,0, true));

			var mod = pcw.LookupModuleName (null, "botan.pubkey.algo.dsa").First();
			var scope = ResolutionTests.N<DMethod> (mod, "DSAVerificationOperation.verify");
			var scopedStmt = ResolutionTests.S (scope, 9);

			ctxt.Push (scope, scopedStmt.Location);

			ITypeDeclaration td = new IdentifierDeclaration ("q"){ Location = scopedStmt.Location };
			AbstractType t;

			var sw = new Stopwatch ();
			Console.WriteLine ("Begin resolving...");
			sw.Restart ();
			t = TypeDeclarationResolver.ResolveSingle(td, ctxt);

			sw.Stop ();

			Console.WriteLine ("Finished resolution. {0} ms.", sw.ElapsedMilliseconds);

			sw.Restart ();
			t = TypeDeclarationResolver.ResolveSingle(td, ctxt);

			sw.Stop ();

			Console.WriteLine ("Finished resolution. {0} ms.", sw.ElapsedMilliseconds);
		}

		static void Parse()
		{
			var e = new System.Threading.ManualResetEvent(false);
			Console.WriteLine ("Parsing...");

			GlobalParseCache.BeginAddOrUpdatePaths (new[]{ srcDir }, false, (ea) => {
				Console.WriteLine ("Finished parsing. {0} files. {1} ms.", ea.FileAmount, ea.ParseDuration);
				e.Set();
			});

			e.WaitOne ();
		}
	}
}

