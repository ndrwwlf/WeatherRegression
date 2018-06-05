using System.Collections.Generic;
using WeatherService.Db;
using WeatherService.Dto;

namespace WeatherService.Services
{
    public interface IWeatherRepository
    {
        List<Location> GetLocations();
        Location GetLocation(int id);
        Location InsertLocation(PutPostLocation location);
        bool DeleteLocation(int id);
        bool UpdateLocation(Location location);
    }
}
