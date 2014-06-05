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
	           ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait
	           /*ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize*/)]
	public class MainActivity : Android.Support.V4.App.FragmentActivity, IDialogInterfaceOnCancelListener
	{
		const int GooglePlayServiceResult = 1000;

		Handler handler;
		PreferenceManager prefs;
		int delayCurrentId = 0;
		bool delayOnBoarding, firstCreate;

		Switch trackSwitch;
		Android.Support.V4.View.ViewPager pager;
		DynamicGradientDrawable background;

		ActivityRecognitionHandler actRecognitionHandler;

		CirclesFragment circlesFragment;
		StatsFragment statsFragment;
		ActionBar.Tab circlesTab, statsTab;

		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);

			firstCreate = true;
			handler = new Handler ();
			prefs = new PreferenceManager (this);

			circlesFragment = new CirclesFragment ();
			statsFragment = new StatsFragment ();

			SetContentView (Resource.Layout.Main);

			pager = FindViewById<Android.Support.V4.View.ViewPager> (Resource.Id.mainPager);
			pager.Adapter = new StaticFragmentPagerAdapter (SupportFragmentManager, circlesFragment, statsFragment);

			background = new DynamicGradientDrawable (Resources.GetColor (Resource.Color.top_shade_1),
			                                          Resources.GetColor (Resource.Color.bottom_shade_1),
			                                          Resources.GetColor (Resource.Color.top_shade_2),
			                                          Resources.GetColor (Resource.Color.bottom_shade_2));
			pager.SetBackgroundDrawable (background);
			pager.PageScrolled += HandlePageScrolled;
			pager.OverScrollMode = OverScrollMode.Never;
			if (prefs.FirstTimeAround)
				pager.Touch += DiscardTouchEventHandler;

			circlesTab = ActionBar.NewTab ().SetIcon (Resource.Drawable.ic_tab_circles);
			statsTab = ActionBar.NewTab ().SetIcon (Resource.Drawable.ic_tab_stats);
			circlesTab.TabSelected += (sender, e) => pager.SetCurrentItem (0, true);
			statsTab.TabSelected += (sender, e) => pager.SetCurrentItem (1, true);
			ActionBar.AddTab (circlesTab);
			ActionBar.AddTab (statsTab);
			pager.PageSelected += (sender, e) => ActionBar.SetSelectedNavigationItem (e.Position);

			circlesFragment.CirclesReady += OnCirclesReady;
		}

		void DiscardTouchEventHandler (object sender, View.TouchEventArgs e)
		{
			e.Handled = true;
		}

		void HandlePageScrolled (object sender, Android.Support.V4.View.ViewPager.PageScrolledEventArgs e)
		{
			var offset = e.Position == 0 ? e.PositionOffset : 1f;
			background.SetGradientRatio (offset);
			ActionBar.GetTabAt (0).Icon.SetAlpha (255 - (int)(offset * 200));
			ActionBar.GetTabAt (1).Icon.SetAlpha (55 + (int)(offset * 200));
		}

		protected override void OnResume ()
		{
			base.OnResume ();
			if (!firstCreate)
				circlesFragment.LoadData (this);
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
				ShowOnboarding (circlesFragment.View);
			else
				SetTrackingEnabled (true);
		}

		void OnCirclesReady (object sender, EventArgs e)
		{
			var fragment = (CirclesFragment)sender;
			fragment.CirclesReady -= OnCirclesReady;

			fragment.SetupInitialAnimations (callback: () => {
				if (prefs.FirstTimeAround && !delayOnBoarding)
					ShowOnboarding (fragment.View);
				else if (!prefs.FirstTimeAround)
					fragment.LoadData (this);
			});

			firstCreate = false;
		}

		async void ShowOnboarding (View baseView)
		{
			var time = Resources.GetInteger (Android.Resource.Integer.ConfigMediumAnimTime);
			var rootView = (ViewGroup)baseView.RootView;
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
				pager.Touch -= DiscardTouchEventHandler;
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
			if (circlesFragment != null)
				circlesFragment.ShowNotification (Resources.GetString (id));

			var delayId = ++delayCurrentId;
			handler.PostDelayed (() => {
				if (delayId != delayCurrentId)
					return;
				SetTrackingEnabled (s.Checked);
			}, 1000);
		}

		void SetTrackingEnabled (bool enabled)
		{
			if (actRecognitionHandler == null)
				actRecognitionHandler = new ActivityRecognitionHandler (this);
			actRecognitionHandler.SetTrackingEnabled (enabled);
		}
	}

	class StaticFragmentPagerAdapter : Android.Support.V4.App.FragmentPagerAdapter
	{
		Android.Support.V4.App.Fragment[] fragments;

		public StaticFragmentPagerAdapter (Android.Support.V4.App.FragmentManager fm, params Android.Support.V4.App.Fragment[] fragments)
			: base (fm)
		{
			this.fragments = fragments;
		}

		public override Android.Support.V4.App.Fragment GetItem (int position)
		{
			return fragments [position];
		}

		public override int Count {
			get {
				return fragments.Length;
			}
		}
	}
}


