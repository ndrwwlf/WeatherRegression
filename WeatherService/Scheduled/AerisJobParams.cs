using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WeatherService.Scheduled
{
    public class AerisJobParams
    {
        public string AerisClientId { get; set; }
        public string AerisClientSecret { get; set; }
        public string MyConnectionString { get; set; }
        public string JitWebData3ConnectionString { get; set; }
        public string RealJitWeatherConnection { get; set; }
    }
}
