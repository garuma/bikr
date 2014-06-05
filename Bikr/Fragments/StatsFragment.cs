
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
using Android.Graphics;
using Android.Animation;

namespace Bikr
{
	public class StatsFragment : Android.Support.V4.App.Fragment
	{
		SnapScrollView statsList;
		ScrollBarExtraordinaire scrollBar;

		PreferenceManager prefs;

		View[] sections;

		public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
		{
			var view = inflater.Inflate (Resource.Layout.StatsLayout, container, false);
			statsList = view.FindViewById<SnapScrollView> (Resource.Id.statsList);
			scrollBar = view.FindViewById<ScrollBarExtraordinaire> (Resource.Id.scrollBar);

			statsList.OverScrollMode = OverScrollMode.Never;
			statsList.RegisterExtraordinaireScrollBar (scrollBar);

			return view;
		}

		public override void OnViewCreated (View view, Bundle savedInstanceState)
		{
			base.OnViewCreated (view, savedInstanceState);

			// Setup titles
			sections = statsList.GetScrollChildViews ().ToArray ();
			sections [0].FindViewById<TextView> (Resource.Id.statTitle).Text =
				Resources.GetString (Resource.String.stat_section_daily);
			sections [1].FindViewById<TextView> (Resource.Id.statTitle).Text =
				Resources.GetString (Resource.String.stat_section_best);
			sections [2].FindViewById<TextView> (Resource.Id.statTitle).Text =
				Resources.GetString (Resource.String.stat_section_mean);

			// Setup shown stat line
			sections [0].FindViewById (Resource.Id.statEntry3).Visibility = ViewStates.Gone;

			// Setup stat description
			sections [0].FindViewById (Resource.Id.statEntry1).FindViewById<TextView> (Resource.Id.statDesc).Text =
				Resources.GetString (Resource.String.stat_desc_this_week);
			sections [0].FindViewById (Resource.Id.statEntry2).FindViewById<TextView> (Resource.Id.statDesc).Text =
				Resources.GetString (Resource.String.stat_desc_this_month);
			sections [1].FindViewById (Resource.Id.statEntry1).FindViewById<TextView> (Resource.Id.statDesc).Text =
				Resources.GetString (Resource.String.stat_desc_today);
			sections [1].FindViewById (Resource.Id.statEntry2).FindViewById<TextView> (Resource.Id.statDesc).Text =
				Resources.GetString (Resource.String.stat_desc_this_week);
			sections [1].FindViewById (Resource.Id.statEntry3).FindViewById<TextView> (Resource.Id.statDesc).Text =
				Resources.GetString (Resource.String.stat_desc_this_month);
			sections [2].FindViewById (Resource.Id.statEntry1).FindViewById<TextView> (Resource.Id.statDesc).Text =
				Resources.GetString (Resource.String.stat_desc_today);
			sections [2].FindViewById (Resource.Id.statEntry2).FindViewById<TextView> (Resource.Id.statDesc).Text =
				Resources.GetString (Resource.String.stat_desc_this_week);
			sections [2].FindViewById (Resource.Id.statEntry3).FindViewById<TextView> (Resource.Id.statDesc).Text =
				Resources.GetString (Resource.String.stat_desc_this_month);
		}

		public override void OnResume ()
		{
			base.OnResume ();
			LoadStats ();
		}

		public override void OnAttach (Android.App.Activity activity)
		{
			base.OnAttach (activity);
			if (prefs == null)
				prefs = new PreferenceManager (activity);
		}

		async void LoadStats ()
		{
			var dataApi = DataApi.Obtain (Activity);
			var stats = await dataApi.GetAggregatedStats ();
			/*var stats = new Dictionary<AggregatedStatsKey, double> {
				{ AggregatedStatsKey.DailyThisWeek, 4000 },
				{ AggregatedStatsKey.DailyThisMonth, 5000 },
				{ AggregatedStatsKey.BestTripToday, 6000 },
				{ AggregatedStatsKey.BestTripInWeek, 7000 },
				{ AggregatedStatsKey.BestTripInMonth, 8000 },
				{ AggregatedStatsKey.MeanTripToday, 9000 },
				{ AggregatedStatsKey.MeanTripInWeek, 10000 },
				{ AggregatedStatsKey.MeanTripInMonth, 11000 },
			};*/

			var fillScheme = new Tuple<int, AggregatedStatsKey, int>[] {
				Tuple.Create (0, AggregatedStatsKey.DailyThisWeek, Resource.Id.statEntry1),
				Tuple.Create (0, AggregatedStatsKey.DailyThisMonth, Resource.Id.statEntry2),
				Tuple.Create (1, AggregatedStatsKey.BestTripToday, Resource.Id.statEntry1),
				Tuple.Create (1, AggregatedStatsKey.BestTripInWeek, Resource.Id.statEntry2),
				Tuple.Create (1, AggregatedStatsKey.BestTripInMonth, Resource.Id.statEntry3),
				Tuple.Create (2, AggregatedStatsKey.MeanTripToday, Resource.Id.statEntry1),
				Tuple.Create (2, AggregatedStatsKey.MeanTripInWeek, Resource.Id.statEntry2),
				Tuple.Create (2, AggregatedStatsKey.MeanTripInMonth, Resource.Id.statEntry3),
			};

			foreach (var fill in fillScheme) {
				double value = 0;
				stats.TryGetValue (fill.Item2, out value);
				sections [fill.Item1].FindViewById (fill.Item3).FindViewById<TextView> (Resource.Id.numericValue).Text =
					prefs.GetDisplayDistance (value, strictValue: true);
				sections [fill.Item1].FindViewById (fill.Item3).FindViewById<TextView> (Resource.Id.unitText).Text =
					prefs.GetUnitForDistance (value);
			}
		}
	}
}

