using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WeatherService.Dao
{
    public class WthNormalParams
    {
        public int AccID { get; set; }
        public int UtilID { get; set; }
        public int UnitID { get; set; }
        public string WthZipCode { get; set; }
        public decimal B1_Original { get; set; }
        public decimal B1_New { get; set; }
        public decimal B2_Original { get; set; }
        public decimal B2_New { get; set; }
        public int B3_Original { get; set; }
        public int B3_New { get; set; }
        public decimal B4_Original { get; set; }
        public decimal B4_New { get; set; }
        public int B5_Original { get; set; }
        public int B5_New { get; set; }
        public decimal? R2_Original { get; set; }
        public decimal R2_New { get; set; }
        public DateTime? YearOfReadsDateStart { get; set; }
        public DateTime? YearOfReadsDateEnd { get; set; }
        public DateTime EndDate_Original {get; set;}
        public int Readings { get; set; }
        public int Days { get; set; }
        public decimal StandardError_New { get; set; }
        public decimal StandardError_Original { get; set; }
        public int RdngID { get; set; }
        public decimal ExpUsage { get; set; }
        public int FTestFailed { get; set; }
    }
}
