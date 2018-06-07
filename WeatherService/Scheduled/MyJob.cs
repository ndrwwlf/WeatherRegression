using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WeatherService.Scheduled
{
    public class MyJob : IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            Console.WriteLine("xxxxxxxx");
            return Task.FromResult(0);
        }
    }
}
