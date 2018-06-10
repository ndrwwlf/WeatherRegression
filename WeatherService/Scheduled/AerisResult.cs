using System;
using System.Collections.Generic;

namespace WeatherService.Scheduled
{

    public class AerisResult
    {
        public bool success { get; set; }
        public object error { get; set; }
        public IList<Response> response { get; set; }
    }

    public class Response
    {
        public string id { get; set; }
        public IList<Period> periods { get; set; }
    }

    public class Temp
    {
        public int maxF { get; set; }
        public int minF { get; set; }
        public double avgF { get; set; }
    }

    public class Dewpt
    {
        public double avgF { get; set; }
    }

    public class Summary
    {
        public DateTime dateTimeISO { get; set; }
        public Temp temp { get; set; }
        public Dewpt dewpt { get; set; }
    }

    public class Period
    {
        public Summary summary { get; set; }
    }
}
