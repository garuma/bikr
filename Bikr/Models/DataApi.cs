using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Globalization;

using SQLite;

namespace Bikr
{
	public struct TripDistanceStats {
		public double Daily { get; set; }
		public double Weekly { get; set; }
		public double Monthly { get; set; }

		public double PrevDay { get; set; }
		public double PrevWeek { get; set; }
		public double PrevMonth { get; set; }
	}

	public enum AggregatedStatsKey {
		DailyThisWeek,
		DailyThisMonth,
		BestTripToday,
		BestTripInWeek,
		BestTripInMonth,
		MeanTripToday,
		MeanTripInWeek,
		MeanTripInMonth
	}

	public class DataApi
	{
		const string BetweenDatePredicate = " where `commit` >= ?1 and `commit` <= ?2";
		const string SumQuery = "select ifnull(sum(distance),0) from BikeTrip where `commit` >= ?";
		const string SumBetweenQuery = "select ifnull(sum(distance),0) from BikeTrip" + BetweenDatePredicate;

		const string DayAverageQuery = "select ifnull(sum(distance),0)/?3 from BikeTrip " + BetweenDatePredicate;
		const string BestTripQuery = "select ifnull(max(distance),0) from BikeTrip" + BetweenDatePredicate;
		const string MeanTripQuery = "select ifnull(avg(distance),0) from BikeTrip" + BetweenDatePredicate;

		string dbPath;

		public DataApi (string dbPath)
		{
			this.dbPath = dbPath;
		}

		#if __ANDROID__
		public static DataApi Obtain (Android.Content.Context ctx)
		{
			var db = ctx.OpenOrCreateDatabase ("trips.db", Android.Content.FileCreationMode.WorldReadable, null);
			var path = db.Path;
			db.Close ();
			return new DataApi (path);
		}
		#endif

		public string DbPath {
			get { return dbPath; }
		}

		public async Task ClearDatabase ()
		{
			using (var connection = new SQLiteConnection (dbPath, true))
				await Task.Run (() => connection.DropTable<BikeTrip> ()).ConfigureAwait (false);
		}

		async Task<SQLiteConnection> GrabConnection ()
		{
			var connection = new SQLiteConnection (dbPath, true);
			await Task.Run (() => connection.CreateTable<BikeTrip> ()).ConfigureAwait (false);
			return connection;
		}

		public async Task AddTrip (BikeTrip trip)
		{
			// Sanitize trip date information
			if (trip.CommitTime == DateTime.MinValue)
				trip.CommitTime = DateTime.UtcNow;
			else if (trip.CommitTime.Kind != DateTimeKind.Utc)
				trip.CommitTime = trip.CommitTime.ToUniversalTime ();
			if (trip.StartTime.Kind != DateTimeKind.Utc)
				trip.StartTime = trip.StartTime.ToUniversalTime ();
			if (trip.EndTime.Kind != DateTimeKind.Utc)
				trip.EndTime = trip.EndTime.ToUniversalTime ();

			using (var connection = await GrabConnection ().ConfigureAwait (false))
				await Task.Run (() => connection.Insert (trip)).ConfigureAwait (false);
		}

		public async Task<List<BikeTrip>> GetTripsAfter (DateTime dt)
		{
			if (dt.Kind != DateTimeKind.Utc)
				dt = dt.ToUniversalTime ();
			using (var connection = await GrabConnection ().ConfigureAwait (false)) {
				return await Task.Run (() => connection
					.Table<BikeTrip> ()
				    .Where (bt => bt.CommitTime >= dt)
					.ToList ()
				).ConfigureAwait (false);
			}
		}

		public Task<TripDistanceStats> GetStats ()
		{
			return GetStats (DateTime.Now);
		}

		public async Task<TripDistanceStats> GetStats (DateTime now)
		{
			using (var connection = await GrabConnection ().ConfigureAwait (false)) {
				if (!connection.Table<BikeTrip> ().Any ())
					return new TripDistanceStats ();

				return await Task.Run (() => new TripDistanceStats {
					Daily = FetchStatsFromDate (connection, now.DayStart ()),
					Weekly = FetchStatsFromDate (connection, now.WeekStart ()),
					Monthly = FetchStatsFromDate (connection, now.MonthStart ()),
					PrevDay = FetchStatsBetweenDates (connection, now.DayStart ().PreviousDay (), now.DayStart ()),
					PrevWeek = FetchStatsBetweenDates (connection, now.WeekStart ().PreviousWeek (), now.WeekStart ()),
					PrevMonth = FetchStatsBetweenDates (connection, now.MonthStart ().PreviousMonth (), now.MonthStart ()),
				}).ConfigureAwait (false);
			}
		}

		double FetchStatsFromDate (SQLiteConnection connection, DateTime dt)
		{
			return connection.ExecuteScalar<double> (SumQuery, dt.ToUniversalTime ());
		}

		double FetchStatsBetweenDates (SQLiteConnection connection, DateTime startTime, DateTime endTime)
		{
			return connection.ExecuteScalar<double> (SumBetweenQuery,
			                                         startTime.ToUniversalTime (),
			                                         endTime.ToUniversalTime ());
		}

		public Task<Dictionary<AggregatedStatsKey, double>> GetAggregatedStats ()
		{
			return GetAggregatedStats (DateTime.Now);
		}

		public async Task<Dictionary<AggregatedStatsKey, double>> GetAggregatedStats (DateTime now)
		{
			var result = new Dictionary<AggregatedStatsKey, double> ();
			using (var connection = await GrabConnection ().ConfigureAwait (false)) {
				if (!connection.Table<BikeTrip> ().Any ())
					return result;

				var dayStart = now.DayStart ().ToUniversalTime ();
				var weekStart = now.WeekStart ().ToUniversalTime ();
				var monthStart = now.MonthStart ().ToUniversalTime ();
				now = now.ToUniversalTime ();

				await Task.Run (() => {
					result[AggregatedStatsKey.DailyThisWeek] =
						connection.ExecuteScalar<double> (DayAverageQuery, weekStart, now, (int)(now - weekStart).TotalDays + 1);
					result[AggregatedStatsKey.DailyThisMonth] =
						connection.ExecuteScalar<double> (DayAverageQuery, monthStart, now, (int)(now - monthStart).TotalDays + 1);
					result[AggregatedStatsKey.BestTripToday] =
						connection.ExecuteScalar<double> (BestTripQuery, dayStart, now);
					result[AggregatedStatsKey.BestTripInWeek] =
						connection.ExecuteScalar<double> (BestTripQuery, weekStart, now);
					result[AggregatedStatsKey.BestTripInMonth] =
						connection.ExecuteScalar<double> (BestTripQuery, monthStart, now);
					result[AggregatedStatsKey.MeanTripToday] =
						connection.ExecuteScalar<double> (BestTripQuery, dayStart, now);
					result[AggregatedStatsKey.MeanTripInWeek] =
						connection.ExecuteScalar<double> (BestTripQuery, weekStart, now);
					result[AggregatedStatsKey.MeanTripInMonth] =
						connection.ExecuteScalar<double> (BestTripQuery, monthStart, now);
				}).ConfigureAwait (false);

				return result;
			}
		}
	}
}

