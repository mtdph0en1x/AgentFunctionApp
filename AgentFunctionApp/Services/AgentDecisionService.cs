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


        public DecisionResult AnalyzeCriticalAlert(CriticalErrorAlert alert)
        {
            var decision = new DecisionResult
            {
                DeviceId = alert.DeviceId,
                LineId = alert.LineId,
                AlertType = "Critical",
                Priority = alert.ErrorPriority
            };

            // Determine action based on error flags
            if (alert.HasEmergencyStop == 1)
            {
                decision.RecommendedAction = "EmergencyStop";
                decision.Urgency = "Critical";
                decision.Reason = "Emergency stop detected - immediate shutdown required";
                decision.AffectedDevices = GetLineDevices(alert.LineId);
                decision.Parameters["SafetyProtocol"] = true;
            }
            else if (alert.HasPowerFailure == 1)
            {
                decision.RecommendedAction = "PowerFailureProtocol";
                decision.Urgency = "Critical";
                decision.Reason = "Power failure - safe shutdown required";
                decision.AffectedDevices = GetLineDevices(alert.LineId);
                decision.Parameters["SafeShutdown"] = true;
            }
            else if (alert.HasSensorFailure == 1)
            {
                decision.RecommendedAction = "SensorDiagnostic";
                decision.Urgency = "High";
                decision.Reason = "Critical sensor failure - diagnostics required";
                decision.AffectedDevices = new List<string> { alert.DeviceId };
                decision.Parameters["DiagnosticLevel"] = "Full";
            }
            else if (alert.HasUnknownError == 1)
            {
                decision.RecommendedAction = "DiagnosticScan";
                decision.Urgency = "High";
                decision.Reason = "Unknown critical error - investigation required";
                decision.AffectedDevices = new List<string> { alert.DeviceId };
                decision.Parameters["InvestigationLevel"] = "Comprehensive";
            }
            else
            {
                decision.RecommendedAction = "ImmediateReset";
                decision.Urgency = "High";
                decision.Reason = $"Critical error code: {alert.DeviceError}";
                decision.AffectedDevices = new List<string> { alert.DeviceId };
            }

            _logger.LogInformation($"Critical alert decision for {alert.DeviceId}: {decision.RecommendedAction}");
            return decision;
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
            // Device-specific analysis
            else if (alert.AlertType == "DeviceSpecific")
            {
                decision = AnalyzeDeviceSpecificAlert(alert, decision);
            }

            _logger.LogInformation($"Decision made for {alert.DeviceType} device {alert.DeviceId}: {decision.RecommendedAction}");
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

        private DecisionResult AnalyzeDeviceSpecificAlert(DeviceAlertMessage alert, DecisionResult decision)
        {
            switch (alert.DeviceType)
            {
                case AgentFunctionApp.Models.DeviceType.Press:
                    return AnalyzePressAlert(alert, decision);

                case AgentFunctionApp.Models.DeviceType.Conveyor:
                    return AnalyzeConveyorAlert(alert, decision);

                case AgentFunctionApp.Models.DeviceType.QualityStation:
                    return AnalyzeQualityStationAlert(alert, decision);

                case AgentFunctionApp.Models.DeviceType.Compressor:
                    return AnalyzeCompressorAlert(alert, decision);

                default:
                    decision.RecommendedAction = "Monitor";
                    decision.Urgency = "Low";
                    decision.Reason = $"Unknown device type: {alert.DeviceType}";
                    break;
            }

            return decision;
        }

        private DecisionResult AnalyzePressAlert(DeviceAlertMessage alert, DecisionResult decision)
        {
            if (alert.Pressure.HasValue)
            {
                if (alert.Pressure > 100) // Critical pressure threshold
                {
                    decision.RecommendedAction = "EmergencyStop";
                    decision.Urgency = "Critical";
                    decision.Reason = $"Critical pressure level: {alert.Pressure} PSI";
                    decision.AffectedDevices = new List<string> { alert.DeviceId };
                }
                else if (alert.Pressure > 85) // High pressure threshold
                {
                    decision.RecommendedAction = "ReducePressure";
                    decision.Urgency = "High";
                    decision.Reason = $"High pressure level: {alert.Pressure} PSI";
                    decision.Parameters["TargetPressure"] = 75;
                }
            }
            return decision;
        }

        private DecisionResult AnalyzeConveyorAlert(DeviceAlertMessage alert, DecisionResult decision)
        {
            if (alert.Speed.HasValue)
            {
                if (alert.Speed < 10) // Too slow
                {
                    decision.RecommendedAction = "AdjustSpeed";
                    decision.Urgency = "Medium";
                    decision.Reason = $"Conveyor speed too low: {alert.Speed} m/min";
                    decision.Parameters["TargetSpeed"] = 20;
                }
                else if (alert.Speed > 50) // Too fast
                {
                    decision.RecommendedAction = "ReduceSpeed";
                    decision.Urgency = "High";
                    decision.Reason = $"Conveyor speed too high: {alert.Speed} m/min";
                    decision.Parameters["TargetSpeed"] = 40;
                }
            }
            return decision;
        }

        private DecisionResult AnalyzeQualityStationAlert(DeviceAlertMessage alert, DecisionResult decision)
        {
            if (alert.PassRate.HasValue)
            {
                if (alert.PassRate < 70) // Poor quality
                {
                    decision.RecommendedAction = "QualityInvestigation";
                    decision.Urgency = "High";
                    decision.Reason = $"Low pass rate: {alert.PassRate}%";
                    decision.AffectedDevices = GetLineDevices(alert.LineId);
                    decision.Parameters["TargetPassRate"] = 95;
                }
                else if (alert.PassRate < 90) // Below target
                {
                    decision.RecommendedAction = "QualityAdjustment";
                    decision.Urgency = "Medium";
                    decision.Reason = $"Pass rate below target: {alert.PassRate}%";
                    decision.Parameters["TargetPassRate"] = 95;
                }
            }
            return decision;
        }

        private DecisionResult AnalyzeCompressorAlert(DeviceAlertMessage alert, DecisionResult decision)
        {
            if (alert.OutputPressure.HasValue && alert.SystemAirPressure.HasValue)
            {
                var pressureDiff = alert.SystemAirPressure.Value - alert.OutputPressure.Value;

                if (pressureDiff > 20) // Significant pressure loss
                {
                    decision.RecommendedAction = "CompressorMaintenance";
                    decision.Urgency = "High";
                    decision.Reason = $"Pressure loss detected: {pressureDiff} PSI difference";
                    decision.Parameters["SystemPressure"] = alert.SystemAirPressure;
                    decision.Parameters["OutputPressure"] = alert.OutputPressure;
                }
                else if (alert.OutputPressure < 80) // Low output pressure
                {
                    decision.RecommendedAction = "IncreaseCompression";
                    decision.Urgency = "Medium";
                    decision.Reason = $"Low output pressure: {alert.OutputPressure} PSI";
                    decision.Parameters["TargetPressure"] = 90;
                }
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
            // This method is now deprecated - use DeviceTwinService.GetDevicesInLineAsync() instead
            return lineId switch
            {
                "ProductionLine1" => new List<string> { "Device1", "Device2", "Device3", "Compressor1" },
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
        public AgentFunctionApp.Models.DeviceType DeviceType { get; set; }
        public string Status { get; set; }
        public string WorkorderId { get; set; }
        public int ProductionRate { get; set; }
        public int MaxProductionRate { get; set; } = 80;
        public double Temperature { get; set; }
        public double QualityPercentage { get; set; } = 95.0;
        public int RecentErrorCount { get; set; }

        // Device-specific properties based on DeviceType
        public double? Pressure { get; set; }              // Press Device
        public double? Speed { get; set; }                 // Conveyor Device
        public int? GoodCount { get; set; }                // Quality Station Device
        public int? BadCount { get; set; }                 // Quality Station Device
        public double? PassRate { get; set; }              // Quality Station Device
        public double? OutputPressure { get; set; }        // Compressor Device
        public double? SystemAirPressure { get; set; }     // Compressor Device
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