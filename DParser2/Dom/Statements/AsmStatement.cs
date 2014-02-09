using System;
using D_Parser.Dom.Expressions;
using System.Text;

namespace D_Parser.Dom.Statements
{
	public class AsmStatement : StatementContainingStatement
	{
		public bool Naked { get; set; }
		public AbstractStatement[] Instructions;

		public override string ToCode()
		{
			var ret = "asm {";

			if (Instructions != null && Instructions.Length > 0)
			{
				foreach (var i in Instructions)
					ret += Environment.NewLine + i.ToCode() + ';';
				ret += Environment.NewLine;
			}

			return ret + '}';
		}

		public override void Accept(StatementVisitor vis) { vis.Visit(this); }
		public override R Accept<R>(StatementVisitor<R> vis) { return vis.Visit(this); }

		public sealed class InstructionStatement : AbstractStatement
		{
			public OpCode Operation { get; set; }
			public IExpression[] Arguments { get; set; }

			public enum OpCode
			{
				// Analysis disable InconsistentNaming
				__UNKNOWN__,

				aaa,
				aad,
				aam,
				aas,
				adc,
				add,
				addpd,
				addps,
				addsd,
				addss,
				and,
				andnpd,
				andnps,
				andpd,
				andps,
				arpl,
				bound,
				bsf,
				bsr,
				bswap,
				bt,
				btc,
				btr,
				bts,
				call,
				cbw,
				cdq,
				clc,
				cld,
				clflush,
				cli,
				clts,
				cmc,
				cmova,
				cmovae,
				cmovb,
				cmovbe,
				cmovc,
				cmove,
				cmovg,
				cmovge,
				cmovl,
				cmovle,
				cmovna,
				cmovnae,
				cmovnb,
				cmovnbe,
				cmovnc,
				cmovne,
				cmovng,
				cmovnge,
				cmovnl,
				cmovnle,
				cmovno,
				cmovnp,
				cmovns,
				cmovnz,
				cmovo,
				cmovp,
				cmovpe,
				cmovpo,
				cmovs,
				cmovz,
				cmp,
				cmppd,
				cmpps,
				cmps,
				cmpsb,
				cmpsd,
				cmpss,
				cmpsw,
				cmpxchg8b, // NOTE: This is mispelled in the spec, which has "cmpxch8b"
				cmpxchg,
				comisd,
				comiss,
				cpuid,
				cvtdq2pd,
				cvtdq2ps,
				cvtpd2dq,
				cvtpd2pi,
				cvtpd2ps,
				cvtpi2pd,
				cvtpi2ps,
				cvtps2dq,
				cvtps2pd,
				cvtps2pi,
				cvtsd2si,
				cvtsd2ss,
				cvtsi2sd,
				cvtsi2ss,
				cvtss2sd,
				cvtss2si,
				cvttpd2dq,
				cvttpd2pi,
				cvttps2dq,
				cvttps2pi,
				cvttsd2si,
				cvttss2si,
				cwd,
				cwde,
				da,
				daa,
				das,
				dec,
				div,
				divpd,
				divps,
				divsd,
				divss,
				emms,
				enter,
				f2xm1,
				fabs,
				fadd,
				faddp,
				fbld,
				fbstp,
				fchs,
				fclex,
				fcmovb,
				fcmovbe,
				fcmove,
				fcmovnb,
				fcmovnbe,
				fcmovne,
				fcmovnu,
				fcmovu,
				fcom,
				fcomi,
				fcomip,
				fcomp,
				fcompp,
				fcos,
				fdecstp,
				fdisi,
				fdiv,
				fdivp,
				fdivr,
				fdivrp,
				feni,
				ffree,
				fiadd,
				ficom,
				ficomp,
				fidiv,
				fidivr,
				fild,
				fimul,
				fincstp,
				finit,
				fist,
				fistp,
				fisub,
				fusubr,
				fld,
				fld1,
				fldcw,
				fldenv,
				fldl2e,
				fldl2t,
				fldlg2,
				fldln2,
				fldpi,
				fldz,
				fmul,
				fmulp,
				fnclex,
				fndisi,
				fneni,
				fninit,
				fnop,
				fnsave,
				fnstcw,
				fnstenv,
				fnstsw,
				fpatan,
				fprem,
				fprem1,
				fptan,
				frndint,
				frstor,
				fsave,
				fscale,
				fsetpm,
				fsin,
				fsincos,
				fsqrt,
				fst,
				fstcw,
				fstenv,
				fstp,
				fstsw,
				fsub,
				fsubp,
				fsubr,
				fsubrp,
				ftst,
				fucom,
				fucomi,
				fucomip,
				fucomp,
				fucompp,
				fwait,
				fxam,
				fxch,
				fxrstor,
				fxsave,
				fxtract,
				fyl2x,
				fyl2xp1,
				hlt,
				idiv,
				imul,
				in_, // NOTE: Keyword.
				inc,
				ins,
				insb,
				insd,
				insw,
				int_, // NOTE: Keyword.
				into,
				invd,
				invlpg,
				iret,
				iretd,
				#region Jump Instructions (short modifier valid)
				ja,
				jae,
				jb,
				jbe,
				jc,
				jcxz,
				je,
				jecxz,
				jg,
				jge,
				jl,
				jle,
				jmp,
				jna,
				jnae,
				jnb,
				jnbe,
				jnc,
				jne,
				jng,
				jnge,
				jnl,
				jnle,
				jno,
				jnp,
				jns,
				jnz,
				jo,
				jp,
				jpe,
				jpo,
				js,
				jz,
				#endregion
				lahf,
				lar,
				ldmxcsr,
				lds,
				lea,
				leave,
				les,
				lfence,
				lfs,
				lgdt,
				lgs,
				lidt,
				lldt,
				lmsw,
				lock_, // NOTE: Keyword
				lods,
				lodsb,
				lodsd,
				lodsw,
				loop,
				loope,
				loopne,
				loopnz,
				loopz,
				lsl,
				lss,
				ltr,
				maskmovdqu,
				maskmovq,
				maxpd,
				maxps,
				maxsd,
				maxss,
				mfence,
				minpd,
				minps,
				minsd,
				minss,
				mov,
				movapd,
				movaps,
				movd,
				movdq2q,
				movdqa,
				movdqu,
				movhlps,
				movhpd,
				movhps,
				movlhps,
				movlpd,
				movlps,
				movmskpd,
				movmskps,
				movntdq,
				movnti,
				movntpd,
				movntps,
				movntq,
				movq,
				movq2dq,
				movs,
				movsb,
				movsd,
				movss,
				movsw,
				movsx,
				movupd,
				movups,
				movzx,
				mul,
				mulpd,
				mulps,
				mulsd,
				mulss,
				neg,
				nop,
				not,
				or,
				orpd,
				orps,
				out_, // NOTE: Keyword
				outs,
				outsb,
				outsd,
				outsw,
				packssdw,
				packsswb,
				packuswb,
				paddb,
				paddd,
				paddq,
				paddsb,
				paddsw,
				paddusb,
				paddusw,
				paddw,
				pand,
				pandn,
				pavgb,
				pavgw,
				pcmpeqb,
				pcmpeqd,
				pcmpeqw,
				pcmpgtb,
				pcmpgtd,
				pcmpgtw,
				pextrw,
				pinsrw,
				pmaddwd,
				pmaxsw,
				pmaxub,
				pminsw,
				pminub,
				pmovmskb,
				pmulhuw,
				pmulhw,
				pmullw,
				pmuludq,
				pop,
				popa,
				popad,
				popf,
				popfd, // 32-bit ONLY
				por,
				prefetchnta,
				prefetcht0,
				prefetch1,
				prefetcht2,
				psadbw,
				pshufd,
				pshufhw,
				pshuflw,
				pshufw,
				pslld,
				pslldq,
				psllq,
				psllw,
				psrad,
				psraw,
				psrld,
				psrldq,
				psrlq,
				psrlw,
				psubb,
				psubd,
				psubq,
				psubsb,
				psubsw,
				psubusb,
				psubusw,
				psubw,
				punpckhbw,
				punpckhdq,
				punpckhqdq,
				punckhwd,
				punpcklbw,
				punpckldq,
				punpcklqdq,
				punpcklwd,
				push,
				pusha,
				pushad,
				pushf,
				pushfd, // 32-bit ONLY
				pxor,
				rcl,
				rcpps,
				rcpss,
				rcr,
				rdmsr,
				rdpmc,
				rdtsc,
				rep,
				repe,
				repne,
				repnz,
				repz,
				ret,
				retf,
				rol,
				ror,
				rsm,
				rsqrtps,
				rsqrtss,
				sahf,
				sal,
				sar,
				sbb,
				scas,
				scasb,
				scasd,
				scasw,
				seta,
				setae,
				setb,
				setbe,
				setc,
				sete,
				setg,
				setge,
				setl,
				setle,
				setna,
				setnae,
				setnb,
				setnbe,
				setnc,
				setne,
				setng,
				setnge,
				setnl,
				setnle,
				setno,
				setnp,
				setns,
				setnz,
				seto,
				setp,
				setpe,
				setpo,
				sets,
				setz,
				sfence,
				sgdt,
				shl,
				shld,
				shr,
				shrd,
				shufpd,
				shufps,
				sidt,
				sldt,
				smsw,
				sqrtpd,
				sqrtps,
				sqrtsd,
				sqrtss,
				stc,
				std,
				sti,
				stmxcsr,
				stos,
				stosb,
				stosd,
				stosw,
				str,
				sub,
				subpd,
				subps,
				subsd,
				subss,
				sysenter,
				sysexit,
				test,
				ucomisd,
				ucomiss,
				ud2,
				unpckhpd,
				unpckhps,
				unpcklpd,
				unpcklps,
				verr,
				verw,
				wait,
				wbinvd,
				wrmsr,
				xadd,
				xchg,
				xlat,
				xlatb,
				xor,
				xorpd,
				xorps,

				// Pentium 4
				addsubpd,
				addsubps,
				fisttp,
				haddpd,
				haddps,
				hsubpd,
				hsubps,
				lddqu,
				monitor,
				movddup,
				movshdup,
				movsldup,
				mwait,

				// AMD
				pavgusb,
				pf2id,
				pfacc,
				pfadd,
				pfcmpeq,
				pfcmpge,
				pfcmpgt,
				pfmax,
				pfmin,
				pfmul,
				pfnacc,
				pfpnacc,
				pfrcp,
				pfrcpit1,
				pfrsqit1,
				pfrsqrt,
				pfsub,
				pfsubr,
				pi2fd,
				pmulhrw,
				pswapd,

				#region Not Listed in Spec

				// AVX
				xsave,
				xrstor,
				xsetbv,
				xgetbv,

				// 64-bit ONLY
				movsq,
				popfq,
				pushfq,

				// SSE 4.1
				mpsadbw,
				phminposuw,
				pmuldq,
				pmulld,
				dpps,
				dppd,
				blendps,
				blendpd,
				blendvps,
				blendvpd,
				pblendvb,
				pblendw,
				pminsb,
				pmaxsb,
				pminuw,
				pmaxuw,
				pminud,
				pmaxud,
				pminsd,
				pmaxsd,
				roundps,
				roundss,
				roundpd,
				roundsd,
				insertps,
				pinsrb,
				pinsrd,
				pinsrq, // 64-bit ONLY
				extractps,
				pextrb,
				pextrd,
				pextrq, // 64-bit ONLY
				pmovsxbw,
				pmovzxbw,
				pmovsxbd,
				pmovzxbd,
				pmovsxwd,
				pmovzxwd,
				pmovsxbq,
				pmovzxbq,
				pmovsxwq,
				pmovzxwq,
				pmovsxdq,
				pmovzxdq,
				ptest,
				pcmpeqq,
				packusdw,
				movntdqa,

				// SSE 4.2
				crc32,
				pcmpestri,
				pcmpestrm,
				pcmpistri,
				pcmpistrm,
				pcmpgtq,

				#endregion

				// Analysis restore InconsistentNaming
			}

			public static bool TryParseOpCode(string str, out OpCode dst)
			{
				switch (str.ToLower())
				{
					case "in":
						dst = OpCode.in_;
						return true;
					case "int":
						dst = OpCode.int_;
						return true;
					case "lock":
						dst = OpCode.lock_;
						return true;
					case "out":
						dst = OpCode.out_;
						return true;
					default:
						return Enum.TryParse(str, true, out dst);
				}
			}

			public static string StringForOpCode(OpCode val)
			{
				switch (val)
				{
					case OpCode.in_:
						return "in";
					case OpCode.int_:
						return "int";
					case OpCode.lock_:
						return "lock";
					case OpCode.out_:
						return "out";
					default:
						return val.ToString();
				}
			}

			public override string ToCode()
			{
				var ret = StringForOpCode(Operation);
				if (Arguments != null)
				{
					for (int i = 0; i < Arguments.Length; i++)
					{
						if (i != 0)
							ret += ",";
						ret += " " + Arguments[i].ToString();
					}
				}
				return ret;
			}

			public override void Accept(StatementVisitor vis) { vis.Visit(this); }
			public override R Accept<R>(StatementVisitor<R> vis) { return vis.Visit(this); }

			public bool IsJmpFamily
			{
				get
				{
					switch (Operation)
					{
						case OpCode.ja:
						case OpCode.jae:
						case OpCode.jb:
						case OpCode.jbe:
						case OpCode.jc:
						case OpCode.jcxz:
						case OpCode.je:
						case OpCode.jecxz:
						case OpCode.jg:
						case OpCode.jge:
						case OpCode.jl:
						case OpCode.jle:
						case OpCode.jmp:
						case OpCode.jna:
						case OpCode.jnae:
						case OpCode.jnb:
						case OpCode.jnbe:
						case OpCode.jnc:
						case OpCode.jne:
						case OpCode.jng:
						case OpCode.jnge:
						case OpCode.jnl:
						case OpCode.jnle:
						case OpCode.jno:
						case OpCode.jnp:
						case OpCode.jns:
						case OpCode.jnz:
						case OpCode.jo:
						case OpCode.jp:
						case OpCode.jpe:
						case OpCode.jpo:
						case OpCode.js:
						case OpCode.jz:
							return true;
						default:
							return false;
					}
				}
			}
		}

		public sealed class RawDataStatement : AbstractStatement
		{
			public DataType TypeOfData { get; set; }
			public IExpression[] Data { get; set; }

			public enum DataType
			{
				__UNKNOWN__,

				Byte,
				Word,
				DWord,
				QWord,
				Single,
				Double,
				Real,
			}

			public static bool TryParseDataType(string str, out DataType tp)
			{
				switch (str.ToLower())
				{
					case "db":
						tp = DataType.Byte;
						return true;
					case "ds":
						tp = DataType.Word;
						return true;
					case "di":
						tp = DataType.DWord;
						return true;
					case "dl":
						tp = DataType.QWord;
						return true;
					case "df":
						tp = DataType.Single;
						return true;
					case "dd":
						tp = DataType.Double;
						return true;
					case "de":
						tp = DataType.Real;
						return true;
					default:
						tp = DataType.__UNKNOWN__;
						return false;
				}
			}

			public override string ToCode()
			{
				var sb = new StringBuilder(Data.Length * 4);
				switch (TypeOfData)
				{
					case DataType.Byte:
						sb.Append("db");
						break;
					case DataType.Word:
						sb.Append("ds");
						break;
					case DataType.DWord:
						sb.Append("di");
						break;
					case DataType.QWord:
						sb.Append("dl");
						break;
					case DataType.Single:
						sb.Append("df");
						break;
					case DataType.Double:
						sb.Append("dd");
						break;
					case DataType.Real:
						sb.Append("de");
						break;
					case DataType.__UNKNOWN__:
						sb.Append("<UNKNOWN>");
						break;
					default:
						throw new NotSupportedException();
				}

				for (int i = 0; i < Data.Length; i++)
				{
					if (i > 0)
						sb.Append(',');
					sb.Append(' ');
					sb.Append(Data[i].ToString());
				}
				return sb.ToString();
			}

			public override void Accept(StatementVisitor vis) { vis.Visit(this); }
			public override R Accept<R>(StatementVisitor<R> vis) { return vis.Visit(this); }
		}

		public sealed class AlignStatement : AbstractStatement
		{
			public IExpression ValueExpression { get; set; }

			public override string ToCode()
			{
				if (ValueExpression == null)
					return "align <NULL>";
				var ie = ValueExpression as IdentifierExpression;
				if (ie != null && ie.Value.Equals(2))
					return "even";
				else
					return "align " + ValueExpression.ToString();
			}

			public override void Accept(StatementVisitor vis) { vis.Visit(this); }
			public override R Accept<R>(StatementVisitor<R> vis) { return vis.Visit(this); }
		}
	}
}

