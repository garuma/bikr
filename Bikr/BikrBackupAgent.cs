using System;

using Android.App.Backup;

namespace Bikr
{
	public class BikrBackupAgent : BackupAgentHelper
	{
		const string DbPrefix = "tripdb_";

		public override void OnCreate ()
		{
			Android.Util.Log.Debug ("BikrBackup", "Created backup agent");
			var dataApi = DataApi.Obtain (this);
			var dbBackupHelper = new FileBackupHelper (this, dataApi.DbPath);
			AddHelper (DbPrefix, dbBackupHelper);
		}
	}
}

