using PoC.Resilience;
using System.Collections.Generic;

namespace PoC.Resilience
{
    public class MyHttpClientConfig
    {
        public int? RetryNumbers { get; set; }
        public int? WaitAndRetryTimeToNewRequest { get; set; }
        public int? EventsAllowedBeforeBreaking { get; set; }
        public int? DurationOfBreakMs { get; set; }
        public List<int> HttpCodes { get; set; }
        public int? TimeOutMs { get; set; }
        public string ChaosOperationKey { get; set; }
    }    
}