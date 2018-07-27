using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using WeatherService.Dao;
using WeatherService.Dto;
using WeatherService.Model;
using WeatherService.Scheduled;
using WeatherService.Service;
using WeatherService.Services;

namespace WeatherService.Db
{
    public class WeatherRepository : IWeatherRepository
    {
        private readonly string _myConnectionString;
        private readonly string _jitWebData3ConnectionString;
        private readonly string _realJitWeatherConnection;

        public WeatherRepository(AerisJobParams aerisJobParams)
        {
            _myConnectionString = aerisJobParams.MyConnectionString;
            _jitWebData3ConnectionString = aerisJobParams.JitWebData3ConnectionString;
            _realJitWeatherConnection = aerisJobParams.RealJitWeatherConnection;
        }

        public List<string> GetDistinctZipCodes()
        {
            List<string> data = new List<string>();


            using (IDbConnection jitWebData3Db = new SqlConnection(_jitWebData3ConnectionString))
            {
                data = jitWebData3Db.Query<string>("SELECT DISTINCT b.Zip FROM Buildings AS b " +
                    "JOIN Accounts AS a ON b.BldID = a.BldID " +
                    "JOIN WthNormalParams AS w ON a.AccID = w.AccID").AsList();
            }
            return data;
        }

        public bool InsertWeatherData(WeatherData weatherData)
        {
            string sql = @"
            INSERT INTO [WeatherData] ([StationId], [ZipCode], [RDate], [HighTmp], [LowTmp], [AvgTmp], [DewPt]) 
            VALUES (@StationId, @ZipCode, @RDate, @HighTmp, @LowTmp, @AvgTmp, @DewPT);
            SELECT CAST(SCOPE_IDENTITY() as int)";

            using (IDbConnection db = new SqlConnection(_myConnectionString))
            {
                int rowsAffected = db.Execute(sql, new
                {
                    StationID = weatherData.StationId,
                    ZipCode = weatherData.ZipCode,
                    RDate = weatherData.RDate.ToShortDateString(),
                    HighTmp = weatherData.HighTmp,
                    LowTmp = weatherData.LowTmp,
                    AvgTmp = weatherData.AvgTmp,
                    DewPT = weatherData.DewPt
                });

                return (rowsAffected == 1);
            }
        }

        public bool GetWeatherDataExistForZipAndDate(string zipCode, DateTime rDate)
        {
            using (IDbConnection db = new SqlConnection(_myConnectionString))
            {
                DateTime date = Convert.ToDateTime(rDate.ToShortDateString());
                bool exists = db.ExecuteScalar<bool>("SELECT COUNT(1) FROM WeatherData WHERE ZipCode=@ZipCode AND RDate=@RDate",
                    new { ZipCode = zipCode, RDate = date });
                return exists;
            }
        }

        public IDictionary<string, IEnumerable<string>> GetAllWeatherData()
        {
            IEnumerable<WeatherData> data;

            IDictionary<string, IEnumerable<string>> map = new Dictionary<string, IEnumerable<string>>();

            using (IDbConnection db = new SqlConnection(_myConnectionString))
            {
                data = db.Query<WeatherData>("SELECT ZipCode, RDate FROM WeatherData");

                foreach (WeatherData weatherData in data)
                {
                    if (map.ContainsKey(weatherData.ZipCode))
                    {
                        //IEnumerable<string> dates = map.GetValueOrDefault(weatherData.ZipCode);
                        IEnumerable<string> dates = map[weatherData.ZipCode];
                        dates.Append(weatherData.RDate.ToShortDateString());
                        map[weatherData.ZipCode] = dates;
                    }
                    else
                    {
                        IEnumerable<string> dates = new List<string>();
                        dates.Append(weatherData.RDate.ToShortDateString());
                        map.Add(weatherData.ZipCode, dates);
                    }
                }
                return map;
            }
        }

        public List<WeatherData> GetWeatherData(PageParams pageParams)
        { 
            var data = new List<WeatherData>();

            string Sql = @"SELECT ID, RTRIM(StationId) AS StationId, ZipCode, RDate, HighTmp, LowTmp, AvgTmp, DewPt FROM WeatherData  
                ORDER BY RDate DESC, StationId ASC 
                OFFSET ((@PageNumber - 1) * @RowsPerPage) ROWS 
                FETCH NEXT @RowsPerPage ROWS ONLY"; 

            using (IDbConnection db = new SqlConnection(_myConnectionString)) 
            { 
               data = db.Query<WeatherData>(Sql, new { pageParams.PageNumber, pageParams.RowsPerPage}).AsList(); 
               return data;
            } 
        } 

        public List<WeatherData> GetWeatherDataByZipCode(string zipCode, PageParams pageParams)
        { 
            var data = new List<WeatherData>();

            string Sql = @"SELECT ID, (RTRIM(StationId)) as StationId, ZipCode, RDate, HighTmp, LowTmp, AvgTmp, DewPt FROM WeatherData  
                             WHERE ZipCode = @ZipCode  
                             ORDER BY RDate DESC, StationId ASC  
                             OFFSET ((@PageNumber - 1) * @RowsPerPage) ROWS  
                             FETCH NEXT @RowsPerPage ROWS ONLY";

            using (IDbConnection db = new SqlConnection(_realJitWeatherConnection))
            {
                data = db.Query<WeatherData>(Sql, new { zipCode, pageParams.PageNumber, pageParams.RowsPerPage }).AsList();
                return data;
            } 
        }

        public int GetWeatherDataRowCount()
        {
            string sql = @"SELECT COUNT(ID) FROM [WeatherData] WHERE ZipCode IS NOT NULL";
            using (IDbConnection db = new SqlConnection(_realJitWeatherConnection))
            {
                return db.ExecuteScalar<int>(sql);
            }
        }

        public int GetWeatherDataRowCountByZip(string ZipCode)
        { 
            var sql = @"SELECT COUNT(*) FROM WeatherData WHERE ZipCode = @ZipCode"; 
         
            using (IDbConnection db = new SqlConnection(_realJitWeatherConnection)) 
            { 
                return db.ExecuteScalar<int>(sql, new { ZipCode }); 
            } 
        }

        private string GetMostRecentWeatherDataDate()
        {
            using (IDbConnection db = new SqlConnection(_realJitWeatherConnection))
            {
                var date = db.Query<DateTime>("SELECT TOP(1) RDate FROM WeatherData ORDER BY RDate DESC").First();
                return date.AddDays(1).ToShortDateString();
            }
        } 

        public List<ReadingsQueryResult> GetReadings(string DateStart)
        {
            string DateEnd = GetMostRecentWeatherDataDate();
            var data = new List<ReadingsQueryResult>();

            //string Sql = @"SELECT r.RdngID, b.Zip, r.DateStart,  r.DateEnd, r.Days, r.Units, r.AccID, r.UnitID as rUnitID, 
            //                      wnp.UnitID as wnpUnitID, wnp.B1, wnp.B2, wnp.B3, wnp.B4, wnp.B5
            //            FROM Readings r 
            //            JOIN WthNormalParams wnp ON wnp.AccID = r.AccID
            //                                        AND wnp.UtilID = r.UtilID
            //                                        AND wnp.UnitID = r.UnitID
            //            JOIN Accounts a ON a.AccID = r.AccID
            //            JOIN Buildings b ON b.BldID = a.BldID
            //            WHERE NOT EXISTS 
            //                (SELECT weu.RdngID FROM WthExpUsage weu
            //                 WHERE weu.RdngID = r.RdngID)
            //            AND r.DateStart >= @DateStart
            //            AND r.DateEnd <= @DateEnd
            //            ORDER BY DateStart asc";

            string Sql = @"select r.RdngID, b.Zip, r.DateStart,  r.DateEnd, r.Days, r.AccID, r.UtilID, r.UnitID as rUnitID, 
                                  wnp.UnitID as wnpUnitID, wnp.B1, wnp.B2, wnp.B3, wnp.B4, wnp.B5
                        from Readings r 
                           join WthNormalParams wnp on wnp.AccID = r.AccID
                                                    and wnp.UtilID = r.UtilID
                                                    and wnp.UnitID = r.UnitID
                        join Accounts a on a.AccID = r.AccID
                        join Buildings b on b.BldID = a.BldID
                        where  r.DateStart >= @DateStart
                           and r.DateEnd <= @DateEnd
                        order by DateStart asc";

            using (IDbConnection db = new SqlConnection(_jitWebData3ConnectionString))
            {
                return db.Query<ReadingsQueryResult>(Sql, new { DateStart, DateEnd }).AsList();
            }
        }

        public int GetExpectedWthExpUsageRowCount(string DateStart)
        {
            string DateEnd = GetMostRecentWeatherDataDate();

            string sql = @"select count(r.RdngID) 
                           from Readings r 
                           join WthNormalParams wnp on wnp.AccID = r.AccID
                                                    and wnp.UtilID = r.UtilID
                                                    and wnp.UnitID = r.UnitID
                        join Accounts a on a.AccID = r.AccID
                        join Buildings b on b.BldID = a.BldID
                        where  r.DateStart >= @DateStart
                           and r.DateEnd <= @DateEnd";

            using (IDbConnection db = new SqlConnection(_jitWebData3ConnectionString))
            {
                return db.ExecuteScalar<int>(sql, new { DateStart, DateEnd });
            }
        }

        public int GetActualWthExpUsageRowCount()
        {
            string sql = @"SELECT COUNT(RdngID) FROM [WthExpUsage]";
            using (IDbConnection db = new SqlConnection(_myConnectionString))
            //using (IDbConnection db = new SqlConnection(_jitWebData3ConnectionString))

            {
                return db.ExecuteScalar<int>(sql);
            }
        }

        public List<WeatherData> GetWeatherDataByZipStartAndEndDate(string ZipCode, DateTime DateStart, DateTime DateEnd)
        {
            var data = new List<WeatherData>();

            string Sql = @"SELECT ID, (RTRIM(StationId)) as StationId, (RTRIM(ZipCode)) as ZipCode, RDate, HighTmp, LowTmp, AvgTmp, DewPt FROM WeatherData  
                             WHERE ZipCode = @ZipCode AND RDATE >= @DateStart AND RDATE < @DateEnd ORDER BY ID";

            using (IDbConnection db = new SqlConnection(_realJitWeatherConnection))
            {
                return db.Query<WeatherData>(Sql, new { ZipCode, DateStart = DateStart.ToShortDateString(), DateEnd = DateEnd.ToShortDateString() }).AsList();
            }
        }

        public bool InsertWthExpUsage(int ReadingId, decimal ExpUsage, int AccID, int UtilID, int UnitID)
        {
            string sql = @"
            INSERT INTO [WthExpUsage] ([RdngID], [ExpUsage], AccID, UtilID, UnitID) 
            VALUES (@ReadingID, @ExpUsage, @AccID, @UtilID, @UnitID);
            SELECT CAST(SCOPE_IDENTITY() as int)";

            /*
             * this is using a mocked WthExpUsage table in local DB. 
             *
            CREATE TABLE [dbo].[WthExpUsage](
	        [RdngID] [int] NOT NULL,
	        [ExpUsage] [decimal](18, 4) NOT NULL
            ) ON [PRIMARY]
            *
            */

            //using (IDbConnection db = new SqlConnection(_jitWebData3ConnectionString))
            using (IDbConnection db = new SqlConnection(_myConnectionString))
            {
                int rowsAffected = db.Execute(sql, new
                {
                    ReadingId,
                    ExpUsage,
                    AccID,
                    UtilID,
                    UnitID
                });

                return (rowsAffected == 1);
            }
        }

        public List<WthNormalParams> GetNormalParamsKeysForRegression()
        {

            string sql = @"select AccID, UtilID, UnitID, B1 as B1_Original, B2 as B2_Original, B3 as B3_Original, B4 as B4_Original, B5 as B5_Original,
                                  R2 as R2_Original, EndDate as EndDate_Original from WthNormalParams";

            using (IDbConnection db = new SqlConnection(_jitWebData3ConnectionString))
            {
                return db.Query<WthNormalParams>(sql).AsList();
            }
        }

        private string GetEndDateAfterFullYear(string YearDateStart, WthNormalParams normalParamsKey)
        {
            var dateTimeStart = new DateTime();
            var dateTimeEnd = new DateTime();

            string sqlForFirstReading = @"SELECT TOP(1) DateStart from Readings WHERE DateStart >= @DateStart 
                                            and AccID = @AccID and UtilID = @UtilID and UnitID = @UnitID order by DateStart asc";

            using (IDbConnection db = new SqlConnection(_jitWebData3ConnectionString))
            {
                dateTimeStart = db.Query<DateTime>(sqlForFirstReading, new { DateStart = YearDateStart, normalParamsKey.AccID, normalParamsKey.UtilID,
                                                    normalParamsKey.UnitID }).First();
            }

            if (dateTimeStart > new DateTime(2017, 2, 1))
            {
                throw new Exception("Readings did not start after 2016-12-1 within two months");
            }

            string sqlForLastReading = @"Select top(1) DateEnd from Readings WHERE DateEnd >= @DateEnd 
                                            and AccID = @AccID and UtilID = @UtilID and UnitID = @UnitID order by DateEnd asc";

            using (IDbConnection dbTwo = new SqlConnection(_jitWebData3ConnectionString))
            {
                dateTimeEnd = dbTwo.Query<DateTime>(sqlForLastReading, new
                {
                    DateEnd = dateTimeStart.AddYears(1).AddDays(-1),
                    normalParamsKey.AccID,
                    normalParamsKey.UtilID,
                    normalParamsKey.UnitID
                }).First();
            }

            //string sqlForLastReading = @"Select top(1) DateEnd from Readings 
            //                                where AccID = @AccID and UtilID = @UtilID and UnitID = @UnitID order by DateEnd desc";
            //using (IDbConnection dbTwo = new SqlConnection(_jitWebData3ConnectionString))
            //{
            //    dateTimeEnd = dbTwo.Query<DateTime>(sqlForLastReading, new
            //    {
            //        normalParamsKey.AccID,
            //        normalParamsKey.UtilID,
            //        normalParamsKey.UnitID
            //    }).First();
            //}

            return dateTimeEnd.ToString();
        }

        public List<ReadingsQueryResult> GetReadingsForRegressionYear(string DesiredStartDate, WthNormalParams normalParamsKey)
        {
            //string DateEnd = GetEndDateAfterFullYear(DesiredStartDate, normalParamsKey);

            //string Sql = @"select r.RdngID, b.Zip, r.DateStart,  r.DateEnd, r.Days, r.Units, r.AccID, r.UnitID as rUnitID, 
            //                      wnp.UnitID as wnpUnitID, wnp.B1, wnp.B2, wnp.B3, wnp.B4, wnp.B5, wnp.EndDate as EndDateOriginal
            //            from Readings r 
            //            join WthNormalParams wnp on wnp.AccID = r.AccID
            //                                        and wnp.UtilID = r.UtilID
            //                                        and wnp.UnitID = r.UnitID
            //            join Accounts a on a.AccID = r.AccID
            //                               and a.AccID = @AccID
            //                               and r.UtilID = @UtilID
            //                               and r.UnitID = @UnitID
            //            join Buildings b on b.BldID = a.BldID
            //            where  r.DateStart >= @DateStart
            //               and r.DateEnd <= @DateEnd
            //            order by DateStart asc";

            //using (IDbConnection db = new SqlConnection(_jitWebData3ConnectionString))
            //{
            //    return db.Query<ReadingsQueryResult>(Sql, 
            //        new { normalParamsKey.AccID, normalParamsKey.UtilID, normalParamsKey.UnitID, DateStart = DesiredStartDate, DateEnd }).AsList();
            //}

            string Sql = @"select r.RdngID, b.Zip, r.DateStart,  r.DateEnd, r.Days, r.Units, r.AccID, r.UtilID, r.UnitID as rUnitID, 
                                  wnp.UnitID as wnpUnitID, wnp.B1, wnp.B2, wnp.B3, wnp.B4, wnp.B5, wnp.EndDate as EndDateOriginal
                        from Readings r 
                        join WthNormalParams wnp on wnp.AccID = r.AccID
                                                    and wnp.UtilID = r.UtilID
                                                    and wnp.UnitID = r.UnitID
                        join Accounts a on a.AccID = r.AccID
                                           and a.AccID = @AccID
                                           and r.UtilID = @UtilID
                                           and r.UnitID = @UnitID
                        join Buildings b on b.BldID = a.BldID
                        where  r.Yr = @Year
                        order by DateStart asc";

            using (IDbConnection db = new SqlConnection(_jitWebData3ConnectionString))
            {
                return db.Query<ReadingsQueryResult>(Sql,
                    new { normalParamsKey.AccID, normalParamsKey.UtilID, normalParamsKey.UnitID, Year = DesiredStartDate }).AsList();
            }
        }

        public bool InsertWthNormalParams(WthNormalParams normalParams, bool Accord)
        {
            string sql = "";

            if (!Accord)
            {
                sql = @"
                INSERT INTO [WthNormalParams] ([AccID], [UtilID], [UnitID], [WthZipCode], 
                [B1_New], [B1_Original],
                [B2_New], [B2_Original],
                [B3_New], [B3_Original],
                [B4_New], [B4_Original],
                [B5_New], [B5_Original],
                [R2_New], [R2_Original], 
                [YearOfReadsDateStart], [YearOfReadsDateEnd], [EndDate_Original], [Readings], [Days]) 
                VALUES (@AccID, @UtilID, @UnitID, @WthZipCode, 
                @B1_New, @B1_Original,
                @B2_New,  @B2_Original,
                @B3_New, @B3_Original,
                @B4_New,  @B4_Original,
                @B5_New,  @B5_Original,
                @R2_New, @R2_Original, 
                @YearOfReadsDateStart, @YearOfReadsDateEnd, @EndDate_Original, @Readings, @Days)";
            }
            else if (Accord)
            { 
                sql = @"
                INSERT INTO [WthNormalParamsAccord] ([AccID], [UtilID], [UnitID], [WthZipCode], 
                [B1_New], [B1_Original],
                [B2_New], [B2_Original],
                [B3_New], [B3_Original],
                [B4_New], [B4_Original],
                [B5_New], [B5_Original],
                [R2_New], [R2_Original], 
                [YearOfReadsDateStart], [YearOfReadsDateEnd], [EndDate_Original], [Readings], [Days]) 
                VALUES (@AccID, @UtilID, @UnitID, @WthZipCode, 
                @B1_New, @B1_Original,
                @B2_New,  @B2_Original,
                @B3_New, @B3_Original,
                @B4_New,  @B4_Original,
                @B5_New,  @B5_Original,
                @R2_New, @R2_Original,
                @YearOfReadsDateStart, @YearOfReadsDateEnd, @EndDate_Original, @Readings, @Days)";
            }
            //string sql = @"
            //INSERT INTO [WthNormalParams] ([AccID], [UtilID], [UnitID], [WthZipCode], 
            //[B1]
            //[B2], [B3], [B4], [B5], [R2], [R2Original], [ReadDateStart], [ReadDateEnd], 
            //[EndDateOriginal], [Readings], [Days]) 
            //VALUES (@AccID, @UtilID, @UnitID, @WthZipCode, @B1_Original, @B1_New, @B2, @B3, @B4, @B5, @R2, @R2Original, @ReadDateStart, @ReadDateEnd, @EndDateOriginal, @Readings, @Days)";

            using (IDbConnection db = new SqlConnection(_myConnectionString))
            {
                int rowsAffected = db.Execute(sql, new
                {
                    normalParams.AccID,
                    normalParams.UtilID,
                    normalParams.UnitID,
                    normalParams.WthZipCode,
                    normalParams.B1_New,
                    normalParams.B1_Original,
                    normalParams.B2_New,
                    normalParams.B2_Original,
                    normalParams.B3_New,
                    normalParams.B3_Original,
                    normalParams.B4_New,
                    normalParams.B4_Original,
                    normalParams.B5_New,
                    normalParams.B5_Original,
                    normalParams.R2_New,
                    normalParams.R2_Original,
                    normalParams.YearOfReadsDateStart,
                    normalParams.YearOfReadsDateEnd,
                    normalParams.EndDate_Original,
                    normalParams.Readings,
                    normalParams.Days
                });

                return (rowsAffected == 1);
            }
        }

        public List<ReadingsQueryResult> GetReadingsFromExpUsageOriginal()
        {
            string Sql = @"select weu.RdngID, weu.ExpUsage, r.Units, r.UnitID as RUnitID, r.AccID, r.UtilID, r.DateStart, r.DateEnd, r.Days 
                            from WthExpUsage weu join Readings r on weu.RdngID = r.RdngID;";

            using (IDbConnection db = new SqlConnection(_jitWebData3ConnectionString))
            {
                return db.Query<ReadingsQueryResult>(Sql).AsList();
            }
        }

        public List<ReadingsQueryResult> GetReadingsFromExpUsageOriginalCorrected()
        {
            List<WthNormalParams> myParams = new List<WthNormalParams>();
            List<ReadingsQueryResult> allReadings = new List<ReadingsQueryResult>();

            string Sql = @"select wnp.AccID, wnp.UtilID, wnp.UnitID, weu.ExpUsage, weu.RdngID from WthNormalParams wnp join WthExpUsage weu on wnp.AccID = weu.AccID and 
                            wnp.UtilID = weu.UtilID and wnp.UnitID = weu.UnitID
where wnp.R2_New > 0.8";
//where wnp.R2_New > 0.8;";

            using (IDbConnection db = new SqlConnection(_myConnectionString))
            {
                myParams = db.Query<WthNormalParams>(Sql).AsList();
            }

            var myParamsGroups = myParams.GroupBy(p => new { p.AccID, p.UtilID, p.UnitID });

            foreach (var group in myParamsGroups)
            {
                List<WthNormalParams> wthNormalParams = group.ToList();

                foreach (WthNormalParams wnp in wthNormalParams)
                {
                    ReadingsQueryResult reading = new ReadingsQueryResult();

                    string Sql2 = @"select RdngID, AccID, UtilID, UnitID as RUnitID, ExpUsage, Units, DateStart, DateEnd, Days from Readings 
                                        where RdngID = @RdngID;";

                    using (IDbConnection db = new SqlConnection(_myConnectionString))
                    {
                        reading = db.Query<ReadingsQueryResult>(Sql2, new { wnp.RdngID }).First();
                    }

                    allReadings.Add(reading);

                    //string Sql2 = @"select AccID, UtilID, UnitID as RUnitID, RdngID, Units, DateStart, DateEnd, Days from Readings 
                    //                    where RdngID = @RdngID;";
                    //try
                    //{
                    //    using (IDbConnection db = new SqlConnection(_jitWebData3ConnectionString))
                    //    {
                    //        reading = db.Query<ReadingsQueryResult>(Sql2, new { wnp.RdngID }).First();
                    //    }

                    //    reading.ExpUsage = wnp.ExpUsage;
                    //    allReadings.Add(reading);
                    //    try
                    //    {
                    //        InsertMyReadings(reading);
                    //    }
                    //    catch (Exception e) { Console.WriteLine(e.Message + " " + e.StackTrace); }

                    //} catch (Exception e)
                    //    {
                    //        Console.WriteLine(e.Message + " " + wnp.RdngID);
                    //    }
                }
            }

            return allReadings;
        }

        public List<ReadingsQueryResult> GetReadingsFromExpUsageOriginalCorrected(AccordResult accord)
        {
            List<WthNormalParams> myParams = new List<WthNormalParams>();
            List<ReadingsQueryResult> allReadings = new List<ReadingsQueryResult>();

            string Sql = @"select wnp.AccID, wnp.UtilID, wnp.UnitID, weu.ExpUsage, weu.RdngID from WthNormalParams wnp join WthExpUsage weu on wnp.AccID = weu.AccID and 
                            wnp.UtilID = weu.UtilID and wnp.UnitID = weu.UnitID where wnp.R2_New > 0.8 and wnp.AccID = @AccID and wnp.UtilID = @UtilID and wnp.UnitID = @UnitID";
            //where wnp.R2_New > 0.8;";

            using (IDbConnection db = new SqlConnection(_myConnectionString))
            {
                myParams = db.Query<WthNormalParams>(Sql, new { accord.AccID, accord.UtilID, accord.UnitID }).AsList();
            }

            var myParamsGroups = myParams.GroupBy(p => new { p.AccID, p.UtilID, p.UnitID });

            foreach (var group in myParamsGroups)
            {
                List<WthNormalParams> wthNormalParams = group.ToList();

                foreach (WthNormalParams wnp in wthNormalParams)
                {
                    ReadingsQueryResult reading = new ReadingsQueryResult();

                    string Sql2 = @"select RdngID, AccID, UtilID, UnitID as RUnitID, ExpUsage, Units, DateStart, DateEnd, Days from Readings 
                                        where RdngID = @RdngID;";

                    using (IDbConnection db = new SqlConnection(_myConnectionString))
                    {
                        reading = db.Query<ReadingsQueryResult>(Sql2, new { wnp.RdngID }).First();
                    }

                    allReadings.Add(reading);

                    //string Sql2 = @"select AccID, UtilID, UnitID as RUnitID, RdngID, Units, DateStart, DateEnd, Days from Readings 
                    //                    where RdngID = @RdngID;";
                    //try
                    //{
                    //    using (IDbConnection db = new SqlConnection(_jitWebData3ConnectionString))
                    //    {
                    //        reading = db.Query<ReadingsQueryResult>(Sql2, new { wnp.RdngID }).First();
                    //    }

                    //    reading.ExpUsage = wnp.ExpUsage;
                    //    allReadings.Add(reading);
                    //    try
                    //    {
                    //        InsertMyReadings(reading);
                    //    }
                    //    catch (Exception e) { Console.WriteLine(e.Message + " " + e.StackTrace); }

                    //} catch (Exception e)
                    //    {
                    //        Console.WriteLine(e.Message + " " + wnp.RdngID);
                    //    }
                }
            }

            return allReadings;
        }

        private void InsertMyReadings(ReadingsQueryResult reading)
        {
            string Sql = @"Insert into Readings (RdngID, ExpUsage, AccID, UtilID, UnitID, Units, DateStart, DateEnd, Days) Values (@RdngID, @ExpUsage, @AccID, @UtilID, 
                            @UnitID, @Units, @DateStart, @DateEnd, @Days)";

            using (IDbConnection db = new SqlConnection(_myConnectionString))
            {
                int rowsAffected = db.Execute(Sql, new
                {
                    reading.RdngID,
                    reading.ExpUsage,
                    reading.AccID,
                    reading.UtilID,
                    UnitID = reading.RUnitID,
                    reading.Units,
                    reading.DateStart,
                    reading.DateEnd,
                    reading.Days
                });
            }
        }

        public WthNormalParams GetParamsForReading(int AccID, int UtilID, int UnitID)
        {
            string Sql = @"select WthZipCode, B1_New, B2_New, B3_New, B4_New, B5_New from WthNormalParams 
                            where AccID = @AccID and UtilID = @UtilID and UnitID = @UnitID";

            using (IDbConnection db = new SqlConnection(_myConnectionString))
            {
                return db.Query<WthNormalParams>(Sql, new { AccID, UtilID, UnitID }).First();
            }
        }

        public bool InsertMyWthExpUsage(WthExpUsage wthExpUsage)
        {
            string sql = @"
            INSERT INTO [MyWthExpUsage] ([RdngID], [Units], [ExpUsage_New], [PercentDelta_New], [ExpUsage_Old], [PercentDelta_Old], [DateStart], [DateEnd],
            [AccID], [UtilID], [UnitID]) 
            VALUES (@RdngID, @Units, @ExpUsage_New, @PercentDelta_New, @ExpUsage_Old, @PercentDelta_Old, @DateStart, @DateEnd, @AccID, @UtilID, @UnitID);";
            //SELECT CAST(SCOPE_IDENTITY() as int)";

            //using (IDbConnection db = new SqlConnection(_jitWebData3ConnectionString))
            using (IDbConnection db = new SqlConnection(_myConnectionString))
            {
                int rowsAffected = db.Execute(sql, new
                {
                    wthExpUsage.RdngID,
                    wthExpUsage.Units,
                    wthExpUsage.ExpUsage_New,
                    wthExpUsage.PercentDelta_New,
                    wthExpUsage.ExpUsage_Old,
                    wthExpUsage.PercentDelta_Old,
                    wthExpUsage.DateStart,
                    wthExpUsage.DateEnd,
                    wthExpUsage.AccID,
                    wthExpUsage.UtilID,
                    wthExpUsage.UnitID
                });

                return (rowsAffected == 1);
            }
        }
        public bool InsertMyWthExpUsage(WthExpUsage wthExpUsage, bool accord)
        {
            if (!accord)
            {
                return false;
            }

            string sql = @"update MyWthExpUsage SET ExpUsage_Accord = @ExpUsage_New, PercentDelta_Accord = @PercentDelta_New 
                            where RdngID = @RdngID";

            //using (IDbConnection db = new SqlConnection(_jitWebData3ConnectionString))
            using (IDbConnection db = new SqlConnection(_myConnectionString))
            {
                int rowsAffected = db.Execute(sql, new
                {
                    wthExpUsage.ExpUsage_New,
                    wthExpUsage.PercentDelta_New,
                    wthExpUsage.RdngID
                });

                return (rowsAffected == 1);
            }
        }
    }

} 
