using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using WeatherService.Db;
using WeatherService.Dto;
using WeatherService.Services;

namespace WeatherService.Controllers
{

    [Route("api/locations")]
    public class LocationController : Controller
    {
        private readonly IWeatherDataService _weatherDataService;

        public LocationController(IWeatherDataService weatherDataService)
        {
            _weatherDataService = weatherDataService;
        }
        
        // GET api/locations
        [HttpGet]
        public IEnumerable<Location> Get()
        {
            return _weatherDataService.GetLocations();
        }

        // GET api/locations/5
        [HttpGet("{id}")]
        [ProducesResponseType(200, Type = typeof(Location))]
        [ProducesResponseType(404)]
        public IActionResult Get(int id)
        {
            Location location = _weatherDataService.GetLocation(id);
            if (location != null)
            {
                return Ok(location);
            } else
            {
               return NotFound(new { message = "location not found for id " + id});
            }
            
        }

        // POST api/locations
        [HttpPost]
        public IActionResult Post([FromBody]PutPostLocation putPostLocation)
        {
            Location location =  _weatherDataService.InsertLocation(putPostLocation);
            return Created("/" + location.ID, location);
        }

        // PUT api/locations/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody]Location location)
        {
        }

        // DELETE api/values/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
