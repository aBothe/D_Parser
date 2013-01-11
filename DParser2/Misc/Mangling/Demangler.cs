using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Resolver;

namespace D_Parser.Misc.Mangling
{
	// For a spec, see http://dlang.org/abi.html
	
	/// <summary>
	/// Description of Demangler.
	/// </summary>
	public class Demangler
	{
		StringReader r;
		StringBuilder sb = new StringBuilder();
		
		public static AbstractType Demangle(string mangledString, out ITypeDeclaration qualifier)
		{
			if(string.IsNullOrEmpty(mangledString))
				throw new ArgumentException("input string must not be null or empty!");
			
			if(!mangledString.StartsWith("_D"))
				throw new ArgumentException("_D expected to be in front of the mangled string");
			
			var dmng = new Demangler(mangledString);
			
			return dmng.MangledName(out qualifier);
		}
		
		Demangler(string s)
		{
			r = new StringReader(s);
		}
		
		AbstractType MangledName(out ITypeDeclaration td)
		{
			r.Read(); // Skip _
			r.Read(); // SKip D
			
			td = QualifiedName();
			
			var t = Type();
			
			if(t is DSymbol)
			{
				var ds = t as DSymbol;
				if(ds.Definition != null && td != null)
				{
					if(td is IdentifierDeclaration)
						ds.Definition.Name = (td as IdentifierDeclaration).Id;
					else if(td is TemplateInstanceExpression)
						ds.Definition.Name = (td as TemplateInstanceExpression).TemplateIdentifier.Id;
				}
			}
			return t;
		}
		
		ITypeDeclaration QualifiedName()
		{
			ITypeDeclaration td = null;
			int n;
			
			while(PeekIsDecNumber)
			{
				// Read number of either the first LName or TemplateInstanceName
				n = (int)Number();
				sb.Clear();
				if((char)r.Peek() == '_')
				{
					r.Read();
					sb.Append('_');
					if((char)r.Peek() == '_')
					{
						r.Read();
						sb.Append('_');
						if((char)r.Peek() == 'T')
						{
							r.Read();
							// We've got to handle a Template instance:
							// Number __T LName TemplateArgs Z
							var tpi = new TemplateInstanceExpression();
							tpi.TemplateIdentifier = new IdentifierDeclaration(LName());
							
							tpi.InnerDeclaration = td;
							td = tpi;
							
							var xx = new List<IExpression>();
							while(r.Peek() != -1)
							{
								var arg = TemplateArg();
								if(arg == null)
									break;
								xx.Add(arg);
							}
							tpi.Arguments = xx.ToArray();
							continue;
						}
					}
				}
				
				// Just an LName
				if(n > sb.Length)
					sb.Append(LName(n-sb.Length));
				
				var ttd = new IdentifierDeclaration(sb.ToString());
				ttd.InnerDeclaration = td;
				td = ttd;
			}
			
			return td;
		}
		
		IExpression TemplateArg()
		{
			switch((char)r.Read())
			{
				case 'T':
					return new TypeDeclarationExpression(Type().TypeDeclarationOf);
				case 'V':
					var t = Type(); // Where should the explicit type be used when there's already a value?
					return Value();
				case 'S':
					return new IdentifierExpression(LName(),LiteralFormat.StringLiteral);
			}
			return null;
		}
		
		#region Type
		AbstractType Type(char type = '\0')
		{
			if(type == '\0')
				type = (char)r.Read();
			switch(type)
			{
				case 'O':
					var t = Type();
					t.Modifier = DTokens.Shared;
					return t;
				case 'x':
					t = Type();
					t.Modifier = DTokens.Const;
					return t;
				case 'y':
					t = Type();
					t.Modifier = DTokens.Immutable;
					return t;
				case 'N':
					switch(r.Read())
					{
						case 'g':
							t = Type();
							t.Modifier = DTokens.InOut;
							return t;
						case 'e': // TypeNewArray ?
							Type();
							return null;
					}
					break;
				case 'A':
					return new ArrayType(Type(), null);
				case 'G':
					var len = (int)Number();
					return new ArrayType(Type(), len, null);
				case 'H':
					var keyType = Type();
					t = Type();
					return new AssocArrayType(t, keyType, null);
				case 'P':
					return new PointerType(Type(), null);
				case 'F':
				case 'U':
				case 'W':
				case 'V':
				case 'R':
					AbstractType ret;
					List<DAttribute> attrs;
					Dictionary<INode,AbstractType> pars;
					Function(out ret, out attrs, out pars, type);
					
					var dm = new DMethod(){ Parameters = pars.Keys.ToList(), Attributes = attrs };
					return new MemberSymbol(dm, ret, null);
				case 'I':
					return new MemberSymbol(null,null, QualifiedName());
				case 'C':
					return new ClassType(null, QualifiedName(), null);
				case 'S':
					return new StructType(null, QualifiedName());
				case 'E':
					return new EnumType(null, null, QualifiedName());
				case 'T':
					return new AliasedType(null, null, QualifiedName());
				case 'D':
					Function(out ret, out attrs, out pars, type);
					var dgArgs = new List<AbstractType>();
					
					foreach(var kv in pars)
						dgArgs.Add(new MemberSymbol(kv.Key as DNode, kv.Value, null));
					
					return new DelegateType(ret,new DelegateDeclaration{ Parameters = pars.Keys.ToList() }, dgArgs);
				case 'v': return new PrimitiveType(DTokens.Void);
				case 'g': return new PrimitiveType(DTokens.Byte);
				case 'h': return new PrimitiveType(DTokens.Ubyte);
				case 's': return new PrimitiveType(DTokens.Short);
				case 't': return new PrimitiveType(DTokens.Ushort);
				case 'i': return new PrimitiveType(DTokens.Int);
				case 'k': return new PrimitiveType(DTokens.Uint);
				case 'l': return new PrimitiveType(DTokens.Long);
				case 'm': return new PrimitiveType(DTokens.Ulong);
				case 'f': return new PrimitiveType(DTokens.Float);
				case 'd': return new PrimitiveType(DTokens.Double);
				case 'e': return new PrimitiveType(DTokens.Real);
				case 'o': return new PrimitiveType(DTokens.Ifloat);
				case 'p': return new PrimitiveType(DTokens.Idouble);
				case 'j': return new PrimitiveType(DTokens.Ireal);
				case 'q': return new PrimitiveType(DTokens.Cfloat);
				case 'r': return new PrimitiveType(DTokens.Cdouble);
				case 'c': return new PrimitiveType(DTokens.Creal);
				case 'b': return new PrimitiveType(DTokens.Bool);
				case 'a': return new PrimitiveType(DTokens.Char);
				case 'u': return new PrimitiveType(DTokens.Wchar);
				case 'w': return new PrimitiveType(DTokens.Dchar);
				case 'n': return null;
				
				case 'B':
					len = (int)Number();
					var items = new List<AbstractType>();
					var c = (char)r.Read();
					for(;len > 0; len--)
					{
						AbstractType item;
						var unused = Argument(ref c, out item);
						items.Add(item);
					}
					
					return new TypeTuple(null, items);
			}
			
			return null;
		}
		
		void Function(out AbstractType returnType,
		              out List<DAttribute> attributes,
		              out Dictionary<INode,AbstractType> parameters,
		              char callConvention = 'F')
		{
			attributes = new List<DAttribute>();

			// Create artificial extern attribute
			var conv = "D";
			switch(callConvention)
			{
				case 'U':
					conv = "C";
					break;
				case 'W':
					conv = "Windows";
					break;
				case 'V':
					conv = "Pascal";
					break;
				case 'R':
					conv = "C++";
					break;
			}
			attributes.Add(new Modifier(DTokens.Extern,conv));
			
			var nextChar = '\0';
			do
			{
				nextChar = (char)r.Read();
			
				if(nextChar == 'N')
				{
					switch(r.Peek())
					{
						case 'a':
							r.Read();
							attributes.Add(new Modifier(DTokens.Pure));
							continue;
						case 'b':
							r.Read();
							attributes.Add(new Modifier(DTokens.Nothrow));
							continue;
						case 'c':
							r.Read();
							attributes.Add(new Modifier(DTokens.Ref));
							continue;
						case 'd':
							r.Read();
							attributes.Add(new BuiltInAtAttribute(BuiltInAtAttribute.BuiltInAttributes.Property));
							continue;
						case 'e':
							r.Read();
							attributes.Add(new BuiltInAtAttribute(BuiltInAtAttribute.BuiltInAttributes.Trusted));
							continue;
						case 'f':
							r.Read();
							attributes.Add(new BuiltInAtAttribute(BuiltInAtAttribute.BuiltInAttributes.Safe));
							continue;
					}
				}
			}
			while(false);
			
			parameters = Arguments(ref nextChar);
			
			if(nextChar == 'X') // variadic T t...) style
			{
				var lastParam = parameters.Keys.Last();
				lastParam.Type = new VarArgDecl(lastParam.Type);
			}
			else if(nextChar == 'Y') // variadic T t,...) style
				parameters.Add(new DVariable{ Type = new VarArgDecl() }, null);
			else if(nextChar != 'Z')
				throw new ArgumentException("Expected 'X','Y' or 'Z' at the end of a function type.");
			
			returnType = Type();
		}
		
		Dictionary<INode,AbstractType> Arguments(ref char c)
		{
			var d = new Dictionary<INode,AbstractType>();
			while(c != 'X' && c != 'Y' && c != 'Z'){
				
				AbstractType parType;
				var par = Argument(ref c, out parType);
				d[par] = parType;
				
				c = (char)r.Read();
			}
			return d;
		}
		
		DVariable Argument(ref char c, out AbstractType parType)
		{
			bool scoped;
			if(scoped = (c == 'M'))
				c = (char)r.Read(); //TODO: Handle scoped
			
			var par = new DVariable{ Attributes = new List<DAttribute>() };
			if(c == 'J' || c == 'K' ||c == 'L')
			{
				if(c == 'J')
					par.Attributes.Add(new Modifier(DTokens.Out));
				else if(c == 'K')
					par.Attributes.Add(new Modifier(DTokens.Ref));
				else if(c == 'L')
					par.Attributes.Add(new Modifier(DTokens.Lazy));
				c = (char)r.Read();
			}
			
			parType = Type(c);
			par.Type = parType.TypeDeclarationOf;
			return par;
		}
		
		#endregion
		
		#region Value
		IExpression Value()
		{
			char p = (char)r.Peek();
			
			switch(p)
			{
				case 'n':
					r.Read();
					return new TokenExpression(DTokens.Null);
				case 'N':
					r.Read();
					return new IdentifierExpression(-Number(), LiteralFormat.Scalar, LiteralSubformat.Integer);
				case 'i':
					r.Read();
					return new IdentifierExpression(-Number(), LiteralFormat.Scalar, LiteralSubformat.Integer | LiteralSubformat.Imaginary);
				case 'e': // HexFloat
					r.Read();
					return HexFloat();
				case 'c': // Complex
					r.Read();
					var re = HexFloat();
					r.Read(); // Skip further c
					var im = HexFloat();
					//TODO
					return re;
				case 'H':
				case 'A':
				case 'S':
					r.Read();
					var n = (int)Number();
					var xx = new List<IExpression>();
					for(int i = n; i > 0; i--)
						xx.Add(Value());
					
					if(p == 'S')
					{
						var inits = new List<StructMemberInitializer>(xx.Count);
						
						for(int i = n-1; i >= 0; i--)
							inits.Add(new StructMemberInitializer{Value = xx[i]});
						
						return new StructInitializer{MemberInitializers = inits.ToArray()};
					}
					
					if(p == 'H' || PeekIsValue) // We've got an AA
					{
						for(int i = n; i > 0; i--)
							xx.Add(Value());
						
						var kv = new List<KeyValuePair<IExpression,IExpression>>(n);
						
						for(int i = (n*2) - 1; i > 0; i-=2)
							kv.Add(new KeyValuePair<IExpression,IExpression>(xx[i-1],xx[i]));
						
						return new AssocArrayExpression{ Elements = kv };
					}
					return new ArrayLiteralExpression(xx);
				case 'a':
				case 'w':
				case 'd':
					r.Read();
					var len = (int)Number();
					sb.Clear();
					
					for(;len > 0; len--)
						sb.Append((char)(Lexer.GetHexNumber((char)r.Read()) << 4 + Lexer.GetHexNumber((char)r.Read())));
					
					return new IdentifierExpression(sb.ToString(), 
					                                LiteralFormat.StringLiteral, p == 'a' ? 
					                                	LiteralSubformat.Utf8 : (p == 'w' ? 
					                                    LiteralSubformat.Utf16 : 
					                                                         LiteralSubformat.Utf32));
			}
			
			if(Lexer.IsLegalDigit(p, 10))
				return new IdentifierExpression(Number(), LiteralFormat.Scalar, LiteralSubformat.Integer);
			
			return null;
		}
		
		bool PeekIsValue
		{
			get{
				var p = (char)r.Peek();
				
				return p == 'n' || 
					p=='N' || p == 'i' ||
					p=='e' || p=='c' || 
					p=='a' || p=='w' || p=='d' || 
					p=='A' || p=='S' || PeekIsDecNumber;
			}
		}
		
		IExpression HexFloat()
		{
			bool neg = false;
			sb.Clear();
			if(r.Peek() == 'N')
			{
				r.Read();
				if(r.Peek() == 'A')
				{
					sb.Append('A');
					r.Read();
					if(r.Peek() == 'N')
					{
						r.Read();
						return new PostfixExpression_Access{ AccessExpression = new IdentifierExpression("nan"), PostfixForeExpression = new TokenExpression(DTokens.Float) };
					}
				}
				neg = true;
			}
			
			if(r.Peek() == 'I')
			{
				r.Read(); // Skip I
				r.Read(); // Skip N
				r.Read(); // Skip F
				var inf = new PostfixExpression_Access{ AccessExpression = new IdentifierExpression("infinity"), PostfixForeExpression = new TokenExpression(DTokens.Float) };
				
				if(neg)
					return new UnaryExpression_Sub{ UnaryExpression = inf };
				return inf;
			}
			
			var n = HexDigits(false);
			
			if(neg)
				n *= -1;
			
			if(r.Peek() == 'P')
			{
				r.Read();
				var exp = Exponent();
				n *= Math.Pow(10, exp);
			}
			
			return new IdentifierExpression(n, LiteralFormat.Scalar | ((Math.Truncate(n) == n) ? 0 : LiteralFormat.FloatingPoint), LiteralSubformat.Double);
		}
		
		string LName(int len = -1)
		{
			if(len < 0)
				len = (int)Number();
			
			if(len == 0)
				return string.Empty;
			
			var chs = new char[len];
			for(int i = 0; i < len; i++)
				chs[i] = (char)r.Read();
			
			return new String(chs);
		}
		
		bool PeekIsDecNumber
		{
			get{
				var d = (char)r.Peek();
				return Lexer.IsLegalDigit(d,10) && d != '_';
			}
		}
		
		double Exponent()
		{
			if((char)r.Peek() == 'N')
			{
				r.Read();
				return -Number();
			}
			return Number();
		}
		
		double HexDigits(bool clearSb = true)
		{
			if(clearSb)
				sb.Clear();
			while(Lexer.IsHex((char)r.Peek()))
				sb.Append((char)r.Read());
			
			return Lexer.ParseFloatValue(sb.ToString(), 16);
		}
		
		double Number(bool clearSb = true)
		{
			if(clearSb)
				sb.Clear();
			while(PeekIsDecNumber)
				sb.Append((char)r.Read());
			
			return Lexer.ParseFloatValue(sb.ToString(), 10);
		}
		#endregion
	}
}
