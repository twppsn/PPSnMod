﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Neo.IronLua;
using TecWare.DE.Server;
using TecWare.DE.Stuff;
using TecWare.PPSn.Server.Wpf;

namespace TecWare.PPSn.Server
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsPackage : DEConfigLogItem, IWpfClientApplicationFileProvider
	{
		private const string LuaApplicationFiles = "ApplicationFiles";

		public PpsPackage(IServiceProvider sp, string name)
			: base(sp, name)
		{
		} // ctor

		public virtual IEnumerable<PpsApplicationFileItem> GetApplicationFiles()
		{
			// check the LuaTable "ApplicationFiles"
			var applicationFileProvider = GetMemberValue(LuaApplicationFiles, rawGet: true) as LuaTable;

			if (applicationFileProvider != null)
			{
				foreach (var c in applicationFileProvider)
				{
					var item = c.Value as LuaTable;
					if (item != null)
					{
						var path = item.GetOptionalValue<string>("path", String.Empty);
						if (!String.IsNullOrEmpty(path))
						{
							path = Name + "/" + path;

							yield return new PpsApplicationFileItem(path,
								item.ReturnOptionalValue<long>("length", -1),
								item.ReturnOptionalValue<DateTime>("lastModified", DateTime.Now)
							);
						}
					}
				}
			}
		} // func GetApplicationFiles

		protected override object OnIndex(object key)
		{
			var value = base.OnIndex(key);
			if (value == null && key is string && (string)key == LuaApplicationFiles)
				value = SetMemberValue(LuaApplicationFiles, new LuaTable(), rawSet: true);
			return value;
		} // func OnIndex
	} // class PpsPackage
}
