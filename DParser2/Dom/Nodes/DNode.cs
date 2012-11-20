using System.Collections.Generic;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;

namespace D_Parser.Dom
{
    public abstract class DNode : AbstractNode
	{
		#region Properties
		public ITemplateParameter[] TemplateParameters; // Functions, Templates
		public IExpression TemplateConstraint;
		public List<DAttribute> Attributes;

		public IEnumerable<TemplateParameterNode> TemplateParameterNodes
		{
			get
			{
				if (TemplateParameters != null)
					foreach (var p in TemplateParameters)
						yield return new TemplateParameterNode(p) { Parent = this };
			}
		}

		public string AttributeString
		{
			get
			{
				string s = "";
				if(Attributes!=null)
					foreach (var attr in Attributes)
						if (attr != null)
							s += attr.ToString() + " ";
				return s.Trim();
			}
		}

		public bool IsClassMember
		{
			get
			{
				return Parent is DClassLike && ((DClassLike)Parent).ClassType == DTokens.Class;
			}
		}

		public bool IsPublic
		{
			get
			{
				return !ContainsAttribute(DTokens.Private, DTokens.Protected);
			}
		}

		public bool IsStatic
		{
			get
			{
				return ContainsAttribute(DTokens.Static);
			}
		}
		#endregion

		public bool ContainsTemplateParameter(string Name)
		{
			if (TemplateParameters != null)
				foreach (var tp in TemplateParameters)
					if (tp.Name == Name)
						return true;

			return false;
		}

        public bool ContainsAttribute(params int[] Token)
        {
            return Modifier.ContainsAttribute(Attributes, Token);
        }

		public bool ContainsPropertyAttribute(string prop="property")
		{
			if(Attributes!=null)
				foreach (var attr in Attributes)
				{
					var mod = attr as Modifier;
					if (mod!=null && mod.LiteralContent is string && ((string)mod.LiteralContent) == prop)
						return true;
				}
			return false;
		}

        public override void AssignFrom(INode other)
        {
			if (other is DNode)
			{
				TemplateParameters = (other as DNode).TemplateParameters;
				Attributes = (other as DNode).Attributes;
			}
			base.AssignFrom(other);
        }

        /// <summary>
        /// Returns attributes, type and name combined to one string
        /// </summary>
        /// <returns></returns>
        public override string ToString(bool Attributes,bool IncludePath)
        {
			string s = ""; 
				
			if(Attributes)
				s=AttributeString+" ";

			s += base.ToString(Attributes,IncludePath);

            // Template parameters
            if (TemplateParameters!=null && TemplateParameters.Length > 0)
            {
				if (this is DVariable)
					s += '!';

                s += "(";
				foreach (var p in TemplateParameters)
					s += p.ToString() + ",";

                s = s.Trim(',')+ ")";
            }
            
            return s.Trim();
        }
	}
}
