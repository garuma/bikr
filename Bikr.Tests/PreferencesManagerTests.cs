using System;
using System.IO;

using NUnit.Framework;
using Android.Content;

using Bikr;
using SQLite;

namespace Bikr.Tests
{
	[TestFixture]
	public class PreferenceManagerTests
	{
		Context context = MainActivity.MainContext;
		PreferenceManager prefs;

		DateTime DefaultReference {
			get {
				return new DateTime (2014, 04, 18, 10, 00, 02);
			}
		}

		[SetUp]
		public void Setup ()
		{
			prefs = new PreferenceManager (context);
			using (var editor = prefs.Preferences.Edit ()) {
				editor.Clear ();
				editor.Commit ();
			}
		}

		[Test]
		public void GetLastMeasures_NoData ()
		{
			AssertLastMeasures (DateTime.Now, 0, 0, 0);
		}

		[Test]
		public void GetLastMeasures_VeryOldData ()
		{
			var reference = DefaultReference;
			SetLastMeasures (reference.AddMonths (-2), 3, 5, 10);

			AssertLastMeasures (reference, 0, 0, 0);
		}

		[Test]
		public void GetLastMeasures_MonthOldData ()
		{
			var reference = DefaultReference;
			SetLastMeasures (reference.AddDays (-9), 3, 5, 10);

			AssertLastMeasures (reference, 0, 0, 10);
		}

		[Test]
		public void GetLastMeasures_WeekOldData ()
		{
			var reference = DefaultReference;
			SetLastMeasures (reference.AddDays (-2), 3, 5, 10);

			AssertLastMeasures (reference, 0, 5, 10);
		}

		[Test]
		public void GetLastMeasures_DayOldData ()
		{
			var reference = DefaultReference;
			SetLastMeasures (reference.AddHours (-2), 3, 5, 10);

			AssertLastMeasures (reference, 3, 5, 10);
		}

		void SetLastMeasures (DateTime reference, double day, double week, double month)
		{
			prefs.SetLastMeasure ("day", day, reference);
			prefs.SetLastMeasure ("week", week, reference);
			prefs.SetLastMeasure ("month", month, reference);
		}

		void AssertLastMeasures (DateTime reference, double day, double week, double month)
		{
			Assert.AreEqual (day, prefs.GetLastDayMeasure (reference));
			Assert.AreEqual (week, prefs.GetLastWeekMeasure (reference));
			Assert.AreEqual (month, prefs.GetLastMonthMeasure (reference));
		}
	}
}

