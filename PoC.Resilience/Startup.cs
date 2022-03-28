using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi.Models;
using PoC.Resilience.Resilience;
using Polly;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Mime;

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
            //Policy
            //    .HandleResult<HttpResponseMessage>(x => x.StatusCode.ToString().StartsWith("5"))
            //    .CircuitBreaker(2, TimeSpan.FromMinutes(1));

            ChaosSettingsConfiguration chaosSettingsConfiguration = new()
            {
                Operations = new()
                {
                    new()
                    {
                        OperationKey = "HttpStatus503",
                        Enabled = true,
                        InjectionRate = 1,
                        LatencyMs = 0,
                        ResponseStatusCode = 503,
                        ResponseMessage = "Simmy - Erro HTTP 503"
                    },
                    new()
                    {
                        OperationKey = "HttpStatus500",
                        Enabled = true,
                        InjectionRate = 1,
                        LatencyMs = 0,
                        ResponseStatusCode = 500,
                        ResponseMessage = "Simmy - Erro HTTP 500"
                    }
                }
            };

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
                waitAndRetryNumberretries: 1,
                waitAndRetryTimeToNewRequest: 500,
                circuitBreakerEventsAllowedBeforeBreaking: 2,
                circuitBreakerDurationOfBreakMs: 30000,
                circuitBreakerHttpCodes: new List<int>() { 500, 503 },
                timeOutMs: 30000
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
