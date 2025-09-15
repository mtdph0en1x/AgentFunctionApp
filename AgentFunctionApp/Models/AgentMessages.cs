using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace AgentFunctionApp.Models
{
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
        public double Temperature { get; set; }
        public int ErrorCount { get; set; }
        public int ProductionRate { get; set; }
        public string AlertType { get; set; } // "Temperature", "Error", "Production"
        public string Status { get; set; }

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
        public string Command { get; set; } // "AdjustRate", "EmergencyStop", "Reset"
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
}