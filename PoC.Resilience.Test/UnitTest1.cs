using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework.Constraints;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using Polly.Timeout;
using System.Diagnostics;
using System.Net;
using System.Net.Http;

namespace PoC.Resilience.Test
{
    public class ResilienceTests
    {


        const string TestClient = "TestClient";
        private bool _isRetryCalled;
        int _retryCount = 0;
        double _timeoutSeconds;
        int _CircuitCount = 0;
        int co = 0;



        [Test]
        public async Task Retry_Successfully()
        {
            // Arrange
            IServiceCollection services = new ServiceCollection();
            _isRetryCalled = false;
            services.AddHttpClient(TestClient, configureClient =>
            {
                configureClient.BaseAddress = new Uri("https://localhost:44306");
            })
                .AddPolicyHandler(GetRetryPolicy())
                .AddHttpMessageHandler(() => new RetryDelegatingHandler());
            HttpClient configuredClient =
                 services
                     .BuildServiceProvider()
                     .GetRequiredService<IHttpClientFactory>()
                     .CreateClient(TestClient);
            // Act
            var result = await configuredClient.GetAsync("status/500");
            // Assert
            Assert.True(_isRetryCalled);
            Assert.That(_retryCount, Is.EqualTo(TestsConfig.Retries));
            Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }
        [Test]
        public async Task Timeout_Successfully()
        {
            Console.Write("Timeout test!!!");
            // Arrange
            IServiceCollection services = new ServiceCollection();
            services.AddHttpClient(TestClient, configureClient =>
            {
                configureClient.BaseAddress = new Uri("https://localhost:44306");
            })
                .AddPolicyHandler(GetTimeoutPolicy())
                .AddHttpMessageHandler(() => new TimeoutDelegatingHandler());
            HttpClient configuredClient =
                 services
                     .BuildServiceProvider()
                     .GetRequiredService<IHttpClientFactory>()
                     .CreateClient(TestClient);
            // Act
            string timeoutType = string.Empty;
            try
            {
                HttpResponseMessage result = await configuredClient.GetAsync("status/500");
            }
            catch (Exception ex)
            {
                timeoutType = ex.GetType().FullName;
            }
            // Assert
            Assert.That(timeoutType, Is.EqualTo("Polly.Timeout.TimeoutRejectedException"));
            Assert.That(_timeoutSeconds, Is.GreaterThanOrEqualTo(TestsConfig.TimeoutInSeconds));
        }




        [Test]
        public async Task Circuit_Breaker_Successfully()
        {
            //Arrange
            IServiceCollection services = new ServiceCollection();
            services.AddHttpClient(TestClient, configureClient =>
            {
                configureClient.BaseAddress = new Uri("https://localhost:44306");
            })
                .AddPolicyHandler(GetCircuitBreakPolicy())
                .AddHttpMessageHandler(() => new CircuitBreakDelegatingHandler());
            HttpClient configuredClient =
                 services
                     .BuildServiceProvider()
                     .GetRequiredService<IHttpClientFactory>()
                     .CreateClient(TestClient);
            
            //Act
            for (int i = 0; i < TestsConfig.Circuit; i++)
            {
                HttpResponseMessage result = await configuredClient.GetAsync("status/500");
            }
            int currentStatusOpen = TestsConfig.Flag;
            Console.WriteLine(currentStatusOpen);

            await Task.Delay(40000);
            await configuredClient.GetAsync("status/200");
            int currentStatusClosed = TestsConfig.Flag;
            Console.WriteLine(currentStatusClosed);

            //Assert
            Assert.That(CircuitBreakDelegatingHandler._count, Is.EqualTo(TestsConfig.Circuit));
            Assert.That(currentStatusOpen, Is.EqualTo(3));
            Assert.That(currentStatusClosed, Is.EqualTo(2));

        }


        public IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        {
            return HttpPolicyExtensions
               .HandleTransientHttpError()
               .WaitAndRetryAsync(TestsConfig.Retries,
                   sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(5),
                   onRetryAsync: OnRetryAsync);
        }
        public IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy()
        {
            return Policy
                .TimeoutAsync<HttpResponseMessage>(timeout: TimeSpan.FromSeconds(TestsConfig.TimeoutInSeconds),
                timeoutStrategy: TimeoutStrategy.Pessimistic,
                onTimeoutAsync: (Context context, TimeSpan timeSpan, Task task, Exception exception) =>
                {
                    _timeoutSeconds = timeSpan.TotalSeconds;
                    return Task.FromResult(new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.RequestTimeout
                    });
                });
        }

        public IAsyncPolicy<HttpResponseMessage> GetCircuitBreakPolicy()
        {

            
            return Policy
           .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
           .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30), OnBreak, OnReset, OnHalfOpen);
            
        }
        private void OnHalfOpen()
        {
            Console.WriteLine("Circuit in test mode, one request will be allowed.");
            TestsConfig.Flag = 1;
            co++;
        }

        private void OnReset()
        {
            Console.WriteLine("Circuit closed, requests flow normally.");
            TestsConfig.Flag = 2;
            co++;
        }

        private void OnBreak(DelegateResult<HttpResponseMessage> result, TimeSpan ts)
        {
            Console.WriteLine("Circuit cut, requests will not flow.");
            TestsConfig.Flag = 3;
            co++;
        }


        private async Task OnRetryAsync(DelegateResult<HttpResponseMessage> outcome, TimeSpan timespan, int retryCount, Context context)
        {
            //Log result
            _isRetryCalled = true;
            _retryCount++;
        }
    }
    public class RetryDelegatingHandler : DelegatingHandler
    {
        private int _count = 0;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
             CancellationToken cancellationToken)
        {
            if (_count < TestsConfig.Retries)
            {
                _count++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
    public class TimeoutDelegatingHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
             CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(TestsConfig.TimeoutInSeconds + 5), CancellationToken.None);
            return new HttpResponseMessage(HttpStatusCode.RequestTimeout);
        }
    }

    public class CircuitBreakDelegatingHandler : DelegatingHandler
    {
        public static int _count = 0;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
             CancellationToken cancellationToken)
        {
            if (_count < TestsConfig.Circuit)
            {
                _count++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    public static class TestsConfig
    {
        public static int Retries = 3;
        public static int TimeoutInSeconds = 10;
        public static int Circuit = 5;
        public static int Flag = 0;

    }
}