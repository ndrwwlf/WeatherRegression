using System;
using System.IO;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.WindowsServices;
using Serilog;
using Serilog.Events;

namespace WeatherService
{
    public class Program
    {
        public static void Main(string[] args)
        {

            //BuildWebHost(args).Run();

            string baseDir = AppContext.BaseDirectory;
            string userDir = "C:\\Users\\User";

            Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("System", LogEventLevel.Information)
            .Enrich.FromLogContext()
            //to outsite of project
            .WriteTo.File(userDir + "/Logs/ErrorLogJIT.txt", restrictedToMinimumLevel: LogEventLevel.Error, rollOnFileSizeLimit: true)
            .WriteTo.RollingFile(userDir + "/Logs/log-{Date}.txt", retainedFileCountLimit: null)
            .WriteTo.Console()
            .CreateLogger();

            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            var directoryPath = Path.GetDirectoryName(exePath);

            try
            {
                //var host = new WebHostBuilder()
                //    .UseKestrel()
                //    .UseContentRoot(directoryPath)
                //    .UseStartup<Startup>()
                //    .UseSerilog()
                //    .Build();

                //host.RunAsService();

                Log.Information("Starting web host");
                BuildWebHost(args).Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
            .UseContentRoot(Directory.GetCurrentDirectory())
                .UseStartup<Startup>()
                .UseSerilog()
                .Build();
    }
}
