using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

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

	public class DataApi
	{
		const string SumQuery = "select ifnull(sum(distance),0) from BikeTrip where start >= ?";
		const string SumBetweenQuery = "select ifnull(sum(distance),0) from BikeTrip where start >= ?1 and start <= ?2";
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
			trip.CommitTime = DateTime.UtcNow;
			using (var connection = await GrabConnection ().ConfigureAwait (false))
				await Task.Run (() => connection.Insert (trip)).ConfigureAwait (false);
		}

		public async Task<List<BikeTrip>> GetTripsAfter (DateTime dt)
		{
			using (var connection = await GrabConnection ().ConfigureAwait (false)) {
				return await Task.Run (() => connection
					.Table<BikeTrip> ()
				    .Where (bt => bt.CommitTime >= dt)
					.ToList ()
				).ConfigureAwait (false);
			}
		}

		public async Task<TripDistanceStats> GetStats ()
		{
			using (var connection = await GrabConnection ().ConfigureAwait (false)) {
				var now = DateTime.Now;

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
	}
}

