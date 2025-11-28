using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace AgentFunctionApp.Models
{
    // Device Types Enum
    public enum DeviceType
    {
        Press = 0,
        Conveyor = 1,
        QualityStation = 2,
        Compressor = 3
    }

    // Base message for all agent communication
    public abstract class AgentMessage
    {
        public string MessageId { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string SenderId { get; set; }
        public string MessageType { get; set; }
        public int Priority { get; set; } = 1; // 1=Low, 5=Critical
    }

    // Device command messages
    public class DeviceCommandMessage : AgentMessage
    {
        public string DeviceId { get; set; }
        public DeviceType DeviceType { get; set; }
        public string Command { get; set; } // "EmergencyStop", "ResetErrorStatus", "AdjustProductionRate"
        public Dictionary<string, object> Parameters { get; set; }
        public bool RequiresAck { get; set; } = true;

        public DeviceCommandMessage()
        {
            MessageType = "DeviceCommand";
            Parameters = new Dictionary<string, object>();
        }
    }

    // Stream Analytics message types for critical error alerts 
    public class CriticalErrorAlert
    {
        public string DeviceId { get; set; }
        public string LineId { get; set; }
        public long DeviceError { get; set; }  
        public int HasEmergencyStop { get; set; }
        public int HasPowerFailure { get; set; }
        public int HasSensorFailure { get; set; }
        public int HasUnknownError { get; set; }
        public int ErrorPriority { get; set; }
        public string MessageType { get; set; }
        public DateTime EventTime { get; set; } = DateTime.UtcNow;
    }

    // Stream Analytics message types for line error alerts 
    public class LineErrorAlert
    {
        public string LineId { get; set; }
        public string LineName { get; set; }
        public DateTime AlertTime { get; set; }
        public int ErrorCount { get; set; }
        public long MaxErrorCode { get; set; }
        public double AvgTemperature { get; set; }
        public int Priority { get; set; }
        public string MessageType { get; set; }
    }

    // Error event stored in CosmosDB for PWA 
    public class ErrorEvent
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("DeviceId")]
        public string DeviceId { get; set; }

        [JsonProperty("LineId")]
        public string LineId { get; set; }

        [JsonProperty("ErrorCode")]
        public long ErrorCode { get; set; }

        [JsonProperty("ErrorType")]
        public string ErrorType { get; set; }

        [JsonProperty("Severity")]
        public int Severity { get; set; }

        [JsonProperty("Timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [JsonProperty("AlertId")]
        public string AlertId { get; set; }

        [JsonProperty("ActionTaken")]
        public string ActionTaken { get; set; }

        [JsonProperty("DocumentType")]
        public string DocumentType { get; set; } = "error-event";

        [JsonProperty("ErrorCount")]
        public int? ErrorCount { get; set; }
    }
}