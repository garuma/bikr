using System.Reflection;
using Android.App;
using Android.OS;
using Android.Content;
using Xamarin.Android.NUnitLite;

namespace Bikr.Tests
{
	[Activity (Label = "Bikr.Tests", MainLauncher = true)]
	public class MainActivity : TestSuiteActivity
	{
		public static Context MainContext;

		protected override void OnCreate (Bundle bundle)
		{
			MainContext = this;
			// tests can be inside the main assembly
			AddTest (Assembly.GetExecutingAssembly ());
			// or in any reference assemblies
			// AddTest (typeof (Your.Library.TestClass).Assembly);

			// Once you called base.OnCreate(), you cannot add more assemblies.
			base.OnCreate (bundle);
		}
	}
}

