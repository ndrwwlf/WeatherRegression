using Newtonsoft.Json;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using WeatherService.Dao;
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

            AerisJobParams jobParams = AerisJobParamsValueOf(context);
            IWeatherRepository weatherRepository = WeatherRepositoryValueOf(jobParams);

            GatherWeatherData(jobParams, weatherRepository);

            //PopulateWthExpdUsageTable(weatherRepository);

            return Task.FromResult(0);
        }

        private void PopulateWthExpdUsageTable(IWeatherRepository weatherRepository)
        {
            // Normalized Energy Usage = E = B1(DAYS) + B2(HDDB3) + B4(CDDB5)
            // [10662.9796123151 x (Days)] + [0 x (HDDB3)] + [88.12471482 x (CDDB5)]
            // HDDB3 = SUM OF ALL HEATING DEGREE DAYS IN BILLING PERIOD (if there were 31 days)
            /*
             * EXAMPLE OF HEATING DEGREE DAYS AND COOLING DEGREE DAYS
                Baseline = 60
                Mean Daily Temp was 70
                Then 10 Cooling Degree Days for that Day

                Baseline = 60
                Mean Daily Temp was 45
                Then 15 Heating Degree Days for that Day
            */
            try
            {

                List<ReadingsQueryResult> readings = weatherRepository.GetReadings("12-1-2016");

                foreach (ReadingsQueryResult result in readings)
                {
                    //Console.WriteLine(result.DateStart + ", " + result.DateEnd + ", " + result.Days + ",  result.B1 = " + result.B1 + ",  result.B2 = " + result.B2 + ", result.B3 = " + result.B3 + ",  result.B4 = " + result.B4);

                    List<WeatherData> weatherDataList = weatherRepository.GetWeatherDataByZipStartAndEndDate(result.Zip, result.DateStart, result.DateEnd);
                    HeatingCoolingDegreeDays heatingCoolingDegreeDays = HeatingCoolingDegreeDaysValueOf(result, weatherDataList);

                    DoCalculation(result, heatingCoolingDegreeDays, weatherRepository);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
            }
           
        }

        private void DoCalculation(ReadingsQueryResult result, HeatingCoolingDegreeDays heatingCoolingDegreeDays, IWeatherRepository weatherRepository)
        {
            // Normalized Energy Usage = E = B1(DAYS) + B2(HDDB3) + B4(CDDB5)
            double? resultAsDouble = (result.B1 * result.Days) + (result.B2 * heatingCoolingDegreeDays.HDD) + (result.B4 * heatingCoolingDegreeDays.CDD);
            decimal resultAsDecimal = decimal.Round(Convert.ToDecimal(resultAsDouble), 4, MidpointRounding.AwayFromZero);
            weatherRepository.InsertWthExpUsage(result.RdngID, resultAsDecimal);
            Console.WriteLine("x = " + resultAsDecimal);
        }

        private HeatingCoolingDegreeDays HeatingCoolingDegreeDaysValueOf(ReadingsQueryResult result, List<WeatherData> weatherDataList)
        {
            HeatingCoolingDegreeDays hcdd = new HeatingCoolingDegreeDays();
            hcdd.CDD = 0.0;
            hcdd.HDD = 0.0;

            if (result.B3 == 0 && result.B5 == 0)
            {
                return hcdd;
            }

            foreach (WeatherData weatherData in weatherDataList)
            {
                
                //Console.WriteLine(weatherData.Id + "," + weatherData.RDate + ", " + weatherData.LowTmp + "," + weatherData.HighTmp + ", " + weatherData.AvgTmp);

                if (result.B5 > 0)
                {
                    if (weatherData.AvgTmp >= result.B5)
                    {
                        hcdd.CDD = hcdd.CDD + (weatherData.AvgTmp - result.B5 );
                    } 
                    
                } else if (result.B3 > 0)
                {
                    if (weatherData.AvgTmp <= result.B3)
                    {
                        hcdd.HDD = hcdd.HDD + (result.B3 - weatherData.AvgTmp);
                    }
                } 

               // Console.WriteLine("hcdd.CDD = " + hcdd.CDD);
               // Console.WriteLine("hcdd.HDD = " + hcdd.HDD);

            }

            return hcdd;
        }

        private void GatherWeatherData(AerisJobParams jobParams, IWeatherRepository weatherRepository)
        {
            Console.WriteLine("Execute Task starting...");

            try
            {
                //GatherHistoricalWeatherData(weatherRepository, jobParams);
                GatherDailyWeatherData(weatherRepository, jobParams, -1);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }

        private void GatherDailyWeatherData(IWeatherRepository weatherRepository, AerisJobParams jobParams, int i)
        {
            DateTime targetDate = DateTime.Now.AddDays(i);
            Console.WriteLine(targetDate.ToShortDateString());

            List<string> zipCodes = weatherRepository.GetDistinctZipCodes();

            foreach (string zipCode in zipCodes)
            {
                if (!weatherRepository.GetWeatherDataExistForZipAndDate(zipCode, targetDate))
                {
                    try
                    {
                        WeatherData weatherData = BuildWeatherData(jobParams, zipCode, targetDate);
                        Console.WriteLine(weatherData.Id + "," + weatherData.RDate + ", " + weatherData.LowTmp + "," + weatherData.HighTmp + ", " + weatherData.AvgTmp);
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

        private void GatherHistoricalWeatherData(IWeatherRepository weatherRepository, AerisJobParams jobParams)
        {
            DateTime today = DateTime.Now;

            // yyyy, mm, dd
            DateTime fromDate = new DateTime(2016, 12, 01);
            
            int days = (int)fromDate.Subtract(today).TotalDays;

            for (int i = days; i <= -1; i++)
            {
                GatherDailyWeatherData(weatherRepository, jobParams, i);
            };
        }

        private IWeatherRepository WeatherRepositoryValueOf(AerisJobParams jobParams)
        {
            return new WeatherRepository(jobParams.JitWeatherConnetionString, jobParams.JitWebData3ConnectionString);
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
                HighTmp = temp.MaxF,
                LowTmp = temp.MinF,
                AvgTmp = temp.AvgF,
                DewPt = dewpt.AvgF
            };

            return weatherData;
        }

        private AerisResult GetAerisResponse(AerisJobParams aerisJobParams, string zipCode, DateTime targetDate)
        {
            
            string fromDate = targetDate.Date.ToString("MM/dd/yyyy");
            string toDate = targetDate.Date.AddDays(1).ToString("MM/dd/yyyy");

            /* 
             * example
            http://api.aerisapi.com/observations/summary/closest?p=94304&query=maxt:!NULL,maxdewpt:!NULL&from=12/03/2014&to=12/03/2014&fields=id,periods.summary.dateTimeISO,periods.summary.temp.maxF,periods.summary.temp.minF,periods.summary.temp.avgF,periods.summary.dewpt.avgF
            */

            string rootUrl = $"http://api.aerisapi.com/observations/summary/closest?p={zipCode}&query=maxt:!NULL,maxdewpt:!NULL" +
                $"&from={fromDate}&to={fromDate}&fields=id,periods.summary.dateTimeISO,periods.summary.temp.maxF,periods.summary.temp.minF," +
                "periods.summary.temp.avgF,periods.summary.dewpt.avgF";

            StringBuilder builder = new StringBuilder();
            builder.Append(rootUrl);
            builder.Append("&client_id=");
            builder.Append(aerisJobParams.AerisAccessId);
            builder.Append("&client_secret=");
            builder.Append(aerisJobParams.AerisSecretKey);

            string url = builder.ToString();
            Console.WriteLine("url: {0}", url);

            using (WebClient wc = new WebClient())
            {
                var json = wc.DownloadString(url);
                Console.WriteLine("json: {0}", json);
                return JsonConvert.DeserializeObject<AerisResult>(json, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Include });       
            }
        }

        private void GetWthNormalParamsResultSet(AerisJobParams aerisJobParams)
        {
            //return false;;
        }

    }
}