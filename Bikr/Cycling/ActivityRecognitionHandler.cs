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
using Android.Gms.Common.Apis;

namespace Bikr
{
	public enum TrackingDelay {
		Long = 60000,
		Short = 10000
	}

	public class ActivityRecognitionHandler : Java.Lang.Object, IGoogleApiClientConnectionCallbacks, IGoogleApiClientOnConnectionFailedListener
	{
		Context context;

		TrackingDelay desiredDelay = TrackingDelay.Long;
		IGoogleApiClient client;
		IActivityRecognitionApi api;
		ConnectionUpdateRequest currentRequest;
		PendingIntent callbackIntent;

		public ActivityRecognitionHandler (Context context)
		{
			this.context = context;
			this.api = ActivityRecognition.ActivityRecognitionApi;
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
				client = CreateGoogleClient ();
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
				api.RequestActivityUpdates (client, (int)desiredDelay, callbackIntent);
				Log.Info ("ActRecognition", "Enabling activity updates w/ {0}", desiredDelay.ToString ());
			} else {
				api.RemoveActivityUpdates (client, callbackIntent);
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
				client = CreateGoogleClient ();
				client.Connect ();
			}
		}

		public void OnConnectionFailed (ConnectionResult connectionResult)
		{
			ServiceUtils.ResolveConnectionFailed (connectionResult);
		}

		public void OnConnectionSuspended (int cause)
		{
			ServiceUtils.ResolveConnectionSuspended (cause);
		}

		IGoogleApiClient CreateGoogleClient ()
		{
			return new GoogleApiClientBuilder (context, this, this)
				.AddApi (ActivityRecognition.Api)
				.Build ();
		}
	}
}

