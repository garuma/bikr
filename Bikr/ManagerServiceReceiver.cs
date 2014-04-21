using System;
using Android.Content;
using Android.App;

namespace Bikr
{
	[BroadcastReceiver]
	[IntentFilter (new[] { "android.intent.action.BOOT_COMPLETED", "android.intent.action.MY_PACKAGE_REPLACED" })]
	public class ManagerServiceReceiver : BroadcastReceiver
	{
		public override void OnReceive (Context context, Intent intent)
		{
			var prefs = new PreferenceManager (context);
			if (!prefs.FirstTimeAround && prefs.Enabled) {
				var serviceIntent = new Intent (context, typeof(ManagerService));
				context.StartService (serviceIntent);
			}
		}
	}
}

