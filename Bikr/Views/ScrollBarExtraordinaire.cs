
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

namespace Bikr
{
	public class ScrollBarExtraordinaire : View
	{
		struct CharSize {
			public float Height, Width;
		}

		string[] words = new string[] {
			"Day", "Best", "Mean"
		};

		Dictionary<char, CharSize> charMetrics = new Dictionary<char, CharSize> ();
		Dictionary<string, float> wordHeights = new Dictionary<string, float> ();

		Color bgColor;
		Paint textPaint;
		Color normalColor, selectedColor;
		Paint.FontMetrics fontMetrics;
		Path clipMask;

		float textSize, spacing, letterSpacing;
		float knobWidth, knobPadding, cornerRadius;
		int lastTouchX = -1, lastTouchY = -1;

		// selected index + scroll delta
		float scrollPosition;
		float overscrollAmount, maxOverscrollAmount;
		ITimeInterpolator overscrollInterpolator;

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
			maxOverscrollAmount = TypedValue.ApplyDimension (ComplexUnitType.Dip, 50, Resources.DisplayMetrics);

			normalColor = Resources.GetColor (Resource.Color.dimmed_text_color);
			selectedColor = Resources.GetColor (Resource.Color.highlight_text_color);

			bgColor = Resources.GetColor (Resource.Color.primary_background);
			textPaint = new Paint { AntiAlias = true };
			textPaint.SetTypeface (Typeface.Create ("sans-serif-condensed", TypefaceStyle.Normal));
			textPaint.TextSize = textSize;
			textPaint.TextAlign = Paint.Align.Center;
			textPaint.Color = normalColor;

			fontMetrics = textPaint.GetFontMetrics ();
			letterSpacing = fontMetrics.Bottom;
			ComputeCharacterSizes ();
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

		public void AbsorbOverscroll (float amount)
		{
			this.overscrollAmount = amount;
			Invalidate ();
		}

		void ComputeCharacterSizes ()
		{
			var chars = words.SelectMany (w => w.ToCharArray ()).Distinct ().ToArray ();
			var rect = new Rect ();
			var array = new char[1];
			foreach (var c in chars) {
				array [0] = c;
				textPaint.GetTextBounds (array, 0, 1, rect);
				charMetrics [c] = new CharSize { Height = rect.Height (), Width = rect.Width () };
			}
		}

		float GetWordHeight (string word)
		{
			float result;
			if (wordHeights.TryGetValue (word, out result))
				return result;

			result = word.Select (c => charMetrics [c].Height).Sum () + (word.Length - 1) * letterSpacing;
			wordHeights.Add (word, result);

			return result;
		}

		protected override void OnMeasure (int widthMeasureSpec, int heightMeasureSpec)
		{
			var height = (words.Length - 1) * spacing + words.Select (w => GetWordHeight (w)).Sum () + PaddingTop + PaddingBottom;
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
				var pos = new float[2 * word.Length];

				for (int j = 0; j < word.Length; j++) {
					var metrics = charMetrics [word [j]];
					pos [2 * j] = (int)(middleX - metrics.Width / 2);
					pos [2 * j + 1] = deltaY + metrics.Height;
					deltaY += metrics.Height;
					if (j != word.Length - 1)
						deltaY += letterSpacing;
				}
				var colorRatio = Math.Abs (i - selectedIndex) > 1 ? 0 : 1 - Math.Abs (i - scrollPosition);
				textPaint.Color = ImageUtils.InterpolateColor (colorRatio, normalColor, selectedColor);
				canvas.DrawPosText (word, pos, textPaint);
				deltaY += spacing;
			}

			// Draw knob
			canvas.Save ();
			var top = selectedIndex * spacing + words.Take (selectedIndex).Select (GetWordHeight).Sum () - knobPadding + PaddingTop;
			var selectedWordHeight = GetWordHeight (words [selectedIndex]);
			var bottom = top + selectedWordHeight + 2 * knobPadding;
			var offset = scrollPosition - SelectedIndex;
			if (offset > 0 && selectedIndex < words.Length - 1) {
				var extraY = offset * (spacing + selectedWordHeight);
				var nextWordHeight = GetWordHeight (words [selectedIndex + 1]);
				top += extraY;
				bottom += extraY;
				bottom += offset * (nextWordHeight - selectedWordHeight);
			}
			if (overscrollAmount != 0 && (selectedIndex == 0 || selectedIndex == words.Length - 1)) {
				var ratio = overscrollInterpolator.GetInterpolation (Math.Min (Math.Abs (overscrollAmount), maxOverscrollAmount) / maxOverscrollAmount);
				var absorbedLength = ratio * ((bottom - top) / 4);
				if (overscrollAmount < 0)
					bottom -= (int)absorbedLength;
				else
					top += (int)absorbedLength;
			}

			canvas.ClipRect (0, top, knobWidth, bottom);
			canvas.DrawColor (Color.White);
			canvas.Restore ();
		}
	}
}

