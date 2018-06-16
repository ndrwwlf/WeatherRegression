using Newtonsoft.Json;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using WeatherService.Db;
using WeatherService.Dto;
using WeatherService.Model;
using WeatherService.Services;

namespace WeatherService.Scheduled
{
    public class AerisJob : IJob
    {
       
        public Task Execute(IJobExecutionContext context)
        {
            
            Console.WriteLine("Execute Task starting...");

            try
            {
                AerisJobParams jobParams = AerisJobParamsValueOf(context);
                IWeatherRepository weatherRepository = WeatherRepositoryValueOf(jobParams);

                //RunHistorical(weatherRepository, jobParams, 30);
                Run(weatherRepository, jobParams, -1);


            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }

            return Task.FromResult(0);
        }

        private void Run(IWeatherRepository weatherRepository, AerisJobParams jobParams, int i)
        {
            DateTime targetDate = DateTime.Now.AddDays(i);
            Console.WriteLine(targetDate.ToShortDateString());

            List<string> zipCodes = weatherRepository.GetDistinctZipCodes();

            zipCodes.Add("80482");
            zipCodes.Add("94304");

            foreach (string zipCode in zipCodes)
            {
                if (!weatherRepository.GetWeatherDataExistForZipAndDate(zipCode, targetDate))
                {
                    try
                    {
                        WeatherData weatherData = BuildWeatherData(jobParams, zipCode, targetDate);

                        weatherRepository.InsertWeatherData(weatherData);
                    } catch (Exception e)
                    {
                        Console.WriteLine($"Error >>>> {e.Message}");
                    }
                } else
                {
                    Console.WriteLine($"data already exists for {zipCode} and {targetDate.ToShortDateString()}");
                }
            };
        }

        private void RunHistorical(IWeatherRepository weatherRepository, AerisJobParams jobParams, int j, int k)
        {
            DateTime now = new DateTime().ToLocalTime();
            // yyyy, mm, dd
            DateTime fromDate = new DateTime(2016, 12, 01);
            DateTime toDate = new DateTime(2016, 12, 31);
            int days = (int)fromDate.Subtract(toDate).TotalDays;

            //for (int i = -925; i <= -900; i++)
            for (int i = -1290; i <= 1261; i++)
            {
                Run(weatherRepository, jobParams, i);
            };
        }

        private IWeatherRepository WeatherRepositoryValueOf(AerisJobParams jobParams)
        {
            return new WeatherRepository(jobParams.DefaultConnectionString, jobParams.JitWebData3ConnectionString);
        }

        private AerisJobParams AerisJobParamsValueOf(IJobExecutionContext context)
        {
            var schedulerContext = context.Scheduler.Context;
            return (AerisJobParams)schedulerContext.Get("aerisJobParams");
        }

        private WeatherData BuildWeatherData(AerisJobParams jobParams, string zipCode, DateTime targetDate)
        {
            AerisResult result = GetAerisResponse(jobParams, zipCode, targetDate);

            Response response = result.Response.First();
            Summary summary = response.Periods.First().Summary;

            Temp temp = summary.Temp;
            Dewpt dewpt = summary.Dewpt;

            WeatherData weatherData = new WeatherData
            {
                StationId = response.Id,
                ZipCode = zipCode,
                RDate = targetDate,
                HighTmp = (int?)(temp.MaxF.HasValue ? (int?)Math.Round(temp.MaxF.Value) : temp.MaxF),
                LowTmp = (int?)(temp.MinF.HasValue ? Math.Round(temp.MinF.Value) : temp.MinF),
                AvgTmp = temp.AvgF,
                DewPt = dewpt.AvgF
            };

            return weatherData;
        }

        private AerisResult GetAerisResponse(AerisJobParams aerisJobParams, string zipCode, DateTime targetDate)
        {
            Console.WriteLine("ZIP:sighhh {0}", zipCode);
            
            string fromDate = targetDate.Date.ToString("MM/dd/yyyy");
            string toDate = targetDate.Date.AddDays(1).ToString("MM/dd/yyyy");
            //string rootUrl = "http://api.aerisapi.com/observations/summary/closest?p=94304&query=maxt:!NULL,maxdewpt:!NULL&from=12/03/2014&to=12/03/2014&fields=id,periods.summary.dateTimeISO,periods.summary.temp.maxF,periods.summary.temp.minF,periods.summary.temp.avgF,periods.summary.dewpt.avgF";

            //string rootUrl = $"http://api.aerisapi.com/observations/summary/closest?p={zipCode}&query=maxt:!NULL,maxdewpt:!NULL" +
            //    $"&from={fromDate}&to={toDate}&fields=id,periods.summary.dateTimeISO,periods.summary.temp.maxF,periods.summary.temp.minF,periods.summary.temp.avgF,periods.summary.dewpt.avgF";

            string rootUrl = $"http://api.aerisapi.com/observations/summary/closest?p={zipCode}&query=maxt:!NULL,maxdewpt:!NULL" +
                $"&from={fromDate}&to={fromDate}&fields=id,periods.summary.dateTimeISO,periods.summary.temp.maxF,periods.summary.temp.minF,periods.summary.temp.avgF,periods.summary.dewpt.avgF";

            StringBuilder builder = new StringBuilder();
            builder.Append(rootUrl);
            builder.Append("&client_id=");
            builder.Append(aerisJobParams.AerisAccessId);
            builder.Append("&client_secret=");
            builder.Append(aerisJobParams.AerisSecretKey);

            string url = builder.ToString();
            Console.WriteLine("urlx = {0}", url);

            using (WebClient wc = new WebClient())
            {
                var json = wc.DownloadString(url);
                Console.WriteLine("json: {0}", json);
                Console.WriteLine("zip = {0}", zipCode);
                //return JsonConvert.DeserializeObject<AerisResult>(json);
                return JsonConvert.DeserializeObject<AerisResult>(json, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Include });       
            }
        }

        private void GetWthNormalParamsResultSet(AerisJobParams aerisJobParams)
        {
            //return false;;
        }

    }
}