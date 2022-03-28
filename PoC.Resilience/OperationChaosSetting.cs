using System.Collections.Generic;

namespace PoC.Resilience
{
    public class OperationChaosSetting
    {
        public string OperationKey { get; set; }
        public bool Enabled { get; set; }
        public double InjectionRate { get; set; }
        public int LatencyMs { get; set; }
        public int ResponseStatusCode { get; set; }
        public string ResponseMessage { get; set; }
    }
    public class ChaosSettingsConfiguration
    {
        public List<OperationChaosSetting> Operations { get; set; }
    }
}
