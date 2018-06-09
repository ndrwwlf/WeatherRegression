// To parse this JSON data, add NuGet 'Newtonsoft.Json' then do:
//
//    using WeatherService.Scheduled;
//
//    var aerisResult = AerisResult.FromJson(jsonString);

using System.Collections.Generic;

namespace WeatherService.Scheduled
{
    public class Temp
    {
        public int maxF { get; set; }
        public int minF { get; set; }
        public double avgF { get; set; }
    }

    public class Dewpt
    {
        public int avgF { get; set; }
    }

    public class Summary
    {
        public int timestamp { get; set; }
        public Temp temp { get; set; }
        public Dewpt dewpt { get; set; }
    }

    public class Period
    {
        public Summary summary { get; set; }
    }

    public class Response
    {
        public string id { get; set; }
        public IList<Period> periods { get; set; }
    }

    public class AerisResult
    {
        public bool success { get; set; }
        public object error { get; set; }
        public IList<Response> response { get; set; }
    }


    //public class AerisResult
    //{
    //    public bool Success { get; set; }
    //    public object Error { get; set; }
    //    public Summary Summary { get; set; }
    //    //public Response[] Response { get; set; }
    //}

    ////public class Response
    ////{
    ////    public Period[] Periods { get; set; }
    ////}

    ////public class Period
    ////{
    ////    public Summary Summary { get; set; }
    ////}

    //public class Summary
    //{
    //    public Temp Temp { get; set; }
    //    public Dewpt Dewpt { get; set; }
    //}

    //public class Temp
    //{
    //    public int MaxF { get; set; }
    //    public int MinF { get; set; }
    //    public float AvgF { get; set; }
    //}

    //public class Dewpt
    //{
    //    public float AvgF { get; set; }
    //}


    ////using System;
    ////using System.Collections.Generic;

    ////using System.Globalization;
    ////using Newtonsoft.Json;
    ////using Newtonsoft.Json.Converters;

    ////public partial class AerisResult
    ////{
    ////    [JsonProperty("success")]
    ////    public bool Success { get; set; }

    ////    [JsonProperty("error")]
    ////    public object Error { get; set; }

    ////    [JsonProperty("response")]
    ////    public List<Response> Response { get; set; }
    ////}

    ////public partial class Response
    ////{
    ////    [JsonProperty("periods")]
    ////    public List<Period> Periods { get; set; }
    ////}

    ////public partial class Period
    ////{
    ////    [JsonProperty("summary")]
    ////    public Summary Summary { get; set; }
    ////}

    ////public partial class Summary
    ////{
    ////    [JsonProperty("temp")]
    ////    public Dictionary<string, double> Temp { get; set; }

    ////    [JsonProperty("dewpt")]
    ////    public Dewpt Dewpt { get; set; }
    ////}

    ////public partial class Dewpt
    ////{
    ////    [JsonProperty("avgF")]
    ////    public double AvgF { get; set; }
    ////}

    ////public partial class AerisResult
    ////{
    ////    public static AerisResult FromJson(string json) => JsonConvert.DeserializeObject<AerisResult>(json, WeatherService.Scheduled.Converter.Settings);
    ////}

    ////public static class Serialize
    ////{
    ////    public static string ToJson(this AerisResult self) => JsonConvert.SerializeObject(self, WeatherService.Scheduled.Converter.Settings);
    ////}

    ////internal static class Converter
    ////{
    ////    public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
    ////    {
    ////        MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
    ////        DateParseHandling = DateParseHandling.None,
    ////        Converters = {
    ////            new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
    ////        },
    ////    };
    ////}
}
