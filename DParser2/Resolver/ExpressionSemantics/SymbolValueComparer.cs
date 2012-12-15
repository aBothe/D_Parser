using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver;

namespace D_Parser.Resolver.ExpressionSemantics
{
	public class SymbolValueComparer
	{
		public static bool IsEqual(IExpression ex, IExpression ex2, AbstractSymbolValueProvider vp)
		{
			var val_x1 = Evaluation.EvaluateValue(ex, vp);
			var val_x2 = Evaluation.EvaluateValue(ex2, vp);

			//TEMPORARILY: Remove the string comparison
			if (val_x1 == null && val_x2 == null)
				return ex.ToString() == ex2.ToString();

			return IsEqual(val_x1, val_x2);
		}

		public static bool IsEqual(ISymbolValue l, ISymbolValue r)
		{
			// If they are integral values or pointers, equality is defined as the bit pattern of the type matches exactly
			if (l is PrimitiveValue && r is PrimitiveValue)
			{
				var pv_l = (PrimitiveValue)l;
				var pv_r = (PrimitiveValue)r;

				return pv_l.Value == pv_r.Value && pv_l.ImaginaryPart == pv_r.ImaginaryPart;
			}
			
			else if(l is AssociativeArrayValue && r is AssociativeArrayValue)
			{
				var aa_l = l as AssociativeArrayValue;
				var aa_r = r as AssociativeArrayValue;
				
				if(aa_l.Elements == null || aa_l.Elements.Count == 0)
					return aa_r.Elements == null || aa_r.Elements.Count == 0;
				else if(aa_r.Elements != null && aa_r.Elements.Count == aa_l.Elements.Count)
				{
					//TODO: Check if each key of aa_l can be found somewhere in aa_r.
					//TODO: If respective keys are equal, check if values are equal
					return true;
				}
			}
			
			else if(l is ArrayValue && r is ArrayValue)
			{
				var av_l = l as ArrayValue;
				var av_r = r as ArrayValue;
				
				if(av_l.IsString == av_r.IsString)
				{
					if(av_l.IsString)
						return av_l.StringValue == av_r.StringValue;
					else
					{
						if(av_l.Elements == null || av_l.Elements.Length == 0)
							return av_r.Elements == null || av_r.Elements.Length == 0;
						else if(av_r.Elements != null && av_r.Elements.Length == av_l.Elements.Length)
						{
							for(int i = av_l.Elements.Length-1; i != -1; i--)
							{
								if(!IsEqual(av_l.Elements[i], av_r.Elements[i]))
									return false;
							}
							return true;
						}
					}
				}
			}
			
			return false;
		}
	}
}
