using System;

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
        public double HighTmp { get; set; }
        public double LowTmp { get; set; }
        public double AvgTmp { get; set; }
        public double DewPt { get; set; }
    }
}
