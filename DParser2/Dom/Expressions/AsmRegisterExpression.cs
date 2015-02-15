using System;
using System.Collections.Generic;

namespace D_Parser.Dom.Expressions
{
	public sealed class AsmRegisterExpression : PrimaryExpression
	{
		public string Register { get; set; }
		public CodeLocation Location { get; set; }
		public CodeLocation EndLocation { get; set; }

		public R Accept<R>(ExpressionVisitor<R> v) { return v.Visit(this); }
		public void Accept(ExpressionVisitor v) { v.Visit(this); }

		public override string ToString()
		{
			return Register;
		}

		public static readonly Dictionary<string, string> x86RegisterTable = new Dictionary<string, string>
		{
			{ "AL", "" },
			{ "AH", "" },
			{ "AX", "" },
			{ "EAX", "" },
			{ "BL", "" },
			{ "BH", "" },
			{ "BX", "" },
			{ "EBX", "" },
			{ "CL", "" },
			{ "CH", "" },
			{ "CX", "" },
			{ "ECX", "" },
			{ "DL", "" },
			{ "DH", "" },
			{ "DX", "" },
			{ "EDX", "" },
			{ "BP", "" },
			{ "EBP", "" },
			{ "SP", "" },
			{ "ESP", "" },
			{ "DI", "" },
			{ "EDI", "" },
			{ "SI", "" },
			{ "ESI", "" },
			{ "ES", "" },
			{ "CS", "" },
			{ "SS", "" },
			{ "DS", "" },
			{ "GS", "" },
			{ "FS", "" },
			{ "CR0", "" },
			{ "CR2", "" },
			{ "CR3", "" },
			{ "CR4", "" },
			{ "DR0", "" },
			{ "DR1", "" },
			{ "DR2", "" },
			{ "DR3", "" },
			{ "DR6", "" },
			{ "DR7", "" },
			{ "TR3", "" },
			{ "TR4", "" },
			{ "TR5", "" },
			{ "TR6", "" },
			{ "TR7", "" },
			{ "MM0", "" },
			{ "MM1", "" },
			{ "MM2", "" },
			{ "MM3", "" },
			{ "MM4", "" },
			{ "MM5", "" },
			{ "MM6", "" },
			{ "MM7", "" },
			{ "XMM0", "" },
			{ "XMM1", "" },
			{ "XMM2", "" },
			{ "XMM3", "" },
			{ "XMM4", "" },
			{ "XMM5", "" },
			{ "XMM6", "" },
			{ "XMM7", "" },
			{ "ST", "" },
			{ "ST(0)", "" },
			{ "ST(1)", "" },
			{ "ST(2)", "" },
			{ "ST(3)", "" },
			{ "ST(4)", "" },
			{ "ST(5)", "" },
			{ "ST(6)", "" },
			{ "ST(7)", "" },
		};

		public static readonly Dictionary<string, string> x64RegisterTable = new Dictionary<string, string>
		{
			{ "RAX", "" },
			{ "RBX", "" },
			{ "RCX", "" },
			{ "RDX", "" },
			{ "BPL", "" },
			{ "RBP", "" },
			{ "SPL", "" },
			{ "RSP", "" },
			{ "DIL", "" },
			{ "RDI", "" },
			{ "SIL", "" },
			{ "RSI", "" },
			{ "R8B", "" },
			{ "R8W", "" },
			{ "R8D", "" },
			{ "R8", "" },
			{ "R9B", "" },
			{ "R9W", "" },
			{ "R9D", "" },
			{ "R9", "" },
			{ "R10B", "" },
			{ "R10W", "" },
			{ "R10D", "" },
			{ "R10", "" },
			{ "R11B", "" },
			{ "R11W", "" },
			{ "R11D", "" },
			{ "R11", "" },
			{ "R12B", "" },
			{ "R12W", "" },
			{ "R12D", "" },
			{ "R12", "" },
			{ "R13B", "" },
			{ "R13W", "" },
			{ "R13D", "" },
			{ "R13", "" },
			{ "R14B", "" },
			{ "R14W", "" },
			{ "R14D", "" },
			{ "R14", "" },
			{ "R15B", "" },
			{ "R15W", "" },
			{ "R15D", "" },
			{ "R15", "" },
			{ "XMM8", "" },
			{ "XMM9", "" },
			{ "XMM10", "" },
			{ "XMM11", "" },
			{ "XMM12", "" },
			{ "XMM13", "" },
			{ "XMM14", "" },
			{ "XMM15", "" },
			{ "YMM0", "" },
			{ "YMM1", "" },
			{ "YMM2", "" },
			{ "YMM3", "" },
			{ "YMM4", "" },
			{ "YMM5", "" },
			{ "YMM6", "" },
			{ "YMM7", "" },
			{ "YMM8", "" },
			{ "YMM9", "" },
			{ "YMM10", "" },
			{ "YMM11", "" },
			{ "YMM12", "" },
			{ "YMM13", "" },
			{ "YMM14", "" },
			{ "YMM15", "" },
		};

		public static bool IsRegister(string str)
		{
			return x86RegisterTable.ContainsKey(str) || x64RegisterTable.ContainsKey(str);
		}
	}
}

