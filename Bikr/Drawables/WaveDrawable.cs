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
using Android.Graphics.Drawables;
using Android.Animation;
using Android.Util;

namespace Bikr
{
	class WaveDrawable : Drawable
	{
		Context context;
		int width, height;

		readonly Color BackgroundColor;
		readonly Color BubbleColor;
		Paint paint;
		Paint bubblePaint;

		const int Count = 21;
		const float Damping = 0.95f;
		const float TimeStep = 0.05f;
		const long AnimationStep = 50;

		float C;

		float[] u1 = new float[Count];
		float[] u2 = new float[Count];
		float[] v = new float[Count];

		bool started;
		bool bubblesEnabled;
		TimeAnimator animator;
		long elapsed;

		public WaveDrawable (Context context, int width, int height)
		{
			this.width = width;
			this.height = height;
			this.context = context;

			this.C = ComputeIdealSpeed ();

			BackgroundColor = context.Resources.GetColor (Resource.Color.secondary_background);
			BubbleColor = context.Resources.GetColor (Resource.Color.bubble_color);

			paint = new Paint {
				Color = BackgroundColor,
				AntiAlias = true,
			};
			bubblePaint = new Paint {
				AntiAlias = true,
				Color = BubbleColor,
				StrokeWidth = TypedValue.ApplyDimension (ComplexUnitType.Dip, 1, context.Resources.DisplayMetrics),
			};
			bubblePaint.SetStyle (Paint.Style.Stroke);
			SetBounds (0, 0, width, height);
		}

		float ComputeIdealSpeed ()
		{
			var radDip = (int)Math.Round (((float)(width / 2)) / context.Resources.DisplayMetrics.Density);
			return radDip * 1.36f;
		}

		public void ResetSize (int width, int height)
		{
			var oldHeight = this.height;
			this.width = width;
			this.height = height;
			this.C = ComputeIdealSpeed ();

			SetBounds (0, 0, width, height);

			// Adjust current u1 values to cope with the height change
			if (oldHeight != height)
				for (int i = 0; i < Count; i++)
					u1 [i] = (u1 [i] / oldHeight) * height;
		}

		public override int IntrinsicWidth {
			get {
				return width;
			}
		}

		public override int IntrinsicHeight {
			get {
				return height;
			}
		}

		public bool BubblesEnabled {
			get { return bubblesEnabled; }
			set {
				bubblesEnabled = value;
				InvalidateSelf ();
			}
		}

		public void SetWavyLevel (int level)
		{
			for (int i = 0; i < Count; i++)
				u1 [i] = level;
			InvalidateSelf ();
		}

		public int MaxWavyLevel { get; set; }

		public void ComputeTouch (MotionEvent e, float intensity)
		{
			var index = (int)(e.GetX () / (width / Count));
			if (index < 0 || index > u1.Length - 1)
				return;
			var amount = Math.Min (Math.Max (0, intensity), 1) * Math.Min (u1[0], 20);
			if (amount == 0)
				return;
			if (index > 2) {
				u1 [index - 2] -= amount;
				u1 [index - 1] += amount / 3;
				u1 [index] += (2 * amount) / 3;
			}
			if (index < u1.Length - 3) {
				u1 [index + 2] -= amount;
				u1 [index + 1] += amount / 3;
				u1 [index] += (2 * amount) / 3;
			}

			StartWaveAnimation ();
		}

		public void StartWaveAnimation ()
		{
			if (!started) {
				started = true;
				animator = new TimeAnimator ();
				animator.Time += HandleTime;
				animator.Start ();
			}
		}

		public void StopWaveAnimation ()
		{
			for (int i = 0; i < Count; i++)
				v [i] = 0;
			started = false;
			if (animator != null) {
				animator.Cancel ();
				animator = null;
			}
		}

		void HandleTime (object sender, TimeAnimator.TimeEventArgs e)
		{
			elapsed += e.DeltaTime;
			if (elapsed > AnimationStep) {
				elapsed = elapsed % AnimationStep;
				Step ();
			}
		}

		public override void Draw (Canvas canvas)
		{
			var w = width / (Count - 2);

			var path = new Path ();
			path.MoveTo (0, height - u1 [0]);
			for (int i = 1; i < Count; i += 3) {
				path.CubicTo (i * w, height - u1 [Math.Min (Count - 1, i)],
				              (i + 1) * w, height - u1 [Math.Min (Count - 1, i + 1)],
				              (i + 2) * w, height - u1 [Math.Min (Count - 1, i + 2)]);
			}

			path.LineTo (width, height);
			path.LineTo (0, height);
			path.Close ();
			canvas.DrawPath (path, paint);
			if (bubblesEnabled)
				DrawBubbles (canvas, w);
		}

		// This drawing routine assume all columns have equal heights, don't use while animating wave
		void DrawBubbles (Canvas canvas, int columnWidth)
		{
			const int Range = 200;
			const int MinRadius = 2;
			const int FadeStartLevel = 50;

			var level = u1 [0];
			var maxRadius = ((columnWidth >> 1) * 8) / 10;
			var maxHeight = height >> 2;

			canvas.Save ();
			canvas.ClipRect (0, height - level, width, height);

			int alphaDecrement = (int)((1 - Math.Min (FadeStartLevel, MaxWavyLevel - level) / ((float)FadeStartLevel)) * 255);

			for (int i = 0; i < Count; i++) {
				var dispersion = ((float)Math.Sin (i * ((4 * Math.PI) / Count)) + 1) * Range;
				var ratio = ((level + dispersion) % Range) / Range;
				var alpha = (int)Math.Round (255 - ratio * 200);
				alpha = Math.Max (0, alpha - alphaDecrement);
				bubblePaint.Color = BlendColor (BackgroundColor, BubbleColor, alpha);
				var cx = i * columnWidth + columnWidth / 2;
				var cy = height - level + (1 - ratio) * (maxHeight - maxRadius);
				var radius = MinRadius + ratio * (maxRadius - MinRadius);
				canvas.DrawCircle (cx, cy, radius, bubblePaint);
			}

			canvas.Restore ();
		}

		static Color BlendColor (Color bg, Color fg, int alpha)
		{
			var falpha = alpha / 255f;
			return new Color ((int)(bg.R * (1 - falpha) + fg.R * falpha),
			                  (int)(bg.G * (1 - falpha) + fg.G * falpha),
			                  (int)(bg.B * (1 - falpha) + fg.B * falpha));
		}

		public override void SetAlpha (int level)
		{
			SetWavyLevel (level);
		}

		public override void SetColorFilter (ColorFilter cf)
		{
		}

		public override int Opacity {
			get {
				return (int)Format.Opaque;
			}
		}

		void Step ()
		{
			var h = ((float)width) / Count;
			var c = Math.Min (C, h / TimeStep);

			for (int i = 0; i < Count; i++) {
				var f = c * c * (u1 [Math.Max (0, i - 1)] + u1 [Math.Min (Count - 1, i + 1)] - 2 * u1 [i]) / (h * h);
				f += -Damping * v[i];
				v [i] += TimeStep * f;
				u2 [i] = Math.Max (0, u1 [i] + v [i] * TimeStep);
			}

			var tmp = u1;
			u1 = u2;
			u2 = tmp;

			var mean = u1.Average ();
			var variance = u1.Sum (f => (f - mean) * (f - mean)) / (Count - 1);
			if (variance > 0.2)
				InvalidateSelf ();
			else
				StopWaveAnimation ();
		}
	}
}

