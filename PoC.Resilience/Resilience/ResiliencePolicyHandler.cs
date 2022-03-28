using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Contrib.Simmy;
using Polly.Contrib.Simmy.Outcomes;
using Polly.Extensions.Http;
using Polly.Timeout;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace PoC.Resilience.Resilience
{
    public class ResiliencePolicyHandler
    {
        private static ILogger _logger;
        public static IAsyncPolicy<HttpResponseMessage> GetPolicy(
           ResiliencePolicyType policy,
           int? waitAndRetryNumberretries,
           int? waitAndRetryTimeToNewRequest,
           int? circuitBreakerEventsAllowedBeforeBreaking,
           int? circuitBreakerDurationOfBreakMs,
           List<int> circuitBreakerHttpCodes,
           int? timeOutMs,
           string chaosOperationKey = "",
           ChaosSettingsConfiguration chaosSettingsConfiguration = null)
        {
            List<IAsyncPolicy<HttpResponseMessage>> policies = new();

            switch (policy)
            {
                case ResiliencePolicyType.TimeOut:
                    {
                        policies.Add(GetFallBackPolicyAsync(policy));
                        policies.Add(GetTimeOutPolicyAsync(timeOutMs.Value));
                        break;
                    }
                case ResiliencePolicyType.WaitAndRetry:
                    {
                        policies.Add(GetFallBackPolicyAsync(policy));
                        policies.Add(GetWaitAndRetryPolicyAsync(waitAndRetryNumberretries.Value, waitAndRetryTimeToNewRequest.Value));
                        policies.Add(GetTimeOutPolicyAsync(timeOutMs.Value));
                        break;
                    }
                case ResiliencePolicyType.CircuitBreaker:
                    {
                        policies.Add(GetFallBackPolicyAsync(policy));
                        policies.Add(GetCircuitBreakerPolicyAsync(circuitBreakerEventsAllowedBeforeBreaking.Value, circuitBreakerDurationOfBreakMs.Value, circuitBreakerHttpCodes));
                        policies.Add(GetWaitAndRetryPolicyAsync(waitAndRetryNumberretries.Value, waitAndRetryTimeToNewRequest.Value));
                        policies.Add(GetTimeOutPolicyAsync(timeOutMs.Value));
                        break;
                    }
            }

            if (!string.IsNullOrWhiteSpace(chaosOperationKey) && chaosSettingsConfiguration?.Operations != null)
            {
                OperationChaosSetting chaosPolicy = chaosSettingsConfiguration.Operations.FirstOrDefault(p => p.OperationKey == chaosOperationKey);
                if (chaosPolicy != null)
                {
                    policies.Add(GetChaosPolicyAsync(chaosPolicy));
                }
            }
            return Policy.WrapAsync(policies.ToArray());
        }

        private static IAsyncPolicy<HttpResponseMessage> GetFallBackPolicyAsync(ResiliencePolicyType policy)
        {
            HttpResponseMessage httpResponseMessage = null;

            switch (policy)
            {
                case ResiliencePolicyType.TimeOut:
                    {
                        httpResponseMessage = new HttpResponseMessage(HttpStatusCode.RequestTimeout);
                        break;
                    }
                case ResiliencePolicyType.WaitAndRetry:
                    {
                        httpResponseMessage = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                        break;
                    }
                case ResiliencePolicyType.CircuitBreaker:
                    {
                        httpResponseMessage = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                        break;
                    }
            }

            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .Or<BrokenCircuitException>()
                .Or<TimeoutRejectedException>()
                .FallbackAsync(httpResponseMessage);
        }

        private static IAsyncPolicy<HttpResponseMessage> GetWaitAndRetryPolicyAsync(int numberRetrys, int timeWaitingForNewRequestInMs)
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .Or<TimeoutRejectedException>()
                .Or<TaskCanceledException>()
                .WaitAndRetryAsync(numberRetrys, 
                    retryAttempt => TimeSpan.FromMilliseconds(timeWaitingForNewRequestInMs),
                    onRetry: (message, retryCount) =>
                    {
                        LogError($"Retry Number {retryCount} - RetryPolicy fired after  {message}");
                    });
        }

        private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicyAsync(int maxNumberAllowedBeforeBreaking, int durationOfBreakMilliseconds, List<int> httpCodes)
        {
            return Policy
                .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode && httpCodes.Contains((int)r.StatusCode))
                .Or<TaskCanceledException>()
                .Or<TimeoutRejectedException>()
                .CircuitBreakerAsync(handledEventsAllowedBeforeBreaking: maxNumberAllowedBeforeBreaking,
                durationOfBreak: TimeSpan.FromMilliseconds(durationOfBreakMilliseconds),
                onBreak: (DelegateResult<HttpResponseMessage> result, TimeSpan timeSpan) => 
                    LogError($"CircuitBreaker - Closed - Requests will be denied on the break delay. {result?.Exception?.Message}"),
                onReset: () => LogError("CircuitBreaker - Reset - Requests flow normally."));
        }

        private static AsyncTimeoutPolicy<HttpResponseMessage> GetTimeOutPolicyAsync(int timeOutMs)
        {
            return Policy.TimeoutAsync<HttpResponseMessage>(timeout: TimeSpan.FromMilliseconds(timeOutMs),
                timeoutStrategy: TimeoutStrategy.Optimistic,
                onTimeoutAsync: (Context context, TimeSpan timeSpan, Task task) =>
                {
                    return task?.ContinueWith(t =>
                    {
                        if (t != null)
                        {
                            LogError($"The execution timed out after {timeSpan.TotalSeconds} seconds, " + (t.IsFaulted ? $"eventually terminated with: {t.Exception}." : "task cancelled."));
                        }
                    });
                });
        }

        private static IAsyncPolicy<HttpResponseMessage> GetChaosPolicyAsync(OperationChaosSetting operationChaosSetting)
        {
            var faultMessage = new HttpResponseMessage()
            {
                StatusCode = (HttpStatusCode)operationChaosSetting.ResponseStatusCode,
                Content = new StringContent(operationChaosSetting.ResponseMessage)
            };

            return MonkeyPolicy
                .InjectResultAsync<HttpResponseMessage>(with =>
                    with.Result(faultMessage)
                        .InjectionRate(operationChaosSetting.InjectionRate)
                        .Enabled(operationChaosSetting.Enabled));
        }

        private static void LogError(string message)
        {
            if (_logger == null) 
            {
                _logger = new LoggerFactory().CreateLogger<ResiliencePolicyHandler>();
            }

            _logger.LogError(message);
        }
    }
}