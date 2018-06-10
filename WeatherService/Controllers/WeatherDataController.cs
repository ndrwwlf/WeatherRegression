using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WeatherService.Services;

namespace WeatherService.Controllers
{
    [Produces("application/json")]
    [Route("api/WeatherData")]
    public class WeatherDataController : Controller
    {
        private readonly IWeatherRepository _weatherRepository;

        public WeatherDataController(IWeatherRepository weatherRepository)
        {
            _weatherRepository = weatherRepository;
        }

        // GET: api/WeatherData
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET: api/WeatherData/KMSO
        [HttpGet("{stationId}", Name = "Get")]
        public IEnumerable<string> Get(string stationId)
        {
            return new string[] { "value1", "value2" };
        }
        
        
    }
}
