using Newtonsoft.Json;

namespace AgentFunctionApp.Models
{
    public class DeviceStatusChange
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("DocumentType")]
        public string DocumentType { get; set; }

        [JsonProperty("DeviceId")]
        public string DeviceId { get; set; }

        [JsonProperty("LineId")]
        public string LineId { get; set; }

        [JsonProperty("DeviceType")]
        public string DeviceType { get; set; }

        [JsonProperty("Timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonProperty("OldStatus")]
        public string OldStatus { get; set; }

        [JsonProperty("NewStatus")]
        public string NewStatus { get; set; }

        [JsonProperty("Reason")]
        public string Reason { get; set; }

        [JsonProperty("Temperature")]
        public double Temperature { get; set; }

        [JsonProperty("ErrorCode")]
        public int ErrorCode { get; set; }

        [JsonProperty("AvailabilityPercentage")]
        public double AvailabilityPercentage { get; set; }

        [JsonProperty("ttl")]
        public int Ttl { get; set; }
    }
}
