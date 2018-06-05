using System.Collections.Generic;
using WeatherService.Db;
using WeatherService.Dto;

namespace WeatherService.Services
{
    public interface IWeatherDataService
    {
        List<Location> GetLocations();
        Location GetLocation(int id);
        Location InsertLocation(PutPostLocation location);
        bool DeleteLocation(int id);
        bool UpdateLocation(Location location);
    }
}
