
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

namespace Bikr
{
	public class SnapScrollView : ScrollView
	{
		ScrollBarExtraordinaire scrollBar;

		Handler handler;
		bool animationOnGoing, snapNeeded;

		public SnapScrollView (Context context) :
			base (context)
		{
			Initialize ();
		}

		public SnapScrollView (Context context, IAttributeSet attrs) :
			base (context, attrs)
		{
			Initialize ();
		}

		public SnapScrollView (Context context, IAttributeSet attrs, int defStyle) :
			base (context, attrs, defStyle)
		{
			Initialize ();
		}

		void Initialize ()
		{
			handler = new Handler ();
		}

		public IEnumerable<View> GetScrollChildViews ()
		{
			var scrollContainer = (ViewGroup)GetChildAt (0);
			for (int i = 0; i < scrollContainer.ChildCount; i++)
				yield return scrollContainer.GetChildAt (i);
		}

		public void RegisterExtraordinaireScrollBar (ScrollBarExtraordinaire scrollBar)
		{
			this.scrollBar = scrollBar;
			scrollBar.ScrollToIndexRequested += index => SmoothScrollTo (0, Height * index);
		}

		public override bool OnTouchEvent (MotionEvent e)
		{
			var result = base.OnTouchEvent (e);
			if (e.Action == MotionEventActions.Up || e.Action == MotionEventActions.Cancel)
				ApplySnappingRule ();
			return result;
		}

		protected override void OnDraw (Android.Graphics.Canvas canvas)
		{
			animationOnGoing = false;
			base.OnDraw (canvas);
			if (!animationOnGoing && snapNeeded) {
				snapNeeded = false;
				handler.PostDelayed (ApplySnappingRule, 100);
			}
		}

		void ApplySnappingRule ()
		{
			if ((ScrollY % Height) != 0) {
				if (animationOnGoing)
					snapNeeded = true;
				else {
					var delta = ScrollY % Height;
					SmoothScrollBy (0, delta > Height / 2 ? Height - delta : -delta);
				}
			}
		}

		protected override void OnLayout (bool changed, int left, int top, int right, int bottom)
		{
			if (changed) {
				var height = Height;
				foreach (var c in GetScrollChildViews ())
					if (c.MinimumHeight != height)
						c.SetMinimumHeight (height);
			}
			base.OnLayout (changed, left, top, right, bottom);
		}

		public override void PostInvalidateOnAnimation ()
		{
			animationOnGoing = true;
			base.PostInvalidateOnAnimation ();
		}

		protected override void OnScrollChanged (int l, int t, int oldl, int oldt)
		{
			base.OnScrollChanged (l, t, oldl, oldt);
			if (scrollBar != null)
				scrollBar.SetScrollPosition (t / Height + (t % Height) / (float)Height);
		}
	}
}

