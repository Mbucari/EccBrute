
namespace EccBrute
{
	class WorkProgress
	{
		public int ThreadID { get; set; }
		public FastEccPoint CurrentPoint { get; set; }
		public long Start { get; set; }
		public long[] CurrentPosition { get; set; }
		public long End { get; set; }
	}
}
