
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
using Android.Animation;
using Android.Views.Animations;
using Android.Text;

namespace Bikr
{
	public class ScrollBarExtraordinaire : View
	{
		string[] words = new string[] {
			"Day", "Best", "Mean"
		};

		Dictionary<string, StaticLayout> textLayouts;

		Color bgColor;
		TextPaint textPaint;
		Color normalColor, selectedColor;
		Path clipMask;

		float textSize, spacing;
		float knobWidth, knobPadding, cornerRadius;
		int lastTouchX = -1, lastTouchY = -1;

		// selected index + scroll delta
		float scrollPosition;

		public event Action<int> ScrollToIndexRequested;

		public ScrollBarExtraordinaire (Context context) :
			base (context)
		{
			Initialize ();
		}

		public ScrollBarExtraordinaire (Context context, IAttributeSet attrs) :
			base (context, attrs)
		{
			Initialize ();
		}

		public ScrollBarExtraordinaire (Context context, IAttributeSet attrs, int defStyle) :
			base (context, attrs, defStyle)
		{
			Initialize ();
		}

		void Initialize ()
		{
			Clickable = true;
			Click += HandleClick;

			textSize = TypedValue.ApplyDimension (ComplexUnitType.Sp, 14, Resources.DisplayMetrics);
			spacing = TypedValue.ApplyDimension (ComplexUnitType.Dip, 24, Resources.DisplayMetrics);
			knobWidth = TypedValue.ApplyDimension (ComplexUnitType.Dip, 6, Resources.DisplayMetrics);
			knobPadding = TypedValue.ApplyDimension (ComplexUnitType.Dip, 2, Resources.DisplayMetrics);
			cornerRadius = TypedValue.ApplyDimension (ComplexUnitType.Dip, 2, Resources.DisplayMetrics);

			normalColor = Resources.GetColor (Resource.Color.dimmed_text_color);
			selectedColor = Resources.GetColor (Resource.Color.highlight_text_color);

			bgColor = Resources.GetColor (Resource.Color.primary_background);
			textPaint = new TextPaint { AntiAlias = true };
			textPaint.SetTypeface (Typeface.Create ("sans-serif-condensed", TypefaceStyle.Normal));
			textPaint.TextSize = textSize;
			textPaint.TextAlign = Paint.Align.Center;
			textPaint.Color = normalColor;

			CreateTextLayouts ();
		}

		public int SelectedIndex {
			get { return (int)scrollPosition; }
			set {
				SetScrollPosition (value);
			}
		}

		public void SetScrollPosition (float scrollPosition)
		{
			this.scrollPosition = Math.Max (0, Math.Min (words.Length - 1, scrollPosition));
			Invalidate ();
		}

		void CreateTextLayouts ()
		{
			textLayouts = words.ToDictionary (
				w => w,
				w => new StaticLayout (w, textPaint, 0,
				                       Android.Text.Layout.Alignment.AlignNormal,
				                       .80f, 0, false)
			);
		}

		protected override void OnMeasure (int widthMeasureSpec, int heightMeasureSpec)
		{
			var height = (words.Length - 1) * spacing
					+ textLayouts.Values.Sum (l => l.Height)
					+ PaddingTop + PaddingBottom;
			SetMeasuredDimension (widthMeasureSpec, MeasureSpec.MakeMeasureSpec ((int)height, MeasureSpecMode.Exactly));
		}

		protected override void OnLayout (bool changed, int left, int top, int right, int bottom)
		{
			base.OnLayout (changed, left, top, right, bottom);
			clipMask = new Path ();
			var rect = new RectF (0, 0, Width, Height);
			clipMask.AddRoundRect (rect, cornerRadius, cornerRadius, Path.Direction.Cw);
		}

		public override bool OnTouchEvent (MotionEvent e)
		{
			lastTouchX = lastTouchY = -1;
			if (e.Action == MotionEventActions.Up) {
				lastTouchX = (int)e.GetX ();
				lastTouchY = (int)e.GetY ();

				if (lastTouchX < 0 || lastTouchX > Width)
					lastTouchX = -1;
				if (lastTouchY < 0 || lastTouchY > Height)
					lastTouchY = -1;
			}
			return base.OnTouchEvent (e);
		}

		void HandleClick (object sender, EventArgs e)
		{
			if (ScrollToIndexRequested != null && lastTouchX != -1 && lastTouchY != -1) {
				var sectionHeight = Height / words.Length;
				for (int index = 1; index <= words.Length; index++) {
					if (lastTouchY <= sectionHeight * index) {
						ScrollToIndexRequested (index - 1);
						break;
					}
				}
				lastTouchX = lastTouchY = -1;
			}
		}

		public override void Draw (Canvas canvas)
		{
			// Draw background
			canvas.Save ();
			canvas.ClipPath (clipMask);
			canvas.DrawColor (bgColor);
			canvas.Restore ();

			// Draw labels
			var middleX = Width / 2;
			var selectedIndex = SelectedIndex;

			float deltaY = PaddingTop;
			for (int i = 0; i < words.Length; i++) {
				var word = words [i];
				var layout = textLayouts [word];
				var colorRatio = Math.Abs (i - selectedIndex) > 1 ? 0 : 1 - Math.Abs (i - scrollPosition);
				textPaint.Color = ImageUtils.InterpolateColor (colorRatio, normalColor, selectedColor);

				canvas.Save (SaveFlags.Matrix);
				canvas.Translate (middleX, deltaY);
				layout.Draw (canvas);
				canvas.Restore ();
				deltaY += layout.Height + spacing;
			}

			// Draw knob
			canvas.Save ();
			var top = selectedIndex * spacing + words.Take (selectedIndex).Select (w => textLayouts[w].Height).Sum () - knobPadding + PaddingTop;
			var selectedWordHeight = textLayouts [words [selectedIndex]].Height;
			var bottom = top + selectedWordHeight + 2 * knobPadding;
			var offset = scrollPosition - SelectedIndex;
			if (offset > 0 && selectedIndex < words.Length - 1) {
				var extraY = offset * (spacing + selectedWordHeight);
				var nextWordHeight = textLayouts [words [selectedIndex + 1]].Height;
				top += extraY;
				bottom += extraY;
				bottom += offset * (nextWordHeight - selectedWordHeight);
			}

			canvas.ClipRect (0, top, knobWidth, bottom);
			canvas.DrawColor (Color.White);
			canvas.Restore ();
		}
	}
}

