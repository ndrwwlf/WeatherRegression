using Accord.Statistics.Models.Regression.Linear;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WeatherService.Model
{
    public class AccordResultNew
    {
        public bool IsSimpleSingleRegression { get; set; }
        public bool IsMultipleLinearRegression { get; set; }
        public double R2Accord { get; set; }

        public double Intercept { get; set; }
        public double B2 { get; set; }
        public int HeatingBP { get; set; }
        public double B4 { get; set; }
        public int CoolingBP { get; set; }
        
        public bool FTestFailed { get; set; }
    }
}
