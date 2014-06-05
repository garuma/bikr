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
using Android.Graphics;
using Android.Util;
using Android.Renderscripts;

namespace Bikr
{
	class ImageUtils
	{
		public static Bitmap Blur (Context context, Bitmap input)
		{
			try {
				var rsScript = RenderScript.Create (context);
				var alloc = Allocation.CreateFromBitmap (rsScript, input);
				var blur = ScriptIntrinsicBlur.Create (rsScript, alloc.Element);
				blur.SetRadius (25);
				blur.SetInput (alloc);

				var result = Bitmap.CreateBitmap (input.Width, input.Height, input.GetConfig ());
				var outAlloc = Allocation.CreateFromBitmap (rsScript, result);
				blur.ForEach (outAlloc);

				outAlloc.CopyTo (result);
				rsScript.Destroy ();
				return result;
			} catch (Exception e) {
				Log.Error ("Blurrer", "Error while trying to blur, fallbacking. " + e.ToString ());
				return Bitmap.CreateBitmap (input);
			}
		}

		public static Color InterpolateColor (float ratio, Color c1, Color c2)
		{
			ratio = Math.Max (0, Math.Min (1, ratio));
			return new Color (
				(int)(ratio * (c2.R - c1.R)) + c1.R,
				(int)(ratio * (c2.G - c1.G)) + c1.G,
				(int)(ratio * (c2.B - c1.B)) + c1.B
			);
		}
	}
}

