using Accord.Statistics.Models.Regression.Linear;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WeatherService.Model
{
    public class AccordResult
    {
        public MultipleLinearRegression AccordRegression { get; set; }
        public MultipleLinearRegression NNAccordRegression { get; set; }
        public int CoolingBP { get; set; } 
        public int HeatingBP { get; set; }
        public double Error { get; set; }
        public double R2Accord {get; set;}
        public double AdjustedR2Accord { get; set; }
        public double R2Coeff { get; set; }
        public double R2CoffAdj { get; set; }
    }
}