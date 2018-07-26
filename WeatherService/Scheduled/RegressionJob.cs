using MathNet.Numerics;
using MathNet.Numerics.LinearRegression;
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
using System.Data;
using MathNet.Numerics.LinearAlgebra.Double;
using Accord.Statistics.Models.Regression.Linear;
using Accord.Math.Optimization.Losses;
using Accord.Statistics.Models.Regression.Fitting;
using Accord.Statistics.Analysis;

namespace WeatherService.Scheduled
{
    public class RegressionJob : IJob
    {
        private AerisJobParams _aerisJobParams;
        private IWeatherRepository _weatherRepository;

        readonly DateTime fromDateStart = new DateTime(2014, 11, 13);

        public Task Execute(IJobExecutionContext context)
        {
            _aerisJobParams = AerisJobParamsValueOf(context);
            _weatherRepository = _weatherRepositoryValueOf(_aerisJobParams);

            PopulateWthNormalParams();

            //PopulateMyWthExpUsage();

            return Task.FromResult(0);
        }

        private void PopulateWthNormalParams()
        {
            List<WthNormalParams> normalParamsKeys = _weatherRepository.GetNormalParamsKeysForRegression();

            foreach (WthNormalParams normalParamsKey in normalParamsKeys)
            {
                try
                {
                    //string desiredStartDate = normalParamsKey.EndDate_Original.AddYears(-1).ToShortDateString();
                    int yearAsInt = normalParamsKey.EndDate_Original.Year - 2000;
                    string desiredStartDate = yearAsInt.ToString();

                    List<BalancePointPair> allBalancePointStatsFromYear = CalculateOneYearExpUsageForAllBalancePoints(desiredStartDate, normalParamsKey).ToList();

                    List<BalancePointPair> analyzedBalancePointData =
                        CalculateLinearRegression(allBalancePointStatsFromYear, normalParamsKey)
                        //.Where(s => s.B1_New >= 0)
                        .OrderByDescending(s => s.RSquared_New).ToList();

                    BalancePointPair winner = analyzedBalancePointData.First();

                    normalParamsKey.WthZipCode = winner.WthZipCode;
                    normalParamsKey.B1_New = winner.B1_New;
                    normalParamsKey.B2_New = winner.B2_New;
                    normalParamsKey.B3_New = winner.HeatingBalancePoint;
                    normalParamsKey.B4_New = winner.B4_New;
                    normalParamsKey.B5_New = winner.CoolingBalancePoint;

                    if (winner.RSquared_New.HasValue)
                    {
                        normalParamsKey.R2_New = decimal.Round(Convert.ToDecimal(winner.RSquared_New), 9, MidpointRounding.AwayFromZero);
                    }

                    normalParamsKey.YearOfReadsDateStart = winner.YearOfReadsDateStart;
                    normalParamsKey.YearOfReadsDateEnd = winner.YearOfReadsDateEnd;
                    normalParamsKey.Readings = winner.ReadingsInNormalYear;
                    normalParamsKey.Days = winner.DaysInNormalYear;
                    normalParamsKey.StandardError_New = decimal.Round(Convert.ToDecimal(winner.StandardError), 9, MidpointRounding.AwayFromZero);

                    _weatherRepository.InsertWthNormalParams(normalParamsKey);
                }
                catch (Exception e)
                {
                    Console.WriteLine(normalParamsKey.AccID + " " + normalParamsKey.EndDate_Original);
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

            foreach (ReadingsQueryResult reading in readings)
            {
                days += reading.Days;
            }

            foreach (ReadingsQueryResult reading in readings)
            {
                HeatingCoolingDegreeDays hcdd = new HeatingCoolingDegreeDays
                {
                    CDD = 0.0,
                    HDD = 0.0
                };

                List<WeatherData> weatherDataList = _weatherRepository.GetWeatherDataByZipStartAndEndDate(reading.Zip, reading.DateStart, reading.DateEnd);

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

                    //int k = range - 1 - i;
                    //while (k >= 0)
                    //{
                    //    int[] both = new int[2] { rangeMin + i, rangeMin + i + k };
                    //    k--;

                    //    comboList.Add(both);
                    //}

                    //for (int j = 0; j < range; j++)
                    //{
                    //    int[] allBoth = new int[2] { rangeMin + i, rangeMin + j };
                    //    comboList.Add(allBoth);
                    //}
                }

                comboList.Add(new int[] { 0, 0 });

                int expectedComboListCount = 0;

                /* cases where (heating bp) != (cooling bp) */
                expectedComboListCount = (range + 1) * ((range) / 2);
                if (range % 2 != 0)
                {
                    expectedComboListCount += ((range) / 2) + 1;
                }

                /* cases where (heating bp) != 0 && (cooling bp) == 0 or
                 *             (heating bp) == 0 && (cooling bp) != 0; and
                 *             (heating bp) == 0 && (cooling bp) == 0.
                 */
                expectedComboListCount += (range * 2) + 1;

                foreach (int[] combo in comboList)
                {
                    reading.B3 = combo[0];
                    reading.B5 = combo[1];

                    hcdd = HeatingCoolingDegreeDaysValueOf(reading, weatherDataList);

                    BalancePointPair balancePoint = new BalancePointPair
                    {
                        RdngID = reading.RdngID,
                        DaysInReading = reading.Days,
                        CoolingBalancePoint = reading.B5,
                        HeatingBalancePoint = reading.B3,
                        CoolingDegreeDays = (double)hcdd.CDD,
                        HeatingDegreeDays = (double)hcdd.HDD,
                        ActualUsage = reading.Units,
                        YearOfReadsDateStart = _ReadDateStart,
                        YearOfReadsDateEnd = _ReadDateEnd,
                        ReadingsInNormalYear = readings.Count,
                        DaysInNormalYear = days,
                        WthZipCode = reading.Zip
                    };

                    allBalancePointPairs.Add(balancePoint);
                }
            }

            return allBalancePointPairs;
        }

        private HeatingCoolingDegreeDays HeatingCoolingDegreeDaysValueOf(ReadingsQueryResult result, List<WeatherData> weatherDataList)
        {
            HeatingCoolingDegreeDays hcdd = new HeatingCoolingDegreeDays
            {
                CDD = 0.0,
                HDD = 0.0,
            };

            if (result.B3 == 0 && result.B5 == 0)
            {
                return hcdd;
            }
            else
            {
                foreach (WeatherData weatherData in weatherDataList)
                {
                    if (!weatherData.AvgTmp.HasValue)
                    {
                        throw new Exception("WeatherData.AvgTmp is null for " + weatherData.ZipCode + " on " + weatherData.RDate);
                    }
                    else if (result.B5 > 0 && weatherData.AvgTmp >= result.B5)
                    {
                        hcdd.CDD += (weatherData.AvgTmp.Value - result.B5);
                    }
                    else if (result.B3 > 0 && weatherData.AvgTmp < result.B3)
                    {
                        hcdd.HDD += (result.B3 - weatherData.AvgTmp.Value);
                    }
                }
            }

            return hcdd;
        }

        private List<BalancePointPair> CalculateLinearRegression(List<BalancePointPair> allBalancePointPairs, WthNormalParams normalParamsKey)
        {
            var updatedBalancePointPairs = new List<BalancePointPair>();

            var allBalancePointGroups = allBalancePointPairs.GroupBy(s => new { s.CoolingBalancePoint, s.HeatingBalancePoint });

            List<(double[], double)> rSvds = new List<(double[], double)>();

            List<AccordResult> accordResults = new List<AccordResult>();


            foreach (var group in allBalancePointGroups)
            {
                List<BalancePointPair> IdenticalBalancePointPairsForAllReadings = group.ToList();
                BalancePointPair _pointPair = IdenticalBalancePointPairsForAllReadings.First();
                int readingsCount = IdenticalBalancePointPairsForAllReadings.Count;

                double[] fullYData = new double[readingsCount];
                double[] fullYDataDailyAvg = new double[readingsCount];

                double[][] hcddMatrix = new double[readingsCount][];

                double[][] hcddMatrixNonDaily = new double[readingsCount][];

                foreach (BalancePointPair balancePointPair in IdenticalBalancePointPairsForAllReadings)
                {
                    fullYData[IdenticalBalancePointPairsForAllReadings.IndexOf(balancePointPair)] = (balancePointPair.ActualUsage);
                    fullYDataDailyAvg[IdenticalBalancePointPairsForAllReadings.IndexOf(balancePointPair)] = (balancePointPair.ActualUsage / balancePointPair.DaysInReading);

                    hcddMatrix[IdenticalBalancePointPairsForAllReadings.IndexOf(balancePointPair)] = new double[]
                        {
                            (balancePointPair.HeatingDegreeDays / balancePointPair.DaysInReading),
                            (balancePointPair.CoolingDegreeDays / balancePointPair.DaysInReading)
                        };

                    hcddMatrixNonDaily[IdenticalBalancePointPairsForAllReadings.IndexOf(balancePointPair)] = new double[]
                        {
                            balancePointPair.DaysInReading,
                            balancePointPair.HeatingDegreeDays,
                            balancePointPair.CoolingDegreeDays
                        };
                }

                double[] avgHddsForEachReadingInYear = new double[readingsCount];
                double[] avgCddsForEachReadingInYear = new double[readingsCount];

                for (int i = 0; i < readingsCount; i++)
                {
                    avgHddsForEachReadingInYear[i] = hcddMatrix[i][0];
                    avgCddsForEachReadingInYear[i] = hcddMatrix[i][1];
                }

                double[][] hcddMatrixNonDailyHeatingOnly = new double[readingsCount][];
                double[][] hcddMatrixNonDailyCoolingOnly = new double[readingsCount][];

                for (int i = 0; i < readingsCount; i++)
                {
                    hcddMatrixNonDailyHeatingOnly[i] = new double[] { hcddMatrixNonDaily[i][0], hcddMatrixNonDaily[i][1] };
                    hcddMatrixNonDailyCoolingOnly[i] = new double[] { hcddMatrixNonDaily[i][0], hcddMatrixNonDaily[i][2] };
                }

                double[] modelParams = new double[3];
                modelParams[0] = 0;
                modelParams[1] = 0;
                modelParams[2] = 0;

                //double[] modelParams = new double[];
                //modelParams[0] = 0;
                //modelParams[1] = 0;
                //modelParams[2] = 0;

                if (_pointPair.HeatingBalancePoint == 0 && _pointPair.CoolingBalancePoint == 0)
                {
                    double[] onesVector = new double[readingsCount];

                    for (int i = 0; i < readingsCount; i++)
                    {
                        onesVector[i] = 1;
                    }

                    modelParams[0] = Fit.LineThroughOrigin(onesVector, fullYDataDailyAvg);

                    //modelParams[0] = Fit.LineThroughOrigin(hcddMatrixNonDaily.Select(x => x[0]).ToArray(), fullYData);

                    OrdinaryLeastSquares ols = new OrdinaryLeastSquares()
                    {
                        UseIntercept = false
                    };

                    SimpleLinearRegression regressionAccord = ols.Learn(hcddMatrixNonDaily.Select(x => x[0]).ToArray(), fullYData);

                    double[] predictedAccord = regressionAccord.Transform(hcddMatrixNonDaily.Select(x => x[0]).ToArray());

                    double errorAccord = new SquareLoss(fullYDataDailyAvg).Loss(predictedAccord);

                    double rAccord = regressionAccord.CoefficientOfDetermination(hcddMatrixNonDaily.Select(x => x[0]).ToArray(), fullYData, adjust: false);
                    double rAdjAccord = regressionAccord.CoefficientOfDetermination(hcddMatrixNonDaily.Select(x => x[0]).ToArray(), fullYData, adjust: true);

                    AccordResult accordResult = new AccordResult()
                    {
                        SimpleLinearRegression = regressionAccord,
                        R2Accord = rAccord,
                        AdjustedR2Accord = rAdjAccord,
                        IsSimpleSingleRegression = true,
                        HeatingBP = _pointPair.HeatingBalancePoint,
                        CoolingBP = _pointPair.CoolingBalancePoint,
                        AccID = normalParamsKey.AccID,
                        UtilID = normalParamsKey.UtilID,
                        UnitID = normalParamsKey.UnitID
                    };

                    //OrdinaryLeastSquares ols = new OrdinaryLeastSquares()
                    //{
                    //    UseIntercept = false
                    //};

                    //SimpleLinearRegression regressionAccord = ols.Learn(hcddMatrixNonDaily.Select(x => x[0]).ToArray(), fullYData);

                    //double[] predictedAccord = regressionAccord.Transform(hcddMatrixNonDaily.Select(x => x[0]).ToArray());

                    //double errorAccord = new SquareLoss(fullYDataDailyAvg).Loss(predictedAccord);

                    //double rAccord = regressionAccord.CoefficientOfDetermination(hcddMatrixNonDaily.Select(x => x[0]).ToArray(), fullYData, adjust: false);
                    //double rAdjAccord = regressionAccord.CoefficientOfDetermination(hcddMatrixNonDaily.Select(x => x[0]).ToArray(), fullYData, adjust: true);

                    //AccordResult accordResult = new AccordResult()
                    //{
                    //    SimpleLinearRegression = regressionAccord,
                    //    R2Accord = rAccord,
                    //    AdjustedR2Accord = rAdjAccord,
                    //    IsSimpleSingleRegression = true,
                    //    HeatingBP = _pointPair.HeatingBalancePoint,
                    //    CoolingBP = _pointPair.CoolingBalancePoint,
                    //    AccID = normalParamsKey.AccID,
                    //    UtilID = normalParamsKey.UtilID,
                    //    UnitID = normalParamsKey.UnitID
                    //};

                    accordResults.Add(accordResult);
                }
                else if (_pointPair.CoolingBalancePoint != 0 && _pointPair.HeatingBalancePoint != 0)
                {
                    modelParams = MultipleRegression.QR(hcddMatrix, fullYDataDailyAvg, intercept: true);

                    double rAvg = MathNet.Numerics.GoodnessOfFit.CoefficientOfDetermination(
                        hcddMatrix.Select((x) => modelParams[0] + modelParams[1] * x[0] + modelParams[2] * x[1]), fullYDataDailyAvg);

                    //modelParams = MultipleRegression.QR(hcddMatrixNonDaily, fullYData, intercept: false);

                    //double r = GoodnessOfFit.CoefficientOfDetermination(
                    //    hcddMatrixNonDaily.Select(x => x[0] * modelParams[0] + x[1] * modelParams[1] + x[2] * modelParams[2]), fullYData);

                    //Accord
                    var ols = new OrdinaryLeastSquares()
                    {
                        UseIntercept = true
                    };

                    try
                    {
                        MultipleLinearRegression accordRegression = ols.Learn(hcddMatrix, fullYDataDailyAvg);

                        double[] predicted = accordRegression.Transform(hcddMatrix);

                        double error = new SquareLoss(expected: fullYDataDailyAvg).Loss(actual: predicted);

                        double r2Accord = new RSquaredLoss(numberOfInputs: 3, expected: fullYDataDailyAvg).Loss(predicted);

                        var r2Loss = new RSquaredLoss(numberOfInputs: 3, expected: fullYDataDailyAvg) { Adjust = true };

                        double adjustedR2Accord = r2Loss.Loss(predicted);

                        double r2Coeff = accordRegression.CoefficientOfDetermination(hcddMatrix, fullYDataDailyAvg, adjust: false);

                        double r2CoeffAdj = accordRegression.CoefficientOfDetermination(hcddMatrix, fullYDataDailyAvg, adjust: true);

                        AccordResult accordResult = new AccordResult()
                        {
                            MultipleRegression = accordRegression,
                            Error = error,
                            R2Accord = r2Accord,
                            AdjustedR2Accord = adjustedR2Accord,
                            R2Coeff = r2Coeff,
                            R2CoffAdj = r2CoeffAdj,
                            HeatingBP = _pointPair.HeatingBalancePoint,
                            CoolingBP = _pointPair.CoolingBalancePoint,
                            IsSimpleSingleRegression = false,
                            AccID = normalParamsKey.AccID,
                            UtilID = normalParamsKey.UtilID,
                            UnitID = normalParamsKey.UnitID
                        };

                        accordResults.Add(accordResult);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message + " " + e.StackTrace);
                    }

                    ////Accord
                    //var ols = new OrdinaryLeastSquares()
                    //{
                    //    UseIntercept = false
                    //};

                    //try
                    //{
                    //    MultipleLinearRegression accordRegression = ols.Learn(hcddMatrixNonDaily, fullYData);

                    //    double[] predicted = accordRegression.Transform(hcddMatrixNonDaily);

                    //    double error = new SquareLoss(expected: fullYData).Loss(actual: predicted);

                    //    double r2Accord = new RSquaredLoss(numberOfInputs: 3, expected: fullYData).Loss(predicted);

                    //    var r2Loss = new RSquaredLoss(numberOfInputs: 3, expected: fullYData) { Adjust = true };

                    //    double adjustedR2Accord = r2Loss.Loss(predicted);

                    //    double r2Coeff = accordRegression.CoefficientOfDetermination(hcddMatrixNonDaily, fullYData, adjust: false);

                    //    double r2CoeffAdj = accordRegression.CoefficientOfDetermination(hcddMatrixNonDaily, fullYData, adjust: true);

                    //    AccordResult accordResult = new AccordResult()
                    //    {
                    //        MultipleRegression = accordRegression,
                    //        Error = error,
                    //        R2Accord = r2Accord,
                    //        AdjustedR2Accord = adjustedR2Accord,
                    //        R2Coeff = r2Coeff,
                    //        R2CoffAdj = r2CoeffAdj,
                    //        HeatingBP = _pointPair.HeatingBalancePoint,
                    //        CoolingBP = _pointPair.CoolingBalancePoint,
                    //        IsSimpleSingleRegression = false,
                    //        AccID = normalParamsKey.AccID,
                    //        UtilID = normalParamsKey.UtilID,
                    //        UnitID = normalParamsKey.UnitID
                    //    };

                    //    accordResults.Add(accordResult);
                    //}
                    //catch (Exception e)
                    //{
                    //    Console.WriteLine(e.Message + " " + e.StackTrace);
                    //}

                    try
                    {
                        //double[] pSvd = MultipleRegression.Svd(hcddMatrix, fullYDataDailyAvg, intercept: true);
                        //double rSvd = EvaluateParametizedModel(IdenticalBalancePointPairsForAllReadings, pSvd, fullYData).Item1;
                        //rSvds.Add((pSvd, rSvd));
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message + " Svd");
                    }
                }
                else if (_pointPair.HeatingBalancePoint > 0)
                {
                    Tuple<double, double> heatingTuple = Fit.Line(avgHddsForEachReadingInYear, fullYDataDailyAvg);
                    modelParams[0] = heatingTuple.Item1;
                    modelParams[1] = heatingTuple.Item2;

                    double r = MathNet.Numerics.GoodnessOfFit.CoefficientOfDetermination(
                        avgHddsForEachReadingInYear.Select(x => heatingTuple.Item1 + heatingTuple.Item2 * x), fullYDataDailyAvg);

                    OrdinaryLeastSquares ols = new OrdinaryLeastSquares()
                    {
                        UseIntercept = true
                    };

                    SimpleLinearRegression regressionAccord = ols.Learn(avgHddsForEachReadingInYear, fullYDataDailyAvg);

                    double[] predictedAccord = regressionAccord.Transform(avgHddsForEachReadingInYear);

                    double errorAccord = new SquareLoss(fullYDataDailyAvg).Loss(predictedAccord);

                    double rAccord = regressionAccord.CoefficientOfDetermination(avgHddsForEachReadingInYear, fullYDataDailyAvg, adjust: false);
                    double rAdjAccord = regressionAccord.CoefficientOfDetermination(avgHddsForEachReadingInYear, fullYDataDailyAvg, adjust: true);

                    AccordResult accordResult = new AccordResult()
                    {
                        SimpleLinearRegression = regressionAccord,
                        R2Accord = rAccord,
                        AdjustedR2Accord = rAdjAccord,
                        IsSimpleSingleRegression = true,
                        HeatingBP = _pointPair.HeatingBalancePoint,
                        CoolingBP = _pointPair.CoolingBalancePoint
                    };

                    accordResults.Add(accordResult);

                    //OrdinaryLeastSquares ols = new OrdinaryLeastSquares()
                    //{
                    //    UseIntercept = false
                    //};

                    //try
                    //{
                    //    MultipleLinearRegression accordRegression = ols.Learn(hcddMatrixNonDailyHeatingOnly, fullYData);

                    //    double[] predicted = accordRegression.Transform(hcddMatrixNonDailyHeatingOnly);

                    //    double error = new SquareLoss(expected: fullYData).Loss(actual: predicted);

                    //    double r2Accord = new RSquaredLoss(numberOfInputs: 3, expected: fullYData).Loss(predicted);

                    //    var r2Loss = new RSquaredLoss(numberOfInputs: 3, expected: fullYData) { Adjust = true };

                    //    double adjustedR2Accord = r2Loss.Loss(predicted);

                    //    double r2Coeff = accordRegression.CoefficientOfDetermination(hcddMatrixNonDailyHeatingOnly, fullYData, adjust: false);

                    //    double r2CoeffAdj = accordRegression.CoefficientOfDetermination(hcddMatrixNonDailyHeatingOnly, fullYData, adjust: true);

                    //    AccordResult accordResult = new AccordResult()
                    //    {
                    //        MultipleRegression = accordRegression,
                    //        Error = error,
                    //        R2Accord = r2Accord,
                    //        AdjustedR2Accord = adjustedR2Accord,
                    //        R2Coeff = r2Coeff,
                    //        R2CoffAdj = r2CoeffAdj,
                    //        HeatingBP = _pointPair.HeatingBalancePoint,
                    //        CoolingBP = _pointPair.CoolingBalancePoint,
                    //        IsSimpleSingleRegression = false,
                    //        AccID = normalParamsKey.AccID,
                    //        UtilID = normalParamsKey.UtilID,
                    //        UnitID = normalParamsKey.UnitID
                    //    };

                    //    accordResults.Add(accordResult);
                    //}
                    //catch (Exception e)
                    //{
                    //    Console.WriteLine(e.Message + " " + e.StackTrace);
                    //}
                }
                else if (_pointPair.CoolingBalancePoint > 0)
                {
                    Tuple<double, double> coolingTuple = Fit.Line(avgCddsForEachReadingInYear, fullYDataDailyAvg);
                    modelParams[0] = coolingTuple.Item1;
                    modelParams[2] = coolingTuple.Item2;

                    OrdinaryLeastSquares ols = new OrdinaryLeastSquares()
                    {
                        UseIntercept = true
                    };

                    SimpleLinearRegression regressionAccord = ols.Learn(avgCddsForEachReadingInYear, fullYDataDailyAvg);

                    double[] predictedAccord = regressionAccord.Transform(avgCddsForEachReadingInYear);

                    double errorAccord = new SquareLoss(fullYDataDailyAvg).Loss(predictedAccord);

                    double rAccord = regressionAccord.CoefficientOfDetermination(avgCddsForEachReadingInYear, fullYDataDailyAvg, adjust: false);
                    double rAdjAccord = regressionAccord.CoefficientOfDetermination(avgCddsForEachReadingInYear, fullYDataDailyAvg, adjust: true);

                    AccordResult accordResult = new AccordResult()
                    {
                        SimpleLinearRegression = regressionAccord,
                        R2Accord = rAccord,
                        AdjustedR2Accord = rAdjAccord,
                        IsSimpleSingleRegression = true,
                        HeatingBP = _pointPair.HeatingBalancePoint,
                        CoolingBP = _pointPair.CoolingBalancePoint
                    };

                    accordResults.Add(accordResult);
                }

                    //OrdinaryLeastSquares ols = new OrdinaryLeastSquares()
                    //{
                    //    UseIntercept = false
                    //};

                    //try
                    //{ 
                    //MultipleLinearRegression accordRegression = ols.Learn(hcddMatrixNonDailyCoolingOnly, fullYData);

                    //double[] predicted = accordRegression.Transform(hcddMatrixNonDailyCoolingOnly);

                    //double error = new SquareLoss(expected: fullYData).Loss(actual: predicted);

                    //double r2Accord = new RSquaredLoss(numberOfInputs: 3, expected: fullYData).Loss(predicted);

                    //var r2Loss = new RSquaredLoss(numberOfInputs: 3, expected: fullYData) { Adjust = true };

                    //double adjustedR2Accord = r2Loss.Loss(predicted);

                    //double r2Coeff = accordRegression.CoefficientOfDetermination(hcddMatrixNonDailyCoolingOnly, fullYData, adjust: false);

                    //double r2CoeffAdj = accordRegression.CoefficientOfDetermination(hcddMatrixNonDailyCoolingOnly, fullYData, adjust: true);

                    //AccordResult accordResult = new AccordResult()
                    //{
                    //    MultipleRegression = accordRegression,
                    //    Error = error,
                    //    R2Accord = r2Accord,
                    //    AdjustedR2Accord = adjustedR2Accord,
                    //    R2Coeff = r2Coeff,
                    //    R2CoffAdj = r2CoeffAdj,
                    //    HeatingBP = _pointPair.HeatingBalancePoint,
                    //    CoolingBP = _pointPair.CoolingBalancePoint,
                    //    IsSimpleSingleRegression = false,
                    //    AccID = normalParamsKey.AccID,
                    //    UtilID = normalParamsKey.UtilID,
                    //    UnitID = normalParamsKey.UnitID
                    //};

                    //accordResults.Add(accordResult);
                    //    }
                    //catch (Exception e)
                    //{
                    //    Console.WriteLine(e.Message + " " + e.StackTrace);
                    //}
                //};

                Tuple<double, double> regressionEval;

                if (AreDoublesAllNotInfinityNorNaN(modelParams))
                {
                    regressionEval = EvaluateParametizedModel(IdenticalBalancePointPairsForAllReadings, modelParams, fullYData);
                    double RSquared = regressionEval.Item1;
                    double standardError = regressionEval.Item2;

                    if (AreDoublesAllNotInfinityNorNaN(new double[] { RSquared }))
                    {
                        _pointPair.B1_New = decimal.Round(Convert.ToDecimal(modelParams[0]), 9, MidpointRounding.AwayFromZero);
                        _pointPair.B2_New = decimal.Round(Convert.ToDecimal(modelParams[1]), 9, MidpointRounding.AwayFromZero);
                        _pointPair.B4_New = decimal.Round(Convert.ToDecimal(modelParams[2]), 9, MidpointRounding.AwayFromZero);
                        _pointPair.RSquared_New = RSquared;
                        _pointPair.StandardError = standardError;
                    }
                    else
                    {
                        _pointPair.HeatingBalancePoint = 0;
                        _pointPair.CoolingBalancePoint = 0;
                    }
                }
                else
                {
                    _pointPair.HeatingBalancePoint = 0;
                    _pointPair.CoolingBalancePoint = 0;
                };

                updatedBalancePointPairs.Add(_pointPair);
            }

            //(double[], double) bigRSvd = rSvds.OrderByDescending(s => s.Item2).ToList().First();
            AccordResult accord = accordResults.OrderByDescending(s => s.R2Accord).ToList().First();
            AccordResult accordAdj = accordResults.OrderByDescending(s => s.AdjustedR2Accord).ToList().First();
            BalancePointPair _winner = updatedBalancePointPairs.OrderByDescending(s => s.RSquared_New).ToList().First();
            double? _bigRNew_OG = _winner.RSquared_New;

            //PopulateMyWthExpUsage(accord);

            return updatedBalancePointPairs;
        }

        private Tuple<double, double> EvaluateParametizedModel(List<BalancePointPair> balancePointPairs, double[] modelParameters, double[] fullYData)
        {
            List<double> predicted = new List<double>();

            foreach (BalancePointPair balancePointPair in balancePointPairs)
            {
                double expUsage_New = CalculateExpUsageForNomalizing(balancePointPair, modelParameters);
                predicted.Add(expUsage_New);
            }

            int degreesOfFreedom = 0;

            foreach (double p in modelParameters)
            {
                if (p > 0 || p < 0)
                {
                    degreesOfFreedom++;
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
            //List<ReadingsQueryResult> allReadings = _weatherRepository.GetReadingsFromExpUsageOriginal();
            List<ReadingsQueryResult> allReadings = _weatherRepository.GetReadingsFromExpUsageOriginalCorrected();

            var readingsByAccount = allReadings.GroupBy(r => new { r.AccID, r.UtilID, r.RUnitID });

            foreach(var group in readingsByAccount)
            {
                try
                {
                    List<ReadingsQueryResult> readings = group.ToList();
                    WthNormalParams _params = 
                        _weatherRepository.GetParamsForReading(readings.First().AccID, readings.First().UtilID, readings.First().RUnitID);

                    foreach (ReadingsQueryResult reading in readings)
                    {
                        WthExpUsage wthExpUsage = new WthExpUsage();
                        wthExpUsage.RdngID = reading.RdngID;
                        wthExpUsage.AccID = reading.AccID;
                        wthExpUsage.UtilID = reading.UtilID;
                        wthExpUsage.UnitID = reading.RUnitID;
                        decimal expUsage_Old = decimal.Round(Convert.ToDecimal(reading.ExpUsage), 4, MidpointRounding.AwayFromZero);
                        wthExpUsage.ExpUsage_Old = expUsage_Old;
                        wthExpUsage.Units = reading.Units;

                        List<WeatherData> weatherDataList =
                            _weatherRepository.GetWeatherDataByZipStartAndEndDate(_params.WthZipCode, reading.DateStart, reading.DateEnd);

                        reading.B3 = _params.B3_New;
                        reading.B5 = _params.B5_New;

                        HeatingCoolingDegreeDays hcdd = HeatingCoolingDegreeDaysValueOf(reading, weatherDataList);

                        decimal expUsage_New = _params.B1_New * reading.Days
                            + (_params.B2_New * decimal.Round(Convert.ToDecimal(hcdd.HDD), 4, MidpointRounding.AwayFromZero))
                            + (_params.B4_New * decimal.Round(Convert.ToDecimal(hcdd.CDD), 4, MidpointRounding.AwayFromZero));

                        wthExpUsage.ExpUsage_New = expUsage_New;

                        if (reading.Units != 0)
                        {
                            wthExpUsage.PercentDelta_Old = Math.Abs((expUsage_Old - reading.Units) / reading.Units);
                            wthExpUsage.PercentDelta_New = Math.Abs((expUsage_New - reading.Units) / reading.Units);
                        }
                        wthExpUsage.DateStart = reading.DateStart;
                        wthExpUsage.DateEnd = reading.DateEnd;

                        _weatherRepository.InsertMyWthExpUsage(wthExpUsage);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message + " " + group.ToList().First().RdngID + " " + group.ToList().First().AccID + " " + group.ToList().First().RUnitID
                        + "\n" + e.StackTrace);
                }
            }
        }

        private void PopulateMyWthExpUsage(AccordResult accord)
        {
            //List<ReadingsQueryResult> allReadings = _weatherRepository.GetReadingsFromExpUsageOriginal();
            List<ReadingsQueryResult> allReadings = _weatherRepository.GetReadingsFromExpUsageOriginalCorrected(accord);

            var readingsByAccount = allReadings.GroupBy(r => new { r.AccID, r.UtilID, r.RUnitID });

            foreach (var group in readingsByAccount)
            {
                try
                {
                    List<ReadingsQueryResult> readings = group.ToList();
                    WthNormalParams _params =
                        _weatherRepository.GetParamsForReading(readings.First().AccID, readings.First().UtilID, readings.First().RUnitID);

                    foreach (ReadingsQueryResult reading in readings)
                    {
                        WthExpUsage wthExpUsage = new WthExpUsage();
                        wthExpUsage.RdngID = reading.RdngID;
                        wthExpUsage.AccID = reading.AccID;
                        wthExpUsage.UtilID = reading.UtilID;
                        wthExpUsage.UnitID = reading.RUnitID;
                        decimal expUsage_Old = decimal.Round(Convert.ToDecimal(reading.ExpUsage), 4, MidpointRounding.AwayFromZero);
                        wthExpUsage.ExpUsage_Old = expUsage_Old;
                        wthExpUsage.Units = reading.Units;

                        List<WeatherData> weatherDataList =
                            _weatherRepository.GetWeatherDataByZipStartAndEndDate(_params.WthZipCode, reading.DateStart, reading.DateEnd);

                        decimal B1 = 0;
                        decimal B2 = 0;
                        decimal B4 = 0;

                        reading.B3 = accord.HeatingBP;
                        reading.B5 = accord.CoolingBP;

                        if (accord.IsSimpleSingleRegression)
                        {
                            if (reading.B3 > 0)
                            {
                                B1 = decimal.Round(Convert.ToDecimal(accord.SimpleLinearRegression.Intercept), 12, MidpointRounding.AwayFromZero);
                                B2 = decimal.Round(Convert.ToDecimal(accord.SimpleLinearRegression.Slope), 12, MidpointRounding.AwayFromZero);
                                B4 = 0;
                            }
                            else if (reading.B5 > 0)
                            {
                                B1 = decimal.Round(Convert.ToDecimal(accord.SimpleLinearRegression.Intercept), 12, MidpointRounding.AwayFromZero);
                                B2 = 0;
                                B4 = decimal.Round(Convert.ToDecimal(accord.SimpleLinearRegression.Slope), 12, MidpointRounding.AwayFromZero);
                            }
                        }
                        else
                        {
                            if (reading.B3 > 0 && reading.B5 > 0)
                            {
                                B1 = decimal.Round(Convert.ToDecimal(accord.MultipleRegression.Weights[0]), 12, MidpointRounding.AwayFromZero);
                                B2 = decimal.Round(Convert.ToDecimal(accord.MultipleRegression.Weights[1]), 12, MidpointRounding.AwayFromZero);
                                B4 = decimal.Round(Convert.ToDecimal(accord.MultipleRegression.Weights[2]), 12, MidpointRounding.AwayFromZero);
                            }
                            else if (reading.B3 > 0)
                            {
                                B1 = decimal.Round(Convert.ToDecimal(accord.MultipleRegression.Weights[0]), 12, MidpointRounding.AwayFromZero);
                                B2 = decimal.Round(Convert.ToDecimal(accord.MultipleRegression.Weights[1]), 12, MidpointRounding.AwayFromZero);
                            }
                            else if (reading.B5 > 0)
                            {
                                B1 = decimal.Round(Convert.ToDecimal(accord.MultipleRegression.Weights[0]), 12, MidpointRounding.AwayFromZero);
                                B4 = decimal.Round(Convert.ToDecimal(accord.MultipleRegression.Weights[1]), 12, MidpointRounding.AwayFromZero);
                            }
                        }

                        HeatingCoolingDegreeDays hcdd = HeatingCoolingDegreeDaysValueOf(reading, weatherDataList);

                        decimal expUsage_New = (B1 * reading.Days) 
                            + (B2 * decimal.Round(Convert.ToDecimal(hcdd.HDD), 12, MidpointRounding.AwayFromZero))
                            + (B4 * decimal.Round(Convert.ToDecimal(hcdd.CDD), 12, MidpointRounding.AwayFromZero));

                        wthExpUsage.ExpUsage_New = expUsage_New;

                        if (reading.Units != 0)
                        {
                            wthExpUsage.PercentDelta_Old = Math.Abs((expUsage_Old - reading.Units) / reading.Units);
                            wthExpUsage.PercentDelta_New = Math.Abs((expUsage_New - reading.Units) / reading.Units);
                        }
                        wthExpUsage.DateStart = reading.DateStart;
                        wthExpUsage.DateEnd = reading.DateEnd;

                        _weatherRepository.InsertMyWthExpUsage(wthExpUsage, accord: true);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message + " " + group.ToList().First().RdngID + " " + group.ToList().First().AccID + " " + group.ToList().First().RUnitID
                        + "\n" + e.StackTrace);
                }
            }
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
