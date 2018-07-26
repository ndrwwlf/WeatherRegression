using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.LinearRegression;
using Newtonsoft.Json;
using Quartz;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
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
        private  AerisJobParams _aerisJobParams;
        private  IWeatherRepository _weatherRepository;

        private int expectedWthExpUsageInserts;
        private int actualWthExpUsageInserts;

        private int expectedDailyWeatherDataInserts;
        private int actualDailyWeatherDataInserts;

        private int expectedTotalWeatherDataEntries;
        private int actualTotalWeatherDataEntries;

        private int expectedHistoricalWeatherDataInserts;
        private int actualHistoricalWeatherDataInserts;

        readonly DateTime fromDateStart = new DateTime(2014, 11, 13);

        public Task Execute(IJobExecutionContext context)
        {
            _aerisJobParams = AerisJobParamsValueOf(context);
            _weatherRepository = _weatherRepositoryValueOf(_aerisJobParams);

            //Log.Information("\nWeather job starting...\n");

            //GatherWeatherData();

            //PopulateWthExpUsageTable();

            //Log.Information($"WeatherData gathered and WthExpUsage calculated for Readings going back to {fromDateStart.ToShortDateString()}");
            //Log.Information("\nWeather job finished.\n");

            //PopulateWthNormalParams();

            return Task.FromResult(0);
        }

        private void GatherWeatherData()
        {
            Log.Information("Starting GatherWeatherData()...");

            try
            {
                GatherHistoricalWeatherData();
                GatherDailyWeatherData(-1);

                actualTotalWeatherDataEntries = _weatherRepository.GetWeatherDataRowCount();
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                Log.Error(ex.StackTrace);
            }

            Log.Information("Finished GatherWeatherData()");
            Log.Information($"Expected Total WeatherData Entries: {expectedTotalWeatherDataEntries}, Actual: {actualTotalWeatherDataEntries}.\n");

        }

        private void GatherDailyWeatherData(int i)
        {
            DateTime targetDate = DateTime.Now.AddDays(i);
            List<string> zipCodes = _weatherRepository.GetDistinctZipCodes();

            Log.Information($"Starting GatherDailyWeatherData(int {i}) for targetDate: {targetDate.ToShortDateString()} and {zipCodes.Count} zip codes...");

            foreach (string zipCode in zipCodes)
            {
                if (!_weatherRepository.GetWeatherDataExistForZipAndDate(zipCode, targetDate))
                {
                    expectedDailyWeatherDataInserts++;

                    try
                    {
                        WeatherData weatherData = BuildWeatherData(zipCode, targetDate);

                        bool success = _weatherRepository.InsertWeatherData(weatherData);

                        if (success)
                        {
                            Log.Information($"Inserted into WeatherData >> StationId: {weatherData.StationId}, Zip Code: {weatherData.ZipCode}, " +
                                $"RDate: {weatherData.RDate.ToShortDateString()}, LowTmp: {weatherData.LowTmp}, HighTmp: {weatherData.HighTmp}, " +
                                $"AvgTmp: {weatherData.AvgTmp}, DewPt: {weatherData.DewPt}");

                            actualDailyWeatherDataInserts++;
                            actualHistoricalWeatherDataInserts++;
                        }
                        else
                        {
                            Log.Error($"Failed attempt: insert into WeatherData >> StationId: {weatherData.StationId}, Zip Code: {weatherData.ZipCode}, " +
                                $"RDate: {weatherData.RDate.ToShortDateString()}, LowTmp: {weatherData.LowTmp}, HighTmp: {weatherData.HighTmp}, " +
                                $"AvgTmp: {weatherData.AvgTmp}, DewPt: {weatherData.DewPt}");
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error(e.Message);
                        Log.Error(e.StackTrace);
                    }
                }
            };

            Log.Information($"Finished GatherDailyWeatherData(int {i}) for {targetDate}.");
            Log.Information($"Expected inserts: {expectedDailyWeatherDataInserts}, Actual inserts: {actualDailyWeatherDataInserts}.\n");

            expectedDailyWeatherDataInserts = 0;
            actualDailyWeatherDataInserts = 0;
        }

        private void GatherHistoricalWeatherData()
        {
            DateTime today = DateTime.Now;

            // yyyy, mm, dd
            //DateTime fromDate = new DateTime(2015, 01, 01);

            int days = (int)fromDateStart.Subtract(today).TotalDays;

            int zipCount = _weatherRepository.GetDistinctZipCodes().Count;

            expectedTotalWeatherDataEntries = ((days * -1) - 1) * zipCount;
            actualTotalWeatherDataEntries = _weatherRepository.GetWeatherDataRowCount();

            if (expectedTotalWeatherDataEntries > actualTotalWeatherDataEntries)
            {
                expectedHistoricalWeatherDataInserts = expectedTotalWeatherDataEntries - actualTotalWeatherDataEntries;

                Log.Information($"Starting GatherHistoricalWeatherData(), from {fromDateStart} to yesterday. {days} days.");

                for (int i = days; i <= -1; i++)
                {
                    GatherDailyWeatherData(i);
                };

                Log.Information($"Finished GatherHistoricalWeatherData().");
                Log.Information($"Expected inserts: {expectedHistoricalWeatherDataInserts}, Actual inserts: {actualHistoricalWeatherDataInserts}.\n");

                expectedHistoricalWeatherDataInserts = 0;
                actualHistoricalWeatherDataInserts = 0;
            }

            expectedTotalWeatherDataEntries += zipCount;
        }

        private void PopulateWthExpUsageTable()
        {
            Log.Information("Starting PopulateWthExpUsage()...");

            string fromDateStartStr = $"{fromDateStart.Month}-{fromDateStart.Day}-{fromDateStart.Year}";

            try
            {
                List<ReadingsQueryResult> readings = _weatherRepository.GetReadings(fromDateStartStr);

                expectedWthExpUsageInserts = readings.Count;

                foreach (ReadingsQueryResult result in readings)
                {
                    //Console.WriteLine(result.DateStart + ", " + result.DateEnd + ", " + result.Days + ",  result.B1 = " + result.B1 + ",  result.B2 = " + result.B2 + ", result.B3 = " + result.B3 + ",  result.B4 = " + result.B4);
                    try
                    {
                        List<WeatherData> weatherDataList = _weatherRepository.GetWeatherDataByZipStartAndEndDate(result.Zip, result.DateStart, result.DateEnd);
                        HeatingCoolingDegreeDays heatingCoolingDegreeDays = HeatingCoolingDegreeDaysValueOf(result, weatherDataList);

                        CalculateExpUsage(result, heatingCoolingDegreeDays);
                    }
                    catch (Exception e)
                    {
                        Log.Error(e.Message);
                        Log.Error(e.StackTrace);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                Log.Error(ex.StackTrace);
            }

            int expectedTotalWthExpUsageEntries = _weatherRepository.GetExpectedWthExpUsageRowCount(fromDateStartStr);
            int actualTotalWthExpUsageEntries = _weatherRepository.GetActualWthExpUsageRowCount();

            Log.Information($"Finished PopulateWthExpUsage(). Expected inserts: {expectedWthExpUsageInserts}, Actual: {actualWthExpUsageInserts}");
            Log.Information($"Expected WthExpUsage total entries: {expectedTotalWthExpUsageEntries}, Actual: {actualTotalWthExpUsageEntries}.\n");

            expectedWthExpUsageInserts = 0;
            actualWthExpUsageInserts = 0;
        }

        private decimal CalculateExpUsage(ReadingsQueryResult result, HeatingCoolingDegreeDays heatingCoolingDegreeDays)
        {
            // Normalized Energy Usage = E = B1(DAYS) + B2(HDDB3) + B4(CDDB5)
            double? resultAsDouble = (result.B1 * result.Days) + (result.B2 * heatingCoolingDegreeDays.HDD) + (result.B4 * heatingCoolingDegreeDays.CDD);
            decimal resultAsDecimal = decimal.Round(Convert.ToDecimal(resultAsDouble), 4, MidpointRounding.AwayFromZero);

            bool success = _weatherRepository.InsertWthExpUsage(result.RdngID, resultAsDecimal, result.AccID, result.UtilID, result.RUnitID);

            if (success)
            {
                Log.Information($"Inserted into WthExpUsage >> RdngID: {result.RdngID} WthExpUsage: {resultAsDecimal} ... B1: {result.B1} B2: {result.B2} " +
                    $"B3: {result.B3} Hdd: {heatingCoolingDegreeDays.HDD} B4: {result.B4} B5: {result.B5} Cdd: {heatingCoolingDegreeDays.CDD} " +
                    $"RdngUnitID: {result.RUnitID} WthNormalParamsUnitID: {result.WnpUnitID}");

                actualWthExpUsageInserts++;
            }
            else
            {
                Log.Error($"FAILED attempt: insert into WthExpUsage >> RdngID: {result.RdngID} WthExpUsage: {resultAsDecimal} ... B1: {result.B1} B2: " +
                    $"{result.B2} B3: {result.B3} Hdd: {heatingCoolingDegreeDays.HDD} B4: {result.B4} B5: {result.B5} Cdd: {heatingCoolingDegreeDays.CDD} " +
                    $"RdngUnitID: {result.RUnitID} WthNormalParamsUnitID: {result.WnpUnitID}");
            }

            return resultAsDecimal;
        }

        private HeatingCoolingDegreeDays HeatingCoolingDegreeDaysValueOf(ReadingsQueryResult result, List<WeatherData> weatherDataList)
        {
            HeatingCoolingDegreeDays hcdd = new HeatingCoolingDegreeDays
            {
                CDD = 0.0,
                HDD = 0.0
            };

            if (result.B3 == 0 && result.B5 == 0)
            {
                return hcdd;
            }

            foreach (WeatherData weatherData in weatherDataList)
            {
                if (!weatherData.AvgTmp.HasValue)
                {
                    throw new Exception("WeatherData.AvgTmp is null for " + weatherData.ZipCode + " on " + weatherData.RDate);
                }
                else if (result.B5 > 0)
                {
                    if (weatherData.AvgTmp >= result.B5)
                    {
                        hcdd.CDD = hcdd.CDD + (weatherData.AvgTmp.Value - result.B5 );
                    }
                }
                else if (result.B3 > 0)
                {
                    if (weatherData.AvgTmp <= result.B3)
                    {
                        hcdd.HDD = hcdd.HDD + (result.B3 - weatherData.AvgTmp.Value);
                    }
                }
            }

            return hcdd;
        }

        private void PopulateWthNormalParams()
        {
            List<WthNormalParams> normalParamsKeys = _weatherRepository.GetNormalParamsKeysForRegression();

            foreach (
                WthNormalParams normalParamsKey in normalParamsKeys)
            {
                try
                {
                    //List<BalancePointPair> allBalancePointStatsFromYear = CalculateOneYearExpUsageForAllBalancePoints("2016-12-01", normalParamsKey).ToList();

                    string DesiredStartDate = normalParamsKey.EndDate_Original.AddYears(-1).ToShortDateString();
                    List<BalancePointPair> allBalancePointStatsFromYear = CalculateOneYearExpUsageForAllBalancePoints(DesiredStartDate, normalParamsKey).ToList();

                    List<BalancePointPair> analyzedBalancePointData =
                        CalculateLinearRegression(allBalancePointStatsFromYear, normalParamsKey)
                        .OrderByDescending(s => s.RSquared_New).ToList();

                    BalancePointPair winner = analyzedBalancePointData.First();

                    normalParamsKey.WthZipCode = winner.WthZipCode;
                    normalParamsKey.B1_New = winner.B1_New;
                    normalParamsKey.B2_New = winner.B2_New;
                    normalParamsKey.B3_New = winner.HeatingBalancePoint;
                    normalParamsKey.B4_New = winner.B4_New;
                    normalParamsKey.B5_New = winner.CoolingBalancePoint;
                    normalParamsKey.R2_New = decimal.Round(Convert.ToDecimal(winner.RSquared_New), 9, MidpointRounding.AwayFromZero);

                    //if (normalParamsKey.B2 != 0 || normalParamsKey.B4 != 0)
                    //{
                    //    normalParamsKey.R2 = decimal.Round(Convert.ToDecimal(winner.RSquared), 9, MidpointRounding.AwayFromZero);
                    //}
                    //else
                    //{
                    //    normalParamsKey.R2Original = decimal.Round(Convert.ToDecimal(winner.RSquared), 9);
                    //    normalParamsKey.R2 = decimal.Round(Convert.ToDecimal(winner.NewRSquaredNonWeather), 9, MidpointRounding.AwayFromZero);
                    //    normalParamsKey.B1_New = decimal.Round(Convert.ToDecimal(winner.B1_New), 9, MidpointRounding.AwayFromZero);
                    //}
                    normalParamsKey.YearOfReadsDateStart = winner.YearOfReadsDateStart;
                    normalParamsKey.YearOfReadsDateEnd = winner.YearOfReadsDateEnd;
                    normalParamsKey.Readings = winner.ReadingsInNormalYear;
                    normalParamsKey.Days = winner.DaysInNormalYear;
                    normalParamsKey.StandardError_New = decimal.Round(Convert.ToDecimal(winner.StandardError), 9, MidpointRounding.AwayFromZero);
                    //}
                    //else
                    //{
                    //BalancePointPair balancePointPair = allBalancePointStatsFromYear.First();
                    //    normalParamsKey.B3 = 0;
                    //    normalParamsKey.B5 = 0;
                    //    normalParamsKey.R2 = 0;
                    //    normalParamsKey.ReadDateStart = balancePointPair.ReadDateStart;
                    //    normalParamsKey.ReadDateEnd = balancePointPair.ReadDateEnd;
                    //    normalParamsKey.EndDateOriginal = balancePointPair.EndDateOriginal;
                    //    normalParamsKey.Readings = winner.ReadingsInNormalYear;
                    //    normalParamsKey.Days = winner.DaysInNormalYear;
                    //}
                    _weatherRepository.InsertWthNormalParams(normalParamsKey, Accord: false);
                }
                catch (Exception e)
                {
                    Console.WriteLine(normalParamsKey.AccID + "" + normalParamsKey.EndDate_Original);
                    Console.WriteLine(e.Message);
                    Console.WriteLine(e.StackTrace);
                }
            }
            Console.WriteLine("PopulateWthNormalParams Finished.");
        }

        private List<BalancePointPair> CalculateOneYearExpUsageForAllBalancePoints(string DesiredStartDate, WthNormalParams accountUtilAndUnit)
        {
            List<BalancePointPair> allBalancePointPairs = new List<BalancePointPair>();

            List<ReadingsQueryResult> readings = _weatherRepository.GetReadingsForRegressionYear(DesiredStartDate, accountUtilAndUnit);

            DateTime _ReadDateStart = readings.First().DateStart;
            DateTime _ReadDateEnd = readings.Last().DateEnd;
            int days = 0;

            decimal expUsage_Original = 0;

            foreach (ReadingsQueryResult reading in readings)
            {
                days += reading.Days;
                //expUsage_Original += _weatherRepository.GetExpUsageOriginal(reading.RdngID);
            }

            foreach (ReadingsQueryResult reading in readings)
            {
                HeatingCoolingDegreeDays hcdd = new HeatingCoolingDegreeDays
                {
                    CDD = 0.0,
                    HDD = 0.0
                };

                List<WeatherData> weatherDataList = _weatherRepository.GetWeatherDataByZipStartAndEndDate(reading.Zip, reading.DateStart, reading.DateEnd);

                int coolingBalancePoint = reading.B5;
                int heatingBalancePoint = reading.B3;

                double coolingCoefficiant = reading.B4;
                double heatingCoefficiant = reading.B2;

                if (heatingCoefficiant == 0 && coolingCoefficiant == 0)
                {
                    decimal expUsage = CalculateExpUsageForNomalizing(reading, hcdd);

                    BalancePointPair balancePointPair = new BalancePointPair
                    {
                        RdngID = reading.RdngID,
                        DaysInReading = reading.Days,
                        CoolingBalancePoint = reading.B5,
                        HeatingBalancePoint = reading.B3,
                        CoolingDegreeDays = (double)hcdd.CDD,
                        HeatingDegreeDays = (double)hcdd.HDD,
                        ExpUsage_New = expUsage,
                        ActualUsage = reading.Units,
                        ExpUsage_Original = expUsage_Original,
                        YearOfReadsDateStart = _ReadDateStart,
                        YearOfReadsDateEnd = _ReadDateEnd,
                        ReadingsInNormalYear = readings.Count,
                        DaysInNormalYear = days,
                        WthZipCode = reading.Zip
                    };

                    allBalancePointPairs.Add(balancePointPair);
                }
                else
                {
                    int[] allCoolingBalancePoints = new int[31];
                    for (int k = 45; k <= 75; k++)
                    {
                        allCoolingBalancePoints[k - 45] = k;
                    }

                    int[] allHeatingBalancePoints = new int[31];
                    for (int k = 45; k <= 75; k++)
                    {
                        allHeatingBalancePoints[k - 45] = k;
                    }

                    if (coolingCoefficiant > 0)
                    {
                        foreach (int coolingPointInstance in allCoolingBalancePoints)
                        {
                            reading.B5 = coolingPointInstance;

                            if (heatingCoefficiant > 0)
                            {
                                foreach (int heatingPointInstance in allHeatingBalancePoints)
                                {
                                    reading.B3 = heatingPointInstance;

                                    hcdd = HeatingCoolingDegreeDaysValueOf(reading, weatherDataList);

                                    decimal expUsage = CalculateExpUsageForNomalizing(reading, hcdd);

                                    BalancePointPair balancePoint = new BalancePointPair
                                    {
                                        RdngID = reading.RdngID,
                                        DaysInReading = reading.Days,
                                        CoolingBalancePoint = reading.B5,
                                        HeatingBalancePoint = reading.B3,
                                        CoolingDegreeDays = (double)hcdd.CDD,
                                        HeatingDegreeDays = (double)hcdd.HDD,
                                        ExpUsage_New = expUsage,
                                        ActualUsage = reading.Units,
                                        //ExpUsageOriginal = WthExpUsageOriginal,
                                        YearOfReadsDateStart = _ReadDateStart,
                                        YearOfReadsDateEnd = _ReadDateEnd,
                                        ReadingsInNormalYear = readings.Count,
                                        DaysInNormalYear = days,
                                        WthZipCode = reading.Zip
                                    };

                                    allBalancePointPairs.Add(balancePoint);
                                }
                            }
                            else
                            {
                                hcdd = HeatingCoolingDegreeDaysValueOf(reading, weatherDataList);

                                decimal expUsage = CalculateExpUsageForNomalizing(reading, hcdd);

                                BalancePointPair balancePoint = new BalancePointPair
                                {
                                    RdngID = reading.RdngID,
                                    DaysInReading = reading.Days,
                                    CoolingBalancePoint = reading.B5,
                                    HeatingBalancePoint = reading.B3,
                                    CoolingDegreeDays = (double)hcdd.CDD,
                                    HeatingDegreeDays = (double)hcdd.HDD,
                                    ExpUsage_New = expUsage,
                                    ActualUsage = reading.Units,
                                    //ExpUsageOriginal = WthExpUsageOriginal,
                                    YearOfReadsDateStart = _ReadDateStart,
                                    YearOfReadsDateEnd = _ReadDateEnd,
                                    ReadingsInNormalYear = readings.Count,
                                    DaysInNormalYear = days,
                                    WthZipCode = reading.Zip
                                };

                                allBalancePointPairs.Add(balancePoint);
                            }
                        }
                    }
                    else if (heatingCoefficiant > 0)
                    {
                        foreach (int heatingIncrement in allHeatingBalancePoints)
                        {
                            reading.B3 = heatingIncrement;

                            hcdd = HeatingCoolingDegreeDaysValueOf(reading, weatherDataList);

                            decimal expUsage = CalculateExpUsageForNomalizing(reading, hcdd);

                            BalancePointPair balancePoint = new BalancePointPair
                            {
                                RdngID = reading.RdngID,
                                DaysInReading = reading.Days,
                                CoolingBalancePoint = reading.B5,
                                HeatingBalancePoint = reading.B3,
                                CoolingDegreeDays = (double)hcdd.CDD,
                                HeatingDegreeDays = (double)hcdd.HDD,
                                ExpUsage_New = expUsage,
                                ActualUsage = reading.Units,
                                //ExpUsageOriginal = WthExpUsageOriginal,
                                YearOfReadsDateStart = _ReadDateStart,
                                YearOfReadsDateEnd = _ReadDateEnd,
                                ReadingsInNormalYear = readings.Count,
                                DaysInNormalYear = days,
                                WthZipCode = reading.Zip
                            };

                            allBalancePointPairs.Add(balancePoint);
                        }
                    }
                }
            }

            return allBalancePointPairs;
        }

        private List<BalancePointPair> CalculateLinearRegression(List<BalancePointPair> allBalancePointPairs, WthNormalParams normalParamsKey)
        {
            var updatedBalancePointPairs = new List<BalancePointPair>();

            var allBalancePointGroups = allBalancePointPairs.GroupBy(s => new { s.CoolingBalancePoint, s.HeatingBalancePoint });

            foreach (var group in allBalancePointGroups)
            {
                List<BalancePointPair> IdenticalBalancePointPairsForAllReadings = group.ToList();

                int readingsCount = IdenticalBalancePointPairsForAllReadings.Count;
                BalancePointPair _pointPair = IdenticalBalancePointPairsForAllReadings.First();

                bool NonWeatherDependant = (normalParamsKey.B2_Original == 0 && normalParamsKey.B4_Original == 0);

                //List<double> expUsageDaily = new List<double>();
                //List<double> hddsDaily = new List<double>();
                //List<double> cddsDaily = new List<double>();
                //List<double> actualUsageDaily = new List<double>();
                double[] fullXData = new double[readingsCount];
                //double[] fullXData_Original = new double[12 * balancePointPairGroup.Count];
                double[] xDays = new double[readingsCount];
                double[] fullYData = new double[readingsCount];
                double[] fullYDataAvg = new double[readingsCount];

                //double?[] hddsDaily = new double?[readingsCount * _pointPair.DaysInNormalYear];
                //double?[] cddsDaily = new double?[readingsCount * _pointPair.DaysInNormalYear];

                //double[][] hcddMatrix = new double[2][];

                //hcddMatrix[0] = new double[readingsCount];
                //hcddMatrix[1] = new double[readingsCount];

                double[][] hcddMatrix = new double[readingsCount][];

                    foreach (BalancePointPair balancePointPair in IdenticalBalancePointPairsForAllReadings)
                {
                    //double[] hddsDailyAvgInReading = new double[readingsCount];
                    //double[] cddsDailyAvgInReading = new double[readingsCount];
                    //expUsageDaily.Add(Convert.ToDouble(balancePointPair.ExpUsage) / balancePointPair.Days);
                    //hddsDaily.Add(balancePointPair.HeatingDegreeDays / balancePointPair.Days);
                    //cddsDaily.Add(balancePointPair.CoolingDegreeDays / balancePointPair.Days);
                    //actualUsageDaily.Add(balancePointPair.ActualUsage / balancePointPair.Days);
                    fullXData[IdenticalBalancePointPairsForAllReadings.IndexOf(balancePointPair)] = Convert.ToDouble(balancePointPair.ExpUsage_New);
                    xDays[IdenticalBalancePointPairsForAllReadings.IndexOf(balancePointPair)] = balancePointPair.DaysInReading;
                    fullYData[IdenticalBalancePointPairsForAllReadings.IndexOf(balancePointPair)] = (balancePointPair.ActualUsage);
                    fullYDataAvg[IdenticalBalancePointPairsForAllReadings.IndexOf(balancePointPair)] = (balancePointPair.ActualUsage / balancePointPair.DaysInReading);

                    //hddsDaily[IdenticalBalancePointPairsForAllReadings.IndexOf(balancePointPair) * ] 
                    //    = balancePointPair.HddList[IdenticalBalancePointPairsForAllReadings.IndexOf(balancePointPair)];

                    //cddsDaily[IdenticalBalancePointPairsForAllReadings.IndexOf(balancePointPair)] 
                    //    = balancePointPair.CddList[IdenticalBalancePointPairsForAllReadings.IndexOf(balancePointPair)];

                    //hddsDailyAvgInReading[IdenticalBalancePointPairsForAllReadings.IndexOf(balancePointPair)] 
                    //    = (balancePointPair.HeatingDegreeDays / balancePointPair.DaysInReading);

                    //cddsDailyAvgInReading[IdenticalBalancePointPairsForAllReadings.IndexOf(balancePointPair)] 
                    //    = (balancePointPair.CoolingDegreeDays / balancePointPair.DaysInReading);

                    hcddMatrix[IdenticalBalancePointPairsForAllReadings.IndexOf(balancePointPair)] = new double[] {
                        (balancePointPair.HeatingDegreeDays / balancePointPair.DaysInReading),
                        (balancePointPair.CoolingDegreeDays / balancePointPair.DaysInReading) };

                    //hcddMatrix[0][IdenticalBalancePointPairsForAllReadings.IndexOf(balancePointPair)] = (balancePointPair.HeatingDegreeDays / balancePointPair.DaysInReading);
                    //hcddMatrix[1][IdenticalBalancePointPairsForAllReadings.IndexOf(balancePointPair)] = (balancePointPair.CoolingDegreeDays / balancePointPair.DaysInReading);
                }

                double[] avgHddsForEachReadingInYear = new double[readingsCount];
                double[] avgCddsForEachReadingInYear = new double[readingsCount];
                
                for (int i = 0; i < readingsCount; i++)
                {
                    avgHddsForEachReadingInYear[i] = hcddMatrix[i][0];
                    avgCddsForEachReadingInYear[i] = hcddMatrix[i][1];
                }


                //double[] expUsageByDay = new double[daysInYear];
                //double[] fullYDataDaiy = new double[daysInYear];
                //List<double> expUsageByDay = new List<double>();
                //List<double> fullYDataByDay = new List<double>();

                //foreach (BalancePointPair balancePointPair in IdenticalBalancePointPairsForAllReadings)
                //{
                //    double dailyExpusage = 0;

                //    dailyExpusage += normalParamsKey.B3 * balancePointPair.HddList[IdenticalBalancePointPairsForAllReadings.IndexOf(balancePointPair)].Value;
                //    dailyExpusage += normalParamsKey.B5 * balancePointPair.CddList[IdenticalBalancePointPairsForAllReadings.IndexOf(balancePointPair)].Value;

                //    expUsageByDay.Add(dailyExpusage);
                //    fullYDataByDay.Add(balancePointPair.ActualUsage / balancePointPair.DaysInReading);
                //}

                //double[] hddsDailyArr = hddsDaily.ToArray();
                //double[] cddsDailyArr = cddsDaily.ToArray();
                //double[][] xy = new double[cddsDailyArr.Length][];

                //for (int i = 0; i < cddsDailyArr.Length; i++)
                //{
                //    double[] row = new double[2];
                //    xy[i] = row;
                //}

                //for (int i = 0; i < hddsDailyArr.Length; i++)
                //{
                //    xy[i][0] = hddsDailyArr[i];
                //    xy[i][1] = cddsDailyArr[i];
                //}

                //double[] p = Fit.LinearMultiDim(hcddMatrix, fullYDataAvg,
                //    d => 1.0,
                //    d => d[0],
                //    d => d[1]);
                //Matrix<double>.Build.DenseOfColumnArrays(hcddMatrix);

                //Tuple<double, double> pSingular;
                double[] p = new double[3];
                p[0] = 0;
                p[1] = 0;
                p[2] = 0;

                if (_pointPair.HeatingBalancePoint == 0 && _pointPair.CoolingBalancePoint == 0)
                {
                    double[] onesVector = new double[readingsCount];
                    for (int i = 0; i < readingsCount; i++)
                    {
                        onesVector[i] = 1;
                    }
                    p[0] = Fit.LineThroughOrigin(onesVector, fullYDataAvg);
                }
                else if (_pointPair.CoolingBalancePoint != 0 && _pointPair.HeatingBalancePoint != 0)
                {
                    p = MultipleRegression.QR(hcddMatrix, fullYDataAvg, intercept: true);
                }
                else if (_pointPair.CoolingBalancePoint == 0)
                {
                    Tuple<double, double> heatingTuple = Fit.Line(avgHddsForEachReadingInYear, fullYDataAvg);
                    p[0] = heatingTuple.Item1;
                    p[1] = heatingTuple.Item2;
                }
                else if (_pointPair.HeatingBalancePoint == 0)
                {
                    Tuple<double, double> coolingTuple = Fit.Line(avgCddsForEachReadingInYear, fullYDataAvg);
                    p[0] = coolingTuple.Item1;
                    p[2] = coolingTuple.Item2;
                }

                //double[] p = Fit.MultiDim(hcddMatrix, fullYDataAvg, intercept: true);


                //double[] fullXData_NewFit = new double[fullYDataAvg.Length];

                double[] fullXData_NewFit = new double[readingsCount];

                foreach (BalancePointPair balancePointPair in IdenticalBalancePointPairsForAllReadings)
                {
                    double t1 = 0;
                    if (IsDoubleNotNaNOrInfinity(p[0]))
                    {
                        t1 = p[0] * balancePointPair.DaysInReading;
                    }

                    double t2 = 0;
                    if (IsDoubleNotNaNOrInfinity(p[1]))
                    {
                        t2 = p[1] * balancePointPair.HeatingDegreeDays;
                    }

                    double t3 = 0;
                    if (IsDoubleNotNaNOrInfinity(p[2]))
                    {
                        t3 = p[2] * balancePointPair.CoolingDegreeDays;
                    }

                    fullXData_NewFit[IdenticalBalancePointPairsForAllReadings.IndexOf(balancePointPair)] = t1 + t2 + t3;
                }

                //double rBest = GoodnessOfFit.CoefficientOfDetermination(
                //    hcddMatrix.Select(x => p[0] + (p[1] * x[0]) + (p[2] * x[1])),
                //    fullYDataAvg);

                double rBest = GoodnessOfFit.CoefficientOfDetermination(fullXData_NewFit, fullYData);

                //double[,] comma = new double[hddsDailyArr.Length, hddsDailyArr.Length];

                //for(int i = 0; i < hddsDailyArr.Length; i++)
                //{
                //    double left = xy[i / 2][0];
                //    double right = xy[i / 2][1];
                //    comma[i, i] = [left, right];
                //}

                //double RSquared = GoodnessOfFit.CoefficientOfDetermination(xy.Select((x, y) => p[0], +(p[1] * xy[0]) + (p[2] * y)), ydata);
                //double RSquared = GoodnessOfFit.CoefficientOfDetermination(comma => p[0], +(p[1] * xy[0]) + (p[2] * y))), ydata);

                BalancePointPair groupLeader = _pointPair;

                double rSquared = GoodnessOfFit.CoefficientOfDetermination(fullXData, fullYData);

                //double rSquaredDaily = GoodnessOfFit.CoefficientOfDetermination(expUsageByDay, fullYDataByDay);

                double standardError_New = MathNet.Numerics.GoodnessOfFit.StandardError(fullXData, fullYData, groupLeader.ReadingsInNormalYear - 2);
                groupLeader.StandardError = standardError_New;

                if (!Double.IsNaN(rBest) && !Double.IsInfinity(rBest))
                {
                    groupLeader.RSquared_New = rBest;
                }
                else
                {
                    groupLeader.RSquared_New = null;
                }

                groupLeader.B1_New = decimal.Round(Convert.ToDecimal(p[0]), 9, MidpointRounding.AwayFromZero);
                groupLeader.B2_New = decimal.Round(Convert.ToDecimal(p[1]), 9, MidpointRounding.AwayFromZero);
                groupLeader.B4_New = decimal.Round(Convert.ToDecimal(p[2]), 9, MidpointRounding.AwayFromZero);

                //if (NonWeatherDependant)
                //{
                //    groupLeader.RSquared = 0;
                //    groupLeader.NewRSquaredNonWeather = 0;
                //double b = Fit.LineThroughOrigin(xDays, fullYData);

                //double[] newXData = new double[xDays.Length];

                //for (int i = 0; i < newXData.Length; i++)
                //{
                //    newXData[i] = xDays[i] * b;
                //}

                //double NewRSquaredNonWeather = GoodnessOfFit.CoefficientOfDetermination(newXData, fullYData);

                //if (!Double.IsNaN(NewRSquaredNonWeather) && !Double.IsInfinity(NewRSquaredNonWeather))
                //{
                //    groupLeader.NewRSquaredNonWeather = GoodnessOfFit.CoefficientOfDetermination(newXData, fullYData);
                //    groupLeader.B1_New = decimal.Round(Convert.ToDecimal(b), 9, MidpointRounding.AwayFromZero);
                //}
                //else
                //{
                //    groupLeader.NewRSquaredNonWeather = null;
                //    groupLeader.B1_New = null;
                //}
                //}

                updatedBalancePointPairs.Add(groupLeader);
            }

            return updatedBalancePointPairs;
        }

        private decimal CalculateExpUsageForNomalizing(ReadingsQueryResult result, HeatingCoolingDegreeDays heatingCoolingDegreeDays)
        {
            double? resultAsDouble = (result.B1 * result.Days) + (result.B2 * heatingCoolingDegreeDays.HDD) + (result.B4 * heatingCoolingDegreeDays.CDD);
            decimal resultAsDecimal = decimal.Round(Convert.ToDecimal(resultAsDouble), 4, MidpointRounding.AwayFromZero);

            return resultAsDecimal;
        }

        private IWeatherRepository _weatherRepositoryValueOf(AerisJobParams aerisJobParams)
        {
            return new WeatherRepository(aerisJobParams);
        }

        private AerisJobParams AerisJobParamsValueOf(IJobExecutionContext context)
        {
            var schedulerContext = context.Scheduler.Context;
            return (AerisJobParams)schedulerContext.Get("aerisJobParams");
        }

        private WeatherData BuildWeatherData(string zipCode, DateTime targetDate)
        {
            AerisResult result = GetAerisResult(zipCode, targetDate);

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

        private AerisResult GetAerisResult(string zipCode, DateTime targetDate)
        {
            string fromDate = targetDate.Date.ToString("MM/dd/yyyy");
            Log.Information($"Calling Aeris for zip: {zipCode} and date: {fromDate}");

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
            builder.Append(_aerisJobParams.AerisClientId);
            builder.Append("&client_secret=");
            builder.Append(_aerisJobParams.AerisClientSecret);

            string url = builder.ToString();
            Console.WriteLine("url: {0}", url);

            using (WebClient wc = new WebClient())
            {
                var json = wc.DownloadString(url);
                //Log.Information($"JSON: {json}");                                                                  
                return JsonConvert.DeserializeObject<AerisResult>(json, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Include });
            }
        }

        private bool IsDoubleNotNaNOrInfinity(double d)
        {
            if (Double.IsNaN(d) || Double.IsInfinity(d))
            {
                return false;
            }
            else
            {
                return true;
            }
        }
    }
}