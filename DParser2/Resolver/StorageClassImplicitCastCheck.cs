using System;
using System.Collections.Generic;
using D_Parser.Parser;

namespace D_Parser.Resolver
{
	static class StorageClassImplicitCastCheck
	{
		[Flags]
		public enum TypeModifierToken : byte
		{
			Mutable = 0,
			Const = 1,
			InOut = 2,
			Shared = 4,
			Immutable = 8
		}

		/// <summary>
		/// https://dlang.org/spec/const3.html#implicit_qualifier_conversions
		/// From modifier set, To modifier set
		/// </summary>
		static readonly TypeModifierToken[][] allowedImplicitConversions_RawTable = new[]
		{
			new[] { TypeModifierToken.Mutable, TypeModifierToken.Mutable },
			new[] { TypeModifierToken.Mutable, TypeModifierToken.Const },
			new[] { TypeModifierToken.Const, TypeModifierToken.Const },
			new[] { TypeModifierToken.Const | TypeModifierToken.InOut, TypeModifierToken.Const },
			new[] { TypeModifierToken.Const | TypeModifierToken.InOut, TypeModifierToken.Const | TypeModifierToken.InOut },
			new[] { TypeModifierToken.Const | TypeModifierToken.Shared, TypeModifierToken.Const | TypeModifierToken.Shared },
			new[] { TypeModifierToken.Const | TypeModifierToken.InOut | TypeModifierToken.Shared, TypeModifierToken.Const | TypeModifierToken.Shared },
			new[] { TypeModifierToken.Const | TypeModifierToken.InOut | TypeModifierToken.Shared, TypeModifierToken.Const | TypeModifierToken.InOut | TypeModifierToken.Shared },
			new[] { TypeModifierToken.Immutable, TypeModifierToken.Const },
			new[] { TypeModifierToken.Immutable, TypeModifierToken.Const | TypeModifierToken.Shared },
			new[] { TypeModifierToken.Immutable, TypeModifierToken.Const | TypeModifierToken.InOut },
			new[] { TypeModifierToken.Immutable, TypeModifierToken.Const | TypeModifierToken.InOut | TypeModifierToken.Shared },
			new[] { TypeModifierToken.Immutable, TypeModifierToken.Immutable },
			new[] { TypeModifierToken.InOut, TypeModifierToken.Const },
			new[] { TypeModifierToken.InOut, TypeModifierToken.InOut },
			new[] { TypeModifierToken.InOut, TypeModifierToken.Const | TypeModifierToken.InOut },
			new[] { TypeModifierToken.Shared, TypeModifierToken.Shared },
			new[] { TypeModifierToken.Shared, TypeModifierToken.Const | TypeModifierToken.Shared },
			new[] { TypeModifierToken.InOut | TypeModifierToken.Shared, TypeModifierToken.Const | TypeModifierToken.Shared },
			new[] { TypeModifierToken.InOut | TypeModifierToken.Shared, TypeModifierToken.InOut | TypeModifierToken.Shared },
			new[] { TypeModifierToken.InOut | TypeModifierToken.Shared, TypeModifierToken.Const | TypeModifierToken.InOut | TypeModifierToken.Shared },
		};
		static HashSet<int> allowedImplicitConversions_Lookup;

		static StorageClassImplicitCastCheck()
		{
			allowedImplicitConversions_Lookup = new HashSet<int>();
			foreach (var fromToTransition in allowedImplicitConversions_RawTable)
				allowedImplicitConversions_Lookup.Add(GetTypeModifierLookupDataRow(fromToTransition[1], fromToTransition[0]));
		}

		static int GetTypeModifierLookupDataRow(TypeModifierToken toType, TypeModifierToken fromType) => ((byte)fromType << 8) + (byte)toType;

		public static TypeModifierToken GetTypeModifierToken(byte token)
		{
			switch (token)
			{
				case DTokens.Const:
					return TypeModifierToken.Const;
				case DTokens.Immutable:
					return TypeModifierToken.Immutable;
				case DTokens.Shared:
					return TypeModifierToken.Shared;
				case DTokens.InOut:
					return TypeModifierToken.InOut;
				default:
					return TypeModifierToken.Mutable;
			}
		}

		public static TypeModifierToken GetTypeModifierToken(AbstractType t)
		{
			var returnValue = TypeModifierToken.Mutable;
			if (t.HasModifiers)
			{
				foreach (var modifier in t.Modifiers)
					returnValue |= GetTypeModifierToken(modifier);
			}
			return returnValue;
		}

		public static bool AreModifiersImplicitlyConvertible(AbstractType targetType, AbstractType sourceType) => AreModifiersImplicitlyConvertible(GetTypeModifierToken(targetType), GetTypeModifierToken(sourceType));
		public static bool AreModifiersImplicitlyConvertible(TypeModifierToken targetType, TypeModifierToken sourceType) => allowedImplicitConversions_Lookup.Contains(GetTypeModifierLookupDataRow(targetType, sourceType));
	}
}
