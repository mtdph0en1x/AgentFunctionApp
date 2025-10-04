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

        [JsonProperty("LineQualityPercentage")]
        public double LineQualityPercentage { get; set; }

        [JsonProperty("LineAvailability")]
        public double LineAvailability { get; set; }

        [JsonProperty("LinePerformanceRate")]
        public double LinePerformanceRate { get; set; }
    }
}
