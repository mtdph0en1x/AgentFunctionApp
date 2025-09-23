using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Devices;
using Newtonsoft.Json;
using AgentFunctionApp.Models;
using AgentFunctionApp.Services;

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
            _logger.LogInformation("HEALTH MONITOR: Checking agent health");

            // This would normally check a database or cache for agent heartbeats
            // For now, we'll simulate some basic health monitoring

            var currentTime = DateTime.UtcNow;
            var productionLines = new[] { "ProductionLine1", "ProductionLine2" };

            foreach (var lineId in productionLines)
            {
                var devices = await _deviceTwinService.GetDevicesInLineAsync(lineId);

                // Simulate checking device health
                foreach (var deviceId in devices)
                {
                    // In a real system, you'd check actual device status
                    // For now, we'll just log that we're monitoring
                    _logger.LogInformation($"Monitoring {deviceId} in {lineId}");
                }
            }

            // If you need to send messages from a timer, use direct Service Bus client
            // await SendHealthAlertIfNeeded(log);
        }


    }
}