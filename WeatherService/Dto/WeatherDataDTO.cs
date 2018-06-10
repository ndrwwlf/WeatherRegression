using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WeatherService.Dto
{
    public class WeatherDataDTO
    {
        public string StationId { get; set; }
        public DateTime DateTime { get; set; }
        public double MaxF { get; set; }
        public double MinF { get; set; }
        public double AvgF { get; set; }
        public double DewPtAvgF { get; set; }
    }
}
