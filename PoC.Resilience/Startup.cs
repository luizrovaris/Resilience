using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi.Models;
using PoC.Resilience.Resilience;
using Polly;
using Polly.Contrib.Simmy;
using Polly.Contrib.Simmy.Outcomes;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Threading;

namespace PoC.Resilience
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRazorPages();

            bool faultInjectionEnabled = Convert.ToBoolean( Configuration["FaultInjectionEnabled"]);

            ChaosSettingsConfiguration chaosSettingsConfiguration = Configuration
                .GetSection(nameof(ChaosSettingsConfiguration))
                .Get<ChaosSettingsConfiguration>();

            MyHttpClientConfig httpConfig = Configuration
                .GetSection(nameof(MyHttpClientConfig))
                .Get<MyHttpClientConfig>();

            services.AddHttpClient("MyPoCHttpClient", clientConfig =>
            {
                clientConfig.BaseAddress = new Uri("https://localhost:44306");//https://httpbin.org/
                clientConfig.DefaultRequestHeaders.Add(HeaderNames.Accept, MediaTypeNames.Application.Json);
            })
                     .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                     {
                         AllowAutoRedirect = false,
                         UseDefaultCredentials = true,
                         AutomaticDecompression = DecompressionMethods.None,
                     })
            .AddPolicyHandler(ResiliencePolicyHandler.GetPolicy(
                policy: ResiliencePolicyType.CircuitBreaker,
                waitAndRetryNumberretries: httpConfig.RetryNumbers,
                waitAndRetryTimeToNewRequest: httpConfig.WaitAndRetryTimeToNewRequest,
                circuitBreakerEventsAllowedBeforeBreaking: httpConfig.EventsAllowedBeforeBreaking,
                circuitBreakerDurationOfBreakMs: httpConfig.DurationOfBreakMs,
                circuitBreakerHttpCodes: httpConfig.HttpCodes,
                timeOutMs: httpConfig.TimeOutMs,
                 faultInjectionEnabled: faultInjectionEnabled,
                chaosOperationKey : httpConfig.ChaosOperationKey,
                chaosSettingsConfiguration : chaosSettingsConfiguration
            ));



            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "PoC.Resilience", Version = "v1" });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "PoC.Resilience v1"));
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
