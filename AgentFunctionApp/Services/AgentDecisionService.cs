using AgentFunctionApp.Models;
using Microsoft.Extensions.Logging;

namespace AgentFunctionApp.Services
{
    public class AgentDecisionService
    {
        private readonly ILogger<AgentDecisionService> _logger;

        public AgentDecisionService(ILogger<AgentDecisionService> logger)
        {
            _logger = logger;
        }

        
        public DecisionResult AnalyzeDeviceAlert(DeviceAlertMessage alert)
        {
            var decision = new DecisionResult
            {
                DeviceId = alert.DeviceId,
                LineId = alert.LineId,
                AlertType = alert.AlertType,
                Priority = alert.Priority
            };

            // Temperature-based decision logic
            if (alert.AlertType == "Temperature")
            {
                decision = AnalyzeTemperatureAlert(alert, decision);
            }
            // Error-based decision logic
            else if (alert.AlertType == "Error")
            {
                decision = AnalyzeErrorAlert(alert, decision);
            }
            // Production-based decision logic
            else if (alert.AlertType == "Production")
            {
                decision = AnalyzeProductionAlert(alert, decision);
            }

            _logger.LogInformation($"Decision made for {alert.DeviceId}: {decision.RecommendedAction}");
            return decision;
        }

        /// <summary>
        /// Determines optimal production rates for a line based on current conditions
        /// </summary>
        public LineOptimizationResult OptimizeProductionLine(string lineId, List<DeviceStatus> deviceStatuses)
        {
            var result = new LineOptimizationResult
            {
                LineId = lineId,
                OptimizationType = "Balanced",
                DeviceAdjustments = new Dictionary<string, int>()
            };

            // Find bottleneck device
            var bottleneck = FindBottleneckDevice(deviceStatuses);
            if (bottleneck != null)
            {
                result.BottleneckDevice = bottleneck.DeviceId;
                result.OptimizationType = "BottleneckOptimized";

                // Calculate optimal rates around bottleneck
                foreach (var device in deviceStatuses)
                {
                    int optimalRate = CalculateOptimalRate(device, bottleneck, deviceStatuses);
                    result.DeviceAdjustments[device.DeviceId] = optimalRate;
                }
            }
            else
            {
                // No bottleneck, optimize for maximum throughput
                foreach (var device in deviceStatuses)
                {
                    int optimalRate = CalculateMaxThroughputRate(device);
                    result.DeviceAdjustments[device.DeviceId] = optimalRate;
                }
            }

            result.ExpectedThroughput = CalculateExpectedThroughput(result.DeviceAdjustments.Values);

            _logger.LogInformation($"Line optimization complete for {lineId}: {result.OptimizationType}");
            return result;
        }

        /// <summary>
        /// Determines if a plant-wide optimization is needed
        /// </summary>
        public PlantOptimizationResult AnalyzePlantOptimization(List<LineStatus> lineStatuses)
        {
            var result = new PlantOptimizationResult
            {
                PlantId = "MainPlant",
                OptimizationNeeded = false,
                RecommendedActions = new List<string>()
            };

            // Check for load balancing opportunities
            var lineUtilizations = lineStatuses.Select(l => l.Utilization).ToList();
            var maxUtilization = lineUtilizations.Max();
            var minUtilization = lineUtilizations.Min();

            if (maxUtilization - minUtilization > 30) // 30% difference threshold
            {
                result.OptimizationNeeded = true;
                result.RecommendedActions.Add("LoadBalance");
                result.OptimizationType = "LoadBalance";

                // Identify which lines need adjustment
                foreach (var line in lineStatuses)
                {
                    if (line.Utilization > maxUtilization - 10)
                    {
                        result.OverloadedLines.Add(line.LineId);
                    }
                    else if (line.Utilization < minUtilization + 10)
                    {
                        result.UnderutilizedLines.Add(line.LineId);
                    }
                }
            }

            // Check for energy optimization opportunities
            var totalEnergyConsumption = lineStatuses.Sum(l => l.EnergyConsumption);
            if (totalEnergyConsumption > 1000) // Threshold for energy optimization
            {
                result.OptimizationNeeded = true;
                result.RecommendedActions.Add("EnergyOptimize");
            }

            // Check for maintenance scheduling opportunities
            var linesNeedingMaintenance = lineStatuses.Where(l => l.LastMaintenanceHours > 720).ToList(); // 30 days
            if (linesNeedingMaintenance.Any())
            {
                result.OptimizationNeeded = true;
                result.RecommendedActions.Add("ScheduleMaintenance");
                result.MaintenanceRequired = linesNeedingMaintenance.Select(l => l.LineId).ToList();
            }

            _logger.LogInformation($"Plant analysis complete. Optimization needed: {result.OptimizationNeeded}");
            return result;
        }

        #region Private Helper Methods

        private DecisionResult AnalyzeTemperatureAlert(DeviceAlertMessage alert, DecisionResult decision)
        {
            if (alert.Temperature > 95)
            {
                decision.RecommendedAction = "EmergencyStop";
                decision.Urgency = "Critical";
                decision.Reason = $"Critical temperature: {alert.Temperature}°C";
                decision.AffectedDevices = new List<string> { alert.DeviceId };
            }
            else if (alert.Temperature > 90)
            {
                decision.RecommendedAction = "ReduceLoad";
                decision.Urgency = "High";
                decision.Reason = $"High temperature: {alert.Temperature}°C";
                decision.AffectedDevices = GetLineDevices(alert.LineId);
                decision.Parameters["TargetReduction"] = 30; // Reduce by 30%
            }
            else if (alert.Temperature > 85)
            {
                decision.RecommendedAction = "OptimizeLoad";
                decision.Urgency = "Medium";
                decision.Reason = $"Elevated temperature: {alert.Temperature}°C";
                decision.AffectedDevices = GetLineDevices(alert.LineId);
                decision.Parameters["TargetReduction"] = 15; // Reduce by 15%
            }

            return decision;
        }

        private DecisionResult AnalyzeErrorAlert(DeviceAlertMessage alert, DecisionResult decision)
        {
            if (alert.ErrorCount > 5)
            {
                decision.RecommendedAction = "StopAndReset";
                decision.Urgency = "High";
                decision.Reason = $"Critical error count: {alert.ErrorCount}";
                decision.AffectedDevices = new List<string> { alert.DeviceId };
            }
            else if (alert.ErrorCount > 3)
            {
                decision.RecommendedAction = "Compensate";
                decision.Urgency = "Medium";
                decision.Reason = $"High error count: {alert.ErrorCount}";
                decision.AffectedDevices = GetLineDevices(alert.LineId);
                decision.Parameters["CompensationRate"] = 20; // Increase others by 20%
            }
            else if (alert.ErrorCount > 0)
            {
                decision.RecommendedAction = "Reset";
                decision.Urgency = "Low";
                decision.Reason = $"Minor errors: {alert.ErrorCount}";
                decision.AffectedDevices = new List<string> { alert.DeviceId };
            }

            return decision;
        }

        private DecisionResult AnalyzeProductionAlert(DeviceAlertMessage alert, DecisionResult decision)
        {
            if (alert.ProductionRate < 20)
            {
                decision.RecommendedAction = "InvestigateAndBoost";
                decision.Urgency = "High";
                decision.Reason = $"Very low production: {alert.ProductionRate} units/hr";
                decision.AffectedDevices = GetLineDevices(alert.LineId);
                decision.Parameters["BoostTarget"] = 60;
            }
            else if (alert.ProductionRate < 40)
            {
                decision.RecommendedAction = "Balance";
                decision.Urgency = "Medium";
                decision.Reason = $"Low production: {alert.ProductionRate} units/hr";
                decision.AffectedDevices = GetLineDevices(alert.LineId);
                decision.Parameters["BalanceTarget"] = 55;
            }

            return decision;
        }

        private DeviceStatus FindBottleneckDevice(List<DeviceStatus> deviceStatuses)
        {
            // Find device with lowest effective rate (considering both production rate and quality)
            return deviceStatuses
                .Where(d => d.Status == "online")
                .OrderBy(d => d.ProductionRate * (d.QualityPercentage / 100.0))
                .FirstOrDefault();
        }

        private int CalculateOptimalRate(DeviceStatus device, DeviceStatus bottleneck, List<DeviceStatus> allDevices)
        {
            // If this is the bottleneck, try to optimize it
            if (device.DeviceId == bottleneck.DeviceId)
            {
                return Math.Min(device.MaxProductionRate, bottleneck.ProductionRate + 10);
            }

            // For upstream devices, match bottleneck rate
            var deviceIndex = allDevices.FindIndex(d => d.DeviceId == device.DeviceId);
            var bottleneckIndex = allDevices.FindIndex(d => d.DeviceId == bottleneck.DeviceId);

            if (deviceIndex < bottleneckIndex)
            {
                return Math.Min(device.MaxProductionRate, bottleneck.ProductionRate);
            }

            // For downstream devices, can run slightly faster
            return Math.Min(device.MaxProductionRate, bottleneck.ProductionRate + 5);
        }

        private int CalculateMaxThroughputRate(DeviceStatus device)
        {
            // Calculate safe maximum rate considering temperature and error history
            var baseRate = device.MaxProductionRate;
            var temperatureFactor = device.Temperature > 80 ? 0.9 : 1.0;
            var errorFactor = device.RecentErrorCount > 0 ? 0.95 : 1.0;

            return (int)(baseRate * temperatureFactor * errorFactor);
        }

        private double CalculateExpectedThroughput(IEnumerable<int> productionRates)
        {
            // Line throughput is limited by the slowest device
            return productionRates.Min();
        }

        private List<string> GetLineDevices(string lineId)
        {
            return lineId switch
            {
                "ProductionLine1" => new List<string> { "Device1", "Device2", "Device3" },
                "ProductionLine2" => new List<string> { "Device4", "Device5", "Device6" },
                _ => new List<string>()
            };
        }

        #endregion
    }

    #region Decision Result Classes

    public class DecisionResult
    {
        public string DeviceId { get; set; }
        public string LineId { get; set; }
        public string AlertType { get; set; }
        public int Priority { get; set; }
        public string RecommendedAction { get; set; }
        public string Urgency { get; set; } = "Low";
        public string Reason { get; set; }
        public List<string> AffectedDevices { get; set; } = new List<string>();
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        public DateTime DecisionTime { get; set; } = DateTime.UtcNow;
    }

    public class LineOptimizationResult
    {
        public string LineId { get; set; }
        public string OptimizationType { get; set; }
        public string BottleneckDevice { get; set; }
        public Dictionary<string, int> DeviceAdjustments { get; set; } = new Dictionary<string, int>();
        public double ExpectedThroughput { get; set; }
        public DateTime OptimizationTime { get; set; } = DateTime.UtcNow;
    }

    public class PlantOptimizationResult
    {
        public string PlantId { get; set; }
        public bool OptimizationNeeded { get; set; }
        public string OptimizationType { get; set; }
        public List<string> RecommendedActions { get; set; } = new List<string>();
        public List<string> OverloadedLines { get; set; } = new List<string>();
        public List<string> UnderutilizedLines { get; set; } = new List<string>();
        public List<string> MaintenanceRequired { get; set; } = new List<string>();
        public DateTime AnalysisTime { get; set; } = DateTime.UtcNow;
    }

    public class DeviceStatus
    {
        public string DeviceId { get; set; }
        public string Status { get; set; }
        public int ProductionRate { get; set; }
        public int MaxProductionRate { get; set; } = 80;
        public double Temperature { get; set; }
        public double QualityPercentage { get; set; } = 95.0;
        public int RecentErrorCount { get; set; }
    }

    public class LineStatus
    {
        public string LineId { get; set; }
        public double Utilization { get; set; }
        public double EnergyConsumption { get; set; }
        public int LastMaintenanceHours { get; set; }
        public List<DeviceStatus> Devices { get; set; } = new List<DeviceStatus>();
    }

    #endregion
}