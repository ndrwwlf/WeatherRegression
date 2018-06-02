using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WeatherService.Db;

namespace WeatherService.Services
{
    public interface IWeatherDataService
    {
        List<Location> ReadAll();
    }
}
