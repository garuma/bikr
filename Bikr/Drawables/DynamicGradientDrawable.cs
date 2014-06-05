
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
using Android.Graphics.Drawables;

namespace Bikr
{
	public class DynamicGradientDrawable : GradientDrawable
	{
		Color topShade1, bottomShade1;
		Color topShade2, bottomShade2;

		int[] colors = new int[2];

		public DynamicGradientDrawable (Color ts1, Color bs1, Color ts2, Color bs2)
		{
			SetGradientType (GradientType.LinearGradient);
			SetOrientation (Orientation.TopBottom);
			topShade1 = ts1; bottomShade1 = bs1;
			topShade2 = ts2; bottomShade2 = bs2;
		}

		public void SetGradientRatio (float ratio)
		{
			colors [0] = ImageUtils.InterpolateColor (ratio, topShade1, topShade2).ToArgb ();
			colors [1] = ImageUtils.InterpolateColor (ratio, bottomShade1, bottomShade2).ToArgb ();

			SetColors (colors);
			InvalidateSelf ();
		}
	}
}

