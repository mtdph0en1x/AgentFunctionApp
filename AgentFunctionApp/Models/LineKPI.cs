using Newtonsoft.Json;

namespace AgentFunctionApp.Models
{
    public class LineKPI
    {
        [JsonProperty("LineId")]
        public string LineId { get; set; }

        [JsonProperty("LineName")]
        public string LineName { get; set; }

        [JsonProperty("WindowEnd")]
        public string WindowEnd { get; set; }

        [JsonProperty("DocumentType")]
        public string DocumentType { get; set; }

        [JsonProperty("TotalGoodCount")]
        public int TotalGoodCount { get; set; }

        [JsonProperty("TotalBadCount")]
        public int TotalBadCount { get; set; }

        [JsonProperty("TotalProductionCount")]
        public int TotalProductionCount { get; set; }

        [JsonProperty("QualityPercentage")]
        public double QualityPercentage { get; set; }

        [JsonProperty("AvgAvailability")]
        public double AvgAvailability { get; set; }

        [JsonProperty("AvgProductionRate")]
        public double AvgProductionRate { get; set; }

        [JsonProperty("ErrorCount")]
        public int ErrorCount { get; set; }
    }
}
