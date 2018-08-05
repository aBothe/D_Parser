using System.Collections.Generic;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using System.Text;

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
				return !ContainsAnyAttribute(DTokens.Private, DTokens.Protected);
			}
		}

		public bool IsStatic
		{
			get
			{
				return ContainsAnyAttribute(DTokens.Static, DTokens.__gshared);
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
				return ContainsAnyAttribute((attr as Modifier).Token);
			else if(attr is BuiltInAtAttribute)
				return ContainsPropertyAttribute((attr as BuiltInAtAttribute).Kind);
			else if(attr is UserDeclarationAttribute)
				return ContainsPropertyAttribute((attr as UserDeclarationAttribute).AttributeExpression);
			return false;
		}

        public bool ContainsAnyAttribute(params byte[] Token)
        {
            return Modifier.ContainsAnyAttributeToken(Attributes, Token);
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
				var hashVisitor = D_Parser.Dom.Visitors.AstElementHashingVisitor.Instance;
				var h = new List<long>(userDefinedAttributeExpression.Length);
				for(int i = userDefinedAttributeExpression.Length -1; i >= 0; i--)
					h.Add(userDefinedAttributeExpression[i].Accept(hashVisitor));
				
				foreach(var attr in Attributes)
				if(attr is UserDeclarationAttribute)
				{
					var uda = attr as UserDeclarationAttribute;
					if(uda.AttributeExpression != null)
						foreach(var x in uda.AttributeExpression){
							if(x != null && h.Contains(x.Accept(hashVisitor))) //FIXME: Only compare the raw & unevaluated expressions
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
				if (TemplateParameters != null)
					foreach (var tp in TemplateParameters)
						tp.Parent = this;
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
			var sb = new StringBuilder ();
				
			if (Attributes) {
				sb.Append (AttributeString);
				if (sb.Length != 0)
					sb.Append (' ');
			}

			sb.Append(base.ToString(Attributes,IncludePath));

            // Template parameters
            if (TemplateParameters!=null && TemplateParameters.Length > 0)
            {
				sb.Append('(');
				foreach (var p in TemplateParameters)
					sb.Append(p.ToString()).Append(", ");
				if (sb [sb.Length - 1] == ' ')
					sb.Length--;
				if (sb [sb.Length - 1] == ',')
					sb.Length--;

				sb.Append(')');
            }

			return sb.ToString ();
        }
	}
}
