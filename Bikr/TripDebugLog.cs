using System;
using System.IO;
using System.Net;
using System.Globalization;

namespace Bikr
{
	public static class TripDebugLog
	{
		enum OpCode {
			StartBikeTripCode = 'S',
			EndBikeTripCode = 'E',
			DeferredBikeTripEndCode = 'D',
			LogActivityEventCode = 'A',
			LogPositionEventCode = 'P',
			LogNewTripCode = 'T'
		}

		const string TripLogPath = "/sdcard/bikr-trip.log";

		public static void CommitTripLog ()
		{
			/*if (!File.Exists (TripLogPath))
				return;

			var text = File.ReadAllText (TripLogPath);
			File.Move (TripLogPath, TripLogPath + DateTime.UtcNow.ToString ("s"));
			if (string.IsNullOrEmpty (text))
				return;
			var client = new WebClient ();
			var retry = true;
			do {
				try {
					client.UploadString ("http://logs.bikrapp.net/log/post", "POST", text);
					retry = false;
				} catch (Exception e) {
					Android.Util.Log.Error ("TripDebugLog", e.ToString ());
					System.Threading.Thread.Sleep (150);
				}
			} while (retry);*/
		}

		public static void StartBikeTrip ()
		{
			//LogEntry (OpCode.StartBikeTripCode, "Starting bike trip");
		}

		public static void EndBikeTrip ()
		{
			//LogEntry (OpCode.EndBikeTripCode, "Ending bike trip");
		}

		public static void DeferredBikeTripEnd ()
		{
			//LogEntry (OpCode.DeferredBikeTripEndCode, "Deferred end trip installed");
		}

		public static void LogActivityEvent (int activity, int confidence)
		{
			//LogEntry (OpCode.LogActivityEventCode, activity + "," + confidence);
		}

		public static void LogPositionEvent (double lat, double lon, double addedDistance, double currentDistance)
		{
			/*LogEntry (OpCode.LogPositionEventCode,
			          lat.ToString (CultureInfo.InvariantCulture) + "," +
			          lon.ToString (CultureInfo.InvariantCulture) + "," +
			          Math.Round (addedDistance).ToString (CultureInfo.InvariantCulture) + "," +
			          Math.Round (currentDistance).ToString (CultureInfo.InvariantCulture));*/
		}

		public static void LogNewTrip (TimeSpan time, double distance)
		{
			/*LogEntry (OpCode.LogNewTripCode,
			          ((long)time.TotalMilliseconds).ToString (CultureInfo.InvariantCulture) + "," +
			          Math.Round (distance).ToString (CultureInfo.InvariantCulture));*/
		}

		static void LogEntry (OpCode opcode, string extraMessage)
		{
			AppendMessage (((char)((int)opcode)).ToString () + '|' + extraMessage);
		}

		static void AppendMessage (string message)
		{
			File.AppendAllText (TripLogPath, FormatMessageWithTime (message), System.Text.Encoding.UTF8);
		}

		static string FormatMessageWithTime (string message)
		{
			return DateTime.UtcNow.ToString ("O") + "\n" + message + "\n";
		}
	}
}

