using System.Collections.Generic;
using D_Parser.Dom.Statements;
using System.Text;
using System;

namespace D_Parser.Dom
{
	public class ModuleStatement:AbstractStatement, StaticStatement
	{
		public ITypeDeclaration ModuleName;

		public override string ToCode()
		{
			return "module " + (ModuleName==null ? "" : ModuleName.ToString());
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
				var r= ModuleAlias == null ? "":(ModuleAlias.Id+" = ");

				if (ModuleIdentifier != null)
					r += ModuleIdentifier.ToString();

				return r;
			}
		}

		public class ImportBinding
		{
			public IdentifierDeclaration Symbol;
			public IdentifierDeclaration Alias;

			public ImportBinding(IdentifierDeclaration symbol, IdentifierDeclaration alias = null)
			{
				Symbol = symbol;
				Alias = alias;
			}

			public override string ToString ()
			{
				if(Symbol == null)
					return "<empty import binding>";

				if (Alias == null)
					return Symbol.ToString();
				return Alias + " = " + Symbol;
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
			public List<ImportBinding> SelectedSymbols = new List<ImportBinding>();

			public override string ToString()
			{
				var sb = new StringBuilder ();
				if (Module != null)
					sb.Append (Module);

				sb.Append(" : ");

				if (SelectedSymbols != null) {
					foreach (var kv in SelectedSymbols) {
						sb.Append (kv.ToString ()).Append (',');
					}
					sb.Remove (sb.Length - 1, 1);
				}

				return sb.ToString();
			}
		}

		public List<Import> Imports = new List<Import>();
		public ImportBindings ImportBindList;

		public override string ToCode()
		{
			var sb = new StringBuilder ();

			if (IsPublic)
				sb.Append ("public ");
			if (IsStatic)
				sb.Append ("static ");
			sb.Append ("import ");

			foreach (var imp in Imports)
				sb.Append(imp.ToString()).Append(',');

			if (ImportBindList == null)
				sb.Remove (sb.Length - 1, 1);
			else
				sb.Append(ImportBindList.ToString());

			return sb.ToString();
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
					PseudoAliases.Add(new ModuleAliasNode(this, imp, Parent));

			if (ImportBindList != null)
			{
				/*
				 * import cv=std.conv : Convert = to;
				 * 
				 * cv can be still used as an alias for std.conv,
				 * whereas Convert is a direct alias for std.conv.to
				 */
				if(ImportBindList.Module.ModuleAlias!=null)
					PseudoAliases.Add(new ModuleAliasNode(this, ImportBindList.Module, Parent));

				foreach (var bind in ImportBindList.SelectedSymbols)
					PseudoAliases.Add(new ImportSymbolAlias(this, bind, Parent));
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

	public abstract class ImportSymbolNode : DVariable
	{
		public readonly ImportStatement ImportStatement;

		public ImportSymbolNode(ImportStatement impStmt,IBlockNode parentNode)
		{
			IsAlias = true;
			this.ImportStatement = impStmt;
			Parent = parentNode;
		}
	}

	/// <summary>
	/// import io = std.stdio;
	/// </summary>
	public class ModuleAliasNode : ImportSymbolNode
	{
		public readonly ImportStatement.Import Import;

		public ModuleAliasNode(ImportStatement impStmt,ImportStatement.Import imp, IBlockNode parentNode)
			: base(impStmt, parentNode)
		{
			this.Import = imp;

			Name = imp.ModuleAlias.Id;
			Location = NameLocation = imp.ModuleIdentifier.Location;

			Type = imp.ModuleIdentifier;
		}
	}

	/// <summary>
	/// import std.stdio : writeln;
	/// import std.stdio : wr = writeln;
	/// </summary>
	public class ImportSymbolAlias : ImportSymbolNode
	{
		public readonly ImportStatement.ImportBinding ImportBinding;

		public ImportSymbolAlias(ImportStatement impStmt,ImportStatement.ImportBinding imp, IBlockNode parentNode)
			: base(impStmt, parentNode)
		{
			ImportBinding = imp;
			var sym = imp.Symbol;

			Name = (imp.Alias ?? sym).Id;
			NameLocation = (imp.Alias ?? sym).Location;
			Location = imp.Symbol.Location;

			Type = new IdentifierDeclaration (sym.Id) {
				Location = sym.Location,
				EndLocation = sym.EndLocation,
				InnerDeclaration = impStmt.ImportBindList.Module.ModuleIdentifier
			};
		}

		public override string ToString ()
		{
			return string.Format ("[ImportSymbolAlias] {0}", ImportBinding.ToString());
		}
	}
}
