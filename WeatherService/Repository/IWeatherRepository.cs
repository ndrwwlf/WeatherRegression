using System.Collections.Generic;
using WeatherService.Db;
using WeatherService.Dto;
using WeatherService.Model;

namespace WeatherService.Services
{
    public interface IWeatherRepository
    {
        List<Location> GetLocations();
        List<string> GetDistinctLocationSationIds();
        Location GetLocation(int id);
        Location InsertLocation(PutPostLocation location);
        bool DeleteLocation(int id);
        Location UpdateLocation(Location location);
        bool GetZipCodeExist(string ZipCode);
        bool GetLocationExist(int LocationID);

        bool InsertWeatherData(WeatherData weatherData);
    }
}
