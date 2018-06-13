using Microsoft.AspNetCore.Mvc;
using WeatherService.Db;

namespace WeatherService.Dto
{
    [ModelMetadataType(typeof(Location))]
    public class PutPostLocation
    {
        public string ZipCode { get; set; }
    }
}
