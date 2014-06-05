
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Run = Java.Lang.Runnable;

namespace Bikr
{
	public class CirclesFragment : Android.Support.V4.App.Fragment, ViewTreeObserver.IOnGlobalLayoutListener
	{
		int[] circleIds = new int[] {
			Resource.Id.dayCircle,
			Resource.Id.weekCircle,
			Resource.Id.monthCircle,
		};

		Handler handler;
		PreferenceManager prefs;

		TextView lastTrip;
		View notificationPanel;
		View rideInfoPanel;
		View circlesLayout;

		CircleBadge[] CircleBadges {
			get {
				return new int[] {
					Resource.Id.dayCircle,
					Resource.Id.weekCircle,
					Resource.Id.monthCircle,
				}.Select (id => View.FindViewById<CircleBadge> (id)).ToArray ();
			}
		}

		public event EventHandler CirclesReady;

		public override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);

			handler = new Handler ();
		}

		public override void OnAttach (Android.App.Activity activity)
		{
			base.OnAttach (activity);
			if (prefs == null)
				prefs = new PreferenceManager (activity);
		}

		public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
		{
			var view = inflater.Inflate (Resource.Layout.CirclesLayout, container, false);

			circlesLayout = view.FindViewById (Resource.Id.circlesLayout);
			notificationPanel = view.FindViewById (Resource.Id.notificationPanel);
			rideInfoPanel = view.FindViewById (Resource.Id.rideInfoPanel);
			lastTrip = view.FindViewById<TextView> (Resource.Id.lastTripText);

			return view;
		}

		public override void OnViewCreated (View view, Bundle savedInstanceState)
		{
			base.OnViewCreated (view, savedInstanceState);

			var circles = CircleBadges;
			circles[0].Distance = prefs.LastDayMeasure;
			circles[1].Distance = prefs.LastWeekMeasure;
			circles[2].Distance = prefs.LastMonthMeasure;

			circlesLayout.ViewTreeObserver.AddOnGlobalLayoutListener (this);
		}

		public void OnGlobalLayout ()
		{
			circlesLayout.ViewTreeObserver.RemoveGlobalOnLayoutListener (this);
			if (CirclesReady != null)
				CirclesReady (this, EventArgs.Empty);
		}

		public void SetupInitialAnimations (Action callback = null)
		{
			int delay = 10;
			var time = Resources.GetInteger (Android.Resource.Integer.ConfigMediumAnimTime);
			var delayIncr = (3 * time) / 4;
			var interpolator = new Android.Views.Animations.DecelerateInterpolator ();
			ViewPropertyAnimator circleAnim = null;

			foreach (var id in circleIds) {
				var circle = circlesLayout.FindViewById (id);

				circle.ScaleX = .3f;
				circle.ScaleY = .3f;
				circle.Alpha = 0;

				circleAnim = circle.Animate ()
					.ScaleX (1)
					.ScaleY (1)
					.Alpha (1)
					.SetStartDelay (delay)
					.SetDuration (time)
					.SetInterpolator (interpolator);

				var last = id == circleIds.Last ();
				if (last && callback != null)
					circleAnim.WithEndAction (new Run (callback));

				circleAnim.Start ();
				delay += delayIncr;
			}
		}

		public void ShowLastTripNotification (TimeSpan d, int count, double distance)
		{
			var tt = d.ToString (d.Hours > 0 ? "hh\\hmm" : "mm\\m\\i\\n");
			var dispDistance = prefs.GetDisplayDistance (distance) + prefs.GetUnitForDistance (distance);
			string msg = null;
			if (count <= 1)
				msg = Resources.GetString (Resource.String.single_trip_notification);
			else
				msg = Resources.GetString (Resource.String.multiple_trip_notification);
			msg = string.Format (msg, count);
			ShowNotification (msg, duration: tt, distance: dispDistance, delay: 2500);
		}

		public void ShowNotification (string notification, string duration = null, string distance = null, int delay = 0)
		{
			notificationPanel.TranslationY =
				notificationPanel.Height + ((LinearLayout.LayoutParams)notificationPanel.LayoutParameters).BottomMargin;
			notificationPanel.TranslationX = 0;
			notificationPanel.ScaleX = .9f;
			notificationPanel.Visibility = ViewStates.Visible;
			lastTrip.Text = notification;
			if (duration == null || distance == null) {
				rideInfoPanel.Visibility = ViewStates.Invisible;
				lastTrip.TranslationY = TypedValue.ApplyDimension (ComplexUnitType.Dip, 8, Resources.DisplayMetrics);
			} else {
				rideInfoPanel.Visibility = ViewStates.Visible;
				rideInfoPanel.FindViewById<TextView> (Resource.Id.timeText).Text = duration;
				rideInfoPanel.FindViewById<TextView> (Resource.Id.distanceText).Text = distance;
				lastTrip.TranslationY = 0;
			}
			var time = Resources.GetInteger (Android.Resource.Integer.ConfigLongAnimTime);
			var decel = new Android.Views.Animations.DecelerateInterpolator ();

			notificationPanel.Animate ().TranslationY (0).ScaleX (1).SetDuration (time).SetStartDelay (delay).SetInterpolator (decel).WithEndAction (new Run (() => {
				var accel = new Android.Views.Animations.AccelerateInterpolator ();
				notificationPanel.Animate ()
					.TranslationX (-Resources.DisplayMetrics.WidthPixels)
					.SetDuration (time)
					.SetStartDelay (6000)
					.SetInterpolator (accel)
					.WithEndAction (new Run (() => notificationPanel.Visibility = ViewStates.Invisible))
					.Start ();
			})).Start ();
		}

		public async void LoadData (Context context)
		{
			try {
				var circles = CircleBadges;

				var dataApi = DataApi.Obtain (context);
				var stats = await dataApi.GetStats ();
				/*var stats = new TripDistanceStats {
					PrevDay = 4500,
					PrevWeek = 25000,
					PrevMonth = 100000,
					Daily = 2800,
					Weekly = 12000,
					Monthly = 30000,
				};*/
				var mapping = new Dictionary<CircleBadge, double> {
					{ circles[0], stats.Daily },
					{ circles[1], stats.Weekly },
					{ circles[2], stats.Monthly },
				};
				foreach (var map in mapping) {
					if (prefs.GetDisplayDistance (map.Value) != prefs.GetDisplayDistance (map.Key.Distance))
						map.Key.SetDistanceAnimated (map.Value, startDelay: 100);
				}
				prefs.SetLastMeasure ("day", stats.Daily);
				prefs.SetLastMeasure ("week", stats.Weekly);
				prefs.SetLastMeasure ("month", stats.Monthly);

				var lastTrips = await dataApi.GetTripsAfter (prefs.LastCheckin);
				/*var lastTrips = new List<BikeTrip> () {
					new BikeTrip { EndTime = DateTime.Now, StartTime = DateTime.Now.AddMinutes (-30), Distance = 3000 }
				};*/
				if (lastTrips != null && lastTrips.Any ())
					ShowLastTripNotification (TimeSpan.FromSeconds (lastTrips.Sum (bt => (bt.EndTime - bt.StartTime).TotalSeconds)),
					                          lastTrips.Count,
					                          lastTrips.Sum (t => t.Distance));
				SetCompletionLevel (circles[0], stats.Daily, stats.PrevDay);
				SetCompletionLevel (circles[1], stats.Weekly, stats.PrevWeek);
				SetCompletionLevel (circles[2], stats.Monthly, stats.PrevMonth);
				prefs.LastCheckin = DateTime.UtcNow;
			} catch (Exception e) {
				Log.Error ("DataStats", e.ToString ());
			}
		}

		void SetCompletionLevel (CircleBadge badge, double actual, double prev)
		{
			var ratio = prev > 0 ? Math.Min (1, actual / prev) : 0;
			handler.PostDelayed (() => badge.SetCompletionRatioAnimated (ratio), 600);
		}
	}
}

