﻿using System.Collections.Generic;
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

		public override void Accept(IStatementVisitor vis)
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

		public class Import : IVisitable<IStatementVisitor>
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

			public void Accept (IStatementVisitor vis)
			{
				vis.VisitImport (this);
			}

			public R Accept<R>(StatementVisitor<R> vis)
			{
				return vis.VisitImport(this);
			}
		}

		public class ImportBinding : IVisitable<IStatementVisitor>
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

			public void Accept (IStatementVisitor vis)
			{
				vis.VisitImport (this);
			}

			public R Accept<R>(StatementVisitor<R> vis)
			{
				return vis.VisitImport(this);
			}
		}

		public class ImportBindings : IVisitable<IStatementVisitor>
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

			public void Accept (IStatementVisitor vis)
			{
				vis.VisitImport (this);
			}

			public R Accept<R>(StatementVisitor<R> vis)
			{
				return vis.VisitImport(this);
			}
		}

		public List<Import> Imports = new List<Import>();
		public ImportBindings ImportBindList;

		public override string ToCode ()
		{
			return ToCode (true);
		}

		public string ToCode(bool attributes)
		{
			var sb = new StringBuilder ();

			if (attributes && Attributes != null)
				foreach (var attr in Attributes)
					sb.Append (attr.ToString()).Append(' ');

			sb.Append ("import ");

			foreach (var imp in Imports)
				sb.Append(imp.ToString()).Append(',');

			if (ImportBindList == null)
				sb.Remove (sb.Length - 1, 1);
			else
				sb.Append(ImportBindList.ToString());

			return sb.ToString();
		}

		public override void Accept(IStatementVisitor vis)
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
			get { return PseudoAliases.ToArray(); }
		}
		#endregion
	}

	/// <summary>
	/// Only import symbol aliases are allowed to search in the parse cache
	/// </summary>
	public abstract class ImportSymbolNode : DVariable
	{
		public readonly ImportStatement ImportStatement;

		protected ImportSymbolNode(ImportStatement impStmt,IBlockNode parentNode)
		{
			IsAlias = true;
			this.ImportStatement = impStmt;
			Parent = parentNode;
		}

		public override void Accept(NodeVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(NodeVisitor<R> vis)
		{
			return vis.Visit(this);
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

		public override void Accept(NodeVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(NodeVisitor<R> vis)
		{
			return vis.Visit(this);
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

		public override void Accept(NodeVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(NodeVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}
}
