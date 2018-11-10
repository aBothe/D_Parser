using System.Collections.Generic;
using D_Parser.Dom;

namespace D_Parser.Parser
{
	public class DTokensSemanticHelpers
	{
		/// <summary>
		/// Checks if modifier array contains member attributes. If so, it returns the last found attribute. Otherwise 0.
		/// </summary>
		/// <param name="mods"></param>
		/// <returns></returns>
		public static DAttribute ContainsStorageClass(IEnumerable<DAttribute> mods)
		{
			if (mods != null)
				foreach (var m in mods)
				{
					if (m is Modifier && IsStorageClass((m as Modifier).Token))
						return m;
					else if (m is AtAttribute)
						return m;
				}
			return Modifier.Empty;
		}


		public static bool ContainsVisMod(List<byte> mods)
		{
			return
			mods.Contains(DTokens.Public) ||
			mods.Contains(DTokens.Private) ||
			mods.Contains(DTokens.Package) ||
			mods.Contains(DTokens.Protected);
		}

		public static void RemoveVisMod(List<byte> mods)
		{
			while (mods.Contains(DTokens.Public))
				mods.Remove(DTokens.Public);
			while (mods.Contains(DTokens.Private))
				mods.Remove(DTokens.Private);
			while (mods.Contains(DTokens.Protected))
				mods.Remove(DTokens.Protected);
			while (mods.Contains(DTokens.Package))
				mods.Remove(DTokens.Package);
		}


		public static bool IsAssignOperator(byte token)
		{
			switch (token)
			{
				case DTokens.Assign: // =
				case DTokens.PlusAssign: // +=
				case DTokens.MinusAssign: // -=
				case DTokens.TimesAssign: // *=
				case DTokens.DivAssign: // /=
				case DTokens.ModAssign: // %=
				case DTokens.BitwiseAndAssign: // &=
				case DTokens.BitwiseOrAssign: // |=
				case DTokens.XorAssign: // ^=
				case DTokens.TildeAssign: // ~=
				case DTokens.ShiftLeftAssign: // <<=
				case DTokens.ShiftRightAssign: // >>=
				case DTokens.TripleRightShiftAssign: // >>>=
				case DTokens.PowAssign: // ^^=
					return true;
				default:
					return false;
			}
		}

		public static readonly byte[] BasicTypes_Array = { DTokens.Bool,
			DTokens.Byte, DTokens.Ubyte, DTokens.Short, DTokens.Ushort, DTokens.Int,
			DTokens.Uint, DTokens.Long, DTokens.Ulong, DTokens.Cent, DTokens.Ucent,
			DTokens.Char, DTokens.Wchar, DTokens.Dchar,
			DTokens.Float, DTokens.Double, DTokens.Real,
			DTokens.Ifloat, DTokens.Idouble, DTokens.Ireal, DTokens.Cfloat,
			DTokens.Cdouble, DTokens.Creal,
			DTokens.Void };

		public static bool IsBasicType(byte token)
		{
			switch (token)
			{
				case DTokens.Bool:
				case DTokens.Byte:
				case DTokens.Ubyte:
				case DTokens.Short:
				case DTokens.Ushort:
				case DTokens.Int:
				case DTokens.Uint:
				case DTokens.Long:
				case DTokens.Ulong:
				case DTokens.Cent:
				case DTokens.Ucent:
				case DTokens.Char:
				case DTokens.Wchar:
				case DTokens.Dchar:
				case DTokens.Float:
				case DTokens.Double:
				case DTokens.Real:
				case DTokens.Ifloat:
				case DTokens.Idouble:
				case DTokens.Ireal:
				case DTokens.Cfloat:
				case DTokens.Cdouble:
				case DTokens.Creal:
				case DTokens.Void:
					return true;
				default:
					return false;
			}
		}

		public static bool IsBasicType_Character(byte token)
		{
			switch (token)
			{
				case DTokens.Char:
				case DTokens.Wchar:
				case DTokens.Dchar:
					return true;
				default:
					return false;
			}
		}

		public static bool IsBasicType_FloatingPoint(byte token)
		{
			switch (token)
			{
				case DTokens.Float:
				case DTokens.Double:
				case DTokens.Real:
				case DTokens.Ifloat:
				case DTokens.Idouble:
				case DTokens.Ireal:
				case DTokens.Cfloat:
				case DTokens.Cdouble:
				case DTokens.Creal:
					return true;
				default:
					return false;
			}
		}

		public static bool IsBasicType_Integral(byte token)
		{
			switch (token)
			{
				case DTokens.Bool:
				case DTokens.Byte:
				case DTokens.Ubyte:
				case DTokens.Short:
				case DTokens.Ushort:
				case DTokens.Int:
				case DTokens.Uint:
				case DTokens.Long:
				case DTokens.Ulong:
				case DTokens.Cent:
				case DTokens.Ucent:
				case DTokens.Char:
				case DTokens.Wchar:
				case DTokens.Dchar:
					return true;
				default:
					return false;
			}
		}

		public static bool IsBasicType_Unsigned(byte token)
		{
			switch (token)
			{
				case DTokens.Ubyte:
				case DTokens.Ushort:
				case DTokens.Uint:
				case DTokens.Ulong:
				case DTokens.Ucent:
					return true;
				default:
					return false;
			}
		}

		public static bool IsBasicType(DToken tk)
		{
			switch (tk.Kind)
			{
				case DTokens.Typeof:
				case DTokens.__vector:
				case DTokens.Identifier:
					return true;
				case DTokens.Dot:
					return tk.Next != null && tk.Next.Kind == (DTokens.Identifier);
				case DTokens.This:
				case DTokens.Super:
					return tk.Next != null && tk.Next.Kind == DTokens.Dot;
				default:
					return IsBasicType(tk.Kind)
						|| IsFunctionAttribute(tk.Kind);
			}
		}

		public static bool IsClassLike(byte token)
		{
			switch (token)
			{
				case DTokens.Class:
				case DTokens.Template:
				case DTokens.Interface:
				case DTokens.Struct:
				case DTokens.Union:
					return true;
				default:
					return false;
			}
		}

		public static bool IsMemberFunctionAttribute(byte token)
		{
			switch (token)
			{
				case DTokens.Return:
				case DTokens.Scope:
				case DTokens.Const:
				case DTokens.Immutable:
				case DTokens.Shared:
				case DTokens.InOut:
				case DTokens.Pure:
				case DTokens.Nothrow:
					return true;
				default:
					return false;
			}
		}

		public static bool IsFunctionAttribute(byte kind)
		{
			return IsMemberFunctionAttribute(kind) || kind == DTokens.At;
		}

		public static bool IsMetaIdentifier(byte token)
		{
			switch (token)
			{
				case DTokens.__DATE__:
				case DTokens.__FILE__:
				case DTokens.__FUNCTION__:
				case DTokens.__LINE__:
				case DTokens.__MODULE__:
				case DTokens.__PRETTY_FUNCTION__:
				case DTokens.__TIMESTAMP__:
				case DTokens.__TIME__:
				case DTokens.__VENDOR__:
				case DTokens.__VERSION__:
					return true;
				default:
					return false;
			}
		}

		public static bool IsModifier(byte token)
		{
			switch (token)
			{
				case DTokens.In:
				case DTokens.Out:
				case DTokens.InOut:
				case DTokens.Ref:
				case DTokens.Static:
				case DTokens.Override:
				case DTokens.Const:
				case DTokens.Public:
				case DTokens.Private:
				case DTokens.Protected:
				case DTokens.Package:
				case DTokens.Export:
				case DTokens.Shared:
				case DTokens.Final:
				case DTokens.Invariant:
				case DTokens.Immutable:
				case DTokens.Pure:
				case DTokens.Deprecated:
				case DTokens.Scope:
				case DTokens.__gshared:
				case DTokens.Lazy:
				case DTokens.Nothrow:
					return true;
				default:
					return false;
			}
		}

		public static bool IsParamModifier(byte token)
		{
			switch (token)
			{
				case DTokens.Return:
				case DTokens.In:
				case DTokens.Out:
				case DTokens.InOut:
				case DTokens.Ref:
				case DTokens.Lazy:
				case DTokens.Scope:
					return true;
				default:
					return false;
			}
		}

		public static bool IsStorageClass(byte token)
		{
			switch (token)
			{
				case DTokens.Abstract:
				case DTokens.Auto:
				case DTokens.Const:
				case DTokens.Deprecated:
				case DTokens.Extern:
				case DTokens.Final:
				case DTokens.Immutable:
				case DTokens.InOut:
				case DTokens.Shared:
				case DTokens.Nothrow:
				case DTokens.Override:
				case DTokens.Pure:
				case DTokens.Scope:
				case DTokens.Static:
				case DTokens.Synchronized:
				case DTokens.Ref:
				case DTokens.__gshared:
					return true;
				default:
					return false;
			}
		}

		public static bool IsVisibilityModifier(byte token)
		{
			switch (token)
			{
				case DTokens.Public:
				case DTokens.Protected:
				case DTokens.Private:
				case DTokens.Package:
					return true;
				default:
					return false;
			}
		}
	}
}
