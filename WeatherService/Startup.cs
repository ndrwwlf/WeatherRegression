using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Swagger;
using WeatherService.Db;
using WeatherService.Scheduled;
using WeatherService.Services;

namespace WeatherService
{
    public class Startup
    {

        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();

            Configuration = builder.Build();

            ILoggerFactory loggerFactory = new LoggerFactory()
            .AddConsole()
            .AddDebug();

            AerisJobParams aerisJobParams = new AerisJobParams();
            aerisJobParams.AerisAccessId = "pvgTjAD4onCI2NmIqcC4T";
            aerisJobParams.AerisSecretKey = "lNuane8qGpRMYVxBQC1mZyYdj3cKtQVGswqz5cNe";
            SchedulerJob.RunAsync(aerisJobParams).GetAwaiter().GetResult();
        }

        public IConfiguration Configuration { get; set; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Info { Title = "Weather Service API", Version = "v1" });
            });

            string connectionString = Configuration.GetSection("ConnectionStrings:DefaultConnection").Value;
            services.AddSingleton<IWeatherRepository>(c => new WeatherRepository(connectionString));

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {

            app.UseMvc();

            // Enable middleware to serve generated Swagger as a JSON endpoint.
            app.UseSwagger();

            // Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.), 
            // specifying the Swagger JSON endpoint.
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Weather Service API V1");
            });
            
 
        }
    }
}
