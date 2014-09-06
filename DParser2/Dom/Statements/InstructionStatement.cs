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

			public enum AT : byte
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
			[AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
			public sealed class FormAttribute : Attribute
			{
				public AT[] Arguments { get; private set; }

				public FormAttribute(params AT[] args)
				{
					this.Arguments = args;
				}
			}

			public enum IS : byte
			{
				X86,
				FPU,
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
				cmpxchg16b, // NOTE: Not listed in spec.
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
				popcnt,
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
				prefetcht1,
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
				punpckhwd, // NOTE: Mispelled in documentation.
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
				syscall,
				[Incomplete]
				sysenter,
				[Incomplete]
				sysexit,
				[Incomplete]
				sysret,
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
				pfrcpit2,
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
				[Invalid32Bit]
				xsave64,
				[Incomplete]
				xsaveopt,
				[Incomplete]
				[Invalid32Bit]
				xsaveopt64,
				[Incomplete]
				xrstor,
				[Incomplete]
				[Invalid32Bit]
				xrstor64,
				[Incomplete]
				xsetbv,
				[Incomplete]
				xgetbv,
				[Incomplete]
				vldmxcsr,
				[Incomplete]
				vstmxcsr,
				[Incomplete]
				vaddpd,
				[Incomplete]
				vaddps,
				[Incomplete]
				vaddsd,
				[Incomplete]
				vaddss,
				[Incomplete]
				vaddsubpd,
				[Incomplete]
				vaddsubps,
				[Incomplete]
				vandnpd,
				[Incomplete]
				vandnps,
				[Incomplete]
				vandpd,
				[Incomplete]
				vandps,
				[Incomplete]
				vblendpd,
				[Incomplete]
				vblendps,
				[Incomplete]
				vblendvpd,
				[Incomplete]
				vblendvps,
				[Incomplete]
				vbroadcastf128,
				[Incomplete]
				vbroadcastsd,
				[Incomplete]
				vbroadcastss,
				[Incomplete]
				vcmppd,
				[Incomplete]
				vcmpps,
				[Incomplete]
				vcmpsd,
				[Incomplete]
				vcmpss,
				[Incomplete]
				vcomisd,
				[Incomplete]
				vcomiss,
				[Incomplete]
				vcvtdq2pd,
				[Incomplete]
				vcvtdq2ps,
				[Incomplete]
				vcvtpd2dq,
				[Incomplete]
				vcvtpd2ps,
				[Incomplete]
				vcvtps2dq,
				[Incomplete]
				vcvtps2pd,
				[Incomplete]
				vcvtsd2si,
				[Incomplete]
				vcvtsd2ss,
				[Incomplete]
				vcvtsi2sd,
				[Incomplete]
				vcvtsi2ss,
				[Incomplete]
				vcvtss2si,
				[Incomplete]
				vcvttpd2dq,
				[Incomplete]
				vcvttps2dq,
				[Incomplete]
				vcvttsd2si,
				[Incomplete]
				vcvttss2si,
				[Incomplete]
				vdppd,
				[Incomplete]
				vdpps,
				[Incomplete]
				vextractf128,
				[Incomplete]
				vextractps,
				[Incomplete]
				vhaddpd,
				[Incomplete]
				vhaddps,
				[Incomplete]
				vinsertf128,
				[Incomplete]
				vinsertps,
				[Incomplete]
				vlddqu,
				[Incomplete]
				vmaskmovdqu,
				[Incomplete]
				vmaskmovpd,
				[Incomplete]
				vmaskmovps,
				[Incomplete]
				vmaxpd,
				[Incomplete]
				vmaxps,
				[Incomplete]
				vmaxsd,
				[Incomplete]
				vmaxss,
				[Incomplete]
				vminpd,
				[Incomplete]
				vminps,
				[Incomplete]
				vminsd,
				[Incomplete]
				vminss,
				[Incomplete]
				vmovapd,
				[Incomplete]
				vmovaps,
				[Incomplete]
				vmovd,
				[Incomplete]
				vmovddup,
				[Incomplete]
				vmovdqa,
				[Incomplete]
				vmovdqu,
				[Incomplete]
				vmovhlps,
				[Incomplete]
				vmovhpd,
				[Incomplete]
				vmovhps,
				[Incomplete]
				vmovlhps,
				[Incomplete]
				vmovlpd,
				[Incomplete]
				vmovlps,
				[Incomplete]
				vmovmskpd,
				[Incomplete]
				vmovmskps,
				[Incomplete]
				vmovntdq,
				[Incomplete]
				vmovntdqa,
				[Incomplete]
				vmovntpd,
				[Incomplete]
				vmovntps,
				[Incomplete]
				vmovq,
				[Incomplete]
				vmovsd,
				[Incomplete]
				vmovshdup,
				[Incomplete]
				vmovsldup,
				[Incomplete]
				vmovss,
				[Incomplete]
				vmovupd,
				[Incomplete]
				vmovups,
				[Incomplete]
				vmpsadbw,
				[Incomplete]
				vorpd,
				[Incomplete]
				vorps,
				[Incomplete]
				vpabsb,
				[Incomplete]
				vpabsd,
				[Incomplete]
				vpabsw,
				[Incomplete]
				vpackssdw,
				[Incomplete]
				vpacksswb,
				[Incomplete]
				vpackusdw,
				[Incomplete]
				vpackuswb,
				[Incomplete]
				vpaddb,
				[Incomplete]
				vpaddd,
				[Incomplete]
				vpaddq,
				[Incomplete]
				vpaddsb,
				[Incomplete]
				vpaddsw,
				[Incomplete]
				vpaddusb,
				[Incomplete]
				vpaddusw,
				[Incomplete]
				vpaddw,
				[Incomplete]
				vpalignr,
				[Incomplete]
				vpand,
				[Incomplete]
				vpandn,
				[Incomplete]
				vpavgb,
				[Incomplete]
				vpavgw,
				[Incomplete]
				vpblendvb,
				[Incomplete]
				vpblendw,
				[Incomplete]
				vpclmulqdq,
				[Incomplete]
				vpcmpeqb,
				[Incomplete]
				vpcmpeqd,
				[Incomplete]
				vpcmpeqq,
				[Incomplete]
				vpcmpeqw,
				[Incomplete]
				vpcmpestri,
				[Incomplete]
				vpcmpestrm,
				[Incomplete]
				vpcmpgtb,
				[Incomplete]
				vpcmpgtd,
				[Incomplete]
				vpcmpgtq,
				[Incomplete]
				vpcmpgtw,
				[Incomplete]
				vpcmpistri,
				[Incomplete]
				vpcmpistrm,
				[Incomplete]
				vperm2f128,
				[Incomplete]
				vpermilpd,
				[Incomplete]
				vpermilps,
				[Incomplete]
				vpextrb,
				[Incomplete]
				vpextrd,
				[Incomplete]
				vpextrq,
				[Incomplete]
				vpextrw,
				[Incomplete]
				vphaddd,
				[Incomplete]
				vphaddsw,
				[Incomplete]
				vphaddw,
				[Incomplete]
				vphminposuw,
				[Incomplete]
				vphsubd,
				[Incomplete]
				vphsubsw,
				[Incomplete]
				vphsubw,
				[Incomplete]
				vpinsrb,
				[Incomplete]
				vpinsrd,
				[Incomplete]
				vpinsrq,
				[Incomplete]
				vpinsrw,
				[Incomplete]
				vpmaddubsw,
				[Incomplete]
				vpmaddwd,
				[Incomplete]
				vpmaxsb,
				[Incomplete]
				vpmaxsd,
				[Incomplete]
				vpmaxsw,
				[Incomplete]
				vpmaxub,
				[Incomplete]
				vpmaxud,
				[Incomplete]
				vpmaxuw,
				[Incomplete]
				vpminsb,
				[Incomplete]
				vpminsd,
				[Incomplete]
				vpminsw,
				[Incomplete]
				vpminub,
				[Incomplete]
				vpminud,
				[Incomplete]
				vpminuw,
				[Incomplete]
				vpmovmskb,
				[Incomplete]
				vpmovsxbd,
				[Incomplete]
				vpmovsxbq,
				[Incomplete]
				vpmovsxbw,
				[Incomplete]
				vpmovsxdq,
				[Incomplete]
				vpmovsxwd,
				[Incomplete]
				vpmovsxwq,
				[Incomplete]
				vpmovzxbd,
				[Incomplete]
				vpmovzxbq,
				[Incomplete]
				vpmovzxbw,
				[Incomplete]
				vpmovzxdq,
				[Incomplete]
				vpmovzxwd,
				[Incomplete]
				vpmovzxwq,
				[Incomplete]
				vpmuldq,
				[Incomplete]
				vpmulhrsw,
				[Incomplete]
				vpmulhuw,
				[Incomplete]
				vpmulhw,
				[Incomplete]
				vpmulld,
				[Incomplete]
				vpmullw,
				[Incomplete]
				vpmuludq,
				[Incomplete]
				vpor,
				[Incomplete]
				vpsadbw,
				[Incomplete]
				vpshufb,
				[Incomplete]
				vpshufd,
				[Incomplete]
				vpshufhw,
				[Incomplete]
				vpshuflw,
				[Incomplete]
				vpsignb,
				[Incomplete]
				vpsignd,
				[Incomplete]
				vpsignw,
				[Incomplete]
				vpslld,
				[Incomplete]
				vpslldq,
				[Incomplete]
				vpsllq,
				[Incomplete]
				vpsllw,
				[Incomplete]
				vpsrad,
				[Incomplete]
				vpsraw,
				[Incomplete]
				vpsrld,
				[Incomplete]
				vpsrldq,
				[Incomplete]
				vpsrlq,
				[Incomplete]
				vpsrlw,
				[Incomplete]
				vpsubb,
				[Incomplete]
				vpsubd,
				[Incomplete]
				vpsubq,
				[Incomplete]
				vpsubsb,
				[Incomplete]
				vpsubsw,
				[Incomplete]
				vpsubusb,
				[Incomplete]
				vpsubusw,
				[Incomplete]
				vpsubw,
				[Incomplete]
				vptest,
				[Incomplete]
				vpunpckhbw,
				[Incomplete]
				vpunpckhdq,
				[Incomplete]
				vpunpckhqdq,
				[Incomplete]
				vpunpckhwd,
				[Incomplete]
				vpunpcklbw,
				[Incomplete]
				vpunpckldq,
				[Incomplete]
				vpunpcklqdq,
				[Incomplete]
				vpunpcklwd,
				[Incomplete]
				vpxor,
				[Incomplete]
				vrcpps,
				[Incomplete]
				vrcpss,
				[Incomplete]
				vroundpd,
				[Incomplete]
				vroundps,
				[Incomplete]
				vroundsd,
				[Incomplete]
				vroundss,
				[Incomplete]
				vshufpd,
				[Incomplete]
				vshufps,
				[Incomplete]
				vsqrtpd,
				[Incomplete]
				vsqrtps,
				[Incomplete]
				vsqrtsd,
				[Incomplete]
				vsqrtss,
				[Incomplete]
				vsubpd,
				[Incomplete]
				vsubps,
				[Incomplete]
				vsubsd,
				[Incomplete]
				vsubss,
				[Incomplete]
				vucomisd,
				[Incomplete]
				vucomiss,
				[Incomplete]
				vunpckhpd,
				[Incomplete]
				vunpckhps,
				[Incomplete]
				vunpcklpd,
				[Incomplete]
				vunpcklps,
				[Incomplete]
				vxorpd,
				[Incomplete]
				vxorps,
				[Incomplete]
				vzeroall,
				[Incomplete]
				vzeroupper,

				[Incomplete]
				[Invalid32Bit]
				movsq,
				[Incomplete]
				[Invalid32Bit]
				popfq,
				[Incomplete]
				[Invalid32Bit]
				pushfq,

				// SSSE3
				[Incomplete]
				psignb,
				[Incomplete]
				psignw,
				[Incomplete]
				psignd,
				[Incomplete]
				pabsb,
				[Incomplete]
				pabsw,
				[Incomplete]
				pabsd,
				[Incomplete]
				palignr,
				[Incomplete]
				pshufb,
				[Incomplete]
				pmulhrsw,
				[Incomplete]
				pmaddubsw,
				[Incomplete]
				phsubw,
				[Incomplete]
				phsubd,
				[Incomplete]
				phsubsw,
				[Incomplete]
				phaddw,
				[Incomplete]
				phaddd,
				[Incomplete]
				phaddsw,

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
			public struct OpCodeDescriptor
			{
				public readonly string Name;
				public readonly string Description;
				public readonly AT[][] ValidForms;
				public readonly byte[] ValidArgumentCounts;
				public readonly OpCode OpCode;
				public readonly IS InstructionSet;
				public readonly bool Incomplete;
				public readonly bool Is32BitOnly;
				public readonly bool Is64BitOnly;

				public OpCodeDescriptor(OpCode opcode, string name, string description, AT[][] validForms, bool incomplete, IS insSet, bool is32BitOnly, bool is64BitOnly)
				{
					this.OpCode = opcode;
					this.Name = name;
					this.Description = string.Intern(description);
					this.ValidForms = validForms;
					this.ValidArgumentCounts = validForms.Select(f => (byte)f.Length).Distinct().ToArray();
					this.Incomplete = incomplete;
					this.InstructionSet = insSet;
					this.Is32BitOnly = is32BitOnly;
					this.Is64BitOnly = is64BitOnly;
				}
			}

			public static Dictionary<string, OpCodeDescriptor> OpCodeMap { get; private set; }
			public static Dictionary<OpCode, string> OpCodeReverseMap { get; private set; }

			static InstructionStatement()
			{
				OpCodeMap = new Dictionary<string, OpCodeDescriptor>(StringComparer.InvariantCultureIgnoreCase);
				OpCodeReverseMap = new Dictionary<OpCode, string>();

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

							OpCodeMap.Add(opCodeName, new OpCodeDescriptor(curOpCode, opCodeName, opCodeDescription, argumentForms.ToArray(), incomplete, iset, invalid32Bit, invalid64Bit));
							OpCodeReverseMap.Add(curOpCode, opCodeName);
						}
					}
				}
			}
			#endregion

			public static bool TryParseOpCode(string str, out OpCode dst)
			{
				OpCodeDescriptor dsc;
				if (OpCodeMap.TryGetValue(str, out dsc))
				{
					dst = dsc.OpCode;
					return true;
				}
				dst = OpCode.__UNKNOWN__;
				return false;
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

