using Newtonsoft.Json;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using WeatherService.Db;
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

                DateTime targetDate = DateTime.Now.AddDays(-1);

                List<string> stationIds = weatherRepository.GetDistinctLocationSationIds();

                foreach (string stationId in stationIds)
                {
                    WeatherData weatherData = 
                        BuildWeatherData(jobParams, stationId, targetDate);

                    Debug(weatherData);
                    //TODO for andy....
                   // weatherRepository.InsertWeatherData(weatherData);

                }
                
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }

            return Task.FromResult(0);
        }

        private IWeatherRepository WeatherRepositoryValueOf(AerisJobParams jobParams)
        {
            return new WeatherRepository(jobParams.DatabaseConnectionString);
        }

        private void Debug(WeatherData weatherData)
        {
            Console.WriteLine("stationId = {0}, dateTime = {1}, maxF = {2}, minF = {3}, " +
                    "avgF = {4}, DewPtAvgF = {5}", weatherData.StationId,
                    weatherData.DateTime, weatherData.MaxF, weatherData.MinF,
                    weatherData.AvgF, weatherData.DewPtAvgF);
        }

        private AerisJobParams AerisJobParamsValueOf(IJobExecutionContext context)
        {
            var schedulerContext = context.Scheduler.Context;
            return (AerisJobParams)schedulerContext.Get("aerisJobParams");
        }

        private WeatherData BuildWeatherData(AerisJobParams jobParams, string stationId, DateTime targetDate)
        {
            
            AerisResult result = GetAerisResponse(jobParams, stationId, targetDate);

            Response response = result.response.First();
            Summary summary = response.periods.First().summary;

            Temp temp = summary.temp;
            Dewpt dewpt = summary.dewpt;

            WeatherData weatherData = new WeatherData();
            weatherData.StationId = response.id;
            weatherData.DateTime = summary.dateTimeISO;
            weatherData.MaxF = temp.maxF;
            weatherData.MinF = temp.minF;
            weatherData.AvgF = temp.avgF;
            weatherData.DewPtAvgF = dewpt.avgF;

            return weatherData;
        }

        private AerisResult GetAerisResponse(AerisJobParams aerisJobParams, string stationId, DateTime targetDate)
        {
            
            string fromDate = targetDate.Date.ToString("MM/dd/yyyy");
            string toDate = targetDate.Date.AddDays(1).ToString("MM/dd/yyyy");

            string rootUrl = $"http://api.aerisapi.com/observations/summary/closest?p={stationId}&from={fromDate}&to={toDate}&fields=id,periods.summary.dateTimeISO,periods.summary.temp.maxF,periods.summary.temp.minF,periods.summary.temp.avgF,periods.summary.dewpt.avgF";
            
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