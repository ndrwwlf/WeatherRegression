using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Configuration;
using WeatherService.Services;

namespace WeatherService.Db
{
    public class WeatherDataService : IWeatherDataService
    {

        private string _connectionString;

        public WeatherDataService(IConfiguration configuration)
        {
            _connectionString = configuration["ConnectionStrings:DefaultConnection"];
        }

        public List<Location> ReadAll()
        {
            var data = new List<Location>();

            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                data = db.Query<Location>("select * from Location").ToList();
            }

            return data;
        }

    }
}
