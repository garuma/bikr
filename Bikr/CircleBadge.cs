using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Util;
using Android.Animation;
using Android.Content.Res;

namespace Bikr
{
	public class CircleBadge : View
	{
		static Typeface robotoCondensed;
		PreferenceManager prefs;

		Paint digitPaint, unitPaint, descPaint;
		float padding;

		string desc;
		double distance;
		double currentRatio;
		float currentEffect;

		Rect bounds = new Rect ();
		WaveDrawable wavy;

		public CircleBadge (Context context) :
			base (context)
		{
			Initialize ();
		}

		public CircleBadge (Context context, IAttributeSet attrs) :
			base (context, attrs)
		{
			Initialize ();
			ProcessAttributeSets (attrs);
		}

		public CircleBadge (Context context, IAttributeSet attrs, int defStyle) :
			base (context, attrs, defStyle)
		{
			Initialize ();
			ProcessAttributeSets (attrs);
		}

		void ProcessAttributeSets (IAttributeSet attrs)
		{
			var resID = attrs.GetAttributeResourceValue (null, "description", -1);
			var val = resID == -1 ?
				attrs.GetAttributeValue (null, "description")
				: Context.Resources.GetString (resID);
			desc = (val ?? string.Empty).ToUpper ();
		}

		void Initialize ()
		{
			if (robotoCondensed == null)
				LoadTypefaces (Context.Assets);
			prefs = new PreferenceManager (Context);

			digitPaint = new Paint {
				AntiAlias = true,
				Color = Color.White,
				TextAlign = Paint.Align.Center,
				TextScaleX = .85f
			};
			unitPaint = new Paint {
				AntiAlias = true,
				Color = Color.Rgb (0xc0, 0xba, 0xb0),
				TextAlign = Paint.Align.Center,
			};
			descPaint = new Paint {
				AntiAlias = true,
				Color = Color.Rgb (0xdb, 0xd7, 0xcf),
				TextAlign = Paint.Align.Center,
			};
			unitPaint.SetTypeface (robotoCondensed);
			descPaint.SetTypeface (robotoCondensed);

			SetBackgroundResource (Resource.Drawable.circle);
		}

		public string Description {
			get { return desc; }
			set {
				desc = value;
				Invalidate ();
			}
		}

		// Internally in meters
		public double Distance {
			get { return distance; }
			set {
				distance = value;
				Invalidate ();
			}
		}

		public void SetDistanceAnimated (double distance, int startDelay = 0)
		{
			var animator = ObjectAnimator.OfFloat (this, "translationX", 0, 1);
			animator.AnimationRepeat += (sender, e) => this.distance = distance;
			animator.RepeatMode = ValueAnimatorRepeatMode.Reverse;
			animator.RepeatCount = 1;
			animator.StartDelay = startDelay;
			animator.SetDuration (280);
			animator.Start ();
		}

		public double CurrentRatio {
			get {
				return currentRatio;
			}
		}

		public void SetCompletionRatioAnimated (double ratio)
		{
			if (wavy == null)
				return;

			var level = ComputeLevel (ratio);
			var currLevel = ComputeLevel (currentRatio);

			if (ratio <= currentRatio) {
				wavy.SetWavyLevel (level);
			} else {
				wavy.MaxWavyLevel = level;
				var animator = ObjectAnimator.OfInt (wavy, "alpha", currLevel, level);
				animator.SetDuration (4000);
				animator.AnimationEnd += (sender, e) => {
					var a = (ObjectAnimator)sender;
					a.RemoveAllListeners ();
					var w = (WaveDrawable)a.Target;
					wavy.BubblesEnabled = false;
				};
				animator.SetInterpolator (new Android.Views.Animations.DecelerateInterpolator ());
				animator.Start ();
				wavy.BubblesEnabled = true;
			}
			currentRatio = ratio;
		}

		int ComputeLevel (double ratio)
		{
			var level = (int)Math.Round (ratio * Height);
			level = Math.Min (Height, Math.Max (0, level));
			return level;
		}

		public override float TranslationX {
			get {
				return currentEffect;
			}
			set {
				currentEffect = value;
				Invalidate ();
			}
		}

		static void LoadTypefaces (Android.Content.Res.AssetManager assets)
		{
			robotoCondensed = Typeface.Create ("sans-serif-condensed", TypefaceStyle.Normal);
		}

		public override void Layout (int l, int t, int r, int b)
		{
			const float Coeff = 3f;
			base.Layout (l, t, r, b);

			var height = b - t - PaddingTop - PaddingBottom;

			digitPaint.TextSize = 0.5f * height;
			unitPaint.TextSize = 0.10f * height;
			descPaint.TextSize = 0.12f * height;
			padding = 0.13f * height;

			if (wavy == null) {
				wavy = new WaveDrawable (Context, Width, Height);
				wavy.Callback = this;
			} else {
				wavy.ResetSize (Width, Height);
			}
		}

		public override void Draw (Canvas canvas)
		{
			base.Draw (canvas);
			DrawWater (canvas);

			var center = Width / 2;

			descPaint.GetTextBounds (desc, 0, desc.Length, bounds);
			canvas.DrawText (desc, center, padding + bounds.Height (), descPaint);

			var dt = prefs.ConvertDistanceInDisplayUnit (Distance);
			var unit = prefs.GetUnitForDistance (Distance);
			digitPaint.GetTextBounds (dt, 0, dt.Length, bounds);
			canvas.Save ();
			canvas.Scale (1 - currentEffect, (1 - currentEffect) * .4f + .6f, center, Height / 2);
			digitPaint.Alpha = (int)((1 - currentEffect) * 255);
			canvas.DrawText (dt, center, Height / 2 + bounds.Height () / 2, digitPaint);
			canvas.Restore ();

			canvas.DrawText (unit, center, Height - 1.2f * padding, unitPaint);
		}

		protected override bool VerifyDrawable (Drawable who)
		{
			return base.VerifyDrawable (who) || who == wavy;
		}

		void DrawWater (Canvas canvas)
		{
			canvas.Save ();
			var circle = new Path ();
			circle.AddCircle (Width / 2, Height / 2, Height / 2, Path.Direction.Ccw);
			circle.Close ();
			canvas.ClipPath (circle);
			wavy.Draw (canvas);
			canvas.Restore ();
		}

		public override bool OnTouchEvent (MotionEvent e)
		{
			var duration = Resources.GetInteger (Android.Resource.Integer.ConfigShortAnimTime);
			if (e.Action == MotionEventActions.Down) {
				this.Animate ().ScaleX (.9f).ScaleY (.9f).SetDuration (duration).SetStartDelay (0).Start ();
			} else if (e.Action == MotionEventActions.Up) {
				this.Animate ().ScaleX (1).ScaleY (1).SetDuration (duration).SetStartDelay (0).Start ();
				wavy.ComputeTouch (e, 1 - (ScaleX - .9f) * 10);
			} else {
				return false;
			}
			return true;
		}
	}
}

