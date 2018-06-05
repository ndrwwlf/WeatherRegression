using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using WeatherService.Dto;
using WeatherService.Services;

namespace WeatherService.Db
{
    public class WeatherDataService : IWeatherDataService
    {
        private readonly string _connectionString;
     
        public WeatherDataService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public List<Location> GetLocations()
        {
            var data = new List<Location>();

            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                data = db.Query<Location>("select ID, TRIM(ZipCode) as ZipCode from Location").ToList();
            }

            return data;
        }

        public Location GetLocation(int id)
        {
            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                return db.Query<Location>("SELECT * FROM Location WHERE ID = @LocationID", 
                    new { LocationID = id }).SingleOrDefault();
            }
        }

        public Location InsertLocation(PutPostLocation putPostLocation)
        {
            string sql = @"
            INSERT INTO [Location] ([ZipCode]) VALUES (@ZipCode);
            SELECT CAST(SCOPE_IDENTITY() as int)";
     
            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                int id = db.Query<int>(sql, new { ZipCode = putPostLocation.ZipCode }).Single();
                Location location = new Location();
                location.ID = id;
                location.ZipCode = putPostLocation.ZipCode;
                return location;
            }
        }

        public bool DeleteLocation(int id)
        {
            throw new NotImplementedException();
        }

        public bool UpdateLocation(Location location)
        {
            throw new NotImplementedException();
        }
    }
}
