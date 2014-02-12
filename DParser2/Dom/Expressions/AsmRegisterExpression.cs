using System;

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

		public ulong GetHash()
		{
			return (ulong)Register.GetHashCode();
		}

		public static bool IsRegister(string str)
		{
			switch (str)
			{
				// x86
				case "AL":
				case "AH":
				case "AX":
				case "EAX":
				case "BL":
				case "BH":
				case "BX":
				case "EBX":
				case "CL":
				case "CH":
				case "CX":
				case "ECX":
				case "DL":
				case "DH":
				case "DX":
				case "EDX":
				case "BP":
				case "EBP":
				case "SP":
				case "ESP":
				case "DI":
				case "EDI":
				case "SI":
				case "ESI":
				case "ES":
				case "CS":
				case "SS":
				case "DS":
				case "GS":
				case "FS":
				case "CR0":
				case "CR2":
				case "CR3":
				case "CR4":
				case "DR0":
				case "DR1":
				case "DR2":
				case "DR3":
				case "DR6":
				case "DR7":
				case "TR3":
				case "TR4":
				case "TR5":
				case "TR6":
				case "TR7":
				case "MM0":
				case "MM1":
				case "MM2":
				case "MM3":
				case "MM4":
				case "MM5":
				case "MM6":
				case "MM7":
				case "XMM0":
				case "XMM1":
				case "XMM2":
				case "XMM3":
				case "XMM4":
				case "XMM5":
				case "XMM6":
				case "XMM7":

				// x86_64
				case "RAX":
				case "RBX":
				case "RCX":
				case "RDX":
				case "BPL":
				case "RBP":
				case "SPL":
				case "RSP":
				case "DIL":
				case "RDI":
				case "SIL":
				case "RSI":
				case "R8B":
				case "R8W":
				case "R8D":
				case "R8":
				case "R9B":
				case "R9W":
				case "R9D":
				case "R9":
				case "R10B":
				case "R10W":
				case "R10D":
				case "R10":
				case "R11B":
				case "R11W":
				case "R11D":
				case "R11":
				case "R12B":
				case "R12W":
				case "R12D":
				case "R12":
				case "R13B":
				case "R13W":
				case "R13D":
				case "R13":
				case "R14B":
				case "R14W":
				case "R14D":
				case "R14":
				case "R15B":
				case "R15W":
				case "R15D":
				case "R15":
				case "XMM8":
				case "XMM9":
				case "XMM10":
				case "XMM11":
				case "XMM12":
				case "XMM13":
				case "XMM14":
				case "XMM15":
				case "YMM0":
				case "YMM1":
				case "YMM2":
				case "YMM3":
				case "YMM4":
				case "YMM5":
				case "YMM6":
				case "YMM7":
				case "YMM8":
				case "YMM9":
				case "YMM10":
				case "YMM11":
				case "YMM12":
				case "YMM13":
				case "YMM14":
				case "YMM15":

				case "ST":
					return true;
				default:
					return false;
			}
		}
	}
}

