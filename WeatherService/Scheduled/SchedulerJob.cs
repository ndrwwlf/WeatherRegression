
using Quartz;
using Quartz.Impl;
using System;
using System.Threading.Tasks;

namespace WeatherService.Scheduled
{
    public class SchedulerJob
    {

        public static async Task RunAsync(AerisJobParams aerisJobParams)
        {
            try
            {
                IScheduler scheduler;
                var schedulerFactory = new StdSchedulerFactory();
                scheduler = schedulerFactory.GetScheduler().Result;
                scheduler.Context.Put("aerisJobParams", aerisJobParams);
                scheduler.Start().Wait();

                //int ScheduleIntervalInMinute = 1;//job will run every minute
                JobKey jobKey = JobKey.Create("AerisJob");

                IJobDetail job = JobBuilder.Create<AerisJob>().WithIdentity(jobKey).Build();

                ITrigger trigger = TriggerBuilder.Create()
                    .WithIdentity("JobTrigger")
                    .UsingJobData("city", "Hello World!")
                    .StartNow()
                    //.WithSimpleSchedule(x => x.WithIntervalInMinutes(ScheduleIntervalInMinute).RepeatForever())
                    .WithSimpleSchedule(x => x.WithIntervalInSeconds(5).WithRepeatCount(0))
                    .Build();

                await scheduler.ScheduleJob(job, trigger);
            }
            catch (ArgumentException e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
}
