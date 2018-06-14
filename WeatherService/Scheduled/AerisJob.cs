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

                //RunHistorical(weatherRepository, jobParams, 10);
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

            zipCodes.ForEach(delegate (string zipCode)
            {
                if (!weatherRepository.GetWeatherDataExistForZipAndDate(zipCode, targetDate))
                {
                    WeatherData weatherData = BuildWeatherData(jobParams, zipCode, targetDate);
                    weatherRepository.InsertWeatherData(weatherData);
                } else
                {
                    Console.WriteLine("data already exists for {0} and {1}",
                        zipCode, targetDate.ToShortDateString());
                }

            });
        }

        private void RunHistorical(IWeatherRepository weatherRepository, AerisJobParams jobParams, int j)
        {
            for (int i = -925; i <= -900; i++)
            {
                Run(weatherRepository, jobParams, i);
            }
        }

        private IWeatherRepository WeatherRepositoryValueOf(AerisJobParams jobParams)
        {
            return new WeatherRepository(jobParams.DatabaseConnectionString);
        }

        private AerisJobParams AerisJobParamsValueOf(IJobExecutionContext context)
        {
            var schedulerContext = context.Scheduler.Context;
            return (AerisJobParams)schedulerContext.Get("aerisJobParams");
        }

        private WeatherData BuildWeatherData(AerisJobParams jobParams, string zipCode, DateTime targetDate)
        {
            
            AerisResult result = GetAerisResponse(jobParams, zipCode, targetDate);

            Response response = result.response.First();
            Summary summary = response.periods.First().summary;

            Temp temp = summary.temp;
            Dewpt dewpt = summary.dewpt;

            WeatherData weatherData = new WeatherData
            {
                StationId = response.id,
                ZipCode = zipCode,
                RDate = targetDate,
                HighTmp = temp.maxF,
                LowTmp = temp.minF,
                AvgTmp = temp.avgF,
                DewPt = dewpt.avgF
            };

            return weatherData;
        }

        private AerisResult GetAerisResponse(AerisJobParams aerisJobParams, string zipCode, DateTime targetDate)
        {
            
            string fromDate = targetDate.Date.ToString("MM/dd/yyyy");
            string toDate = targetDate.Date.AddDays(1).ToString("MM/dd/yyyy");
            string rootUrl = $"http://api.aerisapi.com/observations/summary/closest?p={zipCode}&query=maxt:!NULL,mint:!NULL,avgt:!NULL,avgdewpt:!NULL&fields=id,loc,place,periods.summary.dateTimeISO,periods.summary.temp.maxF,periods.summary.temp.minF,periods.summary.temp.avgF,periods.summary.dewpt.avgF&from={fromDate}&to={toDate}&plimit=1";
            rootUrl += "&fields=id,periods.summary.dateTimeISO,periods.summary.temp.maxF,periods.summary.temp.minF,periods.summary.temp.avgF,periods.summary.dewpt.avgF";
            
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
                return JsonConvert.DeserializeObject<AerisResult>(json);
            }
        }

    }
}