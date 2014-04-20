using System;
using SQLite;

namespace Bikr
{
	public class BikeTrip
	{
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }

		[Indexed, Column("start")]
		public DateTime StartTime { get; set; }
		[Column("end")]
		public DateTime EndTime { get; set; }
		[Column("commit")]
		public DateTime CommitTime { get; set; }

		[Column ("distance")]
		public double Distance { get; set; }
	}
}

