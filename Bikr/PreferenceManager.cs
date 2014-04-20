using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace Bikr
{
	class PreferenceManager
	{
		Context context;

		public PreferenceManager (Context context)
		{
			this.context = context;
		}

		public ISharedPreferences Preferences {
			get { return context.GetSharedPreferences ("org.neteril.Bikr", FileCreationMode.Private); }
		}

		bool UseMiles {
			get { return System.Globalization.CultureInfo.CurrentCulture.Name == "en-US"; }
		}

		public string GetUnitForDistance (double distance)
		{
			return UseMiles ? "mi" : (distance >= 1000 ? "km" : "m");
		}

		public string ConvertDistanceInDisplayUnit (double distance)
		{
			return (UseMiles ? distance * 0.00062137 : (distance >= 1000 ? (distance / 1000) : distance)).ToString ("N0");
		}

		public bool FirstTimeAround {
			get {
				return Preferences.GetBoolean ("firstTime", true);
			}
			set {
				var editor = Preferences.Edit ();
				editor.PutBoolean ("firstTime", value);
				editor.Commit ();
			}
		}

		public bool Enabled {
			get {
				return Preferences.GetBoolean ("trackActivity", false);
			}
			set {
				var editor = Preferences.Edit ();
				editor.PutBoolean ("trackActivity", value);
				editor.Commit ();
			}
		}

		public BikingState CurrentBikingState {
			get {
				return (BikingState)Preferences.GetInt ("currentBikingState", (int)BikingState.NotBiking);
			}
			set {
				var editor = Preferences.Edit ();
				editor.PutInt ("currentBikingState", (int)value);
				editor.Commit ();
			}
		}

		public DateTime LastCheckin {
			get {
				return new DateTime (Preferences.GetLong ("lastCheckIn", DateTime.UtcNow.Ticks), DateTimeKind.Utc);
			}
			set {
				var editor = Preferences.Edit ();
				editor.PutLong ("lastCheckIn", value.Ticks);
				editor.Commit ();
			}
		}

		public void SetLastMeasure (string key, double measure)
		{
			var prefKey = "Measures-" + key;
			var editor = Preferences.Edit ();
			editor.PutFloat (prefKey, (float)measure);
			editor.PutLong ("lastMeasureTime", DateTime.Now.Date.Ticks);
			editor.Commit ();
		}

		public double GetLastMeasure (string key, TimeSpan validitySpan)
		{
			var dt = new DateTime (Preferences.GetLong ("lastMeasureTime", 0));
			if (DateTime.Now.Date - dt >= validitySpan)
				return 0;
			return Preferences.GetFloat ("Measures-" + key, 0);
		}

		public void RegisterListener (ISharedPreferencesOnSharedPreferenceChangeListener listener)
		{
			Preferences.RegisterOnSharedPreferenceChangeListener (listener);
		}

		public void RemoveListener (ISharedPreferencesOnSharedPreferenceChangeListener listener)
		{
			Preferences.UnregisterOnSharedPreferenceChangeListener (listener);
		}
	}
}

