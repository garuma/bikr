using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Util;
using Android.Animation;
using Android.Renderscripts;

using ConfigChanges = Android.Content.PM.ConfigChanges;
using Run = Java.Lang.Runnable;

using Android.Gms.Location;
using Android.Gms.Common;

namespace Bikr
{
	[Activity (Label = "Bikr",
	           MainLauncher = true,
	           Theme = "@style/BikrTheme",
	           ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
	public class MainActivity
		: Activity, ViewTreeObserver.IOnGlobalLayoutListener, IDialogInterfaceOnCancelListener
	{
		const int GooglePlayServiceResult = 1000;

		int[] circleIds = new int[] {
			Resource.Id.dayCircle,
			Resource.Id.weekCircle,
			Resource.Id.monthCircle,
		};

		Handler handler;
		PreferenceManager prefs;
		int delayCurrentId = 0;
		bool delayOnBoarding, firstCreate;

		Switch trackSwitch;
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
				}.Select (id => FindViewById<CircleBadge> (id)).ToArray ();
			}
		}

		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);

			firstCreate = true;
			handler = new Handler ();
			prefs = new PreferenceManager (this);

			SetContentView (Resource.Layout.Main);

			circlesLayout = FindViewById (Resource.Id.circlesLayout);
			notificationPanel = FindViewById (Resource.Id.notificationPanel);
			rideInfoPanel = FindViewById (Resource.Id.rideInfoPanel);
			lastTrip = FindViewById<TextView> (Resource.Id.lastTripText);

			var circles = CircleBadges;
			circles[0].Distance = prefs.GetLastMeasure ("day", TimeSpan.FromDays (1));
			circles[1].Distance = prefs.GetLastMeasure ("week", TimeSpan.FromDays (7));
			circles[2].Distance = prefs.GetLastMeasure ("month", TimeSpan.FromDays (30));

			circlesLayout.ViewTreeObserver.AddOnGlobalLayoutListener (this);
		}

		protected override void OnResume ()
		{
			base.OnResume ();
			if (!firstCreate)
				LoadData ();
			var avail = GooglePlayServicesUtil.IsGooglePlayServicesAvailable (this);
			if (avail == ConnectionResult.Success) {
				if (prefs.Enabled)
					SetTrackingEnabled (true);
				return;
			}
			delayOnBoarding = true;
			GooglePlayServicesUtil.ShowErrorDialogFragment (avail, this, GooglePlayServiceResult, this);
		}

		public void OnCancel (IDialogInterface dialog)
		{
			Finish ();
		}

		protected override void OnActivityResult (int requestCode, Result resultCode, Intent data)
		{
			base.OnActivityResult (requestCode, resultCode, data);
			if (requestCode != GooglePlayServiceResult)
				return;
			if (resultCode != Result.Ok) {
				Finish ();
				return;
			}
			delayOnBoarding = false;
			if (prefs.FirstTimeAround)
				ShowOnboarding ();
			else
				SetTrackingEnabled (true);
		}

		public void OnGlobalLayout ()
		{
			circlesLayout.ViewTreeObserver.RemoveGlobalOnLayoutListener (this);
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
				if (last) {
					if (prefs.FirstTimeAround && !delayOnBoarding)
						circleAnim.WithEndAction (new Run (ShowOnboarding));
					else if (!prefs.FirstTimeAround)
						circleAnim.WithEndAction (new Run (LoadData));
				}

				circleAnim.Start ();
				delay += delayIncr;
			}

			firstCreate = false;
		}

		async void ShowOnboarding ()
		{
			var time = Resources.GetInteger (Android.Resource.Integer.ConfigMediumAnimTime);
			var rootView = (ViewGroup)notificationPanel.RootView;
			rootView.DrawingCacheEnabled = true;
			var cachedBitmap = rootView.DrawingCache;
			var blurredBack = await Task.Run (() => ImageUtils.Blur (this, cachedBitmap));
			rootView.DrawingCacheEnabled = false;
			var view = LayoutInflater.Inflate (Resource.Layout.OnBoarding, rootView, false);
			var rootLayout = view.FindViewById (Resource.Id.onboardingContainer);
			var sw = view.FindViewById<Switch> (Resource.Id.trackSwitch);

			var onboardingBg = view.FindViewById<ImageView> (Resource.Id.onboardingBg);
			onboardingBg.SetColorFilter (Color.Argb (180, 0, 0, 0));
			onboardingBg.SetImageBitmap (blurredBack);
			rootView.AddView (view);

			sw.CheckedChange += (sender, e) => {
				view.Animate ().Alpha (0).WithEndAction (new Run (() => {
					rootView.RemoveView (view);
					prefs.FirstTimeAround = false;
					trackSwitch.Checked = true;
				}));
			};

			view.Alpha = 0;
			rootLayout.Alpha = 0;
			rootLayout.ScaleX = rootLayout.ScaleY = 0.85f;
			view.Animate ().Alpha (1).SetDuration (time).Start ();
			rootLayout.Animate ().Alpha (1).ScaleX (1).ScaleY (1).SetDuration (time).SetStartDelay (time / 3).Start ();
		}

		public override bool OnCreateOptionsMenu (IMenu menu)
		{
			MenuInflater.Inflate (Resource.Menu.menu, menu);
			trackSwitch = (Switch)menu.FindItem (Resource.Id.menu_trackactivity).ActionView;
			trackSwitch.Checked = prefs.Enabled;
			trackSwitch.CheckedChange += HandleTrackSwitchCheckedChange;;
			return base.OnCreateOptionsMenu (menu);
		}

		void HandleTrackSwitchCheckedChange (object sender, CompoundButton.CheckedChangeEventArgs e)
		{
			Switch s = sender as Switch;
			prefs.Enabled = s.Checked;
			var id = s.Checked ? Resource.String.track_enabled : Resource.String.track_disabled;
			ShowNotification (Resources.GetString (id));

			var delayId = ++delayCurrentId;
			handler.PostDelayed (() => {
				if (delayId != delayCurrentId)
					return;
				SetTrackingEnabled (s.Checked);
			}, 1000);
		}

		void ShowLastTripNotification (TimeSpan d, int count, double distance)
		{
			var tt = d.ToString (d.Hours > 0 ? "hh\\hmm" : "mm\\m\\i\\n");
			var dispDistance = prefs.ConvertDistanceInDisplayUnit (distance) + prefs.GetUnitForDistance (distance); 
			string msg = null;
			if (count <= 1)
				msg = Resources.GetString (Resource.String.single_trip_notification);
			else
				msg = Resources.GetString (Resource.String.multiple_trip_notification);
			msg = string.Format (msg, count);
			ShowNotification (msg, duration: tt, distance: dispDistance, delay: 2500);
		}

		void ShowNotification (string notification, string duration = null, string distance = null, int delay = 0)
		{
			notificationPanel.TranslationY =
				notificationPanel.Height + ((LinearLayout.LayoutParams)notificationPanel.LayoutParameters).BottomMargin;
			notificationPanel.TranslationX = 0;
			notificationPanel.ScaleX = .9f;
			notificationPanel.Visibility = ViewStates.Visible;
			lastTrip.Text = notification;
			if (duration == null || distance == null)
				rideInfoPanel.Visibility = ViewStates.Invisible;
			else {
				rideInfoPanel.Visibility = ViewStates.Visible;
				rideInfoPanel.FindViewById<TextView> (Resource.Id.timeText).Text = duration;
				rideInfoPanel.FindViewById<TextView> (Resource.Id.distanceText).Text = distance;
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

		async void LoadData ()
		{
			try {
				var circles = CircleBadges;

				var dataApi = DataApi.Obtain (this);
				var stats = await dataApi.GetStats ();
				/*var stats = new TripDistanceStats {
					PrevDay = 4500,
					PrevWeek = 25000,
					PrevMonth = 100000,
					Daily = 3500,
					Weekly = 12000,
					Monthly = 30000,
				};*/
				var mapping = new Dictionary<CircleBadge, double> {
					{ circles[0], stats.Daily },
					{ circles[1], stats.Weekly },
					{ circles[2], stats.Monthly },
				};
				foreach (var map in mapping) {
					if ((int)map.Value != (int)map.Key.Distance)
						map.Key.SetDistanceAnimated (map.Value, startDelay: 100);
				}
				prefs.SetLastMeasure ("day", stats.Daily);
				prefs.SetLastMeasure ("week", stats.Weekly);
				prefs.SetLastMeasure ("month", stats.Monthly);

				var lastTrips = await dataApi.GetTripsAfter (prefs.LastCheckin);
				//var lastTrips = new List<BikeTrip> ();
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

		void SetTrackingEnabled (bool enabled)
		{
			var intent = new Intent (this, typeof(ManagerService));
			if (enabled)
				StartService (intent);
			else
				StopService (intent);
		}
	}
}


