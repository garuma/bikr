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
	[Service (Name = "bikr.ManagerService",
	          Label = "Bikr Manager Service",
	          Exported = false)]
	public class ManagerService : Service, IGooglePlayServicesClientConnectionCallbacks, IGooglePlayServicesClientOnConnectionFailedListener, ISharedPreferencesOnSharedPreferenceChangeListener
	{
		const string StopTrackingAction = "BikrActionStopTracking";
		const int LongTrackingDelay = 60000;
		const int ShortTrackingDelay = 10000;

		ActivityRecognitionClient client;
		ConnectionUpdateRequest currentRequest;
		PreferenceManager prefs;
		BikingState previousState;
		PendingIntent callbackIntent;
		int desiredDelay = LongTrackingDelay;
		Notification.Builder currentBuilder;

		public override void OnCreate ()
		{
			base.OnCreate ();
			SetTrackingEnabled (true);
			previousState = BikingState.NotBiking;
			prefs = new PreferenceManager (this);
			prefs.RegisterListener (this);
			StopCurrentLocationTracking ();
		}

		public override StartCommandResult OnStartCommand (Intent intent, StartCommandFlags flags, int startId)
		{
			if (intent != null && intent.Action == StopTrackingAction)
				StopCurrentLocationTracking ();
			return base.OnStartCommand (intent, flags, startId);
		}

		public override void OnDestroy ()
		{
			SetTrackingEnabled (false);
			prefs.RemoveListener (this);
			base.OnDestroy ();
		}

		public override IBinder OnBind (Intent intent)
		{
			return null;
		}

		void StopCurrentLocationTracking ()
		{
			StartService (new Intent (BikrActivityService.FinishTripAction,
			                          Android.Net.Uri.Empty,
			                          this,
			                          typeof(BikrActivityService)));
		}

		void SetTrackingEnabled (bool enabled, int desiredDelay = LongTrackingDelay)
		{
			this.desiredDelay = desiredDelay;
			if (!enabled)
				StopCurrentLocationTracking ();
			if (currentRequest != ConnectionUpdateRequest.None)
				return;
			currentRequest = enabled ? ConnectionUpdateRequest.Start : ConnectionUpdateRequest.Stop;
			if (client == null)
				client = new ActivityRecognitionClient (this, this, this);
			if (!(client.IsConnected || client.IsConnecting))
				client.Connect ();
		}

		public void OnConnected (Bundle p0)
		{
			Log.Debug ("ActRecognition", "Connected");
			if (currentRequest == ConnectionUpdateRequest.None)
				return;

			if (callbackIntent == null) {
				var intent = new Intent (this, typeof(BikrActivityService));
				callbackIntent = PendingIntent.GetService (this, Resource.Id.bikr_intent_activity, intent, PendingIntentFlags.UpdateCurrent);
			}
			if (currentRequest == ConnectionUpdateRequest.Start) {
				client.RequestActivityUpdates (desiredDelay, callbackIntent);
				Log.Debug ("ActRecognition", "Enabling activity updates w/ {0}", desiredDelay.ToString ());
			} else {
				client.RemoveActivityUpdates (callbackIntent);
				Log.Debug ("ActRecognition", "Disabling activity updates");
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
				client = new ActivityRecognitionClient (this, this, this);
				client.Connect ();
			}
		}

		public void OnConnectionFailed (ConnectionResult connectionResult)
		{
			ServiceUtils.ResolveConnectionFailed (connectionResult);
		}

		public void OnSharedPreferenceChanged (ISharedPreferences sharedPreferences, string key)
		{
			if (key == "currentBikingState") {
				var newState = prefs.CurrentBikingState;

				if (previousState == BikingState.NotBiking && newState == BikingState.Biking) {
					// A biking trip was started, decrease activity recognition delay and show notification
					ShowNotification ();
					SetTrackingEnabled (true, desiredDelay: ShortTrackingDelay);
				} else if (previousState != BikingState.NotBiking && newState == BikingState.NotBiking) {
					// A biking trip finished, hide notification and restore old activity recognition delay
					HideNotification ();
					SetTrackingEnabled (true, LongTrackingDelay);
				} else if (previousState == BikingState.Biking
				           && (newState == BikingState.InGrace || newState == BikingState.MovingNotOnBike)) {
					// We were put in grace, update the currently shown notification
					UpdateNotification (inGrace: true);
				} else if ((previousState == BikingState.InGrace || previousState == BikingState.MovingNotOnBike)
				           && newState == BikingState.Biking) {
					// We were put out of grace, revert the notification to its old style
					UpdateNotification (inGrace: false);
				}

				previousState = newState;
			}
		}

		void ShowNotification ()
		{
			var manager = NotificationManager.FromContext (this);
			currentBuilder = new Notification.Builder (this)
				.SetContentTitle (Resources.GetString (Resource.String.notification_title_biking))
				.SetContentText (Resources.GetString (Resource.String.notification_subtitle_biking))
				.SetSmallIcon (Resource.Drawable.icon_notification)
				.SetUsesChronometer (true)
				.SetOngoing (true);
			var pending = PendingIntent.GetService (
				this,
				Resource.Id.bikr_intent_notification,
				new Intent (StopTrackingAction, Android.Net.Uri.Empty, this, typeof (ManagerService)),
				PendingIntentFlags.UpdateCurrent
			);
			currentBuilder.AddAction (Resource.Drawable.ic_notif_stop,
			                          Resources.GetString (Resource.String.stop_notif_action),
			                          pending);
			manager.Notify (Resource.Id.bikr_notification, currentBuilder.Build ());
		}

		void HideNotification ()
		{
			var manager = NotificationManager.FromContext (this);
			manager.CancelAll ();
			currentBuilder = null;
		}

		void UpdateNotification (bool inGrace = false)
		{
			if (currentBuilder == null)
				return;
			var manager = NotificationManager.FromContext (this);
			var title = inGrace ? Resource.String.notification_title_ingrace : Resource.String.notification_title_biking;
			var subtitle = inGrace ? Resource.String.notification_subtitle_ingrace : Resource.String.notification_subtitle_biking;
			currentBuilder
				.SetContentTitle (Resources.GetString (title))
				.SetContentText (Resources.GetString (subtitle));
			manager.Notify (Resource.Id.bikr_notification, currentBuilder.Build ());
		}
	}
}

