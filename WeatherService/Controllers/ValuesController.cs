using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using WeatherService.Db;
using WeatherService.Services;

namespace WeatherService.Controllers
{
    [Route("api/[controller]")]
    public class ValuesController : Controller
    {
        private WeatherDataService _dataService;

        public ValuesController(WeatherDataService dataService)
        {
            _dataService = dataService;
        }
        
       
        // GET api/values
        [HttpGet]
        public IEnumerable<Location> Get()
        {
            return _dataService.ReadAll();
            //return locations.Select(i => i.ZipCode.ToString().Trim()).ToArray();
        }

        // GET api/values/5
        [HttpGet("{id}")]
        public string Get(int id)
        {
            return "value";
        }

        // POST api/values
        [HttpPost]
        public void Post([FromBody]string value)
        {
        }

        // PUT api/values/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE api/values/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
