using System;
using System.IO;

using NUnit.Framework;
using Android.Content;

using Bikr;
using SQLite;

namespace Bikr.Tests
{
	[TestFixture]
	public class DataApiTests
	{
		Context context = MainActivity.MainContext;
		DataApi api;

		[SetUp]
		public void Setup ()
		{
			api = DataApi.Obtain (context);
			api.ClearDatabase ().Wait ();
			Console.WriteLine (api.DbPath);
		}

		[Test]
		public void AddTripTest ()
		{
			api.AddTrip (new BikeTrip {
				Distance = 900,
				StartTime = DateTime.Now - TimeSpan.FromHours (1),
				EndTime = DateTime.Now,
			}).Wait ();
			api.AddTrip (new BikeTrip {
				Distance = 800,
				StartTime = DateTime.Now - TimeSpan.FromHours (2),
				EndTime = DateTime.Now,
			}).Wait ();

			Assert.IsTrue (File.Exists (api.DbPath));
			Assert.AreEqual (2, new SQLiteConnection (api.DbPath).Table<BikeTrip> ().Count ());
		}

		[Test]
		public void GetTripsAfterTest_Empty ()
		{
			var trips = api.GetTripsAfter (DateTime.Now - TimeSpan.FromHours (1)).Result;
			Assert.IsNotNull (trips);
			Assert.AreEqual (0, trips.Count);
		}

		[Test]
		public void GetTripsAfterTest ()
		{
			var now = new DateTime (2014, 04, 18, 10, 00, 02);
			api.AddTrip (new BikeTrip {
				Distance = 1200,
				StartTime = now - TimeSpan.FromHours (4),
				EndTime = now - TimeSpan.FromHours (2),
				CommitTime = now - TimeSpan.FromHours (2),
			}).Wait ();
			api.AddTrip (new BikeTrip {
				Distance = 1400,
				StartTime = now - TimeSpan.FromHours (1),
				EndTime = now,
				CommitTime = now,
			}).Wait ();

			var trips = api.GetTripsAfter (now - TimeSpan.FromHours (1)).Result;
			Assert.IsNotNull (trips);
			Assert.AreEqual (1, trips.Count);
			Assert.AreEqual (1400, trips [0].Distance);
		}

		[Test]
		public void GetStatsTest_Empty ()
		{
			var stats = api.GetStats ().Result;
			Assert.AreEqual (0, stats.Daily);
			Assert.AreEqual (0, stats.Monthly);
			Assert.AreEqual (0, stats.Weekly);
		}

		[Test]
		public void GetStatsTest ()
		{
			var month = new DateTime (2014, 04, 02);
			api.AddTrip (new BikeTrip {
				Distance = 1200,
				StartTime = month,
				EndTime = month + TimeSpan.FromMinutes (45),
				CommitTime = month + TimeSpan.FromMinutes (45),
			}).Wait ();
			var week = new DateTime (2014, 04, 15);
			api.AddTrip (new BikeTrip {
				Distance = 1400,
				StartTime = week,
				EndTime = week + TimeSpan.FromHours (1),
				CommitTime = week + TimeSpan.FromHours (1),
			}).Wait ();
			var now = new DateTime (2014, 04, 18, 10, 00, 02);
			api.AddTrip (new BikeTrip {
				Distance = 1600,
				StartTime = now - TimeSpan.FromHours (2),
				EndTime = now - TimeSpan.FromHours (1),
				CommitTime = now - TimeSpan.FromHours (1),
			}).Wait ();
			api.AddTrip (new BikeTrip {
				Distance = 1800,
				StartTime = now - TimeSpan.FromHours (1),
				EndTime = now,
				CommitTime = now,
			}).Wait ();

			var stats = api.GetStats (now).Result;
			Assert.AreEqual (3400, stats.Daily);
			Assert.AreEqual (4800, stats.Weekly);
			Assert.AreEqual (6000, stats.Monthly);
		}

		[Test]
		public void GetStatsTest_BetweenMonth ()
		{
			var now = new DateTime (2014, 05, 02, 11, 02, 30);
			api.AddTrip (new BikeTrip {
				Distance = 100,
				StartTime = now.AddHours (-2),
				EndTime = now.AddHours (-1),
				CommitTime = now.AddHours (-1),
			}).Wait ();
			api.AddTrip (new BikeTrip {
				Distance = 200,
				StartTime = now.AddDays (-4),
				EndTime = now.AddDays (-4).AddHours (1),
				CommitTime = now.AddDays (-4).AddHours (1),
			}).Wait ();

			var stats = api.GetStats (now).Result;
			Assert.AreEqual (100, stats.Daily);
			Assert.AreEqual (300, stats.Weekly);
			Assert.AreEqual (100, stats.Monthly);
		}

		[Test]
		public void JustAddedTripWithNoCommitTest ()
		{
			var now = new DateTime (2014, 04, 18, 10, 00, 02);
			api.AddTrip (new BikeTrip {
				Distance = 100,
				StartTime = now.AddMinutes (-30),
				EndTime = now.AddSeconds (-20),
			}).Wait ();
			var trips = api.GetTripsAfter (DateTime.Now.AddMinutes (-1)).Result;
			Assert.AreEqual (1, trips.Count);
			var t = trips [0];
			Assert.That (t.CommitTime, Is.GreaterThan (DateTime.Now.AddMinutes (-1)));
		}

		[Test]
		public void GetAggregatedStatsTest ()
		{
			var month = new DateTime (2014, 04, 02);
			api.AddTrip (new BikeTrip {
				Distance = 1200,
				StartTime = month,
				EndTime = month + TimeSpan.FromMinutes (45),
				CommitTime = month + TimeSpan.FromMinutes (45),
			}).Wait ();
			var week = new DateTime (2014, 04, 15);
			api.AddTrip (new BikeTrip {
				Distance = 1400,
				StartTime = week,
				EndTime = week + TimeSpan.FromHours (1),
				CommitTime = week + TimeSpan.FromHours (1),
			}).Wait ();
			var now = new DateTime (2014, 04, 18, 10, 00, 02);
			api.AddTrip (new BikeTrip {
				Distance = 1600,
				StartTime = now - TimeSpan.FromHours (2),
				EndTime = now - TimeSpan.FromHours (1),
				CommitTime = now - TimeSpan.FromHours (1),
			}).Wait ();
			api.AddTrip (new BikeTrip {
				Distance = 1800,
				StartTime = now - TimeSpan.FromHours (1),
				EndTime = now,
				CommitTime = now,
			}).Wait ();

			var stats = api.GetAggregatedStats (now).Result;

			// Daily
			Assert.AreEqual ((1400 + 1600 + 1800) / 6, (int)stats[AggregatedStatsKey.DailyThisWeek]);
			Assert.AreEqual ((1200 + 1400 + 1600 + 1800) / 18, (int)stats [AggregatedStatsKey.DailyThisMonth]);
		}
	}
}

