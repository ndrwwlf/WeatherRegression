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

                DateTime targetDate = DateTime.Now.AddDays(-1);

                List<string> stationIds = weatherRepository.GetDistinctLocationSationIds();

                //stationIds.ForEach(delegate (string stationId)
                //{
                //    WeatherDataDTO weatherDataDTO = BuildWeatherData(jobParams, stationId, targetDate);
                //    weatherRepository.InsertWeatherData(weatherDataDTO);
                //});

                WeatherDataDTO weatherDataDTO = BuildWeatherData(jobParams, "KSEA", targetDate);
                weatherRepository.InsertWeatherData(weatherDataDTO);

                //for (int i = 0; i < stationIds.Count; i++)
                //{
                //    WeatherDataDTO weatherDataDTO =
                //        BuildWeatherData(jobParams, stationIds.ElementAt(i), targetDate);

                //    Debug(weatherDataDTO);
                //    //TODO for andy....
                //    weatherRepository.InsertWeatherData(weatherDataDTO);

                //}

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

        private void Debug(WeatherDataDTO weatherDataDTO)
        {
            Console.WriteLine("stationId = {0}, dateTime = {1}, maxF = {2}, minF = {3}, " +
                    "avgF = {4}, DewPtAvgF = {5}", weatherDataDTO.StationId,
                    weatherDataDTO.DateTime, weatherDataDTO.MaxF, weatherDataDTO.MinF,
                    weatherDataDTO.AvgF, weatherDataDTO.DewPtAvgF);
        }

        private AerisJobParams AerisJobParamsValueOf(IJobExecutionContext context)
        {
            var schedulerContext = context.Scheduler.Context;
            return (AerisJobParams)schedulerContext.Get("aerisJobParams");
        }

        private WeatherDataDTO BuildWeatherData(AerisJobParams jobParams, string stationId, DateTime targetDate)
        {
            
            AerisResult result = GetAerisResponse(jobParams, stationId, targetDate);

            Response response = result.response.First();
            Summary summary = response.periods.First().summary;

            Temp temp = summary.temp;
            Dewpt dewpt = summary.dewpt;

            WeatherDataDTO weatherDataDTO = new WeatherDataDTO
            {
                StationId = response.id,
                DateTime = summary.dateTimeISO,
                MaxF = temp.maxF,
                MinF = temp.minF,
                AvgF = temp.avgF,
                DewPtAvgF = dewpt.avgF
            };

            return weatherDataDTO;
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