using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Util;
using Android.Locations;

using Android.Gms.Location;
using Android.Gms.Common;

namespace Bikr
{
	[Service (Name = "bikr.BikrActivityService",
	          Label = "Bikr Activity Recognition Service",
	          Exported = true)]
	public class BikrActivityService
		: Service, IGooglePlayServicesClientConnectionCallbacks, IGooglePlayServicesClientOnConnectionFailedListener
	{
		public const string FinishTripAction = "BikrActionFinishTrip";
		const int ExtremeConfidence = 85;
		const int StrongConfidence = 75;
		const int WeakConfidence = 25;

		static readonly DateTime Epoch = new DateTime (1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes (3);
		static readonly TimeSpan MovingNotBikePeriod = TimeSpan.FromMinutes (2);

		static PreferenceManager prefs;
		static PendingIntent callbackIntent;

		static LocationClient client;
		static ConnectionUpdateRequest currentRequest;
		static ActivityRecognitionHandler actRecognitionHandler;

		static Location lastLocation, prevStoredLocation;
		static double currentDistance;
		static long startFix, currentFix;

		static int graceID = int.MinValue;
		static BikingState currentBikingState;

		Notification.Builder currentBuilder;
		ServiceHandler serviceHandler;
		Looper serviceLooper;

		public override void OnCreate ()
		{
			base.OnCreate ();
			if (prefs == null)
				prefs = new PreferenceManager (this);
			var thread = new HandlerThread ("IntentService[BikrActivityService]");
			thread.Start ();
			serviceLooper = thread.Looper;
			serviceHandler = new ServiceHandler (this, serviceLooper);
		}

		public override StartCommandResult OnStartCommand (Intent intent, StartCommandFlags flags, int startId)
		{
			var msg = serviceHandler.ObtainMessage ();
			msg.Arg1 = startId;
			msg.Obj = intent;
			serviceHandler.SendMessage (msg);

			return StartCommandResult.NotSticky;
		}

		bool OnHandleIntent (Intent intent)
		{
			if (intent != null) {
				if (intent.Action == FinishTripAction)
					FinishTrip ();
				else if (ActivityRecognitionResult.HasResult (intent))
					HandleActivityRecognition (intent);
				else if (intent.HasExtra (LocationClient.KeyLocationChanged))
					HandleLocationUpdate (intent);
			}

			return currentBikingState != BikingState.NotBiking;
		}

		public override void OnDestroy ()
		{
			serviceLooper.Quit ();
		}

		public override IBinder OnBind (Intent intent)
		{
			return null;
		}

		void HandleActivityRecognition (Intent intent)
		{
			var result = ActivityRecognitionResult.ExtractResult (intent);
			var activity = result.MostProbableActivity;

			TripDebugLog.LogActivityEvent (activity.Type, activity.Confidence);

			// We don't care about tilting activity
			if (activity.Type == DetectedActivity.Tilting)
				return;

			var prevBikingState = currentBikingState;

			switch (currentBikingState) {
			case BikingState.NotBiking:
				if (activity.Type == DetectedActivity.OnBicycle && activity.Confidence >= StrongConfidence) {
					SetLocationUpdateEnabled (true);
					currentBikingState = BikingState.Biking;
				}
				break;
			case BikingState.Biking:
				CheckDelayedFinishTripConditions (activity);
				break;
			case BikingState.MovingNotOnBike:
				if (activity.Type == DetectedActivity.OnBicycle && activity.Confidence >= StrongConfidence) {
					currentBikingState = BikingState.Biking;
					Interlocked.Increment (ref graceID);
				}
				break;
			case BikingState.InGrace:
				if (activity.Type == DetectedActivity.OnBicycle && activity.Confidence >= WeakConfidence) {
					currentBikingState = BikingState.Biking;
					Interlocked.Increment (ref graceID);
				} else
					CheckDelayedFinishTripConditions (activity);
				break;
			}

			Log.Debug ("ActivityHandler", "Activity ({0} w/ {1}%): {2} -> {3}",
			           activity.Type, activity.Confidence, prevBikingState, currentBikingState);

			if (prevBikingState != currentBikingState)
				OnBikingStateChanged (prevBikingState, currentBikingState);
		}

		void CheckDelayedFinishTripConditions (DetectedActivity activity)
		{
			// Only increment the grace ID (thus invalidating existing delayed finish transactions
			// if the previous state was an active one.
			bool shouldIncrementGraceID =
				currentBikingState != BikingState.InGrace && currentBikingState != BikingState.MovingNotOnBike;

			// The service mistakenly thinks fast biking is a vehicule activity so we require an even stronger confidence
			if ((activity.Type == DetectedActivity.OnFoot && activity.Confidence > StrongConfidence)
			    || (activity.Type == DetectedActivity.InVehicle && activity.Confidence > ExtremeConfidence)) {
				var id = graceID;
				if (shouldIncrementGraceID)
					id = Interlocked.Increment (ref graceID);
				currentBikingState = BikingState.MovingNotOnBike;
				StartDelayedFinishTrip (id, (long)MovingNotBikePeriod.TotalMilliseconds);
			} else if ((activity.Type == DetectedActivity.OnBicycle && activity.Confidence < WeakConfidence)
			           || (activity.Type != DetectedActivity.OnBicycle && activity.Confidence >= StrongConfidence)) {
				var id = graceID;
				if (shouldIncrementGraceID)
					id = Interlocked.Increment (ref graceID);
				if (currentBikingState != BikingState.InGrace) {
					currentBikingState = BikingState.InGrace;
					StartDelayedFinishTrip (id, (long)GracePeriod.TotalMilliseconds);
				}
			}
		}

		void StartDelayedFinishTrip (int id, long timeout)
		{
			TripDebugLog.DeferredBikeTripEnd ();
			var handler = new Handler ();
			handler.PostDelayed (() => {
				if ((currentBikingState == BikingState.MovingNotOnBike || currentBikingState == BikingState.InGrace)
				    && id == graceID)
					FinishTrip ();
			}, timeout);
		}

		void HandleLocationUpdate (Intent intent)
		{
			var newLocation = intent.GetParcelableExtra (LocationClient.KeyLocationChanged).JavaCast<Location> ();
			if (lastLocation != null) {
				if (currentBikingState == BikingState.Biking || currentBikingState == BikingState.InGrace)
					currentDistance += lastLocation.DistanceTo (newLocation);
				currentFix = newLocation.Time;
			} else {
				startFix = newLocation.Time;
				lastLocation = prevStoredLocation;
				if (lastLocation != null)
					currentDistance += lastLocation.DistanceTo (newLocation);
				prevStoredLocation = null;
			}
			TripDebugLog.LogPositionEvent (newLocation.Latitude,
			                               newLocation.Longitude,
			                               lastLocation == null ? 0 : lastLocation.DistanceTo (newLocation),
			                               currentDistance);
			lastLocation = newLocation;
		}

		internal void FinishTrip ()
		{
			TripDebugLog.EndBikeTrip ();

			SetLocationUpdateEnabled (false);

			// Do we have a trip?
			if (lastLocation != null && currentDistance > 0 && currentFix > startFix) {
				var trip = new BikeTrip {
					Distance = currentDistance,
					StartTime = Epoch + TimeSpan.FromMilliseconds (startFix),
					EndTime = Epoch + TimeSpan.FromMilliseconds (currentFix)
				};
				var dataApi = DataApi.Obtain (this);
				dataApi.AddTrip (trip).Wait ();

				TripDebugLog.LogNewTrip (trip.EndTime - trip.StartTime, trip.Distance);
			}

			lastLocation = null;
			currentDistance = startFix = currentFix = 0;

			var oldState = currentBikingState;
			currentBikingState = BikingState.NotBiking;
			OnBikingStateChanged (oldState, currentBikingState);

			TripDebugLog.CommitTripLog ();
			StopSelf ();
		}

		void SetLocationUpdateEnabled (bool enabled)
		{
			if (currentRequest != ConnectionUpdateRequest.None)
				return;
			currentRequest = enabled ? ConnectionUpdateRequest.Start : ConnectionUpdateRequest.Stop;
			if (client == null)
				client = new LocationClient (this, this, this);
			if (!(client.IsConnected || client.IsConnecting))
				client.Connect ();
			if (enabled)
				TripDebugLog.StartBikeTrip ();
		}

		public void OnConnected (Bundle p0)
		{
			Log.Debug ("LocClient", "Connected");
			if (currentRequest == ConnectionUpdateRequest.None)
				return;

			if (callbackIntent == null) {
				var intent = new Intent (this, typeof(BikrActivityService));
				callbackIntent = PendingIntent.GetService (this, Resource.Id.bikr_intent_location, intent, PendingIntentFlags.UpdateCurrent);
			}
			if (currentRequest == ConnectionUpdateRequest.Start) {
				var req = new LocationRequest ()
					.SetInterval (5000)
					.SetFastestInterval (2000)
					.SetSmallestDisplacement (5)
					.SetPriority (LocationRequest.PriorityHighAccuracy);
				client.RequestLocationUpdates (req, callbackIntent);
				prevStoredLocation = client.LastLocation;
				Log.Info ("LocClient", "Requested updates");
			} else {
				client.RemoveLocationUpdates (callbackIntent);
				prevStoredLocation = null;
				Log.Info ("LocClient", "Finished updates");
			}
			currentRequest = ConnectionUpdateRequest.None;
			client.Disconnect ();
		}

		public void OnDisconnected ()
		{
			Log.Debug ("LocClient", "Disconnected");
			client = null;
			// If the client was disconnected too early
			if (currentRequest != ConnectionUpdateRequest.None) {
				client = new LocationClient (this, this, this);
				client.Connect ();
			}
		}

		public void OnConnectionFailed (ConnectionResult connectionResult)
		{
			ServiceUtils.ResolveConnectionFailed (connectionResult);
		}

		void OnBikingStateChanged (BikingState previousState, BikingState newState)
		{
			if (previousState == BikingState.NotBiking && newState == BikingState.Biking) {
				// A biking trip was started, decrease activity recognition delay and show notification
				ShowNotification ();
				if (actRecognitionHandler == null)
					actRecognitionHandler = new ActivityRecognitionHandler (this);
				actRecognitionHandler.SetTrackingEnabled (true, desiredDelay: TrackingDelay.Short);
			} else if (previousState != BikingState.NotBiking && newState == BikingState.NotBiking) {
				// A biking trip finished, hide notification and restore old activity recognition delay
				HideNotification ();
				if (actRecognitionHandler == null)
					actRecognitionHandler = new ActivityRecognitionHandler (this);
				actRecognitionHandler.SetTrackingEnabled (true, desiredDelay: TrackingDelay.Long);
			} else if (previousState == BikingState.Biking
			           && (newState == BikingState.InGrace || newState == BikingState.MovingNotOnBike)) {
				// We were put in grace, update the currently shown notification
				UpdateNotification (inGrace: true);
			} else if ((previousState == BikingState.InGrace || previousState == BikingState.MovingNotOnBike)
			           && newState == BikingState.Biking) {
				// We were put out of grace, revert the notification to its old style
				UpdateNotification (inGrace: false);
			}
		}

		void ShowNotification ()
		{
			currentBuilder = new Notification.Builder (this)
				.SetContentTitle (Resources.GetString (Resource.String.notification_title_biking))
				.SetContentText (Resources.GetString (Resource.String.notification_subtitle_biking))
				.SetSmallIcon (Resource.Drawable.icon_notification)
				.SetUsesChronometer (true)
				.SetOngoing (true);
			var pending = PendingIntent.GetService (
				this,
				Resource.Id.bikr_intent_notification,
				new Intent (FinishTripAction, Android.Net.Uri.Empty, this, typeof (BikrActivityService)),
				PendingIntentFlags.UpdateCurrent
			);
			currentBuilder.AddAction (Resource.Drawable.ic_notif_stop,
			                          Resources.GetString (Resource.String.stop_notif_action),
			                          pending);
			StartForeground (Resource.Id.bikr_notification, currentBuilder.Build ());
		}

		void HideNotification ()
		{
			StopForeground (true);
			currentBuilder = null;
		}

		void UpdateNotification (bool inGrace = false)
		{
			if (currentBuilder == null)
				return;
			var title = inGrace ? Resource.String.notification_title_ingrace : Resource.String.notification_title_biking;
			var subtitle = inGrace ? Resource.String.notification_subtitle_ingrace : Resource.String.notification_subtitle_biking;
			currentBuilder
				.SetContentTitle (Resources.GetString (title))
				.SetContentText (Resources.GetString (subtitle));
			StartForeground (Resource.Id.bikr_notification, currentBuilder.Build ());
		}

		class ServiceHandler : Handler
		{
			BikrActivityService svc;

			public ServiceHandler (BikrActivityService svc, Looper looper) : base (looper)
			{
				this.svc = svc;
			}

			public override void HandleMessage (Message msg)
			{
				if (!svc.OnHandleIntent ((Intent)msg.Obj))
					svc.StopSelf (msg.Arg1);
			}
		}
	}
}

