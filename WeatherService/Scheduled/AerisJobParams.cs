using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WeatherService.Scheduled
{
    public class AerisJobParams
    {
        public string AerisAccessId { get; set; }
        public string AerisSecretKey { get; set; }
        public string JitWeatherConnetionString { get; set; }
        public string JitWebData3ConnectionString { get; set; }
    }
}
