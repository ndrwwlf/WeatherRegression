
using Quartz;
using Quartz.Impl;
using Serilog;
using System;
using System.Threading.Tasks;

namespace WeatherService.Scheduled
{
    public class SchedulerJob 
    {

        public static async Task RunAsync(AerisJobParams aerisJobParams)
        //public static async Task RunAsync()
        {
            try
            {
                IScheduler scheduler;
                var schedulerFactory = new StdSchedulerFactory();
                scheduler = schedulerFactory.GetScheduler().Result;
                scheduler.Context.Put("aerisJobParams", aerisJobParams);
                scheduler.Start().Wait();

                //int ScheduleIntervalInMinute = 1;//job will run every minute
                JobKey aerisKey = JobKey.Create("AerisJob");
                //JobKey regressionKey = JobKey.Create("RegressionJob");
                JobKey sPRegressionKey = JobKey.Create("SPRegressionJob");

                IJobDetail aerisJob = JobBuilder.Create<AerisJob>().WithIdentity(aerisKey).Build();
                //IJobDetail regressionJob = JobBuilder.Create<RegressionJob>().WithIdentity(regressionKey).Build();
                IJobDetail sPRegressionJob = JobBuilder.Create<SPRegressionJob>().WithIdentity(sPRegressionKey).Build();

                ITrigger aerisTrigger = TriggerBuilder.Create()
                    .WithIdentity("AerisTrigger")
                    .StartNow()
                    .WithSimpleSchedule(x => x.WithIntervalInSeconds(5).WithRepeatCount(0))
                    .Build();

                DateTimeOffset aerisJobFinished = await scheduler.ScheduleJob(aerisJob, aerisTrigger);

                //ITrigger regressionTrigger = TriggerBuilder.Create()
                //    .WithIdentity("RegressionTrigger")
                //    .StartAt(aerisJobFinished)
                //    .WithSimpleSchedule(x => x.WithIntervalInSeconds(5).WithRepeatCount(0))
                //    .Build();

                ITrigger sPRegressionTrigger = TriggerBuilder.Create()
                    .WithIdentity("SPRegressionTrigger")
                    .StartAt(aerisJobFinished)
                    .WithSimpleSchedule(x => x.WithIntervalInSeconds(5).WithRepeatCount(0))
                    .Build();

                //await scheduler.ScheduleJob(regressionJob, regressionTrigger);
                await scheduler.ScheduleJob(sPRegressionJob, sPRegressionTrigger);
            }
            catch (ArgumentException e)
            {
                Console.WriteLine(e.Message);
                Log.Error(e.StackTrace);
            }
        }
    }
}
