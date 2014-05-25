using System.Collections;
using System.Collections.Generic;
using D_Parser.Dom;
using System;

namespace D_Parser.Parser
{
    public class DTokens
    {
        // ----- terminal classes -----
        public const byte EOF = 0;
        public const byte Identifier = 1;
        public const byte Literal = 2;

        // ----- special character -----
        public const byte Assign = 3;
        public const byte Plus = 4;
        public const byte Minus = 5;
        public const byte Times = 6;
        public const byte Div = 7;
        public const byte Mod = 8;
        public const byte Colon = 9;
        public const byte DoubleDot = 10; // ..
        public const byte Semicolon = 11;
        public const byte Question = 12;
        public const byte Dollar = 13;
        public const byte Comma = 14;
        public const byte Dot = 15;
        public const byte OpenCurlyBrace = 16;
        public const byte CloseCurlyBrace = 17;
        public const byte OpenSquareBracket = 18;
        public const byte CloseSquareBracket = 19;
        public const byte OpenParenthesis = 20;
        public const byte CloseParenthesis = 21;
        public const byte GreaterThan = 22;
        public const byte LessThan = 23;
        public const byte Not = 24;
        public const byte LogicalAnd = 25;
        public const byte LogicalOr = 26;
        public const byte Tilde = 27;
        public const byte BitwiseAnd = 28;
        public const byte BitwiseOr = 29;
        public const byte Xor = 30;
        public const byte Increment = 31;
        public const byte Decrement = 32;
        public const byte Equal = 33;
        public const byte NotEqual = 34;
        public const byte GreaterEqual = 35;
        public const byte LessEqual = 36;
        public const byte ShiftLeft = 37;
        public const byte PlusAssign = 38;
        public const byte MinusAssign = 39;
        public const byte TimesAssign = 40;
        public const byte DivAssign = 41;
        public const byte ModAssign = 42;
        public const byte BitwiseAndAssign = 43;
        public const byte BitwiseOrAssign = 44;
        public const byte XorAssign = 45;
        public const byte ShiftLeftAssign = 46;
        public const byte TildeAssign = 47;
        public const byte ShiftRightAssign = 48;
        public const byte TripleRightShiftAssign = 49;

        // ----- keywords -----
        public const byte Align = 50;
        public const byte Asm = 51;
        public const byte Assert = 52;
        public const byte Auto = 53;
        public const byte Body = 54;
        public const byte Bool = 55;
        public const byte Break = 56;
        public const byte Byte = 57;
        public const byte Case = 58;
        public const byte Cast = 59;
        public const byte Catch = 60;
        public const byte Cdouble = 61;
        public const byte Cent = 62;
        public const byte Cfloat = 63;
        public const byte Char = 64;
        public const byte Class = 65;
        public const byte Const = 66;
        public const byte Continue = 67;
        public const byte Creal = 68;
        public const byte Dchar = 69;
        public const byte Debug = 70;
        public const byte Default = 71;
        public const byte Delegate = 72;
        public const byte Delete = 73;
        public const byte Deprecated = 74;
        public const byte Do = 75;
        public const byte Double = 76;
        public const byte Else = 77;
        public const byte Enum = 78;
        public const byte Export = 79;
        public const byte Extern = 80;
        public const byte False = 81;
        public const byte Final = 82;
        public const byte Finally = 83;
        public const byte Float = 84;
        public const byte For = 85;
        public const byte Foreach = 86;
        public const byte Foreach_Reverse = 87;
        public const byte Function = 88;
        public const byte Goto = 89;
        public const byte Idouble = 90;
        public const byte If = 91;
        public const byte Ifloat = 92;
        public const byte Import = 93;
        public const byte Immutable = 94;
        public const byte In = 95;
        public const byte InOut = 96;
        public const byte Int = 97;
        public const byte Interface = 98;
        public const byte Invariant = 99;
        public const byte Ireal = 100;
        public const byte Is = 101;
        public const byte Lazy = 102;
        public const byte Long = 103;
        public const byte Macro = 104;
        public const byte Mixin = 105;
        public const byte Module = 106;
        public const byte New = 107;
        public const byte Nothrow = 108;
        public const byte Null = 109;
        public const byte Out = 110;
        public const byte Override = 111;
        public const byte Package = 112;
        public const byte Pragma = 113;
        public const byte Private = 114;
        public const byte Protected = 115;
        public const byte Public = 116;
        public const byte Pure = 117;
        public const byte Real = 118;
        public const byte Ref = 119;
        public const byte Return = 120;
        public const byte Scope = 121;
        public const byte Shared = 122;
        public const byte Short = 123;
        public const byte Static = 124;
        public const byte Struct = 125;
        public const byte Super = 126;
        public const byte Switch = 127;
        public const byte Synchronized = 128;
        public const byte Template = 129;
        public const byte This = 130;
        public const byte Throw = 131;
        public const byte True = 132;
        public const byte Try = 133;
        public const byte Typedef = 134;
        public const byte Typeid = 135;
        public const byte Typeof = 136;
        public const byte Ubyte = 137;
        public const byte Ucent = 138;
        public const byte Uint = 139;
        public const byte Ulong = 140;
        public const byte Union = 141;
        public const byte Unittest = 142;
        public const byte Ushort = 143;
        public const byte Version = 144;
        public const byte Void = 145;
        public const byte Volatile = 146;
        public const byte Wchar = 147;
        public const byte While = 148;
        public const byte With = 149;
        public const byte __gshared = 150;
        /// <summary>
        /// @
        /// </summary>
        public const byte At = 151;
        public const byte __traits = 152;
        public const byte Abstract = 153;
        public const byte Alias = 154;
        public const byte _unused = 155;
        public const byte GoesTo = 156; // =>  (lambda expressions)
        public const byte INVALID = 157;
        public const byte __vector = 158;

        // Additional operators
		/// <summary>
		/// ^^=
		/// </summary>
        public const byte PowAssign = 159;
		/// <summary>
		/// !&lt;&gt;=
		/// </summary>
        public const byte Unordered = 160;
		/// <summary>
		/// !&lt;&gt;
		/// </summary>
        public const byte UnorderedOrEqual = 161;
        public const byte LessOrGreater = 162; // <>
        public const byte LessEqualOrGreater = 163; // <>=
        public const byte UnorderedGreaterOrEqual = 164; // !<
        public const byte UnorderedOrLess = 165; // !>=
		/// <summary>
		/// !&gt;
		/// </summary>
        public const byte UnorderedLessOrEqual = 166; // !>
        public const byte UnorderedOrGreater = 167; // !<=
		/// <summary>
		/// &gt;&gt;
		/// </summary>
        public const byte ShiftRight = 168; // >>
		/// <summary>
		/// &gt;&gt;&gt;
		/// </summary>
        public const byte ShiftRightUnsigned = 169;
        public const byte Pow = 170; // ^^

        public const byte TripleDot = 171; // ...

		// Meta tokens
        public const byte __VERSION__ = 172;
        public const byte __FILE__ = 173;
        public const byte __LINE__ = 174;
        public const byte __EOF__ = 175;

		public const byte __DATE__ = 176;
		public const byte __TIME__ = 177;
		public const byte __TIMESTAMP__ = 178;
		public const byte __VENDOR__ = 179;

		public const byte __MODULE__ = 180;
		public const byte __FUNCTION__ = 181;
		public const byte __PRETTY_FUNCTION__ = 182;

		public const byte __LOCAL_SIZE = 183;

		public const byte Incomplete = 184;
		public const string IncompleteId = "<Incomplete>";
		public static readonly int IncompleteIdHash = IncompleteId.GetHashCode();

		public const byte Virtual = 185;

		public const byte MaxToken = 186;
        public static BitArray NewSet(params byte[] values)
        {
            BitArray bitArray = new BitArray(MaxToken);
            foreach (byte val in values)
            {
                bitArray[val] = true;
            }
            return bitArray;
        }

		public static readonly Dictionary<byte, string> Keywords = new Dictionary<byte, string> {
			{ __gshared, "__gshared" },
			// {__thread, "__thread"},
			{ __traits, "__traits" },
			{ __vector, "__vector" },

			{ __LINE__, "__LINE__" },
			{ __FILE__, "__FILE__" },
			{ __EOF__, "__EOF__" },

			{ __MODULE__, "__MODULE__" },
			{ __FUNCTION__, "__FUNCTION__" },
			{ __PRETTY_FUNCTION__, "__PRETTY_FUNCTION__" },

			{ __VERSION__, "__VERSION__" },
			{ __DATE__, "__DATE__" },
			{ __TIME__, "__TIME__" },
			{ __TIMESTAMP__, "__TIMESTAMP__" },
			{ __VENDOR__, "__VENDOR__" },

			{ __LOCAL_SIZE, "__LOCAL_SIZE" },

			{ Abstract, "abstract" },
			{ Alias, "alias" },
			{ Align, "align" },
			{ Asm, "asm" },
			{ Assert, "assert" },
			{ Auto, "auto" },
			{ Body, "body" },
			{ Bool, "bool" },
			{ Break, "break" },
			{ Byte, "byte" },

			{ Case, "case" },
			{ Cast, "cast" },
			{ Catch, "catch" },
			{ Cdouble, "cdouble" },
			{ Cent, "cent" },
			{ Cfloat, "cfloat" },
			{ Char, "char" },
			{ Class, "class" },
			{ Const, "const" },
			{ Continue, "continue" },
			{ Creal, "creal" },
			{ Dchar, "dchar" },
			{ Debug, "debug" },
			{ Default, "default" },
			{ Delegate, "delegate" },
			{ Delete, "delete" },
			{ Deprecated, "deprecated" },
			{ Do, "do" },
			{ Double, "double" },
			{ Else, "else" },
			{ Enum, "enum" },
			{ Export, "export" },
			{ Extern, "extern" },
			{ False, "false" },
			{ Final, "final" },
			{ Finally, "finally" },
			{ Float, "float" },
			{ For, "for" },
			{ Foreach, "foreach" },
			{ Foreach_Reverse, "foreach_reverse" },
			{ Function, "function" },
			{ Goto, "goto" },
			{ Idouble, "idouble" },
			{ If, "if" },
			{ Ifloat, "ifloat" },
			{ Import, "import" },
			{ Immutable, "immutable" },
			{ In,"in" },
			{ InOut,"inout" },
			{ Int, "int" },
			{ Interface, "interface" },
			{ Invariant, "invariant" },
			{ Ireal, "ireal" },
			{ Is, "is" },
			{ Lazy, "lazy" },
			{ Long, "long" },
			{ Macro, "macro" },
			{ Mixin, "mixin" },
			{ Module, "module" },
			{ New, "new" },
			{ Nothrow, "nothrow" },
			{ Null, "null" },
			{ Out, "out" },
			{ Override, "override" },
			{ Package, "package" },
			{ Pragma, "pragma" },
			{ Private, "private" },
			{ Protected, "protected" },
			{ Public, "public" },
			{ Pure, "pure" },
			{ Real, "real" },
			{ Ref, "ref" },
			{ Return, "return" },
			{ Scope, "scope" },
			{ Shared, "shared" },
			{ Short, "short" },
			{ Static, "static" },
			{ Struct, "struct" },
			{ Super, "super" },
			{ Switch, "switch" },
			{ Synchronized, "synchronized" },
			{ Template, "template" },
			{ This, "this" },
			{ Throw, "throw" },
			{ True, "true" },
			{ Try, "try" },
			{ Typedef, "typedef" },
			{ Typeid, "typeid" },
			{ Typeof, "typeof" },
			{ Ubyte, "ubyte" },
			{ Ucent, "ucent" },
			{ Uint, "uint" },
			{ Ulong, "ulong" },
			{ Union, "union" },
			{ Unittest, "unittest" },
			{ Ushort, "ushort" },

			{ Version, "version" },
			{ Virtual, "virtual" },
			{ Void, "void" },
			{ Volatile, "volatile" },

			{ Wchar, "wchar" },
			{ While, "while" },
			{ With, "with" }
        };
		public static Dictionary<string, byte> Keywords_Lookup = new Dictionary<string, byte>();

		static DTokens()
		{
			foreach (var kv in Keywords)
				Keywords_Lookup[kv.Value] = kv.Key;
		}

		[Obsolete("Use IsMemberFunctionAttribute instead.")]
		public static readonly BitArray FunctionAttribute = NewSet(Pure, Nothrow);
		[Obsolete("Use IsMemberFunctionAttribute instead.")]
		public static readonly BitArray MemberFunctionAttribute = NewSet(Const, Immutable, Shared, InOut, Pure, Nothrow);
		[Obsolete("Use IsParameterModifier instead.")]
		public static readonly BitArray ParamModifiers = NewSet(In, Out, InOut, Ref, Lazy, Scope);
		[Obsolete("Use IsClassLike instead.")]
		public static readonly BitArray ClassLike = NewSet(Class, Template, Interface, Struct, Union);
		[Obsolete("Use IsBasicType_Integral instead.")]
		public static readonly BitArray BasicTypes_Integral = NewSet(Bool, Byte, Ubyte, Short, Ushort, Int, Uint, Long, Ulong, Cent, Ucent, Char, Wchar, Dchar);
		[Obsolete("Use IsBasicType_FloatingPoint instead.")]
		public static readonly BitArray BasicTypes_FloatingPoint = NewSet(Float, Double, Real, Ifloat, Idouble, Ireal, Cfloat, Cdouble, Creal);
		[Obsolete("Use IsBasicType_Unsigned instead.")]
		public static readonly BitArray BasicTypes_Unsigned = NewSet(Ubyte, Ushort, Uint, Ulong, Ucent);
		[Obsolete("Use IsBasicType_Character instead.")]
		public static readonly BitArray CharTypes = NewSet (Char, Wchar, Dchar);
		[Obsolete("Use IsAssignOperator instead.")]
		public static readonly BitArray AssnStartOp = NewSet(Plus, Minus, Not, Tilde, Times);
		[Obsolete("Use IsAssignOperator instead.")]
		public static readonly BitArray AssignOps = NewSet(Assign, PlusAssign, MinusAssign, TimesAssign, DivAssign, ModAssign, BitwiseAndAssign, BitwiseOrAssign, XorAssign, TildeAssign, ShiftLeftAssign, ShiftRightAssign, TripleRightShiftAssign, PowAssign);
		[Obsolete("Use IsClassLike instead.")]
		public static readonly BitArray TypeDeclarationKW = NewSet(Class, Interface, Struct, Template, Enum, Delegate, Function);
		[Obsolete("Use IsRelationalOperator instead.")]
		public static readonly BitArray RelationalOperators = NewSet(LessThan, LessEqual, GreaterThan, GreaterEqual, Unordered, LessOrGreater, LessEqualOrGreater, UnorderedOrGreater, UnorderedGreaterOrEqual, UnorderedOrLess, UnorderedLessOrEqual, UnorderedOrEqual);
		[Obsolete("Use IsVisibilityModifier instead.")]
		public static readonly BitArray VisModifiers = NewSet(Public, Protected, Private, Package);
		[Obsolete("Use IsModifier instead.")]
		public static readonly BitArray Modifiers = NewSet(In, Out, InOut, Ref, Static, Override, Const, Public, Private, Protected, Package, Export, Shared, Final, Invariant, Immutable, Pure, Deprecated, Scope, __gshared, Lazy, Nothrow);
		[Obsolete("Use IsStorageClass instead.")]
		public static readonly BitArray StorageClass = NewSet(Abstract, Auto, Const, Deprecated, Extern, Final, Immutable, InOut, Shared, Nothrow, Override, Pure, Scope, Static, Synchronized, Ref, __gshared);
		[Obsolete("Use IsMetaIdentifier instead.")]
		public static readonly BitArray MetaIdentifiers = NewSet(__DATE__, __FILE__, __FUNCTION__, __LINE__, __MODULE__, __PRETTY_FUNCTION__, __TIMESTAMP__, __TIME__, __VENDOR__, __VERSION__);


        /// <summary>
        /// Checks if modifier array contains member attributes. If so, it returns the last found attribute. Otherwise 0.
        /// </summary>
        /// <param name="mods"></param>
        /// <returns></returns>
		public static DAttribute ContainsStorageClass(IEnumerable<DAttribute> mods)
        {
			if(mods != null)
				foreach(var m in mods){
            		if(m is Modifier && ((m as Modifier).IsStorageClass))
            			return m;
            		else if(m is AtAttribute)
            			return m;
				}
            return Modifier.Empty;
        }


        public static bool ContainsVisMod(List<byte> mods)
        {
            return
            mods.Contains(Public) ||
            mods.Contains(Private) ||
            mods.Contains(Package) ||
            mods.Contains(Protected);
        }

        public static void RemoveVisMod(List<byte> mods)
        {
            while (mods.Contains(Public))
                mods.Remove(Public);
            while (mods.Contains(Private))
                mods.Remove(Private);
            while (mods.Contains(Protected))
                mods.Remove(Protected);
            while (mods.Contains(Package))
                mods.Remove(Package);
        }

		static readonly Dictionary<byte, string> NonKeywords = new Dictionary<byte, string> {
			// Meta
			{ INVALID, "<Invalid Token>" },
			{ EOF, "<EOF>" },
			{ Identifier, "<Identifier>" },
			{ Literal, "<Literal>" },

			// Math operations
			{ Assign, "=" },
			{ Plus, "+" },
			{ Minus, "-" },
			{ Times, "*" },
			{ Div, "/" },
			{ Mod, "%" },
			{ Pow, "^^" },

			// Special chars
			{ Dot, "." },
			{ DoubleDot, ".." },
			{ TripleDot, "..." },
			{ Colon, ":" },
			{ Semicolon, ";" },
			{ Question, "?" },
			{ Dollar, "$" },
			{ Comma, "," },
			
			// Brackets
			{ OpenCurlyBrace, "{" },
			{ CloseCurlyBrace, "}" },
			{ OpenSquareBracket, "[" },
			{ CloseSquareBracket, "]" },
			{ OpenParenthesis, "(" },
			{ CloseParenthesis, ")" },

			// Relational
			{ GreaterThan, ">" },
			{ UnorderedGreaterOrEqual, "!<" },
			{ LessThan, "<"},
			{ UnorderedLessOrEqual, "!>" },
			{ Not, "!" },
			{ LessOrGreater, "<>" },
			{ UnorderedOrEqual, "!<>" },
			{ LogicalAnd, "&&" },
			{ LogicalOr, "||" },
			{ Tilde, "~" },
			{ BitwiseAnd, "&" },
			{ BitwiseOr, "|" },
			{ Xor, "^" },

			// Shift
			{ ShiftLeft, "<<" },
			{ ShiftRight, ">>" },
			{ ShiftRightUnsigned, ">>>" },

			// Increment
			{ Increment, "++" },
			{ Decrement, "--" },

			// Assign operators
			{ Equal, "==" },
			{ NotEqual, "!=" },
			{ GreaterEqual, ">=" },
			{ LessEqual, "<=" },
			{ PlusAssign, "+=" },
			{ MinusAssign, "-=" },
			{ TimesAssign, "*=" },
			{ DivAssign, "/=" },
			{ ModAssign, "%=" },
			{ BitwiseOrAssign, "|=" },
			{ XorAssign, "^=" },
			{ TildeAssign, "~=" },

			{ ShiftLeftAssign, "<<=" },
			{ ShiftRightAssign, ">>=" },
			{ TripleRightShiftAssign, ">>>=" },
			
			{ PowAssign, "^^=" },
			{ LessEqualOrGreater, "<>=" },
			{ Unordered, "!<>=" },
			{ UnorderedOrLess, "!>=" },
			{ UnorderedOrGreater, "!<=" },

			{ GoesTo, "=>" },
			{ At, "@" }
		};

        public static string GetTokenString(byte token)
        {
			if (Keywords.ContainsKey(token))
				return Keywords[token];
			if (NonKeywords.ContainsKey(token))
				return NonKeywords[token];

			return "<Unknown>";
        }

        public static byte GetTokenID(string token)
        {
			byte k;
			if (Keywords_Lookup.TryGetValue(token, out k) || token == null || token.Length < 1)
				return k;

			foreach (var kv in NonKeywords)
				if (kv.Value == token)
					return kv.Key;

            return INVALID;
        }

        public static string GetDescription(string token)
        {
			switch (token)
			{
				case "@disable":
					return "Disables a declaration\nA reference to a declaration marked with the @disable attribute causes a compile time error\n\nThis can be used to explicitly disallow certain operations \nor overloads at compile time \nrather than relying on generating a runtime error.";
				case "@property":
					return "Property functions \ncan be called without parentheses (hence acting like properties).\n\nstruct S {\n  int m_x;\n  @property {\n    int x() { return m_x; }\n    int x(int newx) { return m_x = newx; }\n  }\n}\n\nvoid foo() {\n  S s;\n  s.x = 3;   // calls s.x(int)\n  bar(s.x);  // calls bar(s.x())\n}";
				case "@safe":
					return "Safe functions\n\nThe following operations are not allowed in safe functions:\n\n- No casting from a pointer type to any type other than void*.\n- No casting from any non-pointer type to a pointer type.\n- No modification of pointer values.\n- Cannot access unions that have pointers or references overlapping with other types.\n- Calling any system functions.\n- No catching of exceptions that are not derived from class Exception.\n- No inline assembler.\n- No explicit casting of mutable objects to immutable.\n- No explicit casting of immutable objects to mutable.\n- No explicit casting of thread local objects to shared.\n- No explicit casting of shared objects to thread local.\n- No taking the address of a local variable or function parameter.\n- Cannot access __gshared variables.\n- Functions nested inside safe functions default to being safe functions.\n\nSafe functions are covariant with trusted or system functions.";
				case "@system":
					return "System functions \nare functions not marked with @safe or @trusted and are not nested inside @safe functions\n\nSystem functions may be marked with the @system attribute.\n \nA function being system does not mean it actually is unsafe, it just means that the compiler is unable to verify that it cannot exhibit undefined behavior.\n\nSystem functions are not covariant with trusted or safe functions.";
				case "@trusted":
					return "Trusted functions\n\n- Are marked with the @trusted attribute,\n- Are guaranteed by the programmer to not exhibit \n  any undefined behavior if called by a safe function,\n- May call safe, trusted, or system functions,\n- Are covariant with safe or system functions";
				default:
					return GetDescription(GetTokenID(token));
			}
        }

        public static string GetDescription(byte token)
        {
            switch (token)
            {
                case Else:
                case If:
                    return "if(a == b)\n{\n   foo();\n}\nelse if(a < b)\n{\n   ...\n}\nelse\n{\n   bar();\n}";
                case For:
                    return "for(int i; i<500; i++)\n{\n   foo();\n}";
                case Foreach_Reverse:
                case Foreach:
					return
                    "foreach"+(token==Foreach_Reverse?"_reverse":"")+
					"(element; array)\n{\n   foo(element);\n}\n\nOr:\nforeach" + (token == Foreach_Reverse ? "_reverse" : "") + 
					"(element, index; array)\n{\n   foo(element);\n}";
                case While:
                    return "while(a < b)\n{\n   foo();\n   a++;\n}";
                case Do:
                    return "do\n{\n   foo();\na++;\n}\nwhile(a < b);";
                case Switch:
                    return "switch(a)\n{\n   case 1:\n      foo();\n      break;\n   case 2:\n      bar();\n      break;\n   default:\n      break;\n}";
                default:
					return "D Keyword";
            }
        }

		public static bool IsAssignOperator(byte token)
		{
			switch (token)
			{
				case Assign: // =
				case PlusAssign: // +=
				case MinusAssign: // -=
				case TimesAssign: // *=
				case DivAssign: // /=
				case ModAssign: // %=
				case BitwiseAndAssign: // &=
				case BitwiseOrAssign: // |=
				case XorAssign: // ^=
				case TildeAssign: // ~=
				case ShiftLeftAssign: // <<=
				case ShiftRightAssign: // >>=
				case TripleRightShiftAssign: // >>>=
				case PowAssign: // ^^=
					return true;
				default:
					return false;
			}
		}

		public static readonly byte[] BasicTypes_Array = { Bool, Byte, Ubyte, Short, Ushort, Int, Uint, Long, Ulong, Cent, Ucent, Char, Wchar, Dchar, Float, Double, Real, Ifloat, Idouble, Ireal, Cfloat, Cdouble, Creal, Void };
		[Obsolete("Use IsBasicType instead.")]
		public static readonly BitArray BasicTypes = NewSet(BasicTypes_Array);
		public static bool IsBasicType(byte token)
		{
			switch (token)
			{
				case Bool:
				case Byte:
				case Ubyte:
				case Short:
				case Ushort:
				case Int:
				case Uint:
				case Long:
				case Ulong:
				case Cent:
				case Ucent:
				case Char:
				case Wchar:
				case Dchar:
				case Float:
				case Double:
				case Real:
				case Ifloat:
				case Idouble:
				case Ireal:
				case Cfloat:
				case Cdouble:
				case Creal:
				case Void:
					return true;
				default:
					return false;
			}
		}

		public static bool IsBasicType_Character(byte token)
		{
			switch (token)
			{
				case Char:
				case Wchar:
				case Dchar:
					return true;
				default:
					return false;
			}
		}

		public static bool IsBasicType_FloatingPoint(byte token)
		{
			switch (token)
			{
				case Float:
				case Double:
				case Real:
				case Ifloat:
				case Idouble:
				case Ireal:
				case Cfloat:
				case Cdouble:
				case Creal:
					return true;
				default:
					return false;
			}
		}

		public static bool IsBasicType_Integral(byte token)
		{
			switch (token)
			{
				case Bool:
				case Byte:
				case Ubyte:
				case Short:
				case Ushort:
				case Int:
				case Uint:
				case Long:
				case Ulong:
				case Cent:
				case Ucent:
				case Char:
				case Wchar:
				case Dchar:
					return true;
				default:
					return false;
			}
		}

		public static bool IsBasicType_Unsigned(byte token)
		{
			switch (token)
			{
				case Ubyte:
				case Ushort:
				case Uint:
				case Ulong:
				case Ucent:
					return true;
				default:
					return false;
			}
		}

		public static bool IsClassLike(byte token)
		{
			switch (token)
			{
				case Class:
				case Template:
				case Interface:
				case Struct:
				case Union:
					return true;
				default:
					return false;
			}
		}

		public static bool IsMemberFunctionAttribute(byte token)
		{
			switch (token)
			{
				case Const:
				case Immutable:
				case Shared:
				case InOut:
				case Pure:
				case Nothrow:
					return true;
				default:
					return false;
			}
		}

		public static bool IsMetaIdentifier(byte token)
		{
			switch (token)
			{
				case __DATE__:
				case __FILE__:
				case __FUNCTION__:
				case __LINE__:
				case __MODULE__:
				case __PRETTY_FUNCTION__:
				case __TIMESTAMP__:
				case __TIME__:
				case __VENDOR__:
				case __VERSION__:
					return true;
				default:
					return false;
			}
		}

		public static bool IsModifier(byte token)
		{
			switch (token)
			{
				case In:
				case Out:
				case InOut:
				case Ref:
				case Static:
				case Override:
				case Const:
				case Public:
				case Private:
				case Protected:
				case Package:
				case Export:
				case Shared:
				case Final:
				case Virtual:
				case Invariant:
				case Immutable:
				case Pure:
				case Deprecated:
				case Scope:
				case __gshared:
				case Lazy:
				case Nothrow:
					return true;
				default:
					return false;
			}
		}

		public static bool IsParamModifier(byte token)
		{
			switch (token)
			{
				case In:
				case Out:
				case InOut:
				case Ref:
				case Lazy:
				case Scope:
					return true;
				default:
					return false;
			}
		}

		public static bool IsRelationalOperator(byte token)
		{
			switch (token)
			{
				case LessThan:
				case LessEqual:
				case GreaterThan:
				case GreaterEqual:
				case Unordered:
				case LessOrGreater:
				case LessEqualOrGreater:
				case UnorderedOrGreater:
				case UnorderedGreaterOrEqual:
				case UnorderedOrLess:
				case UnorderedLessOrEqual:
				case UnorderedOrEqual:
					return true;
				default:
					return false;
			}
		}

		public static bool IsStorageClass(byte token)
		{
			switch (token)
			{
				case Abstract:
				case Auto:
				case Const:
				case Deprecated:
				case Extern:
				case Final:
				case Immutable:
				case InOut:
				case Shared:
				case Nothrow:
				case Override:
				case Pure:
				case Scope:
				case Static:
				case Synchronized:
				case Ref:
				case __gshared:
					return true;
				default:
					return false;
			}
		}

		public static bool IsVisibilityModifier(byte token)
		{
			switch (token)
			{
				case Public:
				case Protected:
				case Private:
				case Package:
					return true;
				default:
					return false;
			}
		}
    }
}
