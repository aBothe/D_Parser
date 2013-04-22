using System.Collections.Generic;
using D_Parser.Dom.Statements;

namespace D_Parser.Dom
{
	public class ModuleStatement:AbstractStatement, StaticStatement
	{
		public ITypeDeclaration ModuleName;

		public override string ToCode()
		{
			return "module "+ModuleName==null?"": ModuleName.ToString();
		}

		public override void Accept(StatementVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.Visit(this);
		}

		public DAttribute[] Attributes
		{
			get { return null; } set{}
		}
	}

	public class ImportStatement:AbstractStatement, IDeclarationContainingStatement, StaticStatement
	{
		public DAttribute[] Attributes { get; set; }
		public bool IsStatic;
		public bool IsPublic;

		public class Import
		{
			/// <summary>
			/// import io=std.stdio;
			/// </summary>
			public IdentifierDeclaration ModuleAlias;
			public ITypeDeclaration ModuleIdentifier;

			public override string ToString()
			{
				var r= string.IsNullOrEmpty(ModuleAlias) ? "":(ModuleAlias.Id+" = ");

				if (ModuleIdentifier != null)
					r += ModuleIdentifier.ToString();

				return r;
			}
		}

		public class ImportBindings
		{
			public Import Module;

			/// <summary>
			/// Keys: symbol alias
			/// Values: symbol
			/// 
			/// If value empty: Key is imported symbol
			/// </summary>
			public List<KeyValuePair<IdentifierDeclaration, IdentifierDeclaration>> SelectedSymbols
				= new List<KeyValuePair<IdentifierDeclaration, IdentifierDeclaration>>();

			public override string ToString()
			{
				var r = Module==null?"":Module.ToString();

				r += " : ";

				if(SelectedSymbols!=null)
					foreach (var kv in SelectedSymbols)
					{
						r += kv.Key;

						if (!string.IsNullOrEmpty(kv.Value))
							r += "="+kv.Value;

						r += ",";
					}

				return r.TrimEnd(',');
			}
		}

		public List<Import> Imports = new List<Import>();
		public ImportBindings ImportBinding;

		public override string ToCode()
		{
			var ret = (IsPublic?"public ":"")+ (IsStatic?"static ":"") + "import ";

			foreach (var imp in Imports)
			{
				ret += imp.ToString()+",";
			}

			if (ImportBinding == null)
				ret = ret.TrimEnd(',');
			else
				ret += ImportBinding.ToString();

			return ret;
		}

		public override void Accept(StatementVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(StatementVisitor<R> vis)
		{
			return vis.Visit(this);
		}

		#region Pseudo alias variable generation
		/// <summary>
		/// These aliases are used for better handling of aliased modules imports and/or selective imports
		/// </summary>
		public List<DVariable> PseudoAliases = new List<DVariable>();

		public void CreatePseudoAliases(IBlockNode Parent)
		{
			PseudoAliases.Clear();

			foreach (var imp in Imports)
				if (imp.ModuleAlias!=null)
					PseudoAliases.Add(new ImportSymbolAlias(this, imp, Parent));

			if (ImportBinding != null)
			{
				/*
				 * import cv=std.conv : Convert = to;
				 * 
				 * cv can be still used as an alias for std.conv,
				 * whereas Convert is a direct alias for std.conv.to
				 */
				if(ImportBinding.Module.ModuleAlias!=null)
					PseudoAliases.Add(new ImportSymbolAlias(this, ImportBinding.Module, Parent));

				foreach (var bind in ImportBinding.SelectedSymbols)
				{
					var impType = bind.Value ?? bind.Key;
					PseudoAliases.Add(new ImportSymbolAlias(Parent)
					{
						IsAlias = true,
						IsModuleAlias = false,
						OriginalImportStatement = this,
						Name = bind.Key.Id,
						Location = bind.Key.Location,
						NameLocation = bind.Key.Location,
						Type = new IdentifierDeclaration(impType.Id)
						{
							Location = impType.Location,
							EndLocation = impType.EndLocation,
							InnerDeclaration = ImportBinding.Module.ModuleIdentifier
						}
					});
				}
			}
		}

		/// <summary>
		/// Returns import pseudo-alias variables
		/// </summary>
		public INode[] Declarations
		{
			get { return PseudoAliases.Count == 0 ? null : PseudoAliases.ToArray(); }
		}
		#endregion
	}

	/// <summary>
	/// import io = std.stdio;
	/// </summary>
	public class ImportSymbolAlias : DVariable
	{
		public bool IsModuleAlias;
		public ImportStatement OriginalImportStatement;

		public ImportSymbolAlias(IBlockNode parentNode)
		{
			Parent = parentNode;
		}

		public ImportSymbolAlias(ImportStatement impStmt,ImportStatement.Import imp, IBlockNode parentNode) : this(parentNode)
		{
			OriginalImportStatement = impStmt;

			IsModuleAlias = true;
			Name = imp.ModuleAlias;
			Type = imp.ModuleIdentifier;
			IsAlias = true;
		}

		public ImportSymbolAlias()	{}

		public override string ToString()
		{
			return OriginalImportStatement.ToString();
		}
	}
}
