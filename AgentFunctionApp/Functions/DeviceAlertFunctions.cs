using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Devices;
using Newtonsoft.Json;
using AgentFunctionApp.Models;

namespace AgentFunctionApp.Functions
{
    public class DeviceAlertFunctions
    {
        private readonly ILogger<DeviceAlertFunctions> _logger;
        private static readonly string IoTHubConnectionString = Environment.GetEnvironmentVariable("IoTHubConnectionString") ?? "";
        private static readonly ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(IoTHubConnectionString);

        private static readonly string ServiceBusConnectionString = Environment.GetEnvironmentVariable("ServiceBusConnection") ?? "";
        private static readonly ServiceBusClient serviceBusClient = new ServiceBusClient(ServiceBusConnectionString);

        public DeviceAlertFunctions(ILogger<DeviceAlertFunctions> logger)
        {
            _logger = logger;
        }

        [Function("ProcessDeviceAlerts")]
        public async Task ProcessDeviceAlerts(
            [ServiceBusTrigger("device-alerts", Connection = "ServiceBusConnection")]
            ServiceBusReceivedMessage message)
        {
            try
            {
                var deviceAlert = JsonConvert.DeserializeObject<DeviceAlertMessage>(
                    message.Body.ToString());

                _logger.LogInformation($"AGENT PROCESSING: {deviceAlert.DeviceId} - {deviceAlert.AlertType} (Priority: {deviceAlert.Priority})");

                await ProcessDeviceAlert(deviceAlert);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing device alert: {ex.Message}");
                throw;
            }
        }

        private async Task ProcessDeviceAlert(DeviceAlertMessage alert)
        {
            var sender = serviceBusClient.CreateSender("line-coordination");

            try
            {
                // Temperature-based decisions
                if (alert.Temperature > 90)
                {
                    _logger.LogWarning($"CRITICAL TEMPERATURE: {alert.DeviceId} at {alert.Temperature}°C");

                    var lineCommand = new LineCoordinationMessage
                    {
                        LineId = alert.LineId,
                        Action = "EmergencyStop",
                        AffectedDevices = new List<string> { alert.DeviceId },
                        Reason = $"Critical temperature: {alert.Temperature}°C",
                        Priority = 5,
                        SenderId = "DeviceAlertAgent"
                    };

                    await sender.SendMessageAsync(new ServiceBusMessage(JsonConvert.SerializeObject(lineCommand)));

                    // Also send direct emergency stop
                    await TriggerEmergencyStopAsync(alert.DeviceId);
                }
                // ... rest of your logic
            }
            finally
            {
                await sender.DisposeAsync();
            }
        }

        private async Task TriggerEmergencyStopAsync(string deviceId)
        {
            try
            {
                var methodInvocation = new CloudToDeviceMethod("EmergencyStop");
                methodInvocation.SetPayloadJson(JsonConvert.SerializeObject(new { reason = "Agent Emergency Stop" }));

                var response = await serviceClient.InvokeDeviceMethodAsync(deviceId, methodInvocation);
                _logger.LogInformation($"Direct emergency stop executed on {deviceId}. Status: {response.Status}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed emergency stop on {deviceId}: {ex.Message}");
            }
        }

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