using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using WeatherService.Db;

namespace WeatherService.Dto
{
    [ModelMetadataType(typeof(Location))]
    public class PutPostLocation
    {
        public string ZipCode { get; set; }
    }
}
