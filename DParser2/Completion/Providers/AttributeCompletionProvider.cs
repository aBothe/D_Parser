using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Parser;
using D_Parser.Resolver;

namespace D_Parser.Completion.Providers
{
	class VersionSpecificationCompletionProvider : AbstractCompletionProvider
	{
		public VersionSpecificationCompletionProvider(ICompletionDataGenerator gen) : base(gen){}

		private static readonly Dictionary<string, string> VersionCompletionItems = new Dictionary<string, string>
		{
			{"DigitalMars","DMD (Digital Mars D) is the compiler"},
			{"GNU","GDC (GNU D Compiler) is the compiler"},
			{"LDC","LDC (LLVM D Compiler) is the compiler"},
			{"SDC","SDC (Stupid D Compiler) is the compiler"},
			{"D_NET","D.NET is the compiler"},
			{"Windows","Microsoft Windows systems"},
			{"Win32","Microsoft 32-bit Windows systems"},
			{"Win64","Microsoft 64-bit Windows systems"},
			{"linux","All Linux systems"},
			{"OSX","Mac OS X"},
			{"FreeBSD","FreeBSD"},
			{"OpenBSD","OpenBSD"},
			{"BSD","All other BSDs"},
			{"Solaris","Solaris"},
			{"Posix","All POSIX systems (includes Linux, FreeBSD, OS X, Solaris, etc.)"},
			{"AIX","IBM Advanced Interactive eXecutive OS"},
			{"SkyOS","The SkyOS operating system"},
			{"SysV3","System V Release 3"},
			{"SysV4","System V Release 4"},
			{"Hurd","GNU Hurd"},
			{"Cygwin","The Cygwin environment"},
			{"MinGW","The MinGW environment"},
			{"X86","Intel and AMD 32-bit processors"},
			{"X86_64","AMD and Intel 64-bit processors"},
			{"ARM","The Advanced RISC Machine architecture (32-bit)"},
			{"PPC","The PowerPC architecture, 32-bit"},
			{"PPC64","The PowerPC architecture, 64-bit"},
			{"IA64","The Itanium architecture (64-bit)"},
			{"MIPS","The MIPS architecture, 32-bit"},
			{"MIPS64","The MIPS architecture, 64-bit"},
			{"SPARC","The SPARC architecture, 32-bit"},
			{"SPARC64","The SPARC architecture, 64-bit"},
			{"S390","The System/390 architecture, 32-bit"},
			{"S390X","The System/390X architecture, 64-bit"},
			{"HPPA","The HP PA-RISC architecture, 32-bit"},
			{"HPPA64","The HP PA-RISC architecture, 64-bit"},
			{"SH","The SuperH architecture, 32-bit"},
			{"SH64","The SuperH architecture, 64-bit"},
			{"Alpha","The Alpha architecture"},
			{"LittleEndian","Byte order, least significant first"},
			{"BigEndian","Byte order, most significant first"},
			{"D_Coverage","Code coverage analysis instrumentation (command line switch -cov) is being generated"},
			{"D_Ddoc","Ddoc documentation (command line switch -D) is being generated"},
			{"D_InlineAsm_X86","Inline assembler for X86 is implemented"},
			{"D_InlineAsm_X86_64","Inline assembler for X86-64 is implemented"},
			{"D_LP64","Pointers are 64 bits (command line switch -m64)"},
			{"D_PIC","Position Independent Code (command line switch -fPIC) is being generated"},
			{"D_SIMD","Vector Extensions are supported"},
			{"unittest","Unit tests are enabled (command line switch -unittest)"},
			{"D_Version2","This is a D version 2 compiler"},
			{"none","Never defined; used to just disable a section of code"},
			{"all","Always defined; used as the opposite of none"}
		};

		protected override void BuildCompletionDataInternal (IEditorData Editor, char enteredChar)
		{
			var ctxt = ResolutionContext.Create (Editor, false);
			foreach (var kv in VersionCompletionItems)
				CompletionDataGenerator.AddTextItem(kv.Key, kv.Value);

			CodeCompletion.DoTimeoutableCompletionTask (CompletionDataGenerator, ctxt, () => {
				ctxt.Push(Editor);
				var cs = ctxt.CurrentContext.DeclarationConditions;
				foreach (var v in cs.GlobalFlags.Versions)
					if(!VersionCompletionItems.ContainsKey(v))
						CompletionDataGenerator.AddTextItem (v, "");
				foreach (var v in cs.LocalFlags.Versions)
					if (!VersionCompletionItems.ContainsKey(v))
						CompletionDataGenerator.AddTextItem (v, "");
			}, Editor.CancelToken);
		}	
	}

	class DebugSpecificationCompletionProvider : AbstractCompletionProvider
	{
		public DebugSpecificationCompletionProvider (ICompletionDataGenerator gen) : base (gen){
		}

		protected override void BuildCompletionDataInternal (IEditorData Editor, char enteredChar)
		{
			var ctxt = ResolutionContext.Create (Editor, false);
			CodeCompletion.DoTimeoutableCompletionTask (CompletionDataGenerator, ctxt, () => {
				ctxt.Push(Editor);
				var cs = ctxt.CurrentContext.DeclarationConditions;
				foreach (var v in cs.GlobalFlags.DebugVersions)
					CompletionDataGenerator.AddTextItem (v, "");
				foreach (var v in cs.LocalFlags.DebugVersions)
					CompletionDataGenerator.AddTextItem (v, "");
			}, Editor.CancelToken);
		}
	}

	class PragmaCompletionProvider : AbstractCompletionProvider
	{
		readonly PragmaAttribute Attribute;

		public PragmaCompletionProvider(PragmaAttribute attr,ICompletionDataGenerator gen) : base(gen) {
			Attribute = attr;
		}

		private static readonly Dictionary<string, string> PragmaAttributeCompletionItems = new Dictionary<string, string>
		{
			{"lib","Inserts a directive in the object file to link in"}, 
			{"msg","Prints a message while compiling"}, 
			{"startaddress","Puts a directive into the object file saying that the function specified in the first argument will be the start address for the program"}
		};

		protected override void BuildCompletionDataInternal (IEditorData Editor, char enteredChar)
		{
			if (string.IsNullOrEmpty(Attribute.Identifier))
				foreach (var kv in PragmaAttributeCompletionItems)
					CompletionDataGenerator.AddTextItem(kv.Key, kv.Value);
		}
	}

	class AttributeCompletionProvider : AbstractCompletionProvider
	{
		public readonly DAttribute Attribute;

		public AttributeCompletionProvider(DAttribute attr,ICompletionDataGenerator gen) : base(gen) {
			this.Attribute = attr;
		}

		private static readonly Dictionary<string, string> ExternAttributeCompletionItems = new Dictionary<string, string>
		{
			{"C",""},
			{"C++","C++ is reserved for future use"},
			{"D",""},
			{"Windows","Implementation Note: for Win32 platforms, Windows and Pascal should exist"},
			{"Pascal","Implementation Note: for Win32 platforms, Windows and Pascal should exist"},
			{"System","System is the same as Windows on Windows platforms, and C on other platforms"}
		};

		protected override void BuildCompletionDataInternal(IEditorData Editor, char enteredChar)
		{
			if (Attribute is Modifier && ((Modifier)Attribute).Token == DTokens.Extern)
			{
				foreach (var kv in ExternAttributeCompletionItems)
					CompletionDataGenerator.AddTextItem(kv.Key, kv.Value);
			}
		}
	}

	internal class ScopeAttributeCompletionProvider : AbstractCompletionProvider
	{
		//public ScopeGuardStatement ScopeStmt;

		public ScopeAttributeCompletionProvider(ICompletionDataGenerator gen) : base(gen) { }


		private static readonly Dictionary<string, string> ScopeAttributeCompletionItems = new Dictionary<string, string>
		{
			{"exit","Executes on leaving current scope"}, 
			{"success", "Executes if no error occurred in current scope"}, 
			{"failure","Executes if error occurred in current scope"}
		};

		protected override void BuildCompletionDataInternal(IEditorData Editor, char enteredChar)
		{
			foreach (var kv in ScopeAttributeCompletionItems)
				CompletionDataGenerator.AddTextItem(kv.Key, kv.Value);
		}
	}

	internal class TraitsExpressionCompletionProvider : AbstractCompletionProvider
	{
		//public TraitsExpression TraitsExpr;

		public TraitsExpressionCompletionProvider(ICompletionDataGenerator gen) : base(gen) { }

		private static readonly Dictionary<string, string> TraitsExpressionCompletionItems = new Dictionary<string, string>
		{
			{"isAbstractClass","If the arguments are all either types that are abstract classes, or expressions that are typed as abstract classes, then true is returned. Otherwise, false is returned. If there are no arguments, false is returned."},
			{"isArithmetic","If the arguments are all either types that are arithmetic types, or expressions that are typed as arithmetic types, then true is returned. Otherwise, false is returned. If there are no arguments, false is returned."},
			{"isAssociativeArray","Works like isArithmetic, except it's for associative array types."},
			{"isFinalClass","Works like isAbstractClass, except it's for final classes."},

			{"isPOD","Takes one argument, which must be a type. It returns true if the type is a POD type, otherwise false."},
			{"isNested","Takes one argument. It returns true if the argument is a nested type which internally stores a context pointer."},
			{"isFuture","Takes one argument. It returns true if the argument is a symbol marked with the @future keyword."},
			{"isDisabled","Takes one argument. It returns true if the argument is and returns true if it is a function declaration marked with @disable."},
			{"isDeprecated","Takes one argument. It returns true if the argument is a symbol marked with the deprecated keyword."},
			{"isFloating","Works like isArithmetic, except it's for floating point types (including imaginary and complex types)."},
			{"isIntegral","Works like isArithmetic, except it's for integral types (including character types)."},
			{"isScalar","Works like isArithmetic, except it's for scalar types."},
			{"isStaticArray","Works like isArithmetic, except it's for static array types."},
			{"isUnsigned","Works like isArithmetic, except it's for unsigned types."},
			{"isDisabled","Takes one argument and returns true if it's a function declaration marked with @disable."},
			{"isVirtualFunction","The same as isVirtualMethod, except that final functions that don't override anything return true."},
			{"isVirtualMethod","Takes one argument. If that argument is a virtual function, true is returned, otherwise false. Final functions that don't override anything return false."},
			{"isAbstractFunction","Takes one argument. If that argument is an abstract function, true is returned, otherwise false."},
			{"isFinalFunction","Takes one argument. If that argument is a final function, true is returned, otherwise false."},
			{"isStaticFunction","Takes one argument. If that argument is a static function, meaning it has no context pointer, true is returned, otherwise false."},
			{"isOverrideFunction","Takes one argument. If that argument is a function marked with override, true is returned, otherwise false."},
			{"isTemplate","Takes one argument. If that argument is a template then true is returned, otherwise false."},
			{"isZeroInit","Takes one argument which must be a type. If the type's default initializer has no non-zero bits then true is returned, otherwise false."},

			{"isRef","Takes one argument. If that argument is a declaration, true is returned if it is ref, otherwise false."},
			{"isOut","Takes one argument. If that argument is a declaration, true is returned if it is out, otherwise false."},
			{"isLazy","Takes one argument. If that argument is a declaration, true is returned if it is lazy, otherwise false."},
			{"hasMember","The first argument is a type that has members, or is an expression of a type that has members. The second argument is a string. If the string is a valid property of the type, true is returned, otherwise false."},
			{"identifier","Takes one argument, a symbol. Returns the identifier for that symbol as a string literal."},
			{"getAliasThis","Takes one argument, a symbol of aggregate type. Returns a list of alias this names, by a tuple of strings."},
			{"getAttributes","Takes one argument, a symbol. Returns a tuple of all attached user-defined attributes."},
			{"getFunctionAttributes","Takes one argument which must either be a function symbol, function literal, or a function pointer. Returns a string tuple of all the attributes of that function excluding user-defined attributes."},
			{"getFunctionVariadicStyle","Takes one argument which must either be a function symbol, or a type that is a function, delegate or a function pointer. Returns a string identifying the kind of variadic arguments."},
			{"getLinkage","Takes one argument, which is a declaration symbol, or the type of a function, delegate, pointer to function, struct, class, or interface. Returns a string representing the linkage of the declaration."},
			{"getMember","Takes two arguments, the second must be a string. The result is an expression formed from the first argument, followed by a ‘.’, followed by the second argument as an identifier."},
			{"getOverloads","The first argument is a class type or an expression of class type. The second argument is a string that matches the name of one of the functions of that class. The result is a tuple of all the overloads of that function."},
			{"getParameterStorageClasses","Takes two arguments, a function symbol or a function/delegate type and an zero-based integer parameter index. Returns a tuple of strings representing the storage classes of the indexed parameter."},
			{"getPointerBitmap","The argument is a type. Returns an array describing the memory used by an instance of the given type."},
			{"getProtection","The argument is a symbol. The result is a string giving its protection level."},
			{"getTargetInfo","Receives a string key as argument. The result is an expression describing the requested target information."},
			{"getVirtualFunctions","The same as getVirtualMethods, except that final functions that do not override anything are included."},
			{"getVirtualMethods","The first argument is a class type or an expression of class type. The second argument is a string that matches the name of one of the functions of that class. The result is a tuple of the virtual overloads of that function. It does not include final functions that do not override anything."},
			{"getUnitTests","Takes one argument, a symbol of an aggregate. The result is a tuple of all the unit test functions of that aggregate."},
			{"getTargetInfo","Takes a string key as argument to select the target detail of interest."},
			{"parent","Takes a single argument which must evaluate to a symbol. The result is the symbol that is the parent of it."},
			{"classInstanceSize","Takes a single argument, which must evaluate to either a class type or an expression of class type. The result is of type size_t, and the value is the number of bytes in the runtime instance of the class type. It is based on the static type of a class, not the polymorphic type."},
			{"getVirtualIndex","Takes a single argument which must evaluate to a function. The result is a ptrdiff_t containing the index of that function within the vtable of the parent type."},
			{"allMembers","Takes a single argument, which must evaluate to either a type or an expression of type. A tuple of string literals is returned, each of which is the name of a member of that type combined with all of the members of the base classes (if the type is a class). No name is repeated. Builtin properties are not included."},
			{"derivedMembers","Takes a single argument, which must evaluate to either a type or an expression of type. A tuple of string literals is returned, each of which is the name of a member of that type. No name is repeated. Base class member names are not included. Builtin properties are not included."},
			{"isSame","Takes two arguments and returns bool true if they are the same symbol, false if not."},
			{"compiles",@"Returns a bool true if all of the arguments compile (are semantically correct). The arguments can be symbols, types, or expressions that are syntactically correct. The arguments cannot be statements or declarations. If there are no arguments, the result is false."},
		};

		protected override void BuildCompletionDataInternal(IEditorData Editor, char enteredChar)
		{
			foreach (var kv in TraitsExpressionCompletionItems)
				CompletionDataGenerator.AddTextItem(kv.Key, kv.Value);
		}
	}
}
