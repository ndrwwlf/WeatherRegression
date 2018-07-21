using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WeatherService.Dao
{
    public class WthExpUsage
    {
        public int RdngID { get; set; }
        public int AccID { get; set; }
        public int UtilID { get; set; }
        public int UnitID { get; set; }
        public decimal? Units { get; set; }
        public decimal? ExpUsage_New { get; set; }
        public decimal? PercentDelta_New { get; set; }
        public decimal? ExpUsage_Old { get; set; }
        public decimal? PercentDelta_Old { get; set; }
        public DateTime DateStart { get; set; }
        public DateTime DateEnd { get; set; }
    }
}