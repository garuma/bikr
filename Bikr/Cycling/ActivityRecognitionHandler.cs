using System;
using System.Linq;
using System.Collections.Generic;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Graphics;
using Android.Util;
using Android.Animation;

using Run = Java.Lang.Runnable;

using Android.Gms.Location;
using Android.Gms.Common;

namespace Bikr
{
	public enum TrackingDelay {
		Long = 60000,
		Short = 10000
	}

	public class ActivityRecognitionHandler : Java.Lang.Object, IGooglePlayServicesClientConnectionCallbacks, IGooglePlayServicesClientOnConnectionFailedListener
	{
		Context context;

		TrackingDelay desiredDelay = TrackingDelay.Long;
		ActivityRecognitionClient client;
		ConnectionUpdateRequest currentRequest;
		PendingIntent callbackIntent;

		public ActivityRecognitionHandler (Context context)
		{
			this.context = context;
		}

		public void StopCurrentLocationTracking ()
		{
			context.StartService (new Intent (BikrActivityService.FinishTripAction,
			                                  Android.Net.Uri.Empty,
			                                  context,
			                                  typeof(BikrActivityService)));
		}

		public void SetTrackingEnabled (bool enabled, TrackingDelay desiredDelay = TrackingDelay.Long)
		{
			this.desiredDelay = desiredDelay;
			if (!enabled)
				StopCurrentLocationTracking ();
			if (currentRequest != ConnectionUpdateRequest.None)
				return;
			currentRequest = enabled ? ConnectionUpdateRequest.Start : ConnectionUpdateRequest.Stop;
			if (client == null)
				client = new ActivityRecognitionClient (context, this, this);
			if (!(client.IsConnected || client.IsConnecting))
				client.Connect ();
		}

		public void OnConnected (Bundle p0)
		{
			Log.Debug ("ActRecognition", "Connected");
			if (currentRequest == ConnectionUpdateRequest.None)
				return;

			if (callbackIntent == null) {
				var intent = new Intent (context, typeof(BikrActivityService));
				callbackIntent = PendingIntent.GetService (context, Resource.Id.bikr_intent_activity, intent, PendingIntentFlags.UpdateCurrent);
			}
			if (currentRequest == ConnectionUpdateRequest.Start) {
				client.RequestActivityUpdates ((int)desiredDelay, callbackIntent);
				Log.Info ("ActRecognition", "Enabling activity updates w/ {0}", desiredDelay.ToString ());
			} else {
				client.RemoveActivityUpdates (callbackIntent);
				Log.Info ("ActRecognition", "Disabling activity updates");
			}
			currentRequest = ConnectionUpdateRequest.None;
			client.Disconnect ();
		}

		public void OnDisconnected ()
		{
			Log.Debug ("ActRecognition", "Disconnected");
			client = null;
			// If the client was disconnected too early
			if (currentRequest != ConnectionUpdateRequest.None) {
				client = new ActivityRecognitionClient (context, this, this);
				client.Connect ();
			}
		}

		public void OnConnectionFailed (ConnectionResult connectionResult)
		{
			ServiceUtils.ResolveConnectionFailed (connectionResult);
		}
	}
}

