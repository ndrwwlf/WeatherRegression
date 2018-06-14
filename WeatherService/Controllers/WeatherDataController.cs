using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using WeatherService.Dto;
using WeatherService.Model;
using WeatherService.Services;
using static WeatherService.Validation.ValidationFilter;

namespace WeatherService.Controllers
{
    [ValidateModel]
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
        [ProducesResponseType(200, Type = typeof(List<WeatherData>))]
        public IActionResult Get([FromQuery]PageParams pageParams)
        {
            int total = _weatherRepository.GetWeatherDataRowCount("all");
            List<WeatherData> data = _weatherRepository.GetWeatherData(pageParams);

            return Ok(new
            {
                Data = data,
                Paging = new
                {
                    Total = total,
                    Limit = pageParams.RowsPerPage,
                    Offset = (pageParams.PageNumber - 1) * pageParams.RowsPerPage,
                    Returned = data.Count,
                    PageAt = pageParams.PageNumber
                }
            });
        }

        // GET: api/WeatherData/KMSO/WeatherData?PageNumber=1&RowsPerPage=20
        [HttpGet("{ZipCpde}")]
        [ProducesResponseType(200, Type = typeof(List<WeatherData>))]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public IActionResult Get([FromRoute]string ZipCpde, [FromQuery]PageParams pageParams)
        {
            int total = _weatherRepository.GetWeatherDataRowCount(ZipCpde);

            if (total < 1)
            {
                return NotFound(new { message = "Weather Station not found for ZipCode " + ZipCpde });
            }
            List<WeatherData> data = _weatherRepository.GetWeatherDataByZipCode(ZipCpde, pageParams);

            return Ok(new
            {
                Data = data,
                Paging = new
                {
                    Total = total,
                    Limit = pageParams.RowsPerPage,
                    Offset = (pageParams.PageNumber - 1) * pageParams.RowsPerPage,
                    Returned = data.Count,
                    PageAt = pageParams.PageNumber
                }
            });
        }
    }
}
