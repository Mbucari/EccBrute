using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EccBrute
{
	[Serializable]
	class WorkProgress
	{
		public int ThreadID { get; set; }
		public FastEccPoint CurrentPoint { get; set; }
		public long Start { get; set; }
		public long CurrentPosition { get; set; }
		public long End { get; set; }
	}

}
