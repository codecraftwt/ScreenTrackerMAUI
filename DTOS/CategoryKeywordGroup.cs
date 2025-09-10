using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScreenTracker1.DTOS
{
    public class CategoryKeywordGroup
    {
        public string category { get; set; }
        public double totalDurationInMinutes { get; set; }
        public List<CategoryTitle> titles { get; set; }
    }
}
