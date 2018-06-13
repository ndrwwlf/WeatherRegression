using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WeatherService.Model
{
    public class WeatherData
    {
        public int Id { get; set; }
        public string StationId { get; set; }
        public DateTime RDate { get; set; }
        public double MaxF { get; set; }
        public double MinF { get; set; }
        public double AvgF { get; set; }
        public double DewPtAvgF { get; set; }
    }
}
