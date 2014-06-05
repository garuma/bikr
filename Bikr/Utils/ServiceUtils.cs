using System;
using System.Linq;
using System.Collections.Generic;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Gms.Common;

namespace Bikr
{
	public static class ServiceUtils
	{
		public static void ResolveConnectionFailed (ConnectionResult connectionResult)
		{
			// TODO: resolution needs an activity to implement error resolution, need to work on that
			// for now we just log the error
			Android.Util.Log.Error ("ServiceUtils", "Connection error, {0}", connectionResult.ErrorCode);
		}
	}
}

