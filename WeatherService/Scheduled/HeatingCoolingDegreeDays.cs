using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WeatherService.Scheduled
{
    public class HeatingCoolingDegreeDays
    {
        public double? HDD { get; set; }
        public double? CDD { get; set; }
        public List<double?> HddList { get; set; }
        public List<double?> CddList { get; set; }
    }

    //public HeatingCoolingDegreeDays
    //{
    //    HddList = new List<double?>();
    //}
}
