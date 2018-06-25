using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;
using WeatherService.Db;
using WeatherService.Scheduled;
using WeatherService.Services;
using WeatherService.Validation;

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

            //if (env.IsDevelopment() || env.IsProduction())
            //{
            //    builder.AddUserSecrets<Startup>();
            //}

            Configuration = builder.Build();

            AerisJobParams aerisJobParams = new AerisJobParams();
            aerisJobParams.AerisClientId = Configuration.GetSection("AerisJobParams:AerisClientID").Value;
            aerisJobParams.AerisClientSecret = Configuration.GetSection("AerisJobParams:AerisClientSecret").Value;
            aerisJobParams.JitWeatherConnectionString = Configuration.GetSection("AerisJobParams:JitWeatherConnectionString").Value;
            aerisJobParams.JitWebData3ConnectionString = Configuration.GetSection("AerisJobParams:JitWebData3ConnectionString").Value;

            SchedulerJob.RunAsync(aerisJobParams).GetAwaiter().GetResult();
        }

        public IConfiguration Configuration { get; set; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var aerisJobParams = new AerisJobParams();
            Configuration.Bind("AerisJobParams", aerisJobParams);
            services.AddSingleton(aerisJobParams);
            services.AddSingleton<IWeatherRepository>(c => new WeatherRepository(aerisJobParams));
            //services.AddSingleton<AerisJob>(c => new AerisJob(aerisJobParams, Configuration.Get<IWeatherRepository>()));

            services.AddMvc(o =>
            {
                o.ModelMetadataDetailsProviders.Add(new RequiredBindingMetadataProvider());
            });

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Info { Title = "Weather Service API", Version = "v1" });
            });

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
