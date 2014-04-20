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
using Android.Util;

namespace Bikr
{
	class CircleLinearLayout : ViewGroup
	{
		int spacing;

		public CircleLinearLayout (Context ctx) : base (ctx)
		{
			Initialize ();
		}

		public CircleLinearLayout (Context ctx, IAttributeSet attrs) : base (ctx, attrs)
		{
			Initialize ();
		}

		public CircleLinearLayout (Context ctx, IAttributeSet attrs, int defStyle) : base (ctx, attrs, defStyle)
		{
			Initialize ();
		}

		void Initialize ()
		{
			spacing = (int)TypedValue.ApplyDimension (ComplexUnitType.Dip, 24, Context.Resources.DisplayMetrics);
		}

		public override bool ShouldDelayChildPressedState ()
		{
			return false;
		}

		protected override void OnMeasure (int widthMeasureSpec, int heightMeasureSpec)
		{
			SetMeasuredDimension (ResolveSize (SuggestedMinimumWidth, widthMeasureSpec),
			                      ResolveSize (SuggestedMinimumHeight, heightMeasureSpec));
		}

		protected override void OnLayout (bool changed, int l, int t, int r, int b)
		{
			var totalWidth = Math.Abs (r - l);
			var totalHeight = Math.Abs (b - t);

			// Vertical layout ?
			if (Resources.Configuration.Orientation != Android.Content.Res.Orientation.Landscape) {
				var mainSize = (int)Math.Round (totalHeight * 0.45);

				// First child get half the available height and is middleX aligned
				var horizontalOffset = ((r - l) - mainSize) / 2;
				GetChildAt (0).Layout (horizontalOffset, 0, horizontalOffset + mainSize, mainSize);

				// Then the rest is split between the remaining children which are offset along the Y axis
				var middleX = (r - l) / 2;
				var remainingSize = totalHeight - mainSize;

				// From deriving a quadratic equation of remainingSize in terms of r and spacing
				var childSize = CalculateChildSize (remainingSize - spacing, spacing);

				// Margin between the first and second element
				var topMargin = CalculateVerticalSpacing (mainSize / 2, childSize / 2);
				// Margin between each of the subsequent child
				var childMargin = CalculateChildVerticalSpacing (childSize / 2);

				int off = 0;
				for (int i = 1; i < ChildCount; i++) {
					var child = GetChildAt (i);
					var left = middleX - off * childSize;
					var top = mainSize + topMargin + (i - 1) * (childMargin + childSize);
					child.Layout (left, top, left + childSize, top + childSize);
					off = (off + 1) % 2;
				}
			} else {
				var childSize = Math.Min (totalHeight,
				                          (totalWidth - 2 * spacing - (ChildCount - 1) * spacing / 2) / ChildCount);
				int off = spacing;
				var innerSpacing = (int)((totalWidth - 2 * spacing - ChildCount * childSize) / (ChildCount - 1));

				for (int i = 0; i < ChildCount; i++) {
					var child = GetChildAt (i);
					child.Layout (off, totalHeight - childSize, off + childSize, totalHeight);
					off += innerSpacing + childSize;
				}
			}
		}

		int CalculateChildSize (int h, int sp)
		{
			return 2 * (int)Math.Round (
				(4 * (h + sp) - Math.Sqrt (Math.Pow (4 * (-h - sp), 2) - 16 * (h * h - sp * sp))) / 8
			);
		}

		int CalculateVerticalSpacing (double radius1, double radius2)
		{
			return (int)Math.Round (
				Math.Sqrt (Math.Pow (radius1 + spacing + radius2, 2) - Math.Pow (radius2, 2)) - radius2 - radius1
			);
		}

		int CalculateChildVerticalSpacing (double radius)
		{
			var doubleRad = 2 * radius;
			return (int)Math.Round (
				Math.Sqrt (Math.Pow (doubleRad + spacing, 2) - Math.Pow (doubleRad, 2)) - doubleRad
			);
		}
	}
}

