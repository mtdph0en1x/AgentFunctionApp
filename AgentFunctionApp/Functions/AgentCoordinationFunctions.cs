using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Devices;
using Newtonsoft.Json;
using AgentFunctionApp.Models;

namespace AgentFunctionApp.Functions
{
    public class AgentCoordinationFunctions
    {
        private readonly ILogger<AgentCoordinationFunctions> _logger;
        private static readonly string IoTHubConnectionString = Environment.GetEnvironmentVariable("IoTHubConnectionString") ?? "";
        private static readonly ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(IoTHubConnectionString);
        
        public AgentCoordinationFunctions(ILogger<AgentCoordinationFunctions> logger)  
        {
            _logger = logger;
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
                var method = new CloudToDeviceMethod("AgentCommand");
                method.SetPayloadJson(JsonConvert.SerializeObject(deviceCommand));
                method.ResponseTimeout = TimeSpan.FromSeconds(30);

                var response = await serviceClient.InvokeDeviceMethodAsync(
                    deviceCommand.DeviceId, method);

                _logger.LogInformation($"Command executed on {deviceCommand.DeviceId}. Status: {response.Status}");

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

        [Function("ProcessAgentStatus")]
        public async Task ProcessAgentStatus(
            [ServiceBusTrigger("agent-status", Connection = "ServiceBusConnection")]
            ServiceBusReceivedMessage message)
        {
            try
            {
                var statusMessage = JsonConvert.DeserializeObject<AgentStatusMessage>(
                    message.Body.ToString());

                _logger.LogInformation($"AGENT STATUS: {statusMessage.AgentId} ({statusMessage.AgentType}) - {statusMessage.Status}");

                // Process agent status updates
                await ProcessAgentStatusUpdate(statusMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing agent status: {ex.Message}");
                throw;
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
                var devices = GetDevicesInLine(lineId);

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

        private async Task ProcessAgentStatusUpdate(AgentStatusMessage statusMessage)
        {
            // Process different types of agent status updates
            switch (statusMessage.AgentType)
            {
                case "Device":
                    await ProcessDeviceAgentStatus(statusMessage);
                    break;

                case "Line":
                    await ProcessLineAgentStatus(statusMessage);
                    break;

                case "Plant":
                    await ProcessPlantAgentStatus(statusMessage);
                    break;

                default:
                    _logger.LogWarning($"Unknown agent type: {statusMessage.AgentType}");
                    break;
            }
        }

        private async Task ProcessDeviceAgentStatus(AgentStatusMessage statusMessage)
        {
            // Log device agent actions
            if (statusMessage.StatusData.ContainsKey("LastAction"))
            {
                var lastAction = statusMessage.StatusData["LastAction"].ToString();
                _logger.LogInformation($"Device agent {statusMessage.AgentId} completed action: {lastAction}");

                // You could trigger follow-up actions here based on device status
                if (lastAction == "EmergencyStopCompleted")
                {
                    _logger.LogInformation($"Emergency stop confirmed for {statusMessage.AgentId}");
                }
                else if (lastAction == "ProductionRateAdjusted")
                {
                    _logger.LogInformation($"Production rate adjustment confirmed for {statusMessage.AgentId}");
                }
            }
        }

        private async Task ProcessLineAgentStatus(AgentStatusMessage statusMessage)
        {
            // Process line-level status updates
            _logger.LogInformation($"Line agent status update: {statusMessage.AgentId} - {statusMessage.Status}");
        }

        private async Task ProcessPlantAgentStatus(AgentStatusMessage statusMessage)
        {
            // Process plant-level status updates
            _logger.LogInformation($"Plant agent status update: {statusMessage.AgentId} - {statusMessage.Status}");
        }

        // Helper method
        private static List<string> GetDevicesInLine(string lineId)
        {
            return lineId switch
            {
                "ProductionLine1" => new List<string> { "Device1", "Device2", "Device3" },
                "ProductionLine2" => new List<string> { "Device4", "Device5", "Device6" },
                _ => new List<string>()
            };
        }
    }
}