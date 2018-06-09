using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Quartz;
using Quartz.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WeatherService.Scheduled;

namespace WeatherService.Scheduled
{
    public class AerisJob : IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            
            Console.WriteLine("Execute Task starting...");

            

            try
            {
                Thread.Sleep(5000);

                var schedulerContext = context.Scheduler.Context;
                var jobParams = (AerisJobParams)schedulerContext.Get("aerisJobParams");
                Console.WriteLine("test = {0}", jobParams.AerisAccessId);

                AerisResponse response = GetAerisResponse(jobParams);

                Console.WriteLine("result: stationId = {0}, temp = {1}, weather = {2}, datetime = {3}", 
                    response.StationId,
                    response.Ob.TempF,
                    response.Ob.Weather,
                    response.ObDateTime.ToLocalTime().ToString());
              
            }

            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
            return Task.FromResult(0);
        }

        private AerisResponse GetAerisResponse(AerisJobParams aerisJobParams)
        {
            //string rootUrl = "http://api.aerisapi.com/observations/seattle,wa?fields=ob.tempF,ob.weather";
            string rootUrl = "http://api.aerisapi.com/observations/closest?p=59804&fields=id,obDateTime,ob";

            //observations / summary / closest ? p = 55403

            StringBuilder builder = new StringBuilder();
            builder.Append(rootUrl);
            builder.Append("&client_id=");
            builder.Append(aerisJobParams.AerisAccessId);
            builder.Append("&client_secret=");
            builder.Append(aerisJobParams.AerisSecretKey);

            string url = builder.ToString();
            Console.WriteLine("url = {0}", url);

            using (WebClient wc = new WebClient())
            {
                var json = wc.DownloadString(url);
                Console.WriteLine(json);
                var result = JsonConvert.DeserializeObject<AerisResult>(json);
                return result.AerisResponse;
               
            }
        }
    }
}
