using System;
using D_Parser.Dom.Expressions;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace D_Parser.Dom.Statements
{
	public partial class AsmStatement
	{
		public sealed class InstructionStatement : AbstractStatement
		{
			public OpCode Operation { get; set; }
			public IExpression[] Arguments { get; set; }

			#region Instruction Annotations

			/// <summary>
			/// Indicates that the op-code is invalid in
			/// 64-bit mode.
			/// </summary>
			[AttributeUsage(AttributeTargets.Field)]
			public sealed class Invalid64BitAttribute : Attribute { }

			/// <summary>
			/// Indicates that the op-code is invalid in
			/// 32-bit mode.
			/// </summary>
			[AttributeUsage(AttributeTargets.Field)]
			public sealed class Invalid32BitAttribute : Attribute { }

			/// <summary>
			/// Indicates that it is valid to use the short
			/// modifier on an argument for this op-code.
			/// </summary>
			[AttributeUsage(AttributeTargets.Field)]
			public sealed class ShortValidAttribute : Attribute { }

			/// <summary>
			/// Indicates that the name of the enum value is
			/// not the actual name of the op-code.
			/// </summary>
			[AttributeUsage(AttributeTargets.Field)]
			public sealed class NameAttribute : Attribute
			{
				public string Name { get; private set; }

				public NameAttribute(string name)
				{
					this.Name = name;
				}
			}

			/// <summary>
			/// Indicates that this op-code is not yet fully
			/// defined, and no errors about the type or number
			/// of it's arguments should be issued. 
			/// </summary>
			[AttributeUsage(AttributeTargets.Field)]
			public sealed class IncompleteAttribute : Attribute { }

			/// <summary>
			/// Represents all valid combinations of arguments
			/// for a single op-code.
			/// </summary>
			public sealed class OpCodeFormatCollection
			{
				public bool Incomplete { get; private set; }
				public byte[] ValidArgumentCounts { get; private set; }

				public OpCodeFormatCollection(bool incomplete, params byte[] argCounts)
				{
					this.Incomplete = incomplete;
					this.ValidArgumentCounts = argCounts;
				}
			}

			public enum AT
			{
				RM8,
				RM16,
				RM32,
				RM64,
				Reg8,
				Reg16,
				Reg32,
				Reg64,
				ST,
				STn,
				DReg,
				CReg,
				TReg,
				MM,
				XMM,
				YMM,
				ZMM,
				Imm8,
				Imm16,
				Imm32,
				Imm64,
			}

			/// <summary>
			/// Indicates a valid combination of arguments
			/// for this op-code.
			/// </summary>
			[AttributeUsage(AttributeTargets.Field)]
			public sealed class FormAttribute : Attribute
			{
				public AT[] Arguments { get; private set; }

				public FormAttribute(params AT[] args)
				{
					this.Arguments = args;
				}
			}

			public enum IS
			{
				X86,
				MMX,
				SSE,
				SSE2,
				SSE3,
				SSSE3,
				SSE41,
				SSE42,
				AVX,
			}

			public sealed class InstructionSetAttribute : Attribute
			{
				public IS Set { get; private set; }

				public InstructionSetAttribute(IS iset)
				{
					this.Set = iset;
				}
			}

			#endregion

			public enum OpCode
			{
				// Analysis disable InconsistentNaming

				[Invalid32Bit]
				[Invalid64Bit]
				__UNKNOWN__,

				[Invalid64Bit]
				[Description("ASCII adjust AL after addition.")]
				aaa,
				[Invalid64Bit]
				[Description("ASCII adjust AX after division.")]
				aad,
				[Invalid64Bit]
				[Description("ASCII adjust AX after multiplication.")]
				aam,
				[Invalid64Bit]
				[Description("ASCII adjust AL after subtraction.")]
				aas,
				[Form(AT.RM8, AT.Imm8)]
				[Form(AT.RM16, AT.Imm16)]
				[Form(AT.RM32, AT.Imm32)]
				[Form(AT.RM64, AT.Imm32)]
				[Form(AT.RM8, AT.Reg8)]
				[Form(AT.RM16, AT.Reg16)]
				[Form(AT.RM32, AT.Reg32)]
				[Form(AT.RM64, AT.Reg64)]
				[Form(AT.Reg8, AT.RM8)]
				[Form(AT.Reg16, AT.RM16)]
				[Form(AT.Reg32, AT.RM32)]
				[Form(AT.Reg64, AT.RM64)]
				[Description("Adds the 2 arguments and adds 1 if the carry flag is set. Literals are sign extended to the operation size.")]
				adc,
				[Incomplete]
				add,
				[Incomplete]
				addpd,
				[Incomplete]
				addps,
				[Incomplete]
				addsd,
				[Incomplete]
				addss,
				[Incomplete]
				and,
				[Incomplete]
				andnpd,
				[Incomplete]
				andnps,
				[Incomplete]
				andpd,
				[Incomplete]
				andps,
				[Incomplete]
				arpl,
				[Incomplete]
				bound,
				[Incomplete]
				bsf,
				[Incomplete]
				bsr,
				[Incomplete]
				bswap,
				[Incomplete]
				bt,
				[Incomplete]
				btc,
				[Incomplete]
				btr,
				[Incomplete]
				bts,
				[Incomplete]
				call,
				[Incomplete]
				cbw,
				[Incomplete]
				cdq,
				[Incomplete]
				clc,
				[Incomplete]
				cld,
				[Incomplete]
				clflush,
				[Incomplete]
				cli,
				[Incomplete]
				clts,
				[Incomplete]
				cmc,
				[Incomplete]
				cmova,
				[Incomplete]
				cmovae,
				[Incomplete]
				cmovb,
				[Incomplete]
				cmovbe,
				[Incomplete]
				cmovc,
				[Incomplete]
				cmove,
				[Incomplete]
				cmovg,
				[Incomplete]
				cmovge,
				[Incomplete]
				cmovl,
				[Incomplete]
				cmovle,
				[Incomplete]
				cmovna,
				[Incomplete]
				cmovnae,
				[Incomplete]
				cmovnb,
				[Incomplete]
				cmovnbe,
				[Incomplete]
				cmovnc,
				[Incomplete]
				cmovne,
				[Incomplete]
				cmovng,
				[Incomplete]
				cmovnge,
				[Incomplete]
				cmovnl,
				[Incomplete]
				cmovnle,
				[Incomplete]
				cmovno,
				[Incomplete]
				cmovnp,
				[Incomplete]
				cmovns,
				[Incomplete]
				cmovnz,
				[Incomplete]
				cmovo,
				[Incomplete]
				cmovp,
				[Incomplete]
				cmovpe,
				[Incomplete]
				cmovpo,
				[Incomplete]
				cmovs,
				[Incomplete]
				cmovz,
				[Incomplete]
				cmp,
				[Incomplete]
				cmppd,
				[Incomplete]
				cmpps,
				[Incomplete]
				cmps,
				[Incomplete]
				cmpsb,
				[Incomplete]
				cmpsd,
				[Incomplete]
				cmpss,
				[Incomplete]
				cmpsw,
				[Incomplete]
				cmpxchg8b, // NOTE: This is mispelled in the spec, which has "cmpxch8b"
				[Incomplete]
				cmpxchg,
				[Incomplete]
				comisd,
				[Incomplete]
				comiss,
				[Incomplete]
				cpuid,
				[Incomplete]
				cvtdq2pd,
				[Incomplete]
				cvtdq2ps,
				[Incomplete]
				cvtpd2dq,
				[Incomplete]
				cvtpd2pi,
				[Incomplete]
				cvtpd2ps,
				[Incomplete]
				cvtpi2pd,
				[Incomplete]
				cvtpi2ps,
				[Incomplete]
				cvtps2dq,
				[Incomplete]
				cvtps2pd,
				[Incomplete]
				cvtps2pi,
				[Incomplete]
				cvtsd2si,
				[Incomplete]
				cvtsd2ss,
				[Incomplete]
				cvtsi2sd,
				[Incomplete]
				cvtsi2ss,
				[Incomplete]
				cvtss2sd,
				[Incomplete]
				cvtss2si,
				[Incomplete]
				cvttpd2dq,
				[Incomplete]
				cvttpd2pi,
				[Incomplete]
				cvttps2dq,
				[Incomplete]
				cvttps2pi,
				[Incomplete]
				cvttsd2si,
				[Incomplete]
				cvttss2si,
				[Incomplete]
				cwd,
				[Incomplete]
				cwde,
				[Incomplete]
				da,
				[Incomplete]
				daa,
				[Incomplete]
				das,
				[Incomplete]
				dec,
				[Incomplete]
				div,
				[Incomplete]
				divpd,
				[Incomplete]
				divps,
				[Incomplete]
				divsd,
				[Incomplete]
				divss,
				[Incomplete]
				emms,
				[Incomplete]
				enter,
				[Incomplete]
				f2xm1,
				[Incomplete]
				fabs,
				[Incomplete]
				fadd,
				[Incomplete]
				faddp,
				[Incomplete]
				fbld,
				[Incomplete]
				fbstp,
				[Incomplete]
				fchs,
				[Incomplete]
				fclex,
				[Incomplete]
				fcmovb,
				[Incomplete]
				fcmovbe,
				[Incomplete]
				fcmove,
				[Incomplete]
				fcmovnb,
				[Incomplete]
				fcmovnbe,
				[Incomplete]
				fcmovne,
				[Incomplete]
				fcmovnu,
				[Incomplete]
				fcmovu,
				[Incomplete]
				fcom,
				[Incomplete]
				fcomi,
				[Incomplete]
				fcomip,
				[Incomplete]
				fcomp,
				[Incomplete]
				fcompp,
				[Incomplete]
				fcos,
				[Incomplete]
				fdecstp,
				[Incomplete]
				fdisi,
				[Incomplete]
				fdiv,
				[Incomplete]
				fdivp,
				[Incomplete]
				fdivr,
				[Incomplete]
				fdivrp,
				[Incomplete]
				feni,
				[Incomplete]
				ffree,
				[Incomplete]
				fiadd,
				[Incomplete]
				ficom,
				[Incomplete]
				ficomp,
				[Incomplete]
				fidiv,
				[Incomplete]
				fidivr,
				[Incomplete]
				fild,
				[Incomplete]
				fimul,
				[Incomplete]
				fincstp,
				[Incomplete]
				finit,
				[Incomplete]
				fist,
				[Incomplete]
				fistp,
				[Incomplete]
				fisub,
				[Incomplete]
				fusubr,
				[Incomplete]
				fld,
				[Incomplete]
				fld1,
				[Incomplete]
				fldcw,
				[Incomplete]
				fldenv,
				[Incomplete]
				fldl2e,
				[Incomplete]
				fldl2t,
				[Incomplete]
				fldlg2,
				[Incomplete]
				fldln2,
				[Incomplete]
				fldpi,
				[Incomplete]
				fldz,
				[Incomplete]
				fmul,
				[Incomplete]
				fmulp,
				[Incomplete]
				fnclex,
				[Incomplete]
				fndisi,
				[Incomplete]
				fneni,
				[Incomplete]
				fninit,
				[Incomplete]
				fnop,
				[Incomplete]
				fnsave,
				[Incomplete]
				fnstcw,
				[Incomplete]
				fnstenv,
				[Incomplete]
				fnstsw,
				[Incomplete]
				fpatan,
				[Incomplete]
				fprem,
				[Incomplete]
				fprem1,
				[Incomplete]
				fptan,
				[Incomplete]
				frndint,
				[Incomplete]
				frstor,
				[Incomplete]
				fsave,
				[Incomplete]
				fscale,
				[Incomplete]
				fsetpm,
				[Incomplete]
				fsin,
				[Incomplete]
				fsincos,
				[Incomplete]
				fsqrt,
				[Incomplete]
				fst,
				[Incomplete]
				fstcw,
				[Incomplete]
				fstenv,
				[Incomplete]
				fstp,
				[Incomplete]
				fstsw,
				[Incomplete]
				fsub,
				[Incomplete]
				fsubp,
				[Incomplete]
				fsubr,
				[Incomplete]
				fsubrp,
				[Incomplete]
				ftst,
				[Incomplete]
				fucom,
				[Incomplete]
				fucomi,
				[Incomplete]
				fucomip,
				[Incomplete]
				fucomp,
				[Incomplete]
				fucompp,
				[Incomplete]
				fwait,
				[Incomplete]
				fxam,
				[Incomplete]
				fxch,
				[Incomplete]
				fxrstor,
				[Incomplete]
				fxsave,
				[Incomplete]
				fxtract,
				[Incomplete]
				fyl2x,
				[Incomplete]
				fyl2xp1,
				[Incomplete]
				hlt,
				[Incomplete]
				idiv,
				[Incomplete]
				imul,
				[Incomplete]
				[Name("in")]
				in_,
				[Incomplete]
				inc,
				[Incomplete]
				ins,
				[Incomplete]
				insb,
				[Incomplete]
				insd,
				[Incomplete]
				insw,
				[Incomplete]
				[Name("int")]
				int_,
				[Incomplete]
				into,
				[Incomplete]
				invd,
				[Incomplete]
				invlpg,
				[Incomplete]
				iret,
				[Incomplete]
				iretd,
				[Incomplete]
				[ShortValid]
				ja,
				[Incomplete]
				[ShortValid]
				jae,
				[Incomplete]
				[ShortValid]
				jb,
				[Incomplete]
				[ShortValid]
				jbe,
				[Incomplete]
				[ShortValid]
				jc,
				[Incomplete]
				[ShortValid]
				jcxz,
				[Incomplete]
				[ShortValid]
				je,
				[Incomplete]
				[ShortValid]
				jecxz,
				[Incomplete]
				[ShortValid]
				jg,
				[Incomplete]
				[ShortValid]
				jge,
				[Incomplete]
				[ShortValid]
				jl,
				[Incomplete]
				[ShortValid]
				jle,
				[Incomplete]
				[ShortValid]
				jmp,
				[Incomplete]
				[ShortValid]
				jna,
				[Incomplete]
				[ShortValid]
				jnae,
				[Incomplete]
				[ShortValid]
				jnb,
				[Incomplete]
				[ShortValid]
				jnbe,
				[Incomplete]
				[ShortValid]
				jnc,
				[Incomplete]
				[ShortValid]
				jne,
				[Incomplete]
				[ShortValid]
				jng,
				[Incomplete]
				[ShortValid]
				jnge,
				[Incomplete]
				[ShortValid]
				jnl,
				[Incomplete]
				[ShortValid]
				jnle,
				[Incomplete]
				[ShortValid]
				jno,
				[Incomplete]
				[ShortValid]
				jnp,
				[Incomplete]
				[ShortValid]
				jns,
				[Incomplete]
				[ShortValid]
				jnz,
				[Incomplete]
				[ShortValid]
				jo,
				[Incomplete]
				[ShortValid]
				jp,
				[Incomplete]
				[ShortValid]
				jpe,
				[Incomplete]
				[ShortValid]
				jpo,
				[Incomplete]
				[ShortValid]
				js,
				[Incomplete]
				[ShortValid]
				jz,
				[Incomplete]
				lahf,
				[Incomplete]
				lar,
				[Incomplete]
				ldmxcsr,
				[Incomplete]
				lds,
				[Incomplete]
				lea,
				[Incomplete]
				leave,
				[Incomplete]
				les,
				[Incomplete]
				lfence,
				[Incomplete]
				lfs,
				[Incomplete]
				lgdt,
				[Incomplete]
				lgs,
				[Incomplete]
				lidt,
				[Incomplete]
				lldt,
				[Incomplete]
				lmsw,
				[Incomplete]
				[Name("lock")]
				lock_,
				[Incomplete]
				lods,
				[Incomplete]
				lodsb,
				[Incomplete]
				lodsd,
				[Incomplete]
				lodsw,
				[Incomplete]
				loop,
				[Incomplete]
				loope,
				[Incomplete]
				loopne,
				[Incomplete]
				loopnz,
				[Incomplete]
				loopz,
				[Incomplete]
				lsl,
				[Incomplete]
				lss,
				[Incomplete]
				ltr,
				[Incomplete]
				maskmovdqu,
				[Incomplete]
				maskmovq,
				[Incomplete]
				maxpd,
				[Incomplete]
				maxps,
				[Incomplete]
				maxsd,
				[Incomplete]
				maxss,
				[Incomplete]
				mfence,
				[Incomplete]
				minpd,
				[Incomplete]
				minps,
				[Incomplete]
				minsd,
				[Incomplete]
				minss,
				[Incomplete]
				mov,
				[Incomplete]
				movapd,
				[Incomplete]
				movaps,
				[Incomplete]
				movd,
				[Incomplete]
				movdq2q,
				[Incomplete]
				movdqa,
				[Incomplete]
				movdqu,
				[Incomplete]
				movhlps,
				[Incomplete]
				movhpd,
				[Incomplete]
				movhps,
				[Incomplete]
				movlhps,
				[Incomplete]
				movlpd,
				[Incomplete]
				movlps,
				[Incomplete]
				movmskpd,
				[Incomplete]
				movmskps,
				[Incomplete]
				movntdq,
				[Incomplete]
				movnti,
				[Incomplete]
				movntpd,
				[Incomplete]
				movntps,
				[Incomplete]
				movntq,
				[Incomplete]
				movq,
				[Incomplete]
				movq2dq,
				[Incomplete]
				movs,
				[Incomplete]
				movsb,
				[Incomplete]
				movsd,
				[Incomplete]
				movss,
				[Incomplete]
				movsw,
				[Incomplete]
				movsx,
				[Incomplete]
				movupd,
				[Incomplete]
				movups,
				[Incomplete]
				movzx,
				[Incomplete]
				mul,
				[Incomplete]
				mulpd,
				[Incomplete]
				mulps,
				[Incomplete]
				mulsd,
				[Incomplete]
				mulss,
				[Incomplete]
				neg,
				[Incomplete]
				nop,
				[Incomplete]
				not,
				[Incomplete]
				or,
				[Incomplete]
				orpd,
				[Incomplete]
				orps,
				[Incomplete]
				[Name("out")]
				out_,
				[Incomplete]
				outs,
				[Incomplete]
				outsb,
				[Incomplete]
				outsd,
				[Incomplete]
				outsw,
				[Incomplete]
				packssdw,
				[Incomplete]
				packsswb,
				[Incomplete]
				packuswb,
				[Incomplete]
				paddb,
				[Incomplete]
				paddd,
				[Incomplete]
				paddq,
				[Incomplete]
				paddsb,
				[Incomplete]
				paddsw,
				[Incomplete]
				paddusb,
				[Incomplete]
				paddusw,
				[Incomplete]
				paddw,
				[Incomplete]
				pand,
				[Incomplete]
				pandn,
				[Incomplete]
				pavgb,
				[Incomplete]
				pavgw,
				[Incomplete]
				pcmpeqb,
				[Incomplete]
				pcmpeqd,
				[Incomplete]
				pcmpeqw,
				[Incomplete]
				pcmpgtb,
				[Incomplete]
				pcmpgtd,
				[Incomplete]
				pcmpgtw,
				[Incomplete]
				pextrw,
				[Incomplete]
				pinsrw,
				[Incomplete]
				pmaddwd,
				[Incomplete]
				pmaxsw,
				[Incomplete]
				pmaxub,
				[Incomplete]
				pminsw,
				[Incomplete]
				pminub,
				[Incomplete]
				pmovmskb,
				[Incomplete]
				pmulhuw,
				[Incomplete]
				pmulhw,
				[Incomplete]
				pmullw,
				[Incomplete]
				pmuludq,
				[Incomplete]
				pop,
				[Incomplete]
				popa,
				[Incomplete]
				popad,
				[Incomplete]
				popf,
				[Incomplete]
				[Invalid64Bit]
				popfd,
				[Incomplete]
				por,
				[Incomplete]
				prefetchnta,
				[Incomplete]
				prefetcht0,
				[Incomplete]
				prefetch1,
				[Incomplete]
				prefetcht2,
				[Incomplete]
				psadbw,
				[Incomplete]
				pshufd,
				[Incomplete]
				pshufhw,
				[Incomplete]
				pshuflw,
				[Incomplete]
				pshufw,
				[Incomplete]
				pslld,
				[Incomplete]
				pslldq,
				[Incomplete]
				psllq,
				[Incomplete]
				psllw,
				[Incomplete]
				psrad,
				[Incomplete]
				psraw,
				[Incomplete]
				psrld,
				[Incomplete]
				psrldq,
				[Incomplete]
				psrlq,
				[Incomplete]
				psrlw,
				[Incomplete]
				psubb,
				[Incomplete]
				psubd,
				[Incomplete]
				psubq,
				[Incomplete]
				psubsb,
				[Incomplete]
				psubsw,
				[Incomplete]
				psubusb,
				[Incomplete]
				psubusw,
				[Incomplete]
				psubw,
				[Incomplete]
				punpckhbw,
				[Incomplete]
				punpckhdq,
				[Incomplete]
				punpckhqdq,
				[Incomplete]
				punckhwd,
				[Incomplete]
				punpcklbw,
				[Incomplete]
				punpckldq,
				[Incomplete]
				punpcklqdq,
				[Incomplete]
				punpcklwd,
				[Incomplete]
				push,
				[Incomplete]
				pusha,
				[Incomplete]
				pushad,
				[Incomplete]
				pushf,
				[Incomplete]
				[Invalid64Bit]
				pushfd,
				[Incomplete]
				pxor,
				[Incomplete]
				rcl,
				[Incomplete]
				rcpps,
				[Incomplete]
				rcpss,
				[Incomplete]
				rcr,
				[Incomplete]
				rdmsr,
				[Incomplete]
				rdpmc,
				[Incomplete]
				rdtsc,
				[Incomplete]
				rep,
				[Incomplete]
				repe,
				[Incomplete]
				repne,
				[Incomplete]
				repnz,
				[Incomplete]
				repz,
				[Incomplete]
				ret,
				[Incomplete]
				retf,
				[Incomplete]
				rol,
				[Incomplete]
				ror,
				[Incomplete]
				rsm,
				[Incomplete]
				rsqrtps,
				[Incomplete]
				rsqrtss,
				[Incomplete]
				sahf,
				[Incomplete]
				sal,
				[Incomplete]
				sar,
				[Incomplete]
				sbb,
				[Incomplete]
				scas,
				[Incomplete]
				scasb,
				[Incomplete]
				scasd,
				[Incomplete]
				scasw,
				[Incomplete]
				seta,
				[Incomplete]
				setae,
				[Incomplete]
				setb,
				[Incomplete]
				setbe,
				[Incomplete]
				setc,
				[Incomplete]
				sete,
				[Incomplete]
				setg,
				[Incomplete]
				setge,
				[Incomplete]
				setl,
				[Incomplete]
				setle,
				[Incomplete]
				setna,
				[Incomplete]
				setnae,
				[Incomplete]
				setnb,
				[Incomplete]
				setnbe,
				[Incomplete]
				setnc,
				[Incomplete]
				setne,
				[Incomplete]
				setng,
				[Incomplete]
				setnge,
				[Incomplete]
				setnl,
				[Incomplete]
				setnle,
				[Incomplete]
				setno,
				[Incomplete]
				setnp,
				[Incomplete]
				setns,
				[Incomplete]
				setnz,
				[Incomplete]
				seto,
				[Incomplete]
				setp,
				[Incomplete]
				setpe,
				[Incomplete]
				setpo,
				[Incomplete]
				sets,
				[Incomplete]
				setz,
				[Incomplete]
				sfence,
				[Incomplete]
				sgdt,
				[Incomplete]
				shl,
				[Incomplete]
				shld,
				[Incomplete]
				shr,
				[Incomplete]
				shrd,
				[Incomplete]
				shufpd,
				[Incomplete]
				shufps,
				[Incomplete]
				sidt,
				[Incomplete]
				sldt,
				[Incomplete]
				smsw,
				[Incomplete]
				sqrtpd,
				[Incomplete]
				sqrtps,
				[Incomplete]
				sqrtsd,
				[Incomplete]
				sqrtss,
				[Incomplete]
				stc,
				[Incomplete]
				std,
				[Incomplete]
				sti,
				[Incomplete]
				stmxcsr,
				[Incomplete]
				stos,
				[Incomplete]
				stosb,
				[Incomplete]
				stosd,
				[Incomplete]
				stosw,
				[Incomplete]
				str,
				[Incomplete]
				sub,
				[Incomplete]
				subpd,
				[Incomplete]
				subps,
				[Incomplete]
				subsd,
				[Incomplete]
				subss,
				[Incomplete]
				sysenter,
				[Incomplete]
				sysexit,
				[Incomplete]
				test,
				[Incomplete]
				ucomisd,
				[Incomplete]
				ucomiss,
				[Incomplete]
				ud2,
				[Incomplete]
				unpckhpd,
				[Incomplete]
				unpckhps,
				[Incomplete]
				unpcklpd,
				[Incomplete]
				unpcklps,
				[Incomplete]
				verr,
				[Incomplete]
				verw,
				[Incomplete]
				wait,
				[Incomplete]
				wbinvd,
				[Incomplete]
				wrmsr,
				[Incomplete]
				xadd,
				[Incomplete]
				xchg,
				[Incomplete]
				xlat,
				[Incomplete]
				xlatb,
				[Incomplete]
				xor,
				[Incomplete]
				xorpd,
				[Incomplete]
				xorps,

				// Pentium 4
				[Incomplete]
				addsubpd,
				[Incomplete]
				addsubps,
				[Incomplete]
				fisttp,
				[Incomplete]
				haddpd,
				[Incomplete]
				haddps,
				[Incomplete]
				hsubpd,
				[Incomplete]
				hsubps,
				[Incomplete]
				lddqu,
				[Incomplete]
				monitor,
				[Incomplete]
				movddup,
				[Incomplete]
				movshdup,
				[Incomplete]
				movsldup,
				[Incomplete]
				mwait,

				// AMD
				[Incomplete]
				pavgusb,
				[Incomplete]
				pf2id,
				[Incomplete]
				pfacc,
				[Incomplete]
				pfadd,
				[Incomplete]
				pfcmpeq,
				[Incomplete]
				pfcmpge,
				[Incomplete]
				pfcmpgt,
				[Incomplete]
				pfmax,
				[Incomplete]
				pfmin,
				[Incomplete]
				pfmul,
				[Incomplete]
				pfnacc,
				[Incomplete]
				pfpnacc,
				[Incomplete]
				pfrcp,
				[Incomplete]
				pfrcpit1,
				[Incomplete]
				pfrsqit1,
				[Incomplete]
				pfrsqrt,
				[Incomplete]
				pfsub,
				[Incomplete]
				pfsubr,
				[Incomplete]
				pi2fd,
				[Incomplete]
				pmulhrw,
				[Incomplete]
				pswapd,

				#region Not Listed in Spec

				// AVX
				[Incomplete]
				xsave,
				[Incomplete]
				xrstor,
				[Incomplete]
				xsetbv,
				[Incomplete]
				xgetbv,

				[Incomplete]
				[Invalid32Bit]
				movsq,
				[Incomplete]
				[Invalid32Bit]
				popfq,
				[Incomplete]
				[Invalid32Bit]
				pushfq,

				// SSE 4.1
				[Incomplete]
				mpsadbw,
				[Incomplete]
				phminposuw,
				[Incomplete]
				pmuldq,
				[Incomplete]
				pmulld,
				[Incomplete]
				dpps,
				[Incomplete]
				dppd,
				[Incomplete]
				blendps,
				[Incomplete]
				blendpd,
				[Incomplete]
				blendvps,
				[Incomplete]
				blendvpd,
				[Incomplete]
				pblendvb,
				[Incomplete]
				pblendw,
				[Incomplete]
				pminsb,
				[Incomplete]
				pmaxsb,
				[Incomplete]
				pminuw,
				[Incomplete]
				pmaxuw,
				[Incomplete]
				pminud,
				[Incomplete]
				pmaxud,
				[Incomplete]
				pminsd,
				[Incomplete]
				pmaxsd,
				[Incomplete]
				roundps,
				[Incomplete]
				roundss,
				[Incomplete]
				roundpd,
				[Incomplete]
				roundsd,
				[Incomplete]
				insertps,
				[Incomplete]
				pinsrb,
				[Incomplete]
				pinsrd,
				[Incomplete]
				[Invalid32Bit]
				pinsrq,
				[Incomplete]
				extractps,
				[Incomplete]
				pextrb,
				[Incomplete]
				pextrd,
				[Incomplete]
				[Invalid32Bit]
				pextrq,
				[Incomplete]
				pmovsxbw,
				[Incomplete]
				pmovzxbw,
				[Incomplete]
				pmovsxbd,
				[Incomplete]
				pmovzxbd,
				[Incomplete]
				pmovsxwd,
				[Incomplete]
				pmovzxwd,
				[Incomplete]
				pmovsxbq,
				[Incomplete]
				pmovzxbq,
				[Incomplete]
				pmovsxwq,
				[Incomplete]
				pmovzxwq,
				[Incomplete]
				pmovsxdq,
				[Incomplete]
				pmovzxdq,
				[Incomplete]
				ptest,
				[Incomplete]
				pcmpeqq,
				[Incomplete]
				packusdw,
				[Incomplete]
				movntdqa,

				// SSE 4.2
				[Incomplete]
				[InstructionSet(IS.SSE42)]
				crc32,
				[Incomplete]
				[InstructionSet(IS.SSE42)]
				pcmpestri,
				[Incomplete]
				[InstructionSet(IS.SSE42)]
				pcmpestrm,
				[Incomplete]
				[InstructionSet(IS.SSE42)]
				pcmpistri,
				[Incomplete]
				[InstructionSet(IS.SSE42)]
				pcmpistrm,
				[Incomplete]
				[InstructionSet(IS.SSE42)]
				pcmpgtq,

				#endregion

				// Analysis restore InconsistentNaming
			}

			#region Instruction Tables
			public static Dictionary<string, string> OpCodeCompletionTable { get; private set; }
			public static Dictionary<string, OpCode> OpCodeMap { get; private set; }
			public static Dictionary<OpCode, string> OpCodeReverseMap { get; private set; }
			public static Dictionary<OpCode, OpCodeFormatCollection> OpCodeFormats { get; private set; }
			public static Dictionary<string, IS> OpCodeInstructionSets { get; private set; }

			static InstructionStatement()
			{
				OpCodeCompletionTable = new Dictionary<string, string>();
				OpCodeMap = new Dictionary<string, OpCode>(StringComparer.InvariantCultureIgnoreCase);
				OpCodeReverseMap = new Dictionary<OpCode, string>();
				OpCodeFormats = new Dictionary<OpCode, OpCodeFormatCollection>();
				OpCodeInstructionSets = new Dictionary<string, IS>();


				foreach (var mi in typeof(OpCode).GetMembers())
				{
					if (mi.MemberType == MemberTypes.Field && mi.Name != "value__")
					{
						string opCodeName = mi.Name;
						string opCodeDescription = "";
						bool invalid32Bit = false;
						bool invalid64Bit = false;
						bool incomplete = false;
						IS iset = IS.X86;
						var argumentForms = new List<AT[]>(16);

						foreach (var at in mi.GetCustomAttributes(false))
						{
							if (at is NameAttribute)
								opCodeName = ((NameAttribute)at).Name;
							else if (at is DescriptionAttribute)
								opCodeDescription = ((DescriptionAttribute)at).Description;
							else if (at is InstructionSetAttribute)
								iset = ((InstructionSetAttribute)at).Set;

							invalid32Bit |= at is Invalid32BitAttribute;
							invalid64Bit |= at is Invalid64BitAttribute;
							incomplete |= at is IncompleteAttribute;
						}

						opCodeName = string.Intern(opCodeName);
						if (!invalid32Bit || !invalid64Bit)
						{
							OpCode curOpCode = (OpCode)Enum.Parse(typeof(OpCode), mi.Name);

							OpCodeCompletionTable.Add(opCodeName, opCodeDescription);
							OpCodeMap.Add(opCodeName, curOpCode);
							OpCodeReverseMap.Add(curOpCode, opCodeName);
							OpCodeFormats.Add(curOpCode, new OpCodeFormatCollection(incomplete, argumentForms.Select(f => (byte)f.Length).Distinct().ToArray()));
							OpCodeInstructionSets.Add(opCodeName, iset);
						}
					}
				}
			}
			#endregion

			public static bool TryParseOpCode(string str, out OpCode dst)
			{
				return OpCodeMap.TryGetValue(str, out dst);
			}

			public static string StringForOpCode(OpCode val)
			{
				string ret;
				return OpCodeReverseMap.TryGetValue(val, out ret) ? ret : "<Unkown instruction>";
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
	}
}

