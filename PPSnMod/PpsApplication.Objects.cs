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
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Networking;
using TecWare.DE.Server;
using TecWare.DE.Server.Http;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using TecWare.PPSn.Server.Data;

namespace TecWare.PPSn.Server
{
	#region -- class PpsObjectTagAccess -------------------------------------------------

	public sealed class PpsObjectTagAccess
	{
		private readonly PpsObjectAccess obj;
		private long id;
		private readonly long objectId;
		private string key;
		private int tagClass;
		private string value;
		private long userId;

		private bool isRemoved = false;

		internal PpsObjectTagAccess(PpsObjectAccess obj, long id, long objectId, int tagClass, string key, string value, long userId)
		{
			this.obj = obj;
			this.id = id;
			this.objectId = objectId;
			this.tagClass = tagClass;
			this.key = key;
			this.value = value;
			this.userId = userId;
		}

		internal PpsObjectTagAccess(PpsObjectAccess obj, IDataRow row)
			: this(obj, row.GetProperty("Id", -1L), row.GetProperty("ObjKId", -1L), row.GetProperty("Class", 0), row.GetProperty("Key", String.Empty), row.GetProperty("Value", String.Empty), row.GetProperty("UserId", -1L))
		{
		}

		public void Remove()
		{
			isRemoved = true;
		}

		public XElement ToXml(string elementName)
			=> new XElement(elementName,
				new XAttribute("objectId", objectId),
				new XAttribute("tagClass", tagClass),
				new XAttribute("key", key),
				new XAttribute("value", value),
				new XAttribute("userId", userId)
			);

		public long Id { get { return this.id; } set { this.id = value; } }
		public long ObjectId { get { return this.objectId; } }
		public int TagClass { get { return this.tagClass; } set { this.tagClass = value; } }
		public string Key
		{
			get
			{
				return this.key;
			}
			set
			{
				if (String.IsNullOrEmpty(value)) throw new ArgumentNullException();
				else this.key = value;
			}
		}
		public string Value
		{
			get
			{
				return this.value;
			}
			set
			{
				if (String.IsNullOrEmpty(value)) throw new ArgumentNullException();
				else this.value = value;
			}
		}
		public long UserId { get { return this.userId; } set { this.userId = value; } }
		public bool IsNew => id < 0;
		public bool IsRemoved => isRemoved;
	}

	#endregion

	#region -- class PpsObjectLinkAccess ------------------------------------------------

	public sealed class PpsObjectLinkAccess
	{
		private readonly PpsObjectAccess obj;
		private long id;
		private readonly long objectId;
		private int refCount;
		private char onDelete;

		private bool isRemoved = false;

		internal PpsObjectLinkAccess(PpsObjectAccess obj, long id, long objectId, int refCount, char onDelete)
		{
			this.obj = obj;
			this.id = id;
			this.objectId = objectId;
			this.refCount = refCount;
			this.onDelete = onDelete;
		} // ctor

		internal PpsObjectLinkAccess(PpsObjectAccess obj, IDataRow row)
			: this(obj, row.GetProperty("Id", -1L), row.GetProperty("ObjKId", -1L), row.GetProperty("RefCount", 0), row.GetProperty("OnDelete", "R")[0])
		{
		} // ctor

		public void Remove()
		{
			isRemoved = true;
		} // proc Remove

		public XElement ToXml(string elementName)
			=> new XElement(elementName,
				new XAttribute("objectId", objectId),
				new XAttribute("refCount", refCount),
				new XAttribute("onDelete", onDelete)
			);

		public long Id { get => id; set => id = value; }
		public long ObjectId => objectId;

		public int RefCount { get => refCount; set => refCount = value; }
		public char OnDelete { get => onDelete; set => onDelete = value; }

		public bool IsNew => id < 0;
		public bool IsRemoved => isRemoved;
	} // class PpsObjectLinkAccess

	#endregion

	#region -- class PpsObjectAccess ----------------------------------------------------

	/// <summary>This class defines a interface to access pps-object model. Some properties are late bound. So, wait for closing the transaction.</summary>
	public sealed class PpsObjectAccess : LuaTable
	{
		private readonly PpsApplication application;
		private readonly IPropertyReadOnlyDictionary defaultData;
		private long objectId;

		private bool? isRev;
		private long revId;

		private List<PpsObjectLinkAccess> linksTo = null;
		private List<PpsObjectTagAccess> tags = null;

		internal PpsObjectAccess(PpsApplication application, IPropertyReadOnlyDictionary defaultData)
		{
			this.application = application;
			this.defaultData = defaultData;

			this.objectId = defaultData.GetProperty(nameof(Id), -1L);
			this.revId = defaultData.GetProperty(nameof(RevId), defaultData.GetProperty(nameof(HeadRevId), -1L));
			this.isRev = defaultData.TryGetProperty<bool>(nameof(IsRev), out var t) ? (bool?)t : null;
		} // ctor

		protected override object OnIndex(object key)
			=> key is string k && defaultData.TryGetProperty(k, out var t) ? t : base.OnIndex(key);

		private void Reset()
		{
			// reload links
			linksTo = null;
		} // proc Reset

		private void CheckRevision()
		{
			if (isRev.HasValue)
				return;

			// get head rev.
			var cmd = new LuaTable
			{
				{ "select", "dbo.ObjK" },
				{ "selectList", new LuaTable { nameof(IsRev), nameof(HeadRevId), } },
				new LuaTable
				{
					{ "Id", objectId }
				}
			};

			var r = application.Database.Main.ExecuteSingleRow(cmd);

			isRev = (bool)r[nameof(IsRev), true];
			revId = (long)(r[nameof(HeadRevId), true] ?? -1);
		} // proc CheckRevision

		private LuaTable GetObjectArguments(bool forInsert)
		{
			var args = new LuaTable
			{
				{ nameof(Nr), Nr }
			};

			if (forInsert)
			{
				var guid = Guid;
				if (guid == Guid.Empty)
					guid = Guid.NewGuid();
				args[nameof(Guid)] = guid;
				args[nameof(Typ)] = Typ;
				args[nameof(IsRev)] = IsRev;
			}
			else
				args[nameof(Id)] = objectId;

			if (MimeType != null)
				args[nameof(MimeType)] = MimeType;

			if (CurRevId > 0)
				args[nameof(CurRevId)] = CurRevId;
			if (HeadRevId > 0)
				args[nameof(HeadRevId)] = HeadRevId;

			return args;
		} // func GetObjectArguments

		[LuaMember]
		public void Update(bool updateObjectOnly = true)
		{
			LuaTable cmd;
			var trans = application.Database.Main;

			// prepare stmt
			if (objectId > 0) // exists an id for the object
			{
				cmd = new LuaTable
				{
					{"update", "dbo.ObjK" },
					GetObjectArguments(false)
				};
			}
			else if (Guid == Guid.Empty) // does not exist
			{
				cmd = new LuaTable
				{
					{"insert", "dbo.ObjK" },
					GetObjectArguments(true)
				};
			}
			else // upsert over guid or id
			{
				var args = GetObjectArguments(IsNew);

				cmd = new LuaTable
				{
					{ "upsert", "dbo.ObjK"},
					args
				};

				if (IsNew)
				{
					args[nameof(Guid)] = Guid;
					cmd.Add("on", new LuaTable { nameof(Guid) });
				}
			}

			trans.ExecuteNoneResult(cmd);

			// update values
			if (IsNew)
			{
				var args = (LuaTable)cmd[1];
				objectId = (long)args[nameof(Id)];
				this[nameof(Guid)] = args[nameof(Guid)];
			}

			if (!updateObjectOnly)
			{
				CheckRevision();
				// tags changed?
				if (tags != null)
				{
					var deleteCmd = new LuaTable
					{
						{ "delete", "dbo.ObjT" }
					};
					var insertCmd = new LuaTable
					{
						{ "insert", "dbo.ObjT" }
					};
					var upsertCmd = new LuaTable
					{
						{ "upsert", "dbo.ObjT" }
					};

					foreach (var t in tags)
					{
						if (t.IsRemoved && t.Id > 0)
						{
							#region tag is going to be removed

							deleteCmd[1] = new LuaTable
							{
								{ "Id", t.Id }
							};

							trans.ExecuteNoneResult(deleteCmd);

							#endregion
						}
						else if (t.IsNew)
						{
							#region tag is new

							insertCmd[1] = new LuaTable
							{
								{ "ObjKId", objectId },
								{ "ObjRId", revId },
								{ "Class", t.TagClass },
								{ "Key", t.Key },
								{ "Value", t.Value },
								{ "UserId", t.UserId }
							};

							trans.ExecuteNoneResult(insertCmd);

							t.Id = ((LuaTable)insertCmd[1]).GetOptionalValue("Id", -1L);

							#endregion
						}
						else
						{
							#region tag is neither obsolete nor new, so upsert

							upsertCmd[1] = new LuaTable
							{
								{ "Id", t.Id },
								{ "ObjKId", objectId },
								{ "ObjRId", revId },
								{ "Class", t.TagClass },
								{ "Key", t.Key },
								{ "Value", t.Value },
								{ "UserId", t.UserId }
							};

							trans.ExecuteNoneResult(upsertCmd);

							#endregion
						}
					}
				}

				// links changed?
				if (linksTo != null)
				{
					#region -- update links --
					cmd = new LuaTable
					{
						{ "delete", "dbo.ObjL" }
					};
					foreach (var l in linksTo)
					{
						if (l.IsRemoved && l.Id > 0)
						{
							cmd[1] = new LuaTable
							{
								{ "Id", l.Id }
							};

							trans.ExecuteNoneResult(cmd);
						}
					}

					// upsert, insert links
					foreach (var l in linksTo)
					{
						if (l.IsNew)
						{
							cmd = new LuaTable
							{
								{ "insert", "dbo.ObjL" },
								new LuaTable
								{
									{ "ParentObjKId", objectId },
									{ "ParentObjRId", revId},
									{ "LinkObjKId", l.ObjectId },
									{ "RefCount", l.RefCount },
									{ "OnDelete", l.OnDelete }
								}
							};

							trans.ExecuteNoneResult(cmd);

							l.Id = ((LuaTable)cmd[1]).GetOptionalValue("Id", -1L);
						}
						else
						{
							cmd = new LuaTable
							{
								{ "upsert", "dbo.ObjL" },
								new LuaTable
								{
									{ "Id", l.Id },
									{ "ParentObjKId", objectId },
									{ "ParentObjRId", revId},
									{ "LinkObjKId", l.ObjectId },
									{ "RefCount", l.RefCount },
									{ "OnDelete", l.OnDelete }
								}
							};

							trans.ExecuteNoneResult(cmd);
						}
					}
					#endregion
				}

				Reset();
			}
		} // proc Update

		[LuaMember]
		public void SetRevision(long newRevId, bool refreshLinks = true)
		{
			CheckRevision();
			if (revId == newRevId)
				return;

			revId = newRevId;
			if (refreshLinks)
				Reset();
		} // proc SetRevision

		private void SetDocumentArguments(LuaTable args, Action<Stream> copyData, long contentLength, bool deflateStream)
		{
			args["IsDocumentDeflate"] = deflateStream;

			using (var dstMemory = new MemoryStream())
			{
				if (deflateStream)
				{
					using (var dstDeflate = new GZipStream(dstMemory, CompressionMode.Compress))
						copyData(dstDeflate);
				}
				else
					copyData(dstMemory);
				dstMemory.Flush();

				args["Document"] = dstMemory.ToArray();
			}
		} // proc SetDocumentArguments

		[LuaMember]
		public void UpdateData(object content, long contentLength = -1, bool changeHead = true, bool forceReplace = false)
		{
			if (objectId < 0)
				throw new ArgumentOutOfRangeException("Id", "Object Id is invalid.");

			var args = new LuaTable
			{
				{ "ObjkId", objectId },

				{ "CreateUserId", DEScope.GetScopeService<IPpsPrivateDataContext>().UserId },
				{ "CreateDate",  DateTime.Now }
			};

			var insertNew = isRev.Value || (!forceReplace && revId > 0);
			if (insertNew && revId > 0)
				args["ParentId"] = revId;

			// convert data
			var shouldText = MimeType != null && MimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase);
			var shouldDeflate = shouldText;

			if (content != null)
			{
				switch (content)
				{
					case string s:
						shouldText = true;
						SetDocumentArguments(args, dst =>
							{
								using (var sr = new StreamWriter(dst, Encoding.Unicode, 4096, true))
									sr.Write(s);
							},
							s.Length, shouldDeflate
						);
						break;
					case StringBuilder sb:
						shouldText = true;
						SetDocumentArguments(args, dst =>
							{
								using (var sr = new StreamWriter(dst, Encoding.Unicode, 4096, true))
									sr.Write(sb.ToString());
							},
							sb.Length, shouldDeflate
						);
						break;
					case byte[] data:
						SetDocumentArguments(args, dst => dst.Write(data, 0, data.Length), data.Length, shouldDeflate);
						break;

					case Action<Stream> copyStream:
						SetDocumentArguments(args, copyStream, contentLength, shouldDeflate);
						break;
					default:
						throw new ArgumentException("Document data format is unknown (only string or byte[] allowed).");
				}
			}
			else
				throw new ArgumentNullException("Document data is missing.");

			args["IsDocumentText"] = shouldText;

			LuaTable cmd;
			if (insertNew)
			{
				cmd = new LuaTable
				{
					{  "insert", "dbo.ObjR" },
					args
				};
			}
			else// upsert current rev
			{
				args["Id"] = revId;

				cmd = new LuaTable
				{
					{  "upsert", "dbo.ObjR" },
					args
				};
			}

			application.Database.Main.ExecuteNoneResult(cmd);
			var newRevId = (long)args["Id"];

			// update
			if (insertNew && changeHead)
			{
				this["HeadRevId"] = newRevId;
				SetRevision(newRevId, false);
			}

			revId = newRevId;
		} // proc UpdateData

		[LuaMember]
		public Stream GetDataStream()
		{
			CheckRevision();

			var cmd = new LuaTable
			{
				{ "select", "dbo.ObjR" },
				{ "selectList", new LuaTable { "Id", "IsDocumentText","IsDocumentDeflate","Document","DocumentId","DocumentLink" } },
				revId > -1 ? new LuaTable
				{
					{ "Id", revId }
				} : new LuaTable
				{
					{ "ObjkId", objectId }
				}
			};

			var row = application.Database.Main.ExecuteSingleRow(cmd);
			if (row == null)
				throw new ArgumentException($"Could not read revision '{revId}'.");

			var isDeflated = row.GetProperty("IsDocumentDeflate", false);
			var rawData = (byte[])row["Document"];

			var src = (Stream)new MemoryStream(rawData, false);
			if (isDeflated)
				src = new GZipStream(src, CompressionMode.Decompress, false);
			return src;
		} // func GetDataStream

		[LuaMember]
		public byte[] GetBytes()
		{
			using (var src = GetDataStream())
				return src.ReadInArray();
		} // func GetBytes

		[LuaMember]
		public string GetText()
		{
			// todo: in DESCore\Networking\Requests.cs:BaseWebRequest.CheckMimeType 
			using (var tr = Procs.OpenStreamReader(GetDataStream(), DataEncoding))
				return tr.ReadToEnd();
		} // func GetText

		[LuaMember]
		public XElement ToXml(bool onlyObjectData = false)
		{
			var x = new XElement(
				"object",
				Procs.XAttributeCreate(nameof(Id), objectId, -1),
				Procs.XAttributeCreate(nameof(RevId), RevId, -1),
				Procs.XAttributeCreate(nameof(HeadRevId), HeadRevId, -1),
				Procs.XAttributeCreate(nameof(CurRevId), CurRevId, -1),
				Procs.XAttributeCreate(nameof(Typ), Typ, null),
				Procs.XAttributeCreate(nameof(MimeType), MimeType, null),
				Procs.XAttributeCreate(nameof(Nr), Nr),
				Procs.XAttributeCreate(nameof(IsRev), IsRev, false)
			);

			if (!onlyObjectData)
			{
				// append links
				foreach (var cur in LinksTo)
					x.Add(cur.ToXml("linksTo"));

				// append tags
				foreach (var cur in Tags)
					x.Add(cur.ToXml("tag"));
			}

			return x;
		} // func ToXml

		public PpsObjectTagAccess AddTag(XElement x)
		{
			GetTags(ref tags);

			var objectId = x.GetAttribute("objectId", -1L);
			var tagClass = x.GetAttribute("class", -1);
			var key = x.GetAttribute("key", String.Empty);
			var value = x.GetAttribute("value", String.Empty);
			var userId = x.GetAttribute("userId", -1L);

			tags.Add(new PpsObjectTagAccess(this, -1, objectId, tagClass, key, value, userId));

			return null;
		}

		public PpsObjectLinkAccess AddLinkTo(XElement x)
		{
			// refresh link list
			GetLinks(true, ref linksTo);

			var objectId = x.GetAttribute("objectId", -1L);
			var cur = linksTo.Find(l => l.ObjectId == objectId);
			var refCount = x.GetAttribute("refCount", 0);
			var onDelete = x.GetAttribute("onDelete", "R");
			if (cur == null) // new
				linksTo.Add(new PpsObjectLinkAccess(this, -1, objectId, refCount, onDelete[0]));
			else
			{
				cur.RefCount = refCount;
				cur.OnDelete = onDelete[0];
			}

			return null;
		}

		[LuaMember]
		public void AddLink(long objectId)
		{
			var existingLink = GetLinks(true, ref linksTo).FirstOrDefault(c => c.ObjectId == objectId);
			if (existingLink == null)
				linksTo.Add(new PpsObjectLinkAccess(this, -1, objectId, 0, 'R'));
		} // proc AddLink

		private List<PpsObjectTagAccess> GetTags(ref List<PpsObjectTagAccess> tags)
		{
			if (tags != null)
				return tags;
			else if (IsNew)
			{
				tags = new List<PpsObjectTagAccess>();
			}
			else
			{
				{
					var cmd = new LuaTable
				{
					{ "select", "dbo.ObjT" },
					{ "selectList",
						new LuaTable
						{
							"Id",
							"ObjKId",
							"ObjRId",
							"Class",
							"Key",
							"Value",
							"UserId"
						}
					},

					new LuaTable
					{
						{ "ObjKId", objectId },
						{ "ObjRId", revId }
					}
				};

					tags = new List<PpsObjectTagAccess>();
					foreach (var c in application.Database.Main.ExecuteSingleResult(cmd))
						tags.Add(new PpsObjectTagAccess(this, c));
				}
			}
			return tags;
		}

		private List<PpsObjectLinkAccess> GetLinks(bool linksToThis, ref List<PpsObjectLinkAccess> links)
		{
			if (links != null)
				return links;
			else if (IsNew)
			{
				links = new List<PpsObjectLinkAccess>();
			}
			else
			{
				var cmd = new LuaTable
				{
					{ "select", "dbo.ObjL" },
					{ "selectList",
						new LuaTable
						{
							"Id",
							{ linksToThis ? "LinkObjKId" : "ParentObjKId", "ObjKId" },
							{ linksToThis ? "LinkObjRId" : "ParentObjRId", "ObjRId" },
							"RefCount",
							"OnDelete"
						}
					},

					new LuaTable
					{
						{ "ParentObjKId", objectId },
						{ "ParentObjRId", revId }
					}
				};

				links = new List<PpsObjectLinkAccess>();
				foreach (var c in application.Database.Main.ExecuteSingleResult(cmd))
					links.Add(new PpsObjectLinkAccess(this, c));
			}
			return links;
		} // func GetLinks

		[LuaMember]
		public long Id => objectId;

		public Guid Guid => this.TryGetValue<Guid>(nameof(Guid), out var t) ? t : Guid.Empty;
		public string Typ => this.TryGetValue<string>(nameof(Typ), out var t) ? t : null;
		public string MimeType => this.TryGetValue<string>(nameof(MimeType), out var t) ? t : null;
		public string Nr => this.TryGetValue<string>(nameof(Nr), out var t) ? t : null;
		public long CurRevId => this.TryGetValue<long>(nameof(CurRevId), out var t) ? t : -1;
		public long HeadRevId => this.TryGetValue<long>(nameof(HeadRevId), out var t) ? t : -1;

		public Encoding DataEncoding => Encoding.Unicode;

		[LuaMember]
		public long RevId
		{
			get
			{
				CheckRevision();
				return revId;
			}
		}

		[LuaMember]
		public bool IsRev
		{
			get
			{
				CheckRevision();
				return isRev.Value;
			}
			set
			{
				isRev = value;
			}
		} // prop IsRev

		[LuaMember]
		public bool IsNew => objectId < 0;

		[LuaMember]
		public long ContentLength => throw new NotImplementedException();

		public IEnumerable<PpsObjectLinkAccess> LinksTo => GetLinks(true, ref linksTo);

		public IEnumerable<PpsObjectTagAccess> Tags => GetTags(ref tags);
	} // class PpsObjectAccess

	#endregion

	#region -- interface IPpsObjectItem -------------------------------------------------

	/// <summary>Description of an object item.</summary>
	public interface IPpsObjectItem
	{
		/// <summary>The name or object typ of the object.</summary>
		string ObjectType { get; }
		/// <summary>Optional description for the object.</summary>
		string ObjectSource { get; }
		/// <summary>Optional default pane for the object.</summary>
		string DefaultPane { get; }
		/// <summary>Returns the default revision behaviour for the object</summary>
		bool IsRevDefault { get; }
	} // interface IPpsObjectItem

	#endregion

	#region -- class PpsObjectItem ------------------------------------------------------

	/// <summary>Base class for all objects, that can be processed from the server.</summary>
	public abstract class PpsObjectItem<T> : PpsPackage, IPpsObjectItem
		where T : class
	{
		private readonly PpsApplication application;

		#region -- Ctor/Dtor ------------------------------------------------------------

		public PpsObjectItem(IServiceProvider sp, string name)
			: base(sp, name)
		{
			this.application = sp.GetService<PpsApplication>(true);
		} // ctor

		#endregion

		#region -- GetObject ------------------------------------------------------------

		private PpsObjectAccess VerfiyObjectType(PpsObjectAccess obj)
		{
			if (String.Compare(obj.Typ, ObjectType, StringComparison.OrdinalIgnoreCase) != 0)
				throw new ArgumentOutOfRangeException(nameof(obj), obj.Typ, "Object is '{obj.Typ}'. Expected: '{ObjectType}'");
			return obj;
		} // func VerfiyObjectType

		public PpsObjectAccess GetObject(long id)
			=> VerfiyObjectType(application.Objects.GetObject(id));

		public PpsObjectAccess GetObject(Guid guid)
			=> VerfiyObjectType(application.Objects.GetObject(guid));

		/// <summary>Serialize data to an stream of bytes.</summary>
		protected abstract void WriteDataToStream(T data, Stream dst);

		protected abstract T GetDataFromStream(Stream src);

		#endregion

		#region -- Pull -----------------------------------------------------------------

		protected virtual T PullData(PpsObjectAccess obj)
		{
			using (var src = obj.GetDataStream())
				return GetDataFromStream(src);
		} // func PullData

		[LuaMember("Pull")]
		private LuaResult LuaPull(long? objectId, Guid? guidId, long? revId)
		{
			// initialize object access
			var obj = objectId.HasValue
				? GetObject(objectId.Value)
				: guidId.HasValue
					? GetObject(guidId.Value)
					: throw new ArgumentNullException("objectId|guidId");

			if (revId.HasValue)
				obj.SetRevision(revId.Value);

			var data = PullData(obj);

			return new LuaResult(data, obj);
		} // func PullDataSet

		[
		DEConfigHttpAction("pull", IsSafeCall = false, SecurityToken = "user"),
		Description("Reads the revision from the server.")
		]
		private void HttpPullAction(long id, long rev = -1)
		{
			var ctx = DEScope.GetScopeService<IDEWebRequestScope>(true);
			try
			{
				var trans = application.Database.GetDatabase();

				// get the object and set the correct revision
				var obj = GetObject(id);
				if (rev >= 0)
					obj.SetRevision(rev);

				// prepare object data
				var headerBytes = Encoding.Unicode.GetBytes(obj.ToXml().ToString(SaveOptions.DisableFormatting));
				ctx.OutputHeaders["ppsn-header-length"] = headerBytes.Length.ChangeType<string>();

				// get content
				var data = PullData(obj);

				// write all data to the application
				using (var dst = ctx.GetOutputStream(MimeTypes.Application.OctetStream))
				{
					// write header bytes
					dst.Write(headerBytes, 0, headerBytes.Length);

					// write content
					WriteDataToStream(data, dst);
				}

				// commit
				trans.Commit();
			}
			catch (Exception e)
			{
				ctx.WriteSafeCall(e);
			}
		} // proc HttpPullAction

		#endregion

		#region -- Push -----------------------------------------------------------------

		protected virtual bool IsDataRevision(T data)
			=> false;

		private object GetNextNumberMethod()
		{
			// test for next number
			var nextNumber = this["NextNumber"];
			if (nextNumber != null)
				return nextNumber;

			// test for length
			var nrLength = Config.GetAttribute("nrLength", 0);
			if (nrLength > 0)
				return nrLength;

			return null;
		} // func GetNextNumberMethod

		protected virtual void SetNextNumber(PpsDataTransaction transaction, PpsObjectAccess obj, T data)
		{
			// set the object number for new objects
			var nextNumber = GetNextNumberMethod();
			if (nextNumber == null && obj.Nr == null) // no next number and no number --> error
				throw new ArgumentException($"The field 'Nr' is null or no nextNumber is given.");
			else if (Config.GetAttribute("forceNextNumber", false) || obj.Nr == null) // force the next number or there is no number
				obj["Nr"] = application.Objects.GetNextNumber(obj.Typ, nextNumber, data);
			else  // check the number format
				application.Objects.ValidateNumber(obj.Nr, nextNumber, data);
		} // func SetNextNumber

		protected void InsertNewObject(PpsObjectAccess obj, T data)
		{
			obj.IsRev = IsDataRevision(data);
			SetNextNumber(application.Database.GetDatabaseAsync().AwaitTask(), obj, data);

			// insert the new object
			obj.Update(true);
			obj.UpdateData(new Action<Stream>(dst => WriteDataToStream(data, dst)));
			obj.Update(false);
		} // proc InsertNewObject

		/// <summary>Persist the data in the server</summary>
		/// <param name="transaction">Transaction to the database.</param>
		/// <param name="obj">Object information.</param>
		/// <param name="data">Data to push</param>
		/// <param name="release">Has this data a release request.</param>
		/// <returns></returns>
		protected virtual bool PushData(PpsObjectAccess obj, T data, bool release)
		{
			// set IsRev
			if (obj.IsNew)
				InsertNewObject(obj, data);

			// update database
			obj.Update(true);
			if (data != null)
				obj.UpdateData(new Action<Stream>(dst => WriteDataToStream(data, dst)));
			obj.Update(false);

			return true;
		} // func PushData

		[LuaMember("Push")]
		protected virtual bool LuaPush(PpsObjectAccess obj, object data, bool relase)
			=> PushData(obj, (T)data, relase);

		[
		DEConfigHttpAction("push", IsSafeCall = false),
		Description("Writes a new revision to the object store.")
		]
		private void HttpPushAction(IDEWebRequestScope ctx)
		{
			var currentUser = ctx.GetUser<IPpsPrivateDataContext>();

			try
			{
				// read header length
				var headerLength = ctx.GetProperty("ppsn-header-length", -1L);
				if (headerLength > 10 << 20 || headerLength < 10) // ignore greater than 10mb or smaller 10bytes (<object/>)
					throw new ArgumentOutOfRangeException("header-length");

				var pulledId = ctx.GetProperty("ppsn-pulled-revId", -1L);
				var releaseRequest = ctx.GetProperty("ppsn-release", false);
				
				var src = ctx.GetInputStream();

				// parse the object body
				XElement xObject;
				using (var headerStream = new WindowStream(src, 0, headerLength, false, true))
				using (var xmlHeader = XmlReader.Create(headerStream, Procs.XmlReaderSettings))
					xObject = XElement.Load(xmlHeader);

				// read the data
				using (var transaction = currentUser.CreateTransactionAsync(application.MainDataSource).AwaitTask())
				{
					// first the get the object data
					var obj = application.Objects.ObjectFromXml(transaction, xObject);
					VerfiyObjectType(obj);

					// revision to update
					if (obj.HeadRevId != -1 && obj.IsRev)
					{
						if (pulledId == -1)
							throw new ArgumentException("Pulled revId is missing.");
						else
							obj.SetRevision(pulledId);
					}

					// create and load the dataset
					var data = GetDataFromStream(src);

					// push data in the database
					if (PushData(obj, data, releaseRequest))
					{
						// write the object definition to client
						using (var tw = ctx.GetOutputTextWriter(MimeTypes.Text.Xml))
						using (var xml = XmlWriter.Create(tw, GetSettings(tw)))
							obj.ToXml(true).WriteTo(xml);
					}
					else
					{
						ctx.WriteSafeCall(
							new XElement("push",
								new XAttribute("headRevId", obj.HeadRevId),
								new XAttribute("pullRequest", Boolean.TrueString)
							)
						);
					}

					transaction.Commit();
				}
			}
			catch (HttpResponseException)
			{
				throw;
			}
			catch (Exception e)
			{
				Log.Except("Push failed.", e);
				ctx.WriteSafeCall(e);
			}
		} // proc HttpPushAction

		#endregion

		[LuaMember]
		public virtual string ObjectType => Name;
		public virtual string ObjectSource => null;
		public virtual string DefaultPane => null;

		public bool IsRevDefault => IsDataRevision(null);

		public PpsApplication Application => application;
	} // class PpsObjectItem

	#endregion

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Function to store and load object related data.</summary>
	public partial class PpsApplication
	{
		#region -- class PpsObjectsLibrary ----------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		public sealed class PpsObjectsLibrary : LuaTable
		{
			private readonly PpsApplication application;

			public PpsObjectsLibrary(PpsApplication application)
			{
				this.application = application;
			} // ctor

			/// <summary>todo</summary>
			/// <param name="nr"></param>
			/// <param name="nextNumber"></param>
			/// <param name="data"></param>
			[LuaMember(nameof(ValidateNumber))]
			public void ValidateNumber(string nr, object nextNumber, object data)
			{
				//throw new NotImplementedException("todo");
			} // proc ValidateNumber

			/// <summary>Gets the next number of an object class</summary>
			/// <param name="trans"></param>
			/// <param name="typ"></param>
			/// <returns></returns>
			[LuaMember(nameof(GetNextNumber))]
			public string GetNextNumber(string typ, object nextNumber, object data)
			{
				// get the highest number
				var args = Procs.CreateLuaTable(
					new PropertyValue("sql", "select max(Nr) from dbo.Objk where [Typ] = @Typ")
				);
				args[1] = Procs.CreateLuaTable(new PropertyValue("Typ", typ));

				var row = application.Database.Main.ExecuteSingleRow(args);

				string nr;
				if (nextNumber == null || nextNumber is int) // fill with zeros
				{
					if (row == null || row[0] == null)
						nr = "1"; // first number
					else if (Int64.TryParse(row[0].ToString(), out var i))
						nr = (i + 1L).ToString(CultureInfo.InvariantCulture);
					else
						throw new ArgumentException($"GetNextNumber failed, could not parse '{row[0]}' to number.");

					if (nextNumber != null)
						nr = nr.PadLeft((int)nextNumber, '0');
				}
				else if (nextNumber is string) // format mask
				{
					// V<YY><NR>
					throw new NotImplementedException();
				}
				else if (Lua.RtInvokeable(nextNumber)) // use a function
				{
					nr = new LuaResult(Lua.RtInvoke(nextNumber, row?[0], data)).ToString();
				}
				else
					throw new ArgumentException($"Unknown number format '{nextNumber}'.", "nextNumber");

				return nr;
			} // func GetNextNumber

			/// <summary>Create a complete new object.</summary>
			/// <param name="trans"></param>
			/// <param name="args"></param>
			/// <returns></returns>
			[LuaMember]
			public PpsObjectAccess CreateNewObject(LuaTable args)
				=> new PpsObjectAccess(application, new LuaTableProperties(args));

			/// <summary>Opens a object for an update operation.</summary>
			/// <param name="trans"></param>
			/// <param name="x"></param>
			/// <returns></returns>
			[LuaMember]
			public PpsObjectAccess ObjectFromXml(PpsDataTransaction trans, XElement x)
			{
				var objectId = x.GetAttribute(nameof(PpsObjectAccess.Id), -1L);
				var objectGuid = x.GetAttribute(nameof(PpsObjectAccess.Guid), Guid.Empty);

				// try to find the object in the database
				var obj = (PpsObjectAccess)null;
				if (objectId > 0)
					obj = GetObject(new LuaTable { { nameof(PpsObjectAccess.Id), objectId } });
				else if (objectGuid != Guid.Empty)
					obj = GetObject(new LuaTable { { nameof(PpsObjectAccess.Guid), objectGuid } });

				// create a new object
				if (obj == null)
				{
					obj = new PpsObjectAccess(application, PropertyDictionary.EmptyReadOnly);
					if (objectId > 0)
						throw new ArgumentOutOfRangeException(nameof(objectId), objectId, "Could not found object.");
					if (objectGuid != Guid.Empty)
						obj[nameof(PpsObjectAccess.Guid)] = objectGuid;
				}

				// update the values
				// Do not use CurRev and HeadRev from xml
				if (x.TryGetAttribute<string>(nameof(PpsObjectAccess.Nr), out var nr))
					obj[nameof(PpsObjectAccess.Nr)] = nr;
				if (x.TryGetAttribute<string>(nameof(PpsObjectAccess.Typ), out var typ))
					obj[nameof(PpsObjectAccess.Typ)] = typ;
				if (x.TryGetAttribute<string>(nameof(PpsObjectAccess.MimeType), out var mimeType))
					obj[nameof(PpsObjectAccess.MimeType)] = mimeType;

				// ToDo: maybe provide an Interface and merge these two functions
				// update tags
				void UpdateTags(string tagName, IEnumerable<PpsObjectTagAccess> currentTags, Func<XElement, PpsObjectTagAccess> addTag)
				{
					var removeList = new List<PpsObjectTagAccess>(currentTags);
					foreach (var c in x.Elements(tagName))
					{
						var idx = removeList.IndexOf(addTag(c));
						if (idx != -1)
							removeList.RemoveAt(idx);
					}
					removeList.ForEach(c => c.Remove());
				}

				// update the links
				void UpdateLinks(string tagName, IEnumerable<PpsObjectLinkAccess> currentLinks, Func<XElement, PpsObjectLinkAccess> addLink)
				{
					var removeList = new List<PpsObjectLinkAccess>(currentLinks);
					foreach (var c in x.Elements(tagName))
					{
						var idx = removeList.IndexOf(addLink(c));
						if (idx != -1)
							removeList.RemoveAt(idx);
					}
					removeList.ForEach(c => c.Remove());
				} // proc UpdateLinks

				UpdateLinks("linksTo", obj.LinksTo, obj.AddLinkTo);
				UpdateTags("tag", obj.Tags, obj.AddTag);

				return obj;
			} // func ObjectFromXml

			[LuaMember]
			public PpsDataSelector GetObjectSelector()
			{
				var selector = application.GetViewDefinition("dbo.objects").SelectorToken;
				return selector.CreateSelector(application.Database.Main.Connection);
			} // func GetObjectSelector

			private PpsObjectAccess GetObject(Func<PpsDataSelector, PpsDataSelector> applyFilter)
			{
				// create selector
				var selector = applyFilter(GetObjectSelector());

				// return only the first object
				var r = selector.Select(c => new SimpleDataRow(c)).FirstOrDefault();
				return r == null ? null : new PpsObjectAccess(application, r);
			} // func GetObject

			[LuaMember]
			public PpsObjectAccess GetObject(long id)
				=> GetObject(s => s.ApplyFilter(PpsDataFilterExpression.Compare("Id", PpsDataFilterCompareOperator.Equal, id)));

			[LuaMember]
			public PpsObjectAccess GetObject(Guid guid)
				=> GetObject(s => s.ApplyFilter(PpsDataFilterExpression.Compare("Guid", PpsDataFilterCompareOperator.Equal, guid)));

			/// <summary>Returns object data.</summary>
			/// <param name="args"></param>
			/// <returns></returns>
			[LuaMember(nameof(GetObject))]
			public PpsObjectAccess GetObject(LuaTable args)
				=> GetObject(s =>
				{
					// append filter
					if (args.TryGetValue<long>(nameof(PpsObjectAccess.Id), out var objectId))
						return s.ApplyFilter(PpsDataFilterExpression.Compare("Id", PpsDataFilterCompareOperator.Equal, objectId));
					else if (args.TryGetValue<Guid>(nameof(PpsObjectAccess.Guid), out var guidId))
						return s.ApplyFilter(PpsDataFilterExpression.Compare("Guid", PpsDataFilterCompareOperator.Equal, guidId));
					else
						throw new ArgumentNullException(nameof(PpsObjectAccess.Id), "Id or Guid needed to select an object.");
				});

			/// <summary>Returns a view on the objects.</summary>
			/// <param name="trans"></param>
			/// <returns></returns>
			[LuaMember(nameof(GetObjects))]
			public IEnumerable<PpsObjectAccess> GetObjects()
			{
				foreach (var r in GetObjectSelector())
					yield return new PpsObjectAccess(application, r);
			} // func GetObjects
		} // class PpsObjectsLibrary

		#endregion

		#region -- class PpsDatabaseLibrary ---------------------------------------------

		public sealed class PpsDatabaseLibrary : LuaTable
		{
			private readonly PpsApplication application;

			public PpsDatabaseLibrary(PpsApplication application)
			{
				this.application = application;
			} // ctor

			public async Task<PpsDataTransaction> GetDatabaseAsync(string name = null)
			{
				var scope = DEScope.GetScopeService<IDECommonScope>(true);

				// find existing source
				var dataSource = name == null ? application.MainDataSource : application.GetDataSource(name, true);
				if (scope.TryGetGlobal<PpsDataTransaction>(this, dataSource, out var trans))
					return trans;

				// get datasource
				var user = DEScope.GetScopeService<IPpsPrivateDataContext>(true);

				// create and register transaction
				trans = await user.CreateTransactionAsync(dataSource, true);

				scope.RegisterCommitAction(new Action(trans.Commit));
				scope.RegisterRollbackAction(new Action(trans.Rollback));
				scope.RegisterDispose(trans);

				scope.SetGlobal(this, dataSource, trans);

				return trans;
			} // func GetDatabaseAsync
			
			[LuaMember]
			public PpsDataTransaction GetDatabase(string name = null)
				=> GetDatabaseAsync(name).AwaitTask();

			[LuaMember]
			public PpsDataTransaction Main => GetDatabase();
		} // class PpsDatabaseLibrary

		#endregion

		#region -- class PpsHttpLibrary -------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class PpsHttpLibrary : LuaTable
		{
			private readonly PpsApplication application;

			public PpsHttpLibrary(PpsApplication application)
			{
				this.application = application;
			} // ctor

			private static IDEWebRequestScope CheckContextArgument(IDEWebRequestScope r)
			{
				if (r == null)
					throw new ArgumentNullException("r", "No context given.");
				return r;
			} // func CheckContextArgument

			/// <summary>Creates a XmlWriter for the output stream</summary>
			/// <param name="r"></param>
			/// <returns></returns>
			[LuaMember]
			public static XmlWriter CreateXmlWriter()
			{
				var r = DEScope.GetScopeService<IDEWebRequestScope>(true);
				CheckContextArgument(r);
				return XmlWriter.Create(r.GetOutputTextWriter(MimeTypes.Text.Xml, r.Http.DefaultEncoding), Procs.XmlWriterSettings);
			} // func CreateXmlWriter

			/// <summary>Creates a XmlReader for the input stream.</summary>
			/// <param name="r"></param>
			/// <returns></returns>
			[LuaMember]
			public static XmlReader CreateXmlReader()
			{
				var r = DEScope.GetScopeService<IDEWebRequestScope>(true);
				CheckContextArgument(r);
				return XmlReader.Create(r.GetInputTextReader(), Procs.XmlReaderSettings);
			} // func CreateXmlReader

			/// <summary>Writes the XElement in the output stream.</summary>
			/// <param name="r"></param>
			/// <param name="x"></param>
			[LuaMember]
			public static void WriteXml(XElement x)
			{
				using (var xml = CreateXmlWriter())
					x.WriteTo(xml);
			} // proc WriteXml

			/// <summary>Gets the input stream as an XElement.</summary>
			/// <param name="r"></param>
			/// <returns></returns>
			[LuaMember]
			public static XElement GetXml()
			{
				using (var xml = CreateXmlReader())
					return XElement.Load(xml);
			} // proc WriteXml

			/// <summary>Write the table in the output stream.</summary>
			/// <param name="r"></param>
			/// <param name="t"></param>
			[LuaMember]
			public static void WriteTable(LuaTable t)
				=> WriteXml(t.ToXml());

			/// <summary>Gets the input stream as an lua-table.</summary>
			/// <param name="r"></param>
			/// <returns></returns>
			[LuaMember]
			public static LuaTable GetTable()
				=> Procs.CreateLuaTable(GetXml());
		} // class PpsHttpLibrary

		#endregion

		private readonly PpsObjectsLibrary objectsLibrary;
		private readonly PpsDatabaseLibrary databaseLibrary;
		private readonly PpsHttpLibrary httpLibrary;

		/// <summary>Library for access the object store.</summary>
		[LuaMember("Db")]
		public PpsDatabaseLibrary Database => databaseLibrary;

		/// <summary>Library for access the object store.</summary>
		[LuaMember]
		public PpsObjectsLibrary Objects => objectsLibrary;
		/// <summary>Library for easy creation of http-results.</summary>
		[LuaMember]
		public LuaTable Http => httpLibrary;
	} // class PpsApplication
}
