using Newtonsoft.Json;

namespace AgentFunctionApp.Models
{
    public class DeviceTelemetry
    {
        [JsonProperty("DeviceId")]
        public string DeviceId { get; set; }
        
        [JsonProperty("DeviceType")]
        public string DeviceType { get; set; }
        
        [JsonProperty("LineId")]
        public string LineId { get; set; }
        
        [JsonProperty("LineName")]
        public string LineName { get; set; }
        
        [JsonProperty("AvgTemperature")]
        public double AvgTemperature { get; set; }
        
        [JsonProperty("AvgProductionRate")]
        public double AvgProductionRate { get; set; }
        
        [JsonProperty("AvailabilityPercentage")]
        public double AvailabilityPercentage { get; set; }
        
        [JsonProperty("CurrentErrorCode")]
        public int CurrentErrorCode { get; set; }
        
        [JsonProperty("WindowEnd")]
        public string WindowEnd { get; set; }
        
        [JsonProperty("DocumentType")]
        public string DocumentType { get; set; }

        // Device-specific properties
        [JsonProperty("AvgSystemAirPressure")]
        public double? AvgSystemAirPressure { get; set; }
        
        [JsonProperty("AvgOutputAirPressure")]
        public double? AvgOutputAirPressure { get; set; }
        
        [JsonProperty("Efficiency")]
        public double? Efficiency { get; set; }
        
        [JsonProperty("AvgPressure")]
        public double? AvgPressure { get; set; }
        
        [JsonProperty("MinPressure")]
        public double? MinPressure { get; set; }
        
        [JsonProperty("MaxPressure")]
        public double? MaxPressure { get; set; }
        
        [JsonProperty("PressureStatus")]
        public string PressureStatus { get; set; }
        
        [JsonProperty("GoodCount")]
        public int? GoodCount { get; set; }
        
        [JsonProperty("BadCount")]
        public int? BadCount { get; set; }
        
        [JsonProperty("QualityPercentage")]
        public double? QualityPercentage { get; set; }
        
        [JsonProperty("InspectionStatus")]
        public string InspectionStatus { get; set; }
        
        [JsonProperty("ConveyorStatus")]
        public string ConveyorStatus { get; set; }

        [JsonProperty("ErrorEvents")]
        public int? ErrorEvents { get; set; }
    }
}
