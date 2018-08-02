using MathNet.Numerics;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WeatherService.Dao;
using WeatherService.Db;
using WeatherService.Dto;
using WeatherService.Model;
using WeatherService.Services;
using Accord.Statistics.Models.Regression.Linear;
using Accord.Math.Optimization.Losses;
using Accord.Statistics.Analysis;
using Accord.Statistics.Testing;
using Accord.Math;
using Accord.Statistics;
using Serilog;

namespace WeatherService.Scheduled
{
    public class SPRegressionJob : IJob
    {
        private AerisJobParams _aerisJobParams;
        private IWeatherRepository _weatherRepository;

        readonly DateTime fromDateStart = new DateTime(2014, 11, 13);

        public Task Execute(IJobExecutionContext context)
        {
            _aerisJobParams = AerisJobParamsValueOf(context);
            _weatherRepository = _weatherRepositoryValueOf(_aerisJobParams);

            var watch = System.Diagnostics.Stopwatch.StartNew();

            PopulateWthNormalParams();
            watch.Stop();

            var t = watch.Elapsed;

            Log.Error(t.ToString());

            //PopulateMyWthExpUsage();

            return Task.FromResult(0);
        }

        private void PopulateWthNormalParams()
        {
            List<WNRdngData> allWNRdngData = _weatherRepository.GetAllReadingsFromStoredProcedure();

            var wNRdngDataGroups = allWNRdngData.GroupBy(s => new { s.AccID, s.UtilID, s.UnitID });

            foreach (var wNRdngGroup in wNRdngDataGroups)
            {
                List<WNRdngData> wNRdngList = wNRdngGroup.OrderBy(s => s.MoID).ToList();

                WNRdngData lastRead = wNRdngList.LastOrDefault();

                NormalParamsAccord nParamsAccord = new NormalParamsAccord()
                {
                    AccID = lastRead.AccID,
                    UtilID = lastRead.UtilID,
                    UnitID = lastRead.UnitID,
                    WstID = lastRead.WstID,
                    ZipW = lastRead.Zip,
                    EndDate = lastRead.DateEnd,
                    EMoID = lastRead.EMoID,
                    MoCt = lastRead.MoCt
                };

                try
                {
                    List<BPPairAccord> allBalancePointStatsFromYear = CalculateOneYearOfDegreeDaysForAllBalancePoints(wNRdngList);

                    int daysInYear = allBalancePointStatsFromYear.FirstOrDefault().DaysInYear;
                    nParamsAccord.DaysInYear = daysInYear;

                    if (allBalancePointStatsFromYear.Count == 0)
                    {
                        //_weatherRepository.InsertWthNormalParams(normalParamsKey, Accord: true);
                        _weatherRepository.InsertWthNormalParamsFinal(nParamsAccord);
                        continue;
                    }

                    AccordResultNew accord = CalculateLinearRegression(allBalancePointStatsFromYear, nParamsAccord);

                    if (accord.FTestFailed)
                    {
                        Console.WriteLine("F Test failed... " + nParamsAccord.AccID + " " + nParamsAccord.UtilID + " " + nParamsAccord.UnitID);
                    }

                    nParamsAccord.B1 = decimal.Round(Convert.ToDecimal(accord.Intercept), 9, MidpointRounding.AwayFromZero);

                    if (accord.IsSimpleSingleRegression == true && accord.HeatingBP > 0)
                    {
                        nParamsAccord.B2 = decimal.Round(Convert.ToDecimal(accord.B2), 9, MidpointRounding.AwayFromZero);
                        nParamsAccord.B3 = accord.HeatingBP;
                    }
                    else if (accord.IsSimpleSingleRegression == true && accord.CoolingBP > 0)
                    {
                        nParamsAccord.B4 = decimal.Round(Convert.ToDecimal(accord.B4), 9, MidpointRounding.AwayFromZero);
                        nParamsAccord.B5 = accord.CoolingBP;
                    }
                    else if (accord.IsMultipleLinearRegression == true)
                    {
                        nParamsAccord.B2 = decimal.Round(Convert.ToDecimal(accord.B2), 9, MidpointRounding.AwayFromZero);
                        nParamsAccord.B3 = accord.HeatingBP;
                        nParamsAccord.B4 = decimal.Round(Convert.ToDecimal(accord.B4), 9, MidpointRounding.AwayFromZero);
                        nParamsAccord.B5 = accord.CoolingBP;
                    }

                    if (!Double.IsNaN(accord.R2Accord) && !Double.IsInfinity(accord.R2Accord))
                    {
                        nParamsAccord.R2 = decimal.Round(Convert.ToDecimal(accord.R2Accord), 9, MidpointRounding.AwayFromZero);
                    }

                    //nParams.YearOfReadsDateStart = accord.bpPair.YearOfReadsDateStart;
                    //nParams.YearOfReadsDateEnd = accord.bpPair.YearOfReadsDateEnd;
                    //nParams.Readings = accord.bpPair.ReadingsInNormalYear;
                    //nParams.Days = accord.bpPair.DaysInNormalYear;
                    //nParams.WthZipCode = accord.bpPair.WthZipCode;

                    //_weatherRepository.InsertWthNormalParams(nParams, Accord: true);

                    _weatherRepository.InsertWthNormalParamsFinal(nParamsAccord);
                }
                catch (Exception e)
                {
                    Console.WriteLine(nParamsAccord.AccID + " " + nParamsAccord.UtilID + " " + nParamsAccord.UnitID);
                    Console.WriteLine(e.Message);
                    Console.WriteLine(e.StackTrace);
                }
            }

            Console.WriteLine("PopulateWthNormalParams Finished.");
        }

        private List<BPPairAccord> CalculateOneYearOfDegreeDaysForAllBalancePoints(List<WNRdngData> wNRdngData)
        {
            List<BPPairAccord> allBalancePointPairs = new List<BPPairAccord>();

            DateTime _yearOfReadsDateStart = wNRdngData.First().DateStart;
            DateTime _yearOfReadsDateEnd = wNRdngData.Last().DateEnd;
            int _readingsCount = wNRdngData.First().MoID;
            int daysInYear = 0;

            foreach (WNRdngData reading in wNRdngData)
            {
                var t = reading.DateEnd.Subtract(reading.DateStart).Days;
                daysInYear += t;
            }

            foreach (WNRdngData reading in wNRdngData)
            {
                int daysInReading = reading.DateEnd.Subtract(reading.DateStart).Days;

                HeatingCoolingDegreeDays hcdd = new HeatingCoolingDegreeDays
                {
                    CDD = 0.0,
                    HDD = 0.0
                };

                List<WeatherData> weatherDataList = _weatherRepository.GetWeatherDataByZipStartAndEndDate(reading.Zip, reading.DateStart, reading.DateEnd);

                if (weatherDataList.Count != daysInReading)
                {
                    Log.Error($"WeatherData.Count != daysInReading: " + weatherDataList.Count + " " 
                        + daysInReading + " AccID: " + reading.AccID + " UtilID: " + reading.UtilID + " UnitID: " + reading.UnitID + " Zip: " + reading.Zip + " MoID: " + reading.MoID);
                }

                int rangeMin = 45;
                int rangeMax = 75;
                int range = rangeMax - rangeMin + 1;

                List<int[]> comboList = new List<int[]>();

                for (int i = 0; i < range; i++)
                {
                    int[] hdsOnly = new int[2] { rangeMin + i, 0 };
                    int[] cdsOnly = new int[2] { 0, rangeMin + i };

                    comboList.Add(hdsOnly);
                    comboList.Add(cdsOnly);

                    int k = range - 1 - i;
                    while (k >= 0)
                    {
                        int[] both = new int[2] { rangeMin + i, rangeMin + i + k };
                        k--;

                        comboList.Add(both);
                    }
                }

                comboList.Add(new int[] { 0, 0 });

                //int expectedComboListCount = 0;

                ///* cases where (heating bp) != (cooling bp) */
                //expectedComboListCount = (range + 1) * ((range) / 2);
                //if (range % 2 != 0)
                //{
                //    expectedComboListCount += ((range) / 2) + 1;
                //}

                ///* cases where (heating bp) != 0 && (cooling bp) == 0 or
                // *             (heating bp) == 0 && (cooling bp) != 0; and
                // *             (heating bp) == 0 && (cooling bp) == 0.
                // */
                //expectedComboListCount += (range * 2) + 1;

                foreach (int[] combo in comboList)
                {
                    //BalancePointPair bpPair = new BalancePointPair
                    //{
                    //    CoolingBalancePoint = combo[1],
                    //    HeatingBalancePoint = combo[0]
                    //};

                    //hcdd = HeatingCoolingDegreeDaysValueOf(bpPair, weatherDataList);

                    //bpPair.CoolingDegreeDays = hcdd.CDD;
                    //bpPair.HeatingDegreeDays = hcdd.HDD;
                    //bpPair.ActualUsage = reading.Units;
                    //bpPair.YearOfReadsDateStart = _yearOfReadsDateStart;
                    //bpPair.YearOfReadsDateEnd = _yearOfReadsDateEnd;
                    //bpPair.ReadingsInNormalYear = _readingsCount;
                    ////DaysInNormalYear = days;
                    //bpPair.WthZipCode = reading.Zip;
                    //bpPair.DaysInReading = daysInReading;

                    BPPairAccord bpPair = new BPPairAccord
                    {
                        CoolingBalancePoint = combo[1],
                        HeatingBalancePoint = combo[0]
                    };

                    hcdd = HeatingCoolingDegreeDaysValueOf(bpPair, weatherDataList);

                    bpPair.CoolingDegreeDays = hcdd.CDD;
                    bpPair.HeatingDegreeDays = hcdd.HDD;
                    bpPair.ActualUsage = reading.Units;
                    bpPair.ZipCode = reading.Zip;
                    bpPair.DaysInReading = daysInReading;
                    bpPair.DaysInYear = daysInYear;

                    allBalancePointPairs.Add(bpPair);
                }
            }

            return allBalancePointPairs;
        }

        private HeatingCoolingDegreeDays HeatingCoolingDegreeDaysValueOf(BPPairAccord bpPair, List<WeatherData> weatherDataList)
        {
            HeatingCoolingDegreeDays hcdd = new HeatingCoolingDegreeDays
            {
                CDD = 0.0,
                HDD = 0.0,
            };

            foreach (WeatherData weatherData in weatherDataList)
            {
                if (!weatherData.AvgTmp.HasValue)
                {
                    throw new Exception("WeatherData.AvgTmp is null for " + weatherData.ZipCode + " on " + weatherData.RDate);
                }
                else if (bpPair.CoolingBalancePoint > 0 && weatherData.AvgTmp >= bpPair.CoolingBalancePoint)
                {
                    hcdd.CDD += (weatherData.AvgTmp.Value - bpPair.CoolingBalancePoint);
                }
                else if (bpPair.HeatingBalancePoint > 0 && weatherData.AvgTmp < bpPair.HeatingBalancePoint)
                {
                    hcdd.HDD += (bpPair.HeatingBalancePoint - weatherData.AvgTmp.Value);
                }
            }

            return hcdd;
        }

        private AccordResultNew CalculateLinearRegression(List<BPPairAccord> allBalancePointPairs, NormalParamsAccord nPKey)
        {
            var allBalancePointGroups = allBalancePointPairs.GroupBy(s => new { s.CoolingBalancePoint, s.HeatingBalancePoint });

            List<AccordResultNew> accordResults = new List<AccordResultNew>();
            //List<AccordResult> rejectedAccords = new List<AccordResult>();

            foreach (var group in allBalancePointGroups)
            {
                try
                {
                    List<BPPairAccord> IdenticalBalancePointPairsFromAllReadings = group.ToList();
                    BPPairAccord _pointPair = IdenticalBalancePointPairsFromAllReadings.First();
                    int readingsCount = IdenticalBalancePointPairsFromAllReadings.Count;

                    double[] fullYData = new double[readingsCount];
                    double[] fullYDataDailyAvg = new double[readingsCount];

                    double[][] hcddMatrix = new double[readingsCount][];

                    double[][] hcddMatrixNonDaily = new double[readingsCount][];

                    foreach (BPPairAccord balancePointPair in IdenticalBalancePointPairsFromAllReadings)
                    {
                        fullYData[IdenticalBalancePointPairsFromAllReadings.IndexOf(balancePointPair)] = (balancePointPair.ActualUsage);

                        fullYDataDailyAvg[IdenticalBalancePointPairsFromAllReadings.IndexOf(balancePointPair)]
                            = (balancePointPair.ActualUsage / balancePointPair.DaysInReading);

                        hcddMatrix[IdenticalBalancePointPairsFromAllReadings.IndexOf(balancePointPair)] = new double[]
                            {
                            (balancePointPair.HeatingDegreeDays / balancePointPair.DaysInReading),
                            (balancePointPair.CoolingDegreeDays / balancePointPair.DaysInReading)
                            };
                    }

                    double[] avgHddsForEachReadingInYear = new double[readingsCount];
                    double[] avgCddsForEachReadingInYear = new double[readingsCount];

                    for (int i = 0; i < readingsCount; i++)
                    {
                        avgHddsForEachReadingInYear[i] = hcddMatrix[i][0];
                        avgCddsForEachReadingInYear[i] = hcddMatrix[i][1];
                    }

                    double[] modelParams = new double[3];
                    modelParams[0] = 0;
                    modelParams[1] = 0;
                    modelParams[2] = 0;

                    //if (fullYData.Sum() == 0)
                    //{
                    //    AccordResultNew empty = new AccordResultNew();
                    //    accordResults.Add(empty);
                    //}
                    if (_pointPair.HeatingBalancePoint == 0 && _pointPair.CoolingBalancePoint == 0)
                    {
                        double[] onesVector = new double[readingsCount];

                        for (int i = 0; i < readingsCount; i++)
                        {
                            onesVector[i] = 1;
                        }

                        modelParams[0] = Fit.LineThroughOrigin(onesVector, fullYDataDailyAvg);

                        OrdinaryLeastSquares ols = new OrdinaryLeastSquares()
                        {
                            UseIntercept = false
                        };

                        //SimpleLinearRegression regressionAccord = ols.Learn(onesVector, fullYDataDailyAvg);

                        //double[] predictedAccord = regressionAccord.Transform(onesVector);

                        double r2 = MathNet.Numerics.GoodnessOfFit.CoefficientOfDetermination(onesVector.Select(x => x * modelParams[0]), fullYDataDailyAvg);

                        //double mean = fullYDataDailyAvg.Mean();

                        //if (mean != modelParams[0] || mean != regressionAccord.Slope)
                        //{
                        //    Console.WriteLine("Hey!");
                        //}

                        AccordResultNew accordResult = new AccordResultNew()
                        {
                            IsSimpleSingleRegression = true,
                            HeatingBP = _pointPair.HeatingBalancePoint,
                            CoolingBP = _pointPair.CoolingBalancePoint,
                            Intercept = modelParams[0],
                            R2Accord = r2,
                        };

                        accordResults.Add(accordResult);
                    }
                    else if (_pointPair.CoolingBalancePoint != 0 && _pointPair.HeatingBalancePoint != 0)
                    {
                        //modelParams = MultipleRegression.QR(hcddMatrix, fullYDataDailyAvg, intercept: true);

                        //Accord
                        //var ols = new OrdinaryLeastSquares()
                        //{
                        //    UseIntercept = true
                        //};

                        try
                        {
                            MultipleLinearRegressionAnalysis mlra = new MultipleLinearRegressionAnalysis(intercept: true);
                            mlra.Learn(hcddMatrix, fullYDataDailyAvg);

                            //
                            //MultipleLinearRegression regressionAccord = ols.Learn(hcddMatrix, fullYDataDailyAvg);

                            var regressionAccord = mlra.Regression;

                            double[] predicted = regressionAccord.Transform(hcddMatrix);

                            double r2Accord = new RSquaredLoss(numberOfInputs: 2, expected: fullYDataDailyAvg) { Adjust = false }.Loss(predicted);

                            double r2Coeff = regressionAccord.CoefficientOfDetermination(hcddMatrix, fullYDataDailyAvg, adjust: false);

                            bool FTestFailed = !mlra.FTest.Significant;

                            //double r2Math = MathNet.Numerics.GoodnessOfFit.CoefficientOfDetermination(hcddMatrix.Select(
                            //    x => (x[0] * regressionAccord.Weights[0]) + (x[1] * regressionAccord.Weights[1]) + regressionAccord.Intercept
                            //), fullYDataDailyAvg);

                            //double r2MathPred = MathNet.Numerics.GoodnessOfFit.CoefficientOfDetermination(predicted, fullYDataDailyAvg);

                            AccordResultNew accordResult = new AccordResultNew()
                            {
                                IsMultipleLinearRegression = true,
                                //MultipleRegression = regressionAccord,
                                HeatingBP = _pointPair.HeatingBalancePoint,
                                CoolingBP = _pointPair.CoolingBalancePoint,
                                Intercept = regressionAccord.Intercept,
                                B2 = regressionAccord.Weights[0],
                                B4 = regressionAccord.Weights[1],
                                R2Accord = r2Accord,
                                FTestFailed = FTestFailed

                            };

                            if (mlra.Coefficients.All(x => x.TTest.Significant))
                            {
                                accordResults.Add(accordResult);
                            }
                            //else
                            //{
                            //    rejectedAccords.Add(accordResult);
                            //}
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(nPKey.AccID + " " + nPKey.UtilID + " " + nPKey.UnitID + " " + e.Message + " " + e.StackTrace);
                        }
                    }
                    else if (_pointPair.HeatingBalancePoint > 0)
                    {
                        //    Tuple<double, double> heatingTuple = Fit.Line(avgHddsForEachReadingInYear, fullYDataDailyAvg);
                        //    modelParams[0] = heatingTuple.Item1;
                        //    modelParams[1] = heatingTuple.Item2;

                        //    double r = MathNet.Numerics.GoodnessOfFit.CoefficientOfDetermination(
                        //        avgHddsForEachReadingInYear.Select(x => heatingTuple.Item1 + heatingTuple.Item2 * x), fullYDataDailyAvg);

                        OrdinaryLeastSquares ols = new OrdinaryLeastSquares()
                        {
                            UseIntercept = true
                        };

                        SimpleLinearRegression regressionAccord = ols.Learn(avgHddsForEachReadingInYear, fullYDataDailyAvg);

                        double[] predictedAccord = regressionAccord.Transform(avgHddsForEachReadingInYear);

                        double r2Accord = new RSquaredLoss(1, fullYDataDailyAvg).Loss(predictedAccord);

                        //double rAccord2 = regressionAccord.CoefficientOfDetermination(avgHddsForEachReadingInYear, fullYDataDailyAvg, adjust: false);

                        //double r2Math = MathNet.Numerics.GoodnessOfFit.CoefficientOfDetermination(avgHddsForEachReadingInYear.Select(
                        //    x => (x * regressionAccord.Slope) + regressionAccord.Intercept
                        //    ), fullYDataDailyAvg);

                        //double r2 = MathNet.Numerics.GoodnessOfFit.CoefficientOfDetermination(predictedAccord, fullYDataDailyAvg);

                        int degreesOfFreedom = nPKey.MoCt - 2;
                        double ssx = Math.Sqrt((avgHddsForEachReadingInYear.Subtract(avgHddsForEachReadingInYear.Mean())).Pow(2).Sum());
                        double s = Math.Sqrt(((fullYDataDailyAvg.Subtract(predictedAccord).Pow(2)).Sum()) / degreesOfFreedom);

                        double error = regressionAccord.GetStandardError(avgHddsForEachReadingInYear, fullYDataDailyAvg);

                        double seSubB = s / ssx;

                        double hypothesizedValue = 0;

                        TTest tTest = new TTest(
                            estimatedValue: regressionAccord.Slope, standardError: seSubB, degreesOfFreedom: degreesOfFreedom,
                            hypothesizedValue: hypothesizedValue, alternate: OneSampleHypothesis.ValueIsDifferentFromHypothesis
                            );

                        AccordResultNew accordResult = new AccordResultNew()
                        {
                            IsSimpleSingleRegression = true,
                            HeatingBP = _pointPair.HeatingBalancePoint,
                            Intercept = regressionAccord.Intercept,
                            B2 = regressionAccord.Slope,
                            R2Accord = r2Accord
                        };

                        if (tTest.Significant)
                        {
                            accordResults.Add(accordResult);
                        }
                        //else
                        //{
                        //    rejectedAccords.Add(accordResult);
                        //}
                    }
                    else if (_pointPair.CoolingBalancePoint > 0)
                    {
                        //Tuple<double, double> coolingTuple = Fit.Line(avgCddsForEachReadingInYear, fullYDataDailyAvg);
                        //modelParams[0] = coolingTuple.Item1;
                        //modelParams[2] = coolingTuple.Item2;

                        OrdinaryLeastSquares ols = new OrdinaryLeastSquares()
                        {
                            UseIntercept = true
                        };

                        SimpleLinearRegression regressionAccord = ols.Learn(avgCddsForEachReadingInYear, fullYDataDailyAvg);

                        double[] predictedAccord = regressionAccord.Transform(avgCddsForEachReadingInYear);
                        double rAccord = new RSquaredLoss(1, fullYDataDailyAvg).Loss(predictedAccord);

                        //double r2Math = MathNet.Numerics.GoodnessOfFit.CoefficientOfDetermination(avgCddsForEachReadingInYear.Select(
                        //    x => (x * regressionAccord.Slope) + regressionAccord.Intercept
                        //    ), fullYDataDailyAvg);

                        //double r2 = MathNet.Numerics.GoodnessOfFit.CoefficientOfDetermination(predictedAccord, fullYDataDailyAvg);

                        int degreesOfFreedom = nPKey.MoCt - 2;
                        double ssx = Math.Sqrt(avgCddsForEachReadingInYear.Subtract(avgCddsForEachReadingInYear.Mean()).Pow(2).Sum());
                        double s = Math.Sqrt(((fullYDataDailyAvg.Subtract(predictedAccord).Pow(2)).Sum()) / degreesOfFreedom);

                        double seSubB = s / ssx;
                        double hypothesizedValue = 0;

                        double myT = seSubB / regressionAccord.Slope;

                        TTest tTest = new TTest(
                            estimatedValue: regressionAccord.Slope, standardError: seSubB, degreesOfFreedom: degreesOfFreedom,
                            hypothesizedValue: hypothesizedValue, alternate: OneSampleHypothesis.ValueIsDifferentFromHypothesis
                            );

                        AccordResultNew accordResult = new AccordResultNew()
                        {
                            IsSimpleSingleRegression = true,
                            CoolingBP = _pointPair.CoolingBalancePoint,
                            Intercept = regressionAccord.Intercept,
                            B4 = regressionAccord.Slope,
                            R2Accord = rAccord
                        };

                        if (tTest.Significant)
                        {
                            accordResults.Add(accordResult);
                        }
                        //else
                        //{
                        //    rejectedAccords.Add(accordResult);
                        //}
                    };
                }
                catch (Exception e)
                {
                    Console.WriteLine(nPKey.AccID + " " + nPKey.UtilID + " " + nPKey.UnitID + " " + e.Message + e.StackTrace);
                }
            }

            //rejectedAccords = rejectedAccords.OrderByDescending(s => s.R2Accord).ToList();

            AccordResultNew accordWinner = accordResults
                .Where(s => s.Intercept >= 0)
                .OrderByDescending(s => s.R2Accord).ToList().FirstOrDefault();

            return accordWinner;
        }

        private Tuple<double, double> EvaluateParametizedModel(List<BalancePointPair> balancePointPairs, double[] modelParameters, double[] fullYData)
        {
            List<double> predicted = new List<double>();

            foreach (BalancePointPair balancePointPair in balancePointPairs)
            {
                double expUsage_New = CalculateExpUsageForNomalizing(balancePointPair, modelParameters);
                predicted.Add(expUsage_New);
            }

            int degreesOfFreedom = fullYData.Length;

            foreach (double p in modelParameters)
            {
                if (p > 0 || p < 0)
                {
                    degreesOfFreedom--;
                }
            }

            double rBest = 0;
            double standardError = 0;

            try
            {
                rBest = MathNet.Numerics.GoodnessOfFit.CoefficientOfDetermination(predicted.ToArray(), fullYData);

                standardError = MathNet.Numerics.GoodnessOfFit.StandardError(predicted.ToArray(), fullYData, degreesOfFreedom);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            return new Tuple<double, double>(rBest, standardError);
        }

        private double CalculateExpUsageForNomalizing(BalancePointPair balancePointPair, double[] p)
        {
            double t1 = p[0] * balancePointPair.DaysInReading;
            double t2 = p[1] * balancePointPair.HeatingDegreeDays;
            double t3 = p[2] * balancePointPair.CoolingDegreeDays;

            return t1 + t2 + t3;
        }

        private bool AreDoublesAllNotInfinityNorNaN(double[] doubles)
        {
            foreach (double d in doubles)
            {
                if (Double.IsNaN(d) || Double.IsInfinity(d))
                {
                    return false;
                }
            }
            return true;
        }

        private void PopulateMyWthExpUsage()
        {
            ////List<ReadingsQueryResult> allReadings = _weatherRepository.GetReadingsFromExpUsageOriginal();
            //List<ReadingsQueryResult> allReadings = _weatherRepository.GetReadingsFromExpUsageOriginalCorrected();

            //var readingsByAccount = allReadings.GroupBy(r => new { r.AccID, r.UtilID, r.RUnitID });

            //foreach (var group in readingsByAccount)
            //{
            //    try
            //    {
            //        List<ReadingsQueryResult> readings = group.ToList();
            //        WthNormalParams _params =
            //            _weatherRepository.GetParamsForReading(readings.First().AccID, readings.First().UtilID, readings.First().RUnitID);

            //        foreach (ReadingsQueryResult reading in readings)
            //        {
            //            WthExpUsage wthExpUsage = new WthExpUsage();
            //            wthExpUsage.RdngID = reading.RdngID;
            //            wthExpUsage.AccID = reading.AccID;
            //            wthExpUsage.UtilID = reading.UtilID;
            //            wthExpUsage.UnitID = reading.RUnitID;
            //            decimal expUsage_Old = decimal.Round(Convert.ToDecimal(reading.ExpUsage), 4, MidpointRounding.AwayFromZero);
            //            wthExpUsage.ExpUsage_Old = expUsage_Old;
            //            wthExpUsage.Units = reading.Units;

            //            List<WeatherData> weatherDataList =
            //                _weatherRepository.GetWeatherDataByZipStartAndEndDate(_params.WthZipCode, reading.DateStart, reading.DateEnd);

            //            reading.B3 = _params.B3_New;
            //            reading.B5 = _params.B5_New;

            //            HeatingCoolingDegreeDays hcdd = HeatingCoolingDegreeDaysValueOf(reading, weatherDataList);

            //            decimal expUsage_New = _params.B1_New * reading.Days
            //                + (_params.B2_New * decimal.Round(Convert.ToDecimal(hcdd.HDD), 4, MidpointRounding.AwayFromZero))
            //                + (_params.B4_New * decimal.Round(Convert.ToDecimal(hcdd.CDD), 4, MidpointRounding.AwayFromZero));

            //            wthExpUsage.ExpUsage_New = expUsage_New;

            //            if (reading.Units != 0)
            //            {
            //                wthExpUsage.PercentDelta_Old = Math.Abs((expUsage_Old - reading.Units) / reading.Units);
            //                wthExpUsage.PercentDelta_New = Math.Abs((expUsage_New - reading.Units) / reading.Units);
            //            }
            //            wthExpUsage.DateStart = reading.DateStart;
            //            wthExpUsage.DateEnd = reading.DateEnd;

            //            _weatherRepository.InsertMyWthExpUsage(wthExpUsage);
            //        }
            //    }
            //    catch (Exception e)
            //    {
            //        Console.WriteLine(e.Message + " " + group.ToList().First().RdngID + " " + group.ToList().First().AccID + " " + group.ToList().First().RUnitID
            //            + "\n" + e.StackTrace);
            //    }
            //}
        }

        private void PopulateMyWthExpUsage(AccordResult accord)
        {
            ////List<ReadingsQueryResult> allReadings = _weatherRepository.GetReadingsFromExpUsageOriginal();
            //List<ReadingsQueryResult> allReadings = _weatherRepository.GetReadingsFromExpUsageOriginalCorrected(accord);

            //var readingsByAccount = allReadings.GroupBy(r => new { r.AccID, r.UtilID, r.RUnitID });

            //foreach (var group in readingsByAccount)
            //{
            //    try
            //    {
                    //List<ReadingsQueryResult> readings = group.ToList();
                    //WthNormalParams _params =
                    //    _weatherRepository.GetParamsForReading(readings.First().AccID, readings.First().UtilID, readings.First().RUnitID);

                    //foreach (ReadingsQueryResult reading in readings)
                    //{
                    //    WthExpUsage wthExpUsage = new WthExpUsage();
                    //    wthExpUsage.RdngID = reading.RdngID;
                    //    wthExpUsage.AccID = reading.AccID;
                    //    wthExpUsage.UtilID = reading.UtilID;
                    //    wthExpUsage.UnitID = reading.RUnitID;
                    //    decimal expUsage_Old = decimal.Round(Convert.ToDecimal(reading.ExpUsage), 4, MidpointRounding.AwayFromZero);
                    //    wthExpUsage.ExpUsage_Old = expUsage_Old;
                    //    wthExpUsage.Units = reading.Units;

                    //    List<WeatherData> weatherDataList =
                    //        _weatherRepository.GetWeatherDataByZipStartAndEndDate(_params.WthZipCode, reading.DateStart, reading.DateEnd);

                    //    decimal B1 = 0;
                    //    decimal B2 = 0;
                    //    decimal B4 = 0;

                    //    reading.B3 = accord.HeatingBP;
                    //    reading.B5 = accord.CoolingBP;

                    //    if (accord.IsSimpleSingleRegression)
                    //    {
                    //        if (reading.B3 > 0)
                    //        {
                    //            B1 = decimal.Round(Convert.ToDecimal(accord.SimpleLinearRegression.Intercept), 12, MidpointRounding.AwayFromZero);
                    //            B2 = decimal.Round(Convert.ToDecimal(accord.SimpleLinearRegression.Slope), 12, MidpointRounding.AwayFromZero);
                    //            B4 = 0;
                    //        }
                    //        else if (reading.B5 > 0)
                    //        {
                    //            B1 = decimal.Round(Convert.ToDecimal(accord.SimpleLinearRegression.Intercept), 12, MidpointRounding.AwayFromZero);
                    //            B2 = 0;
                    //            B4 = decimal.Round(Convert.ToDecimal(accord.SimpleLinearRegression.Slope), 12, MidpointRounding.AwayFromZero);
                    //        }
                    //    }
                    //    else
                    //    {
                    //        if (reading.B3 > 0 && reading.B5 > 0)
                    //        {
                    //            B1 = decimal.Round(Convert.ToDecimal(accord.MultipleRegression.Weights[0]), 12, MidpointRounding.AwayFromZero);
            //                    B2 = decimal.Round(Convert.ToDecimal(accord.MultipleRegression.Weights[1]), 12, MidpointRounding.AwayFromZero);
            //                    B4 = decimal.Round(Convert.ToDecimal(accord.MultipleRegression.Weights[2]), 12, MidpointRounding.AwayFromZero);
            //                }
            //                else if (reading.B3 > 0)
            //                {
            //                    B1 = decimal.Round(Convert.ToDecimal(accord.MultipleRegression.Weights[0]), 12, MidpointRounding.AwayFromZero);
            //                    B2 = decimal.Round(Convert.ToDecimal(accord.MultipleRegression.Weights[1]), 12, MidpointRounding.AwayFromZero);
            //                }
            //                else if (reading.B5 > 0)
            //                {
            //                    B1 = decimal.Round(Convert.ToDecimal(accord.MultipleRegression.Weights[0]), 12, MidpointRounding.AwayFromZero);
            //                    B4 = decimal.Round(Convert.ToDecimal(accord.MultipleRegression.Weights[1]), 12, MidpointRounding.AwayFromZero);
            //                }
            //            }

            //            HeatingCoolingDegreeDays hcdd = HeatingCoolingDegreeDaysValueOf(reading, weatherDataList);

            //            decimal expUsage_New = (B1 * reading.Days)
            //                + (B2 * decimal.Round(Convert.ToDecimal(hcdd.HDD), 12, MidpointRounding.AwayFromZero))
            //                + (B4 * decimal.Round(Convert.ToDecimal(hcdd.CDD), 12, MidpointRounding.AwayFromZero));

            //            wthExpUsage.ExpUsage_New = expUsage_New;

            //            if (reading.Units != 0)
            //            {
            //                wthExpUsage.PercentDelta_Old = Math.Abs((expUsage_Old - reading.Units) / reading.Units);
            //                wthExpUsage.PercentDelta_New = Math.Abs((expUsage_New - reading.Units) / reading.Units);
            //            }
            //            wthExpUsage.DateStart = reading.DateStart;
            //            wthExpUsage.DateEnd = reading.DateEnd;

            //            _weatherRepository.InsertMyWthExpUsage(wthExpUsage, Accord: true);
            //        }
            //    }
            //    catch (Exception e)
            //    {
            //        Console.WriteLine(e.Message + " " + group.ToList().First().RdngID + " " + group.ToList().First().AccID + " " + group.ToList().First().RUnitID
            //            + "\n" + e.StackTrace);
            //    }
            //}
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
    }
}
