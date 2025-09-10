using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScreenTracker1.Models
{
    public class PagedResultWrapper<T>
    {
        public List<T> Users { get; set; } = new();
        public int TotalCount { get; set; }
    }
}
