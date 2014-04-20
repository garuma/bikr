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
			api.AddTrip (new BikeTrip {
				Distance = 1200,
				StartTime = DateTime.Now - TimeSpan.FromHours (4),
				EndTime = DateTime.Now - TimeSpan.FromHours (2),
			}).Wait ();
			api.AddTrip (new BikeTrip {
				Distance = 1400,
				StartTime = DateTime.Now - TimeSpan.FromHours (1),
				EndTime = DateTime.Now,
			}).Wait ();

			var trips = api.GetTripsAfter (DateTime.Now - TimeSpan.FromHours (1)).Result;
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
			var month = DateTime.Now.MonthStart () + TimeSpan.FromHours (7);
			api.AddTrip (new BikeTrip {
				Distance = 1200,
				StartTime = month,
				EndTime = month + TimeSpan.FromMinutes (45),
			}).Wait ();
			var week = DateTime.Now.WeekStart () + TimeSpan.FromHours (2);
			api.AddTrip (new BikeTrip {
				Distance = 1400,
				StartTime = week,
				EndTime = week + TimeSpan.FromHours (1),
			}).Wait ();
			api.AddTrip (new BikeTrip {
				Distance = 1600,
				StartTime = DateTime.Now - TimeSpan.FromHours (2),
				EndTime = DateTime.Now - TimeSpan.FromHours (1),
			}).Wait ();
			api.AddTrip (new BikeTrip {
				Distance = 1800,
				StartTime = DateTime.Now - TimeSpan.FromHours (1),
				EndTime = DateTime.Now,
			}).Wait ();

			var stats = api.GetStats ().Result;
			Assert.AreEqual (3400, stats.Daily);
			Assert.AreEqual (4800, stats.Weekly);
			Assert.AreEqual (6000, stats.Monthly);
		}
	}
}

