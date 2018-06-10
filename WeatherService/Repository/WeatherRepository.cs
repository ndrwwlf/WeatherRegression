using Dapper;
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
     
        public WeatherRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public List<Location> GetLocations()
        {
            var data = new List<Location>();

            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                data = db.Query<Location>("select ID, RTRIM(ZipCode) as ZipCode, RTRIM(StationId) as StationId from Location").ToList();
            }

            return data;
        }

        public Location GetLocation(int id)
        {
            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                return db.Query<Location>("SELECT * FROM Location WHERE ID = @ID", 
                          new { ID = id }).SingleOrDefault();
            }
        }

        public Location InsertLocation(PutPostLocation putPostLocation)
        {
            string sql = @"
            INSERT INTO [Location] ([ZipCode]) VALUES (@ZipCode);
            SELECT CAST(SCOPE_IDENTITY() as int)";

            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                int id = db.Query<int>(sql, new { putPostLocation.ZipCode }).Single();
                Location location = new Location
                {
                    Id = id,
                    ZipCode = putPostLocation.ZipCode
                };
                return location;
            }
        }

        public bool DeleteLocation(int id)
        {
            string sql = "DELETE FROM [Location] WHERE ID = @ID";

            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                var affectedRows = db.Execute(sql, new { id });

                return affectedRows > 0;
            }
        }

        public Location UpdateLocation(Location location)
        {
            string sql = @"UPDATE [Location] SET [ZipCode] = @ZipCode WHERE ID = @ID";

            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                db.Execute(sql, new { location.ZipCode, location.Id });
            }

            return location;
        }

        public bool GetZipCodeExist(string zipCode)
        {
            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                var exists = db.ExecuteScalar<bool>("select count(1) from Location where ZipCode=@ZipCode", new { zipCode });
                return exists;
            }
        }

        public bool GetLocationExist(int id)
        {
            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                var exists = db.ExecuteScalar<bool>("select count(1) from Location where ID=@ID", new { id });
                return exists;
            }
        }

        public bool InsertWeatherData(WeatherData weatherData)
        {
            throw new System.NotImplementedException();
        }

        public List<string> GetDistinctLocationSationIds()
        {
            var data = new List<string>();

            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                data = db.Query<string>("select DISTINCT(RTRIM(StationId)) as StationId from Location WHERE StationId IS NOT NULL").ToList();
            }

            return data;
        }
    }
}
