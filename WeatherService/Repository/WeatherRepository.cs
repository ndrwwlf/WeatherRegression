using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using WeatherService.Dto;
using WeatherService.Model;
using WeatherService.Services;

namespace WeatherService.Db
{
    public class WeatherRepository : IWeatherRepository
    {
        private readonly string _connectionString;
        private readonly string _jitWebData3ConnectionString;
        //private int i;

        public WeatherRepository(string connectionString, string jitWebData3ConnectionString)
        {
            _connectionString = connectionString;
            _jitWebData3ConnectionString = jitWebData3ConnectionString;
        }

        public List<string> GetDistinctZipCodes()
        {
            List<string> data = new List<string>();

            //using (IDbConnection db = new SqlConnection(_connectionString))
            //{
            //    data = db.Query<string>("select DISTINCT(RTRIM(ZipCode)) as ZipCode from Location WHERE ZipCode IS NOT NULL").ToList();
            //}

            using (IDbConnection jitWebData3Db = new SqlConnection(_jitWebData3ConnectionString))
            {
                data = jitWebData3Db.Query<string>("select distinct Zip from Buildings as b " +
                    "join Accounts as a on b.BldID = a.BldID " +
                    "join WthNormalParams as w on a.AccID = w.AccID; ").AsList();
            }
            return data;
        }

        public bool InsertWeatherData(WeatherData weatherData)
        {
            string sql = @"
            INSERT INTO [WeatherData] ([StationId], [ZipCode], [RDate], [HighTmp], [LowTmp], [AvgTmp], [DewPt]) 
            VALUES (@StationId, @ZipCode, @RDate, @HighTmp, @LowTmp, @AvgTmp, @DewPT);
            SELECT CAST(SCOPE_IDENTITY() as int)";

            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                int rowsAffected = db.Execute(sql, new
                {
                    StationID = weatherData.StationId,
                    ZipCode = weatherData.ZipCode,
                    RDate = weatherData.RDate.ToShortDateString(),
                    HighTmp = weatherData.HighTmp,
                    LowTmp = weatherData.LowTmp,
                    AvgTmp = (int?)(weatherData.AvgTmp.HasValue ? Math.Round(weatherData.AvgTmp.Value) : weatherData.AvgTmp),
                    DewPT = (int?)(weatherData.DewPt.HasValue ? Math.Max(0, Math.Round(weatherData.DewPt.Value)) : weatherData.DewPt)
                });

                return (rowsAffected == 1);
            }
        }

        //public bool InsertWeatherData(WeatherData weatherData)
        //{
        //    string sql = @"
        //    INSERT INTO [WeatherData] ([WstID], [RDate], [HighTmp], [LowTmp], [AvgTmp], [DewPt]) 
        //    VALUES (@WstID, @RDate, @HighTmp, @LowTmp, @AvgTmp, @DewPT);
        //    SELECT CAST(SCOPE_IDENTITY() as int)";

        //    using (IDbConnection db = new SqlConnection(_connectionString))
        //    {
        //        int rowsAffected = db.Execute(sql, new
        //        {
        //            WstID = weatherData.ZipCode,
        //            RDate = weatherData.RDate.ToShortDateString(),
        //            HighTmp = (int)Math.Round(weatherData.HighTmp),
        //            LowTmp = (int)Math.Round(weatherData.LowTmp),
        //            AvgTmp = (int)Math.Round(weatherData.AvgTmp),
        //            DewPt = (int)Math.Round(weatherData.DewPt)
        //            //this fucker can't be negative... this this'll work
        //            //DewPt = Math.Max(0, (int)Math.Round(weatherData.DewPt))
        //        });

        //        return (rowsAffected == 1);
        //    }
        //}

        public bool GetWeatherDataExistForZipAndDate(string zipCode, DateTime rDate)
        {
            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                DateTime date = Convert.ToDateTime(rDate.ToShortDateString());
                Console.WriteLine("hotdog");
                bool exists = db.ExecuteScalar<bool>("select count(1) from WeatherData where ZipCode=@ZipCode AND RDate=@RDate",
                    new { ZipCode = zipCode, RDate = date });
                Console.WriteLine(exists);
                return exists;
            }

            //using (IDbConnection db = new SqlConnection(_connectionString))
            //{
            //    DateTime date = Convert.ToDateTime(rDate.ToShortDateString());
            //    Console.WriteLine("hotdog");
            //    bool exists = db.ExecuteScalar<bool>("select count(1) from WstDateZip where ZipCode=@ZipCode AND RDate=@RDate",
            //        new { ZipCode = zipCode, RDate = date });
            //    Console.WriteLine(exists);
            //    //db.Close();
            //    return exists;
            //}
        }

        public List<WeatherData> GetWeatherData(PageParams pageParams)
       { 
            var data = new List<WeatherData>();

            string Sql = @"SELECT ID, RTRIM(StationId) AS StationId, ZipCode, RDate, HighTmp, LowTmp, AvgTmp, DewPt FROM WeatherData  
                ORDER BY RDate DESC, StationId ASC 
                OFFSET ((@PageNumber - 1) * @RowsPerPage) ROWS 
                FETCH NEXT @RowsPerPage ROWS ONLY"; 

            using (IDbConnection db = new SqlConnection(_connectionString)) 
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

            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                data = db.Query<WeatherData>(Sql, new { zipCode, pageParams.PageNumber, pageParams.RowsPerPage }).AsList();
                return data;
            } 
        }

        public int GetWeatherDataRowCount(string ZipCode)
        {
            string sql = @"SELECT COUNT(*) FROM WeatherData WHERE ZipCode IS NOT NULL";
            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                return db.ExecuteScalar<int>(sql, new { ZipCode });
            }
        }

        public int GetWeatherDataRowCountByZip(string ZipCode)
        { 
            var sql = @"SELECT COUNT(*) FROM WeatherData WHERE ZipCode = @ZipCode"; 
         
            using (IDbConnection db = new SqlConnection(_connectionString)) 
            { 
                return db.ExecuteScalar<int>(sql, new { ZipCode }); 
            } 
        }  
    } 
}
