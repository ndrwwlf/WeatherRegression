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
                    string desiredStartDate = normalParamsKey.EndDate_Original.AddYears(-1).ToShortDateString();

                    //
                    //desiredStartDate = "2015-01-01";

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

                    int k = range - 1 - i;
                    while (k >= 0)
                    {
                        int[] both = new int[2] { rangeMin + i, rangeMin + i + k };
                        k--;

                        comboList.Add(both);
                    }

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
                expectedComboListCount += (range * 3) + 1;

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
                    if (result.B5 > 0 && weatherData.AvgTmp >= result.B5)
                    {
                        hcdd.CDD += (weatherData.AvgTmp - result.B5);
                    }
                    if (result.B3 > 0 && weatherData.AvgTmp < result.B3)
                    {
                        hcdd.HDD += (result.B3 - weatherData.AvgTmp);
                    }
                }
            }

            return hcdd;
        }

        private List<BalancePointPair> CalculateLinearRegression(List<BalancePointPair> allBalancePointPairs, WthNormalParams normalParamsKey)
        {
            var updatedBalancePointPairs = new List<BalancePointPair>();

            var allBalancePointGroups = allBalancePointPairs.GroupBy(s => new { s.CoolingBalancePoint, s.HeatingBalancePoint });

            //List<(double[], double)> rNormals = new List<(double[], double)>();
            List<(double[], double)> rSvds = new List<(double[], double)>();
            //List<(double[], double)> rQRs = new List<(double[], double)>();
            //List<(double[], double)> rOGs = new List<(double[], double)>();

            //List<(double[], double)> rDirects = new List<(double[], double)>();
            //List<(double[], double)> rLinearMultiDims = new List<(double[], double)>();
            //List<(double[], double)> rMultiDims = new List<(double[], double)>(); 
            //List<(double[], double)> r_Hats = new List<(double[], double)>();
            //List<(double[], double)> rPolys = new List<(double[], double)>();
            //List<(double[], double)> rPowers = new List<(double[], double)>();
            List<AccordResult> accordResults = new List<AccordResult>();


            foreach (var group in allBalancePointGroups)
            {
                List<BalancePointPair> IdenticalBalancePointPairsForAllReadings = group.ToList();

                int readingsCount = IdenticalBalancePointPairsForAllReadings.Count;
                BalancePointPair _pointPair = IdenticalBalancePointPairsForAllReadings.First();
                
                double[] fullYData = new double[readingsCount];
                double[] fullYDataDailyAvg = new double[readingsCount];

                double[][] hcddMatrix = new double[readingsCount][];

                foreach (BalancePointPair balancePointPair in IdenticalBalancePointPairsForAllReadings)
                {
                    fullYData[IdenticalBalancePointPairsForAllReadings.IndexOf(balancePointPair)] = (balancePointPair.ActualUsage);
                    fullYDataDailyAvg[IdenticalBalancePointPairsForAllReadings.IndexOf(balancePointPair)] = (balancePointPair.ActualUsage / balancePointPair.DaysInReading);

                    hcddMatrix[IdenticalBalancePointPairsForAllReadings.IndexOf(balancePointPair)] = new double[]
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

                if (_pointPair.HeatingBalancePoint == 0 && _pointPair.CoolingBalancePoint == 0)
                {
                    double[] onesVector = new double[readingsCount];

                    for (int i = 0; i < readingsCount; i++)
                    {
                        onesVector[i] = 1;
                    }
                    modelParams[0] = Fit.LineThroughOrigin(onesVector, fullYDataDailyAvg);
                }
                else if (_pointPair.CoolingBalancePoint != 0 && _pointPair.HeatingBalancePoint != 0)
                {
                    modelParams = MultipleRegression.QR(hcddMatrix, fullYDataDailyAvg, intercept: true);

                    //Accord
                    //var ols = new NonNegativeLeastSquares()
                    //{
                    //    MaxIterations = 1000
                    //};

                    //try
                    //{
                    //    MultipleLinearRegression accordRegression = ols.Learn(hcddMatrix, fullYDataDailyAvg);

                    //    double[] predicted = accordRegression.Transform(hcddMatrix);

                    //    double error = new SquareLoss(expected: fullYDataDailyAvg).Loss(actual: predicted);

                    //    double r2Accord = new RSquaredLoss(numberOfInputs: 2, expected: fullYDataDailyAvg).Loss(predicted);

                    //    var r2Loss = new RSquaredLoss(numberOfInputs: 2, expected: fullYDataDailyAvg) { Adjust = true };

                    //    double adjustedR2Accord = r2Loss.Loss(predicted);

                    //    double r2Coeff = accordRegression.CoefficientOfDetermination(hcddMatrix, fullYDataDailyAvg, adjust: false);

                    //    double r2CoeffAdj = accordRegression.CoefficientOfDetermination(hcddMatrix, fullYDataDailyAvg, adjust: true);

                    //    AccordResult accordResult = new AccordResult()
                    //    {
                    //        AccordRegression = accordRegression,
                    //        Error = error,
                    //        R2Accord = r2Accord,
                    //        AdjustedR2Accord = adjustedR2Accord,
                    //        R2Coeff = r2Coeff,
                    //        R2CoffAdj = r2CoeffAdj,
                    //        HeatingBP = _pointPair.HeatingBalancePoint,
                    //        CoolingBP = _pointPair.CoolingBalancePoint
                    //    };

                    //    accordResults.Add(accordResult);
                    //}catch (Exception e) { Console.WriteLine(e.Message); }

                    double[][] forceIntercept = new double[readingsCount][];

                    for (int i = 0; i < readingsCount; i++)
                    {
                        forceIntercept[i] = new double[]
                        {
                            1, avgHddsForEachReadingInYear[i], avgCddsForEachReadingInYear[i]
                        };
                    }

                    // Create a NN LS learning algorithm
                    var nnls = new NonNegativeLeastSquares()
                    {
                        MaxIterations = 100
                    };

                    // Use the algorithm to learn a multiple linear regression
                    MultipleLinearRegression nnRegression = nnls.Learn(forceIntercept, fullYDataDailyAvg);

                    // None of the regression coefficients should be negative:
                    double[] coefficientsNN = nnRegression.Weights; // should be

                    // Check the quality of the regression:
                    double[] predictionNN = nnRegression.Transform(forceIntercept);

                    double errorNN = new SquareLoss(expected: fullYDataDailyAvg)
                        .Loss(actual: predictionNN); // 

                    double rNN = nnRegression.CoefficientOfDetermination(forceIntercept, fullYDataDailyAvg, adjust: false);
                    double rNNAdj = nnRegression.CoefficientOfDetermination(forceIntercept, fullYDataDailyAvg, adjust: true);

                    AccordResult nNAccord = new AccordResult()
                    {
                        NNAccordRegression = nnRegression, 
                        R2Accord = rNN,
                        AdjustedR2Accord = rNNAdj
                    };

                    accordResults.Add(nNAccord);
                    //double rQR = EvaluateParametizedModel(IdenticalBalancePointPairsForAllReadings, modelParams, fullYData).Item1;
                    //rQRs.Add((modelParams, rQR));

                    //try
                    //{
                    //    double[] pNormal = MultipleRegression.NormalEquations(hcddMatrix, fullYDataDailyAvg, intercept: true);
                    //    double rNormal = EvaluateParametizedModel(IdenticalBalancePointPairsForAllReadings, pNormal, fullYData).Item1;
                    //    rNormals.Add((pNormal, rNormal));
                    //}
                    //catch (Exception e)
                    //{
                    //    Console.WriteLine(e.Message + " NormalEquations");
                    //}
                    try
                    {
                        double[] pSvd = MultipleRegression.Svd(hcddMatrix, fullYDataDailyAvg, intercept: true);
                        double rSvd = EvaluateParametizedModel(IdenticalBalancePointPairsForAllReadings, pSvd, fullYData).Item1;
                        rSvds.Add((pSvd, rSvd));
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message + " Svd");
                    }

                    //try
                    //{
                    //    double[] pDirect = MultipleRegression.DirectMethod(hcddMatrix, fullYDataDailyAvg, true);
                    //    double rDirect = EvaluateParametizedModel(IdenticalBalancePointPairsForAllReadings, pDirect, fullYData).Item1;
                    //    rDirects.Add((pDirect, rDirect));
                    //}catch (Exception e)
                    //{
                    //    Console.WriteLine(e.Message + " DirectMethods");
                    //}

                    //try
                    //{
                    //    double[] pLinearMultiDim = Fit.LinearMultiDim(hcddMatrix, fullYDataDailyAvg,
                    //        d => 1,
                    //        d => d[0],
                    //        d => d[1]);
                    //    double rLinearMultiDim = EvaluateParametizedModel(IdenticalBalancePointPairsForAllReadings, pLinearMultiDim, fullYData).Item1;
                    //    rLinearMultiDims.Add((pLinearMultiDim, rLinearMultiDim));
                    //}
                    //catch (Exception e)
                    //{
                    //    Console.WriteLine(e.Message + " LinearMultiDim");
                    //}

                    //try
                    //{
                    //    double[] pMultiDim = Fit.MultiDim(hcddMatrix, fullYDataDailyAvg, true);
                    //    double rMulitDim = EvaluateParametizedModel(IdenticalBalancePointPairsForAllReadings, pMultiDim, fullYData).Item1;
                    //    rMultiDims.Add((pMultiDim, rMulitDim));
                    //}
                    //catch (Exception e)
                    //{
                    //    Console.WriteLine(e.Message + " MultiDim");
                    //}

                    //double[] fullYDataAvg_Hat = fullYDataDailyAvg.Select(r => Math.Log(r)).ToArray();
                    //double[] p_Hat = Fit.LinearMultiDim(hcddMatrix, fullYDataAvg_Hat,
                    //    d => 1.0,
                    //    d => Math.Log(d[0]),
                    //    d => Math.Log(d[1]));
                    //p_Hat[0] = Math.Exp(p_Hat[0]);
                    //double r_Hat = EvaluateParametizedModel_Hat(IdenticalBalancePointPairsForAllReadings, p_Hat, fullYData).Item1;
                    //r_Hats.Add((p_Hat, r_Hat));
                }
                else if (_pointPair.HeatingBalancePoint > 0)
                {
                    Tuple<double, double> heatingTuple = Fit.Line(avgHddsForEachReadingInYear, fullYDataDailyAvg);
                    modelParams[0] = heatingTuple.Item1;
                    modelParams[1] = heatingTuple.Item2;

                    double[][] forceIntercept = new double[readingsCount][];

                    for (int i = 0; i < readingsCount; i++)
                    {
                        forceIntercept[i] = new double[]
                        {
                            1, avgHddsForEachReadingInYear[i], avgCddsForEachReadingInYear[i]
                        };
                    }

                    // Create a NN LS learning algorithm
                    var nnls = new NonNegativeLeastSquares()
                    {
                        MaxIterations = 100
                    };

                    // Use the algorithm to learn a multiple linear regression
                    MultipleLinearRegression nnRegression = nnls.Learn(forceIntercept, fullYDataDailyAvg);

                    // None of the regression coefficients should be negative:
                    double[] coefficientsNN = nnRegression.Weights; // should be

                    // Check the quality of the regression:
                    double[] predictionNN = nnRegression.Transform(forceIntercept);

                    double errorNN = new SquareLoss(expected: fullYDataDailyAvg)
                        .Loss(actual: predictionNN); // 

                    double rNN = nnRegression.CoefficientOfDetermination(forceIntercept, fullYDataDailyAvg, adjust: false);
                    double rNNAdj = nnRegression.CoefficientOfDetermination(forceIntercept, fullYDataDailyAvg, adjust: true);

                    AccordResult nNAccord = new AccordResult()
                    {
                        NNAccordRegression = nnRegression,
                        R2Accord = rNN,
                        AdjustedR2Accord = rNNAdj
                    };

                    accordResults.Add(nNAccord);

                    //double[] pPoly = Fit.Polynomial(avgHddsForEachReadingInYear, fullYDataDailyAvg, 2);
                    //double rPoly = EvaluateParametizedModelPoly(IdenticalBalancePointPairsForAllReadings, pPoly, fullYData, true).Item1;
                    //rPolys.Add((pPoly, rPoly));

                    //Tuple<double, double> pPower = Fit.Power(avgCddsForEachReadingInYear, fullYDataDailyAvg);
                    //double rPower = EvaluateParametizedModelPower(IdenticalBalancePointPairsForAllReadings, pPower, fullYData, true).Item1;
                    //rPowers.Add((new double[] { pPower.Item1, pPower.Item2 }, rPower));
                }
                else if (_pointPair.CoolingBalancePoint > 0)
                {
                    Tuple<double, double> coolingTuple = Fit.Line(avgCddsForEachReadingInYear, fullYDataDailyAvg);
                    modelParams[0] = coolingTuple.Item1;
                    modelParams[2] = coolingTuple.Item2;


                    double[][] forceIntercept = new double[readingsCount][];

                    for (int i = 0; i < readingsCount; i++)
                    {
                        forceIntercept[i] = new double[]
                        {
                            1, avgHddsForEachReadingInYear[i], avgCddsForEachReadingInYear[i]
                        };
                    }

                    // Create a NN LS learning algorithm
                    var nnls = new NonNegativeLeastSquares()
                    {
                        MaxIterations = 100
                    };

                    // Use the algorithm to learn a multiple linear regression
                    MultipleLinearRegression nnRegression = nnls.Learn(forceIntercept, fullYDataDailyAvg);

                    // None of the regression coefficients should be negative:
                    double[] coefficientsNN = nnRegression.Weights; // should be

                    // Check the quality of the regression:
                    double[] predictionNN = nnRegression.Transform(forceIntercept);

                    double errorNN = new SquareLoss(expected: fullYDataDailyAvg)
                        .Loss(actual: predictionNN); // 

                    double rNN = nnRegression.CoefficientOfDetermination(forceIntercept, fullYDataDailyAvg, adjust: false);
                    double rNNAdj = nnRegression.CoefficientOfDetermination(forceIntercept, fullYDataDailyAvg, adjust: true);

                    AccordResult nNAccord = new AccordResult()
                    {
                        NNAccordRegression = nnRegression,
                        R2Accord = rNN,
                        AdjustedR2Accord = rNNAdj
                    };

                    accordResults.Add(nNAccord);

                    //double[] pPoly = Fit.Polynomial(avgCddsForEachReadingInYear, fullYDataDailyAvg, 2);
                    //double rPoly = EvaluateParametizedModelPoly(IdenticalBalancePointPairsForAllReadings, pPoly, fullYData, false).Item1;
                    //rPolys.Add((pPoly,rPoly));

                    //Tuple<double, double> pPower = Fit.Power(avgCddsForEachReadingInYear, fullYDataDailyAvg);
                    //double rPower = EvaluateParametizedModelPower(IdenticalBalancePointPairsForAllReadings, pPower, fullYData, false).Item1;
                    //rPowers.Add((new double[] { pPower.Item1, pPower.Item2 }, rPower));
                };

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
                }
                else
                {
                    _pointPair.HeatingBalancePoint = 0;
                    _pointPair.CoolingBalancePoint = 0;
                };

                updatedBalancePointPairs.Add(_pointPair);
            }

            //(double[], double) bigRNormal = rNormals.OrderByDescending(s => s.Item2).ToList().First();
            (double[], double) bigRSvd = rSvds.OrderByDescending(s => s.Item2).ToList().First();
            //(double[], double) bigRQR = rQRs.OrderByDescending(s => s.Item2).ToList().First();
            //(double[], double) bigROG = rOGs.OrderByDescending(s => s.Item2).ToList().First();
            ////rDirects.OrderByDescending(s => s.Item2).ToList();
            ////double bigRDirect = rDirects.First().Item2;
            ////rMultiDims.OrderByDescending(s => s.Item2).ToList();
            ////double bigRMultiDim = rMultiDims.First().Item2;
            ////rLinearMultiDims.OrderByDescending(s => s.Item2).ToList();
            ////double bigRLinearMultiDim = rLinearMultiDims.First().Item2;
            //(double[], double) bigR_Hat = r_Hats.OrderByDescending(s => s.Item2).ToList().First();
            //(double[], double) bigRPoly = rPolys.OrderByDescending(s => s.Item2).ToList().First();
            //(double[], double) bigRPower = rPowers.OrderByDescending(s => s.Item2).ToList().First();

            //double _r2Accord = accordResults.OrderByDescending(s => s.R2Accord).ToList().First().R2Accord;
            AccordResult accord = accordResults.OrderByDescending(s => s.AdjustedR2Accord).ToList().First();

            double? _bigRNew_OG = updatedBalancePointPairs.OrderByDescending(s => s.RSquared_New).ToList().First().RSquared_New;

            //PopulateMyWthExpUsage(accord);
            //if(bigRNew_OG <= bigRPoly.Item2 || bigRNew_OG < bigRPower.Item2)
            //{
            //    Console.WriteLine("hey!");
            //}

            return updatedBalancePointPairs;
        }

        private Tuple<double, double> EvaluateParametizedModel(List<BalancePointPair> balancePointPairs, double[] modelParameters, double[] fullYData)
        {
            List<double> fullXData_NewFit = new List<double>();

            foreach (BalancePointPair balancePointPair in balancePointPairs)
            {
                double expUsage_New = CalculateExpUsageForNomalizing(balancePointPair, modelParameters);
                fullXData_NewFit.Add(expUsage_New);
            }

            int degreesOfFreedom = 0;
            foreach (double p in modelParameters)
            {
                if (p != 0)
                {
                    degreesOfFreedom++;
                }
            }

            double rBest = 0;
            double standardError = 0;

            try
            {
                rBest = GoodnessOfFit.CoefficientOfDetermination(fullXData_NewFit.ToArray(), fullYData);

                standardError = GoodnessOfFit.StandardError(fullXData_NewFit.ToArray(), fullYData, degreesOfFreedom);
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

        private Tuple<double, double> EvaluateParametizedModelPoly(List<BalancePointPair> balancePointPairs, double[] modelParameters, double[] fullYData, bool heating)
        {
            List<double> fullXData_NewFit = new List<double>();
            DenseMatrix d = new DenseMatrix(balancePointPairs.First().ReadingsInNormalYear);

            if (heating)
            {
                foreach (BalancePointPair balancePointPair in balancePointPairs)
                {
                    double expUsage_New =
                        (modelParameters[0] * balancePointPair.DaysInReading) +
                        (modelParameters[1] * balancePointPair.HeatingDegreeDays) +
                        (modelParameters[2] * Math.Pow(balancePointPair.HeatingDegreeDays, 2));

                    fullXData_NewFit.Add(expUsage_New);
                }
            }
            else
            {
                foreach (BalancePointPair balancePointPair in balancePointPairs)
                {
                    double expUsage_New =
                        (modelParameters[0] * balancePointPair.DaysInReading) +
                        (modelParameters[1] * balancePointPair.CoolingDegreeDays) +
                        (modelParameters[2] * Math.Pow(balancePointPair.CoolingDegreeDays, 2));

                    fullXData_NewFit.Add(expUsage_New);
                }
            }

            int degreesOfFreedom = 0;
            foreach (double p in modelParameters)
            {
                if (p != 0)
                {
                    degreesOfFreedom++;
                }
            }

            double rBest = 0;
            double standardError = 0;

            try
            {
                rBest = GoodnessOfFit.CoefficientOfDetermination(fullXData_NewFit.ToArray(), fullYData);

                standardError = GoodnessOfFit.StandardError(fullXData_NewFit.ToArray(), fullYData, degreesOfFreedom);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            return new Tuple<double, double>(rBest, standardError);
        }

        private Tuple<double, double> EvaluateParametizedModel_Hat(List<BalancePointPair> balancePointPairs, double[] p_Hat, double[] fullYData)
        {
            List<double> fullXData_NewFit = new List<double>();

            foreach (BalancePointPair balancePointPair in balancePointPairs)
            {
                double expUsage_New =
                    (p_Hat[0]) * Math.Pow(balancePointPair.HeatingDegreeDays, p_Hat[1]) * Math.Pow(balancePointPair.CoolingDegreeDays, p_Hat[2]);
                fullXData_NewFit.Add(expUsage_New);
            }

            int degreesOfFreedom = 0;
            foreach (double p in p_Hat)
            {
                if (p != 0)
                {
                    degreesOfFreedom++;
                }
            }

            double rBest = 0;
            double standardError = 0;

            try
            {
                rBest = GoodnessOfFit.CoefficientOfDetermination(fullXData_NewFit.ToArray(), fullYData);

                standardError = GoodnessOfFit.StandardError(fullXData_NewFit.ToArray(), fullYData, degreesOfFreedom);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            return new Tuple<double, double>(rBest, standardError);
        }

        private Tuple<double, double> EvaluateParametizedModelPower(List<BalancePointPair> balancePointPairs, 
            Tuple<double, double> pPower, double[] fullYData, bool heating)
        {
            List<double> fullXData_NewFit = new List<double>();

            if (heating)
            {
                foreach (BalancePointPair balancePointPair in balancePointPairs)
                {
                    double expUsage_New = pPower.Item1 * Math.Pow(balancePointPair.HeatingDegreeDays, pPower.Item2);
                    fullXData_NewFit.Add(expUsage_New);
                }
            }
            else
            {
                foreach (BalancePointPair balancePointPair in balancePointPairs)
                {
                    double expUsage_New = pPower.Item1 * Math.Pow(balancePointPair.CoolingDegreeDays, pPower.Item2);
                    fullXData_NewFit.Add(expUsage_New);
                }
            }

            //int degreesOfFreedom = 0;
            //foreach (double p in pPower)
            //{
            //    if (p != 0)
            //    {
            //        degreesOfFreedom++;
            //    }
            //}

            double rBest = 0;
            double standardError = 0;

            try
            {
                rBest = GoodnessOfFit.CoefficientOfDetermination(fullXData_NewFit.ToArray(), fullYData);

                //standardError = GoodnessOfFit.StandardError(fullXData_NewFit.ToArray(), fullYData, degreesOfFreedom);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            return new Tuple<double, double>(rBest, standardError);
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
                    Console.WriteLine(e.Message + " " + group.ToList().First().RdngID + " " + group.ToList().First().AccID + " " + group.ToList().First().RUnitID);
                }
            }
        }

        private void PopulateMyWthExpUsage(AccordResult accord)
        {
            //List<ReadingsQueryResult> allReadings = _weatherRepository.GetReadingsFromExpUsageOriginal();
            List<ReadingsQueryResult> allReadings = _weatherRepository.GetReadingsFromExpUsageOriginalCorrected();

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

                        reading.B3 = accord.HeatingBP;
                        reading.B5 = accord.CoolingBP;

                        decimal B1 = decimal.Round(Convert.ToDecimal(accord.AccordRegression.Intercept), 12, MidpointRounding.AwayFromZero);
                        decimal B2 = decimal.Round(Convert.ToDecimal(accord.AccordRegression.Weights[0]), 12, MidpointRounding.AwayFromZero);
                        decimal B4 = decimal.Round(Convert.ToDecimal(accord.AccordRegression.Weights[1]), 12, MidpointRounding.AwayFromZero);

                        HeatingCoolingDegreeDays hcdd = HeatingCoolingDegreeDaysValueOf(reading, weatherDataList);
                        decimal expUsage_New = _params.B1_New * reading.Days
                            + (B2 * decimal.Round(Convert.ToDecimal(hcdd.HDD), 12, MidpointRounding.AwayFromZero))
                            + (_params.B4_New * decimal.Round(Convert.ToDecimal(hcdd.CDD), 12, MidpointRounding.AwayFromZero));
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
                    Console.WriteLine(e.Message + " " + group.ToList().First().RdngID + " " + group.ToList().First().AccID + " " + group.ToList().First().RUnitID);
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
