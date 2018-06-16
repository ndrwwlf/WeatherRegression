using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace WeatherService.Scheduled
{
    public class AerisResult
    {
        public bool Success { get; set; }
        public object Error { get; set; }
        public IList<Response> Response { get; set; }
    }

    public class Response
    {
        public string Id { get; set; }
        public IList<Period> Periods { get; set; }
    }

    public class Temp
    {
        public double? MaxF { get; set; }
        public double? MinF { get; set; }
        public double? AvgF { get; set; }
    }

    public class Dewpt
    {
        public double? AvgF { get; set; }
    }

    public class Summary
    {
        public DateTime DateTimeISO { get; set; }
        public Temp Temp { get; set; }
        public Dewpt Dewpt { get; set; }
    }

    public class Period
    {
        public Summary Summary { get; set; }
    }

    //public class Loc
    //{
    //    public double? Latitude { get; set; }
    //    public double? Longitude { get; set; }
    //}

    //public class Place
    //{
    //    public string Name { get; set; }
    //    public string State { get; set; }
    //}
}
