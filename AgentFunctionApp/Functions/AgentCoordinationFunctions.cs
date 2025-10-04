using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using AgentFunctionApp.Models;
using AgentFunctionApp.Services;
using System.Collections.Concurrent;

namespace AgentFunctionApp.Functions
{
    public class AgentCoordinationFunctions
    {
        private readonly ILogger<AgentCoordinationFunctions> _logger;
        private readonly AgentDecisionService _decisionService;
        private readonly DeviceTwinService _deviceTwinService;
        private static readonly string IoTHubConnectionString = Environment.GetEnvironmentVariable("IoTHubConnectionString") ?? "";
        private static readonly ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(IoTHubConnectionString);
        private static readonly string ServiceBusConnectionString = Environment.GetEnvironmentVariable("ServiceBusConnection") ?? "";
        private static readonly ServiceBusClient serviceBusClient = new ServiceBusClient(ServiceBusConnectionString);
        private static readonly CosmosClient cosmosClient = new CosmosClient(Environment.GetEnvironmentVariable("COSMOS_CONNECTION_STRING"));
        private static readonly Container cosmosContainer = cosmosClient.GetContainer("IIoTMonitoring", "Telemetry");
        private static readonly ConcurrentDictionary<string, string> deviceStatusCache = new ConcurrentDictionary<string, string>();

        public AgentCoordinationFunctions(ILogger<AgentCoordinationFunctions> logger, AgentDecisionService decisionService, DeviceTwinService deviceTwinService)
        {
            _logger = logger;
            _decisionService = decisionService;
            _deviceTwinService = deviceTwinService;
        }

        [Function("ExecuteDeviceCommands")]
        public async Task ExecuteDeviceCommands(
            [ServiceBusTrigger("device-commands", Connection = "ServiceBusConnection")]
            ServiceBusReceivedMessage message)
        {
            var deviceCommand = JsonConvert.DeserializeObject<DeviceCommandMessage>(
                message.Body.ToString());

            _logger.LogInformation($"EXECUTING: {deviceCommand.Command} on {deviceCommand.DeviceId}");

            try
            {
                string methodName = deviceCommand.Command switch
                {
                    "EmergencyStop" => "HandleEmergencyStopAsync",
                    "ResetErrorStatus" => "HandleResetErrorStatusAsync",
                    "AdjustProductionRate" => "HandleAdjustProductionRateAsync",
                    _ => deviceCommand.Command // fallback to original name
                };

                var method = new CloudToDeviceMethod(methodName);
                method.SetPayloadJson(JsonConvert.SerializeObject(deviceCommand.Parameters));
                method.ResponseTimeout = TimeSpan.FromSeconds(30);

                var response = await serviceClient.InvokeDeviceMethodAsync(
                    deviceCommand.DeviceId, method);

                _logger.LogInformation($"Command {methodName} executed on {deviceCommand.DeviceId}. Status: {response.Status}");

                if (response.Status != 200)
                {
                    _logger.LogWarning($"Command execution failed on {deviceCommand.DeviceId}. Response: {response.GetPayloadAsJson()}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to execute command on {deviceCommand.DeviceId}: {ex.Message}");
                throw; // Let Service Bus handle retry
            }
        }


        [Function("MonitorAgentHealth")]
        public async Task MonitorAgentHealth(
            [TimerTrigger("0 */2 * * * *")] TimerInfo timer)
        {
            _logger.LogInformation("HEALTH MONITOR: Checking device status and recording changes");

            try
            {
                // Fetch latest telemetry for all devices
                var query = new QueryDefinition(
                    @"SELECT * FROM c
                      WHERE c.DocumentType IN ('telemetry-compressor', 'telemetry-press', 'telemetry-conveyor', 'telemetry-quality')
                      ORDER BY c.WindowEnd DESC");

                var iterator = cosmosContainer.GetItemQueryIterator<DeviceTelemetry>(query);
                var items = new List<DeviceTelemetry>();

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    items.AddRange(response);
                }

                // Get latest record per device
                var latestByDevice = items
                    .GroupBy(x => x.DeviceId)
                    .Select(g => g.OrderByDescending(x => x.WindowEnd).First())
                    .ToList();

                var currentTime = DateTime.UtcNow;

                // Check status for each device
                foreach (var device in latestByDevice)
                {
                    var newStatus = DetermineDeviceStatus(device, currentTime);

                    // Check if we have a previous status for this device
                    if (deviceStatusCache.TryGetValue(device.DeviceId, out var previousStatus))
                    {
                        // Device exists in cache - check for change
                        if (newStatus != previousStatus)
                        {
                            _logger.LogInformation($"Status change detected for {device.DeviceId}: {previousStatus} -> {newStatus}");

                            var documentId = $"status-{device.DeviceId}-{currentTime:yyyy-MM-dd-HH-mm-ss}";
                            var statusChange = new
                            {
                                id = documentId,
                                DocumentType = "status-change",
                                DeviceId = device.DeviceId,
                                LineId = device.LineId,
                                DeviceType = device.DeviceType,
                                Timestamp = currentTime,
                                OldStatus = previousStatus,
                                NewStatus = newStatus,
                                Reason = GetStatusChangeReason(device, newStatus, currentTime),
                                Temperature = device.AvgTemperature,
                                ErrorCode = device.CurrentErrorCode,
                                AvailabilityPercentage = device.AvailabilityPercentage,
                                ttl = 2592000 // 30 days
                            };

                            await cosmosContainer.CreateItemAsync(statusChange, new PartitionKey(device.DeviceId));
                            deviceStatusCache[device.DeviceId] = newStatus;
                        }
                    }
                    else
                    {
                        // First time seeing this device - record initial status
                        _logger.LogInformation($"Recording initial status for {device.DeviceId}: {newStatus}");

                        var documentId = $"status-{device.DeviceId}-{currentTime:yyyy-MM-dd-HH-mm-ss}";
                        var statusChange = new
                        {
                            id = documentId,
                            DocumentType = "status-change",
                            DeviceId = device.DeviceId,
                            LineId = device.LineId,
                            DeviceType = device.DeviceType,
                            Timestamp = currentTime,
                            OldStatus = (string)null,
                            NewStatus = newStatus,
                            Reason = $"Initial status: {newStatus}",
                            Temperature = device.AvgTemperature,
                            ErrorCode = device.CurrentErrorCode,
                            AvailabilityPercentage = device.AvailabilityPercentage,
                            ttl = 2592000 // 30 days
                        };

                        await cosmosContainer.CreateItemAsync(statusChange, new PartitionKey(device.LineId));
                        deviceStatusCache[device.DeviceId] = newStatus;
                    }
                }

                _logger.LogInformation($"Health monitor completed. Checked {latestByDevice.Count} devices.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in health monitor: {ex.GetType().Name} - {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    _logger.LogError($"Inner exception: {ex.InnerException.Message}");
                }
            }
        }

        private string DetermineDeviceStatus(DeviceTelemetry device, DateTime currentTime)
        {
            // Check if device is offline (no data in last 5 minutes)
            var lastUpdateTime = DateTime.Parse(device.WindowEnd);
            var minutesSinceUpdate = (currentTime - lastUpdateTime).TotalMinutes;

            if (minutesSinceUpdate > 5) return "offline";
            if (device.CurrentErrorCode != 0) return "error";
            if (device.AvgTemperature > 80) return "warning";
            return "online";
        }

        private string GetStatusChangeReason(DeviceTelemetry device, string newStatus, DateTime currentTime)
        {
            var lastUpdateTime = DateTime.Parse(device.WindowEnd);
            var minutesSinceUpdate = (currentTime - lastUpdateTime).TotalMinutes;

            return newStatus switch
            {
                "offline" => $"No data received for {Math.Round(minutesSinceUpdate, 1)} minutes",
                "error" => $"Error code {device.CurrentErrorCode} detected",
                "warning" => $"Temperature {Math.Round(device.AvgTemperature, 1)}°C exceeded threshold (80°C)",
                "online" => "Device returned to normal operation",
                _ => "Status changed"
            };
        }


    }
}