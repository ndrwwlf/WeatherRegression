using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using WeatherService.Db;
using WeatherService.Dto;
using WeatherService.Services;
using static WeatherService.Validation.ValidationFilter;

namespace WeatherService.Controllers
{

    [Route("api/Location")]
    [ValidateModel]
    public class LocationController : Controller
    {
        private readonly IWeatherRepository _weatherRepository;

        public LocationController(IWeatherRepository weatherRepository)
        {
            _weatherRepository = weatherRepository;
        }
        
        // GET api/locations
        [HttpGet]
        public IEnumerable<Location> Get()
        {
            return _weatherRepository.GetLocations();
        }

        // GET api/locations/5
        [HttpGet("{id}")]
        [ProducesResponseType(200, Type = typeof(Location))]
        [ProducesResponseType(404)]
        public IActionResult Get([FromRoute]int id)
        {
            Location location = _weatherRepository.GetLocation(id);
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
        [ProducesResponseType(201)]
        [ProducesResponseType(400)]
        public IActionResult Post([FromBody]PutPostLocation putPostLocation)
        {
            Location location = _weatherRepository.InsertLocation(putPostLocation);
            return Created("/" + location.Id, location);
        }

        // PUT api/locations/5
        [HttpPut("{id}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public IActionResult Put([FromRoute]int id, [FromBody]PutPostLocation putPostLocation)
        {
            var userZipCode = putPostLocation.ZipCode;

            if (_weatherRepository.GetLocationExist(id))
            {
                if (!_weatherRepository.GetZipCodeExist(userZipCode))
                {
                    Location location = new Location
                    {
                        Id = id,
                        ZipCode = userZipCode
                    };

                    Location result = _weatherRepository.UpdateLocation(location);

                    if (result == location)
                    {
                        return Ok(location);
                    }
                    else
                    {
                        return BadRequest(new { message = "Location for id " + id + "not updated successfully" });
                    }
                }
                else
                {
                    return BadRequest(new { message = "ZipCode " + userZipCode + " already exists" });
                }
            }
            else
            {
                return NotFound(new { message = "location not found for id " + id });

            }
        }

        // DELETE api/values/5
        [HttpDelete("{id}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public IActionResult Delete([FromRoute]int id)
        {
            if (_weatherRepository.GetLocationExist(id))
            {
                return Ok();
            }
            else
            {
                return NotFound(new { message = "Location not found for id " + id });
            }
        }

        private Location LocationValueOf(PutPostLocation putPostLocation)
        {
            Location location = new Location
            {
                ZipCode = putPostLocation.ZipCode
            };
            return location;
        }
    }
}
