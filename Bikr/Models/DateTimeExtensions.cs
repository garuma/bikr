using System;

namespace Bikr
{
	public static class DateTimeExtensions
	{
		public static DateTime DayStart (this DateTime dt)
		{
			return dt.Date;
		}

		public static DateTime WeekStart (this DateTime dt)
		{
			return dt.AddDays (-(int)dt.DayOfWeek).Date;
		}

		public static DateTime MonthStart (this DateTime dt)
		{
			return new DateTime (dt.Year, dt.Month, 1, 0, 0, 0, dt.Kind);
		}

		public static DateTime PreviousDay (this DateTime dt)
		{
			return dt.DayStart ().AddDays (-1);
		}

		public static DateTime PreviousWeek (this DateTime dt)
		{
			return dt.WeekStart ().AddDays (-7);
		}

		public static DateTime PreviousMonth (this DateTime dt)
		{
			return dt.MonthStart ().AddMonths (-1);
		}
	}
}

