using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Quartz;
using Quartz.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace WeatherService.Scheduled
{
    public class AerisJob : IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            Console.WriteLine("xxxxxxxx");

            using (var client = new HttpClient())
            {
                try
                {
                    string url = @"http://api.aerisapi.com/observations/summary/closest?p=55403&from=6/8/2018&to=6/9/2018&fields=id,periods.summary.timestamp,periods.summary.temp.maxF,periods.summary.temp.minF,periods.summary.temp.avgF,periods.summary.dewpt.avgF&client_id=OykRW4nKiliWp4Ge6YsGy&client_secret=MGeWiicMgXt7nRD0o2LQ5rwW5uAKoCUdgU46WbZ4";
                    using (WebClient wc = new WebClient())
                    {
                        var json = wc.DownloadString(url);
                        Console.WriteLine(json);
                        var result = JsonConvert.DeserializeObject<AerisResult>(json);
                        Console.WriteLine(result.response.First().periods.First().summary.temp.avgF);

                    }
                }
                catch (HttpRequestException httpRequestException)
                {
                    Console.WriteLine($"Error getting weather from Aeris: {httpRequestException.Message}");
                }

            }
            return Task.FromResult(0);
        }   
    }
}