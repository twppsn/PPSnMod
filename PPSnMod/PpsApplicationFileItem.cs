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

namespace TecWare.PPSn.Server
{
	#region -- class PpsApplicationFileItem -------------------------------------------

	public sealed class PpsApplicationFileItem
	{
		private readonly string path;
		private readonly long length;
		private readonly DateTime lastWriteTime;

		public PpsApplicationFileItem(string path, long length, DateTime lastWriteTime)
		{
			this.path = path;
			this.length = length;
			this.lastWriteTime = lastWriteTime.Ticks == 0 ? lastWriteTime : new DateTime(lastWriteTime.Ticks - (lastWriteTime.Ticks % TimeSpan.TicksPerSecond), lastWriteTime.Kind);
		} // ctor

		public string Path => path;
		public long Length => length;
		public DateTime LastWriteTime => lastWriteTime;
	} // class PpsApplicationFileItem

	#endregion
}
