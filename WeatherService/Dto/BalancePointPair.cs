using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WeatherService.Dto
{
    public class BalancePointPair
    {
        public int RdngID { get; set; }
        public int DaysInReading { get; set; }

        public double CoolingDegreeDays { get; set; }
        public double HeatingDegreeDays { get; set; }

        public decimal ExpUsage_New { get; set; }
        public int ActualUsage { get; set; }
        public decimal ExpUsage_Original { get; set; }

        public DateTime YearOfReadsDateStart { get; set; }
        public DateTime YearOfReadsDateEnd { get; set; }
        public DateTime EndDateOriginal { get; set; }
        public int ReadingsInNormalYear { get; set; }
        public int DaysInNormalYear { get; set; }
        public string WthZipCode { get; set; }

        public decimal B1_New { get; set; }
        public decimal B2_New { get; set; }
        public int HeatingBalancePoint { get; set; }
        public decimal B4_New { get; set; }
        public int CoolingBalancePoint { get; set; }
        public double? RSquared_New { get; set; }
        public double StandardError { get; set; }
    }
}
