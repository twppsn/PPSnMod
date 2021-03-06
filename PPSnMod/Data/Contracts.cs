﻿#region -- copyright --
//
// Licensed under the EUPL, Version 1.1 or - as soon they will be approved by the
// European Commission - subsequent versions of the EUPL(the "Licence"); You may
// not use this work except in compliance with the Licence.
//
// You may obtain a copy of the Licence at:
// http://ec.europa.eu/idabc/eupl
//
// Unless required by applicable law or agreed to in writing, software distributed
// under the Licence is distributed on an "AS IS" basis, WITHOUT WARRANTIES OR
// CONDITIONS OF ANY KIND, either express or implied. See the Licence for the
// specific language governing permissions and limitations under the Licence.
//
#endregion
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using System.Threading.Tasks;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.Server.Data
{
	#region -- class PpsUserIdentity ----------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Special Identity, for the system user.</summary>
	public abstract class PpsUserIdentity : IIdentity, IEquatable<IIdentity>, IDisposable
	{
		#region -- class PpsSystemIdentity ----------------------------------------------

		public sealed class PpsSystemIdentity : PpsUserIdentity
		{
			private const string name = "system";

			internal PpsSystemIdentity()
			{
			} // ctor

			public override bool Equals(IIdentity other)
				=> other is PpsSystemIdentity;

			protected override PpsCredentials GetCredentialsFromIdentityCore(IIdentity identity)
				=> Equals(identity) ? new PpsIntegratedCredentials(WindowsIdentity.GetCurrent(), false) : null;

			public override bool IsAuthenticated => true;
			public override string Name => "des\\" + name;
		} // class PpsSystemIdentity

		#endregion

		#region -- class PpsBasicIdentity -----------------------------------------------

		private sealed class PpsBasicIdentity : PpsUserIdentity
		{
			private readonly string userName;
			private readonly byte[] passwordHash;

			public PpsBasicIdentity(string userName, byte[] passwordHash)
			{
				this.userName = userName ?? throw new ArgumentNullException(nameof(userName));
				this.passwordHash = passwordHash ?? throw new ArgumentNullException(nameof(passwordHash));
			} // ctor

			protected override void Dispose(bool disposing)
			{
				Array.Clear(passwordHash, 0, passwordHash.Length);
				base.Dispose(disposing);
			} // proc Dispose

			public override bool Equals(IIdentity other)
			{
				if (other is HttpListenerBasicIdentity basicIdentity)
				{
					if (String.Compare(userName, other.Name, StringComparison.OrdinalIgnoreCase) != 0)
						return false;
					return ProcsDE.PasswordCompare(basicIdentity.Password, passwordHash);
				}
				else if (other is PpsBasicIdentity checkSql)
				{
					if (String.Compare(userName, checkSql.Name, StringComparison.OrdinalIgnoreCase) != 0)
						return false;
					return Procs.CompareBytes(passwordHash, checkSql.passwordHash);
				}
				else
					return false;
			} // func Equals

			protected override PpsCredentials GetCredentialsFromIdentityCore(IIdentity identity)
				=> identity is HttpListenerBasicIdentity p ? new PpsUserCredentials(p) : null;

			public override string Name => userName;
			public override bool IsAuthenticated => passwordHash != null;
		} // class PpsBasicIdentity

		#endregion

		#region -- class PpsWindowsIdentity ---------------------------------------------

		private sealed class PpsWindowsIdentity : PpsUserIdentity
		{
			private readonly SecurityIdentifier identityId;
			private readonly NTAccount identityAccount;

			public PpsWindowsIdentity(string domainName, string userName)
			{
				this.identityAccount = new NTAccount(domainName, userName);
				this.identityId = (SecurityIdentifier)identityAccount.Translate(typeof(SecurityIdentifier));
			} // ctor

			public override bool Equals(IIdentity other)
			{
				if (other is WindowsIdentity checkWindows && checkWindows.IsAuthenticated)
					return identityId == checkWindows.User;
				else if (other is PpsWindowsIdentity checkWin)
					return identityId == checkWin.identityId;
				else
					return false;
			} // func Equals

			protected override PpsCredentials GetCredentialsFromIdentityCore(IIdentity identity)
				=> identity is WindowsIdentity w ? new PpsIntegratedCredentials(w, true) : null;

			public override string Name => identityAccount.ToString();
			public override bool IsAuthenticated => false;
		} // class PpsWindowsIdentity

		#endregion

		private PpsUserIdentity()
		{
		} // ctor

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			Dispose(true);
		} // proc Dispose

		protected virtual void Dispose(bool disposing)
		{
		} // proc Dispose

		public override int GetHashCode()
			=> Name.GetHashCode();

		public override bool Equals(object obj)
		{
			if (Object.ReferenceEquals(this, obj))
				return true;
			else if (obj is IIdentity i)
				return Equals(i);
			else
				return false;
		} // func Equals

		public abstract bool Equals(IIdentity other);

		internal PpsCredentials GetCredentialsFromIdentity(IIdentity identity)
		{
			if (identity == null)
				throw new ArgumentNullException(nameof(identity));

			return GetCredentialsFromIdentityCore(identity) ?? throw new ArgumentException($"Identity from type {identity.GetType().Name} is not compatible.", nameof(identity));
		} // func GetCredentialsFromIdentity

		protected abstract PpsCredentials GetCredentialsFromIdentityCore(IIdentity identity);

		/// <summary>des</summary>
		public string AuthenticationType => "des";
		/// <summary>Immer <c>true</c></summary>
		public abstract bool IsAuthenticated { get; }
		/// <summary>system</summary>
		public abstract string Name { get; }

		/// <summary>Singleton</summary>
		public static PpsUserIdentity System { get; } = new PpsSystemIdentity();

		internal static PpsUserIdentity CreateBasicIdentity(string userName, byte[] passwordHash)
			=> new PpsBasicIdentity(userName, passwordHash);

		internal static PpsUserIdentity CreateIntegratedIdentity(string userName)
		{
			var p = userName.IndexOf('\\');
			return p == -1
				? new PpsWindowsIdentity(null, userName)
				: new PpsWindowsIdentity(userName.Substring(0, p), userName.Substring(p + 1));
		} // func CreateIntegratedIdentity
	} // class PpsUserIdentity

	#endregion

	#region -- class PpsCredentials -----------------------------------------------------

	public abstract class PpsCredentials : IDisposable
	{
		public void Dispose()
		{
			Dispose(true);
		} // proc Dispose

		protected virtual void Dispose(bool disposing) { }
	} // class PpsCredentials

	#endregion

	#region -- class PpsIntegratedCredentials -------------------------------------------

	public sealed class PpsIntegratedCredentials : PpsCredentials
	{
		private readonly WindowsIdentity identity;

		internal PpsIntegratedCredentials(WindowsIdentity identity, bool doClone)
		{
			if (identity == null)
				throw new ArgumentNullException(nameof(identity));

			this.identity = doClone ? (WindowsIdentity)identity.Clone() : identity;
		} // ctor

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			if (disposing)
				identity.Dispose();
		} // proc Dispose

		public IDisposable Impersonate()
			=> WindowsIdentity.GetCurrent().User != identity.User
				? identity.Impersonate()
				: null;
	} // class PpsIntegratedCredentials

	#endregion

	#region -- class PpsUserCredentials -------------------------------------------------

	public sealed class PpsUserCredentials : PpsCredentials
	{
		private readonly string userName;
		private readonly SecureString password;

		internal unsafe PpsUserCredentials(HttpListenerBasicIdentity identity)
		{
			if (identity == null)
				throw new ArgumentNullException(nameof(identity));

			if (String.IsNullOrEmpty(identity.Name))
				throw new ArgumentException($"{nameof(identity)}.{nameof(HttpListenerBasicIdentity.Name)} is null or empty.");

			if (String.IsNullOrEmpty(identity.Password))
				throw new ArgumentException($"{nameof(identity)}.{nameof(HttpListenerBasicIdentity.Password)} is null or empty.");

			// copy the arguments
			this.userName = identity.Name;
			var passwordPtr = Marshal.StringToHGlobalUni(identity.Password);
			try
			{
				this.password = new SecureString((char*)passwordPtr.ToPointer(), identity.Password.Length);
			}
			finally
			{
				Marshal.ZeroFreeGlobalAllocUnicode(passwordPtr);
			}
			this.password.MakeReadOnly();
		} // ctor

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			if (disposing)
				password?.Dispose();
		} // proc Dispose

		public string UserName => userName;
		public SecureString Password => password;
	} // class PpsUserCredentials

	#endregion

	#region -- interface IPpsConnectionHandle -------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Represents a connection of a data source</summary>
	public interface IPpsConnectionHandle : IDisposable
	{
		event EventHandler Disposed;

		/// <summary>Enforces the connection.</summary>
		/// <returns></returns>
		Task<bool> EnsureConnectionAsync(bool throwException = true);

		/// <summary>Is the connection still active.</summary>
		bool IsConnected { get; }

		/// <summary>DataSource of the current connection.</summary>
		PpsDataSource DataSource { get; }
	} // interface IPpsConnectionHandle

	#endregion

	#region -- interface IPpsPrivateDataContext -----------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Hold's the connection and context data for one user.</summary>
	public interface IPpsPrivateDataContext : IPropertyReadOnlyDictionary, IDisposable
	{
		/// <summary>Creates a selector for a view.</summary>
		/// <param name="name">Name of the view</param>
		/// <param name="columns">Column definition.</param>
		/// <param name="filter">Filter rules</param>
		/// <param name="order">Order rules</param>
		/// <param name="throwException">Should the method throw on an exception on failure.</param>
		/// <returns></returns>
		Task<PpsDataSelector> CreateSelectorAsync(string name, PpsDataColumnExpression[] columns = null, PpsDataFilterExpression filter = null, PpsDataOrderExpression[] order = null, bool throwException = true);

		/// <summary>Creates a transaction to manipulate data.</summary>
		/// <param name="dataSource"></param>
		/// <param name="throwException">Should the method throw on an exception on failure.</param>
		/// <returns></returns>
		Task<PpsDataTransaction> CreateTransactionAsync(string dataSourceName, bool throwException = true);
		/// <summary>Creates a transaction to manipulate data.</summary>
		/// <param name="dataSource">Datasource specified as object.</param>
		/// <param name="throwException">Should the method throw on an exception on failure.</param>
		/// <returns></returns>
		Task<PpsDataTransaction> CreateTransactionAsync(PpsDataSource dataSource, bool throwException = true);

		/// <summary>Creates the credentials for to user for external tasks (like database connections).</summary>
		/// <returns></returns>
		PpsCredentials GetNetworkCredential();
		/// <summary>Creates the credentials for the local computer (e.g. file operations).</summary>
		/// <returns></returns>
		PpsIntegratedCredentials GetLocalCredentials();

		/// <summary>UserId of the user in the main database.</summary>
		long UserId { get; }
		/// <summary>Name (display info) of the current user.</summary>
		string UserName { get; }
		/// <summary>Returns the current identity.</summary>
		PpsUserIdentity Identity { get; }
	} // interface IPpsPrivateDataContext

	#endregion

	#region -- interface IPpsColumnDescription ------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Description for a column.</summary>
	public interface IPpsColumnDescription : IDataColumn
	{
		/// <summary>Returns a specific column implemenation.</summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		T GetColumnDescription<T>() where T : IPpsColumnDescription;
	} // interface IPpsProviderColumnDescription

	#endregion

	#region -- class PpsColumnDescription -----------------------------------------------

	[DebuggerDisplay("{DebuggerDisplay,nq}")]
	public class PpsColumnDescription : IPpsColumnDescription
	{
		private readonly IPpsColumnDescription parent;
		private readonly IPropertyEnumerableDictionary attributes;

		private readonly string name;
		private readonly Type dataType;

		public PpsColumnDescription(IPpsColumnDescription parent, string name, Type dataType)
		{
			this.parent = parent;
			this.attributes = CreateAttributes();

			this.name = name ?? throw new ArgumentNullException(nameof(name));
			this.dataType = dataType ?? throw new ArgumentNullException(nameof(dataType));
		} // ctor

		protected virtual IPropertyEnumerableDictionary CreateAttributes()
			=> parent == null ? PropertyDictionary.EmptyReadOnly : parent.Attributes;

		public T GetColumnDescription<T>() where T : IPpsColumnDescription
			=> this.GetColumnDescriptionParentImplementation<T>(parent);

		private string DebuggerDisplay
			=> $"Column: {Name} : {DataType.Name}";

		public string Name => name;
		public Type DataType => dataType;

		public IPpsColumnDescription Parent => parent;
		public IPropertyEnumerableDictionary Attributes => attributes;
	} // class PpsColumnDescription

	public static class PpsColumnDescriptionHelper
	{
		#region -- class PpsColumnDescriptionAttributes ---------------------------------

		private sealed class PpsColumnDescriptionAttributes : IPropertyEnumerableDictionary
		{
			private readonly IPropertyEnumerableDictionary self;
			private readonly IPropertyEnumerableDictionary parent;
			private readonly List<string> emittedProperties = new List<string>();

			public PpsColumnDescriptionAttributes(IPropertyEnumerableDictionary self, IPropertyEnumerableDictionary parent)
			{
				this.self = self;
				this.parent = parent;
			} // ctor

			private bool PropertyEmitted(string name)
			{
				var idx = emittedProperties.BinarySearch(name, StringComparer.OrdinalIgnoreCase);
				if (idx >= 0)
					return true;
				emittedProperties.Insert(~idx, name);
				return false;
			} // func PropertyEmitted

			public IEnumerator<PropertyValue> GetEnumerator()
			{
				foreach (var c in self)
				{
					if (!PropertyEmitted(c.Name))
						yield return c;
				}

				if (parent != null)
				{
					foreach (var c in parent)
					{
						if (!PropertyEmitted(c.Name))
							yield return c;
					}
				}
			} // func GetEnumerator

			public bool TryGetProperty(string name, out object value)
			{
				if (self.TryGetProperty(name, out value))
					return true;
				else if (parent != null && parent.TryGetProperty(name, out value))
					return true;

				value = null;
				return false;
			} // func TryGetProperty

			IEnumerator IEnumerable.GetEnumerator()
				=> GetEnumerator();
		} // class PpsColumnDescriptionAttributes

		#endregion

		#region -- class PpsDataColumnDescription ---------------------------------------

		private sealed class PpsDataColumnDescription : IPpsColumnDescription
		{
			private readonly IPpsColumnDescription parent;
			private readonly IDataColumn column;

			public PpsDataColumnDescription(IPpsColumnDescription parent, IDataColumn column)
			{
				this.parent = parent;
				this.column = column;
			} // ctor

			public T GetColumnDescription<T>()
				where T : IPpsColumnDescription
				=> this.GetColumnDescriptionParentImplementation<T>(parent);

			public string Name => column.Name;
			public Type DataType => column.DataType;

			public IPropertyEnumerableDictionary Attributes
				=> new PpsColumnDescriptionAttributes(column.Attributes, parent.Attributes);
		} // class PpsDataColumnDescription

		#endregion

		public static IPropertyEnumerableDictionary GetColumnDescriptionParentAttributes(IPropertyEnumerableDictionary properties, IPpsColumnDescription parent)
			=> new PpsColumnDescriptionAttributes(properties, parent?.Attributes);

		public static T GetColumnDescriptionParentImplementation<T>(this IPpsColumnDescription @this, IPpsColumnDescription parent)
			where T : IPpsColumnDescription
		{
			if (typeof(T).IsAssignableFrom(@this.GetType()))
				return (T)(IPpsColumnDescription)@this;
			else if (parent != null)
				return parent.GetColumnDescription<T>();
			else
				return default(T);
		} // func GetColumnDescriptionParentImplementation

		public static bool TryGetColumnDescriptionImplementation<T>(this IPpsColumnDescription columnDescription, out T value)
			where T : IPpsColumnDescription
			=> (value = columnDescription.GetColumnDescription<T>()) != null;

		public static IPpsColumnDescription ToColumnDescription(this IDataColumn column, IPpsColumnDescription parent = null)
			=> new PpsDataColumnDescription(parent, column);
	} // class PpsColumnDescriptionHelper

	#endregion

	#region -- interface IPpsSelectorToken ----------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public interface IPpsSelectorToken
	{
		PpsDataSelector CreateSelector(IPpsConnectionHandle connection, bool throwException = true);

		/// <summary>Get the defintion for a column from the native column name.</summary>
		/// <param name="selectorColumn"></param>
		/// <returns></returns>
		IPpsColumnDescription GetFieldDescription(string selectorColumn);

		/// <summary>Name of the selector.</summary>
		string Name { get; }
		/// <summary>Attached datasource</summary>
		PpsDataSource DataSource { get; }

		/// <summary>Column description of the selector, before execution.</summary>
		IEnumerable<IPpsColumnDescription> Columns { get; }
	} // interface IPpsSelectorToken

	#endregion

	#region -- class PpsPrivateDataContextHelper --------------------------------------

	public static class PpsPrivateDataContextHelper
	{
		public static Task<PpsDataSelector> CreateSelectorAsync(this IPpsPrivateDataContext ctx, string name, string columns = null, string filter = null, string order = null, bool throwException = true)
			=> ctx.CreateSelectorAsync(
				name,
				PpsDataColumnExpression.Parse(columns).ToArray(),
				PpsDataFilterExpression.Parse(filter),
				PpsDataOrderExpression.Parse(order).ToArray(),
				throwException
			);

		public static Task<PpsDataSelector> CreateSelectorAsync(this IPpsPrivateDataContext ctx, LuaTable table, bool throwException = true)
			=> ctx.CreateSelectorAsync(
				table.GetOptionalValue("name", (string)null),
				PpsDataColumnExpression.Parse(table.GetMemberValue("columns")).ToArray(),
				PpsDataFilterExpression.Parse(table.GetMemberValue("filter")),
				PpsDataOrderExpression.Parse(table.GetMemberValue("order")).ToArray(),
				throwException
			);
	} // class PpsPrivateDataContextHelper

	#endregion
}
