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
	enum BikingState {
		NotBiking,
		Biking,
		MovingNotOnBike,
		InGrace,
	}

	[Service (Name = "bikr.BikrActivityService",
	          Label = "Bikr Activity Recognition Service",
	          Exported = true)]
	public class BikrActivityService
		: IntentService, IGooglePlayServicesClientConnectionCallbacks, IGooglePlayServicesClientOnConnectionFailedListener
	{
		public const string FinishTripAction = "BikrActionFinishTrip";
		const int ExtremeConfidence = 85;
		const int StrongConfidence = 75;
		const int WeakConfidence = 25;

		static readonly DateTime Epoch = new DateTime (1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes (3);
		static readonly TimeSpan MovingNotBikePeriod = TimeSpan.FromMinutes (2);

		static Handler handler;
		static PreferenceManager prefs;
		static PendingIntent callbackIntent;

		static LocationClient client;
		static ConnectionUpdateRequest currentRequest;

		static Location lastLocation, prevStoredLocation;
		static double currentDistance;
		static long startFix, currentFix;

		static int graceID = int.MinValue;
		static BikingState currentBikingState;

		public override void OnCreate ()
		{
			base.OnCreate ();
			if (prefs == null)
				prefs = new PreferenceManager (this);
			if (handler == null)
				handler = new Handler ();
		}

		protected override void OnHandleIntent (Intent intent)
		{
			if (intent == null)
				return;
			if (intent.Action == FinishTripAction)
				FinishTrip ();
			else if (ActivityRecognitionResult.HasResult (intent))
				HandleActivityRecognition (intent);
			else if (intent.HasExtra (LocationClient.KeyLocationChanged))
				HandleLocationUpdate (intent);
		}

		void HandleActivityRecognition (Intent intent)
		{
			var result = ActivityRecognitionResult.ExtractResult (intent);
			var activity = result.MostProbableActivity;

			TripDebugLog.LogActivityEvent (activity.Type, activity.Confidence);

			// We don't care about tilting activity
			if (activity.Type == DetectedActivity.Tilting)
				return;

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
			           activity.Type, activity.Confidence, prefs.CurrentBikingState, currentBikingState);
			prefs.CurrentBikingState = currentBikingState;
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
			handler.PostDelayed (() => {
				if ((currentBikingState == BikingState.MovingNotOnBike || currentBikingState == BikingState.InGrace)
				    && id == graceID) {

					prefs.CurrentBikingState = currentBikingState = BikingState.NotBiking;
					FinishTrip ();
				}
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
				//new Android.App.Backup.BackupManager (this).DataChanged ();

				TripDebugLog.LogNewTrip (trip.EndTime - trip.StartTime, trip.Distance);
			}

			lastLocation = null;
			currentDistance = startFix = currentFix = 0;
			prefs.CurrentBikingState = currentBikingState = BikingState.NotBiking;

			TripDebugLog.CommitTripLog ();
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
				Log.Debug ("LocClient", "Requested updates");
			} else {
				client.RemoveLocationUpdates (callbackIntent);
				prevStoredLocation = null;
				Log.Debug ("LocClient", "Finished updates");
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
	}
}

