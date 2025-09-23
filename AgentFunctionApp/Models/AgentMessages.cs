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

    // Data Node Types Enum
    public enum DataNodeType
    {
        // Core nodes (all devices)
        ProductionStatus,
        DeviceType,
        WorkorderId,
        ProductionRate,
        Temperature,
        DeviceError,

        // Device-specific nodes
        Pressure,              // Press Device
        Speed,                 // Conveyor Device
        GoodCount,             // Quality Station Device
        BadCount,              // Quality Station Device
        PassRate,              // Quality Station Device
        OutputPressure,        // Compressor Device
        SystemAirPressure      // Compressor Device
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

    // Device-level messages
    public class DeviceAlertMessage : AgentMessage
    {
        public string DeviceId { get; set; }
        public string LineId { get; set; }
        public DeviceType DeviceType { get; set; }
        public string WorkorderId { get; set; }
        public double Temperature { get; set; }
        public int ErrorCount { get; set; }
        public int ProductionRate { get; set; }
        public string AlertType { get; set; } // "Temperature", "Error", "Production"
        public string Status { get; set; }

        // Device-specific properties based on DeviceType
        public double? Pressure { get; set; }              // Press Device
        public double? Speed { get; set; }                 // Conveyor Device
        public int? GoodCount { get; set; }                // Quality Station Device
        public int? BadCount { get; set; }                 // Quality Station Device
        public double? PassRate { get; set; }              // Quality Station Device
        public double? OutputPressure { get; set; }        // Compressor Device
        public double? SystemAirPressure { get; set; }     // Compressor Device

        public DeviceAlertMessage()
        {
            MessageType = "DeviceAlert";
        }
    }

    // Line coordination messages
    public class LineCoordinationMessage : AgentMessage
    {
        public string LineId { get; set; }
        public string Action { get; set; } // "Optimize", "Balance", "EmergencyStop"
        public List<string> AffectedDevices { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public string Reason { get; set; }

        public LineCoordinationMessage()
        {
            MessageType = "LineCoordination";
            AffectedDevices = new List<string>();
            Parameters = new Dictionary<string, object>();
        }
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

    // Plant-level optimization messages
    public class PlantOptimizationMessage : AgentMessage
    {
        public string PlantId { get; set; }
        public string OptimizationType { get; set; } // "LoadBalance", "EnergyOptimize", "Maintenance"
        public List<LineOptimization> LineOptimizations { get; set; }
        public DateTime TargetCompletionTime { get; set; }

        public PlantOptimizationMessage()
        {
            MessageType = "PlantOptimization";
            LineOptimizations = new List<LineOptimization>();
        }
    }

    public class LineOptimization
    {
        public string LineId { get; set; }
        public int TargetProductionRate { get; set; }
        public List<string> WorkOrderIds { get; set; }
        public Dictionary<string, int> DeviceRates { get; set; }
    }

    // Agent status messages
    public class AgentStatusMessage : AgentMessage
    {
        public string AgentId { get; set; }
        public string AgentType { get; set; } // "Device", "Line", "Plant"
        public string Status { get; set; } // "Online", "Busy", "Error"
        public Dictionary<string, object> StatusData { get; set; }
        public DateTime LastHeartbeat { get; set; }

        public AgentStatusMessage()
        {
            MessageType = "AgentStatus";
            StatusData = new Dictionary<string, object>();
            LastHeartbeat = DateTime.UtcNow;
        }
    }


    // Stream Analytics message types for critical error alerts (immediate device errors)
    public class CriticalErrorAlert
    {
        public string DeviceId { get; set; }
        public string LineId { get; set; }
        public long DeviceError { get; set; }  // Changed to match ASA output
        public int HasEmergencyStop { get; set; }
        public int HasPowerFailure { get; set; }
        public int HasSensorFailure { get; set; }
        public int HasUnknownError { get; set; }
        public int ErrorPriority { get; set; }
        public string MessageType { get; set; }
        public DateTime EventTime { get; set; } = DateTime.UtcNow;
    }

    // Stream Analytics message types for line error alerts (windowed aggregation)
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
}