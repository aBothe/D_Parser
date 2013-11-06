using System.Collections.Generic;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;

namespace D_Parser.Dom
{
    public abstract class DNode : AbstractNode
	{
		#region Properties
		public TemplateParameter[] TemplateParameters; // Functions, Templates
		public IExpression TemplateConstraint;
		public List<DAttribute> Attributes;

		public IEnumerable<TemplateParameter.Node> TemplateParameterNodes
		{
			get
			{
				if (TemplateParameters != null)
					foreach (var p in TemplateParameters)
						if (p != null)
							yield return p.Representation;
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
		
		public bool ContainsTemplateParameter(TemplateParameter p)
		{
			if(TemplateParameters != null)
				for(int i = 0; i < TemplateParameters.Length; i++)
					if(TemplateParameters[i] == p)
						return true;
			return false;
		}

		public bool TryGetTemplateParameter(int nameHash, out TemplateParameter p)
		{
			if (TemplateParameters != null)
				for (int i = 0; i < TemplateParameters.Length; i++)
					if (TemplateParameters[i].NameHash == nameHash)
					{
						p = TemplateParameters[i];
						return true;
					}

			p = null;
			return false;
		}
		
		public bool ContainsAttribute(DAttribute attr)
		{
			if(attr is Modifier)
				return ContainsAttribute((attr as Modifier).Token);
			else if(attr is BuiltInAtAttribute)
				return ContainsPropertyAttribute((attr as BuiltInAtAttribute).Kind);
			else if(attr is UserDeclarationAttribute)
				return ContainsPropertyAttribute((attr as UserDeclarationAttribute).AttributeExpression);
			return false;
		}

        public bool ContainsAttribute(params byte[] Token)
        {
            return Modifier.ContainsAttribute(Attributes, Token);
        }

		public bool ContainsPropertyAttribute(BuiltInAtAttribute.BuiltInAttributes kind)
		{
			if(Attributes!=null)
				foreach (var attr in Attributes)
				{
					var mod = attr as BuiltInAtAttribute;
					if (mod!=null && mod.Kind == kind)
						return true;
				}
			return false;
		}
		
		public bool ContainsPropertyAttribute(params IExpression[] userDefinedAttributeExpression)
		{
			if(Attributes != null && userDefinedAttributeExpression != null)
			{
				var h = new List<ulong>(userDefinedAttributeExpression.Length);
				for(int i = userDefinedAttributeExpression.Length -1; i >= 0; i--)
					h.Add(userDefinedAttributeExpression[i].GetHash());
				
				foreach(var attr in Attributes)
				if(attr is UserDeclarationAttribute)
				{
					var uda = attr as UserDeclarationAttribute;
					foreach(var x in uda.AttributeExpression){
						if(h.Contains(x.GetHash())) //FIXME: Only compare the raw & unevaluated expressions
							return true;
					}
				}
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
