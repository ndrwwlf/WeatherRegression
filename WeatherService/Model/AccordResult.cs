using Accord.Statistics.Analysis;
using Accord.Statistics.Models.Regression.Linear;
using Accord.Statistics.Testing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WeatherService.Dto;

namespace WeatherService.Model
{
    public class AccordResult
    {
        public SimpleLinearRegression SimpleLinearRegression { get; set; }
        public MultipleLinearRegression MultipleRegression { get; set; }
        public MultipleLinearRegression NNMultipleAccordRegression { get; set; }
        public bool IsSimpleSingleRegression { get; set; }
        public bool IsMultipleLinearRegression { get; set; }
        public int CoolingBP { get; set; } 
        public int HeatingBP { get; set; }
        public double SsyError { get; set; }
        public double R2Accord {get; set;}
        public double AdjustedR2Accord { get; set; }
        public double R2Coeff { get; set; }
        public double R2CoffAdj { get; set; }
        public int AccID { get; set; }
        public int UtilID { get; set; }
        public int UnitID { get; set; }
        public MultipleLinearRegressionAnalysis MLRA { get; set; }
        public TTest TTest { get; set; }
        public double Intercept { get; set; }
        public BalancePointPair bpPair { get; set; }
    }
}