using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Devices;
using Newtonsoft.Json;
using AgentFunctionApp.Models;
using AgentFunctionApp.Services;

namespace AgentFunctionApp.Functions
{
    public class DeviceAlertFunctions
    {
        private readonly ILogger<DeviceAlertFunctions> _logger;
        private readonly DeviceTwinService _deviceTwinService;
        private static readonly string IoTHubConnectionString = Environment.GetEnvironmentVariable("IoTHubConnectionString") ?? "";
        private static readonly ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(IoTHubConnectionString);

        private static readonly string ServiceBusConnectionString = Environment.GetEnvironmentVariable("ServiceBusConnection") ?? "";
        private static readonly ServiceBusClient serviceBusClient = new ServiceBusClient(ServiceBusConnectionString);
        private static readonly RegistryManager registryManager = RegistryManager.CreateFromConnectionString(IoTHubConnectionString);

        public DeviceAlertFunctions(ILogger<DeviceAlertFunctions> logger, DeviceTwinService deviceTwinService)
        {
            _logger = logger;
            _deviceTwinService = deviceTwinService;
        }

        [Function("ProcessCriticalAlerts")]
        public async Task ProcessCriticalAlerts(
            [ServiceBusTrigger("critical-alerts", Connection = "ServiceBusConnection")]
            ServiceBusReceivedMessage message)
        {
            try
            {
                var criticalAlert = JsonConvert.DeserializeObject<CriticalErrorAlert>(message.Body.ToString());

                // Validate critical alert data
                if (string.IsNullOrEmpty(criticalAlert.DeviceId) ||
                    criticalAlert.DeviceError <= 0)
                {
                    _logger.LogWarning($"INVALID CRITICAL ALERT: {criticalAlert.DeviceId} - Missing DeviceId or ErrorCode. Skipping processing.");
                    return;
                }

                _logger.LogInformation($"CRITICAL ALERT: {criticalAlert.DeviceId} - Code: {criticalAlert.DeviceError}, Priority: {criticalAlert.ErrorPriority}");

                await ProcessCriticalAlert(criticalAlert);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing critical alert: {ex.Message}");
                throw;
            }
        }

        private async Task ProcessCriticalAlert(CriticalErrorAlert alert)
        {
            _logger.LogWarning($"IMMEDIATE CRITICAL ACTION: {alert.DeviceId} - ErrorCode: {alert.DeviceError}, Priority: {alert.ErrorPriority}");

            try
            {
                string methodName;
                string actionReason;

                // Determine action based on error flags
                if (alert.HasEmergencyStop == 1 || alert.HasPowerFailure == 1)
                {
                    methodName = "EmergencyStop";
                    actionReason = alert.HasEmergencyStop == 1 ? "Emergency stop detected" : "Power failure detected";

                    // Emergency stop affects entire line
                    var affectedDevices = await _deviceTwinService.GetDevicesInLineAsync(alert.LineId);

                    foreach (var deviceId in affectedDevices)
                    {
                        await CallDirectMethod(deviceId, methodName, actionReason);
                    }
                }
                else
                {
                    methodName = "ResetErrorStatus";
                    actionReason = alert.HasSensorFailure == 1 ? "Sensor failure detected" : "Unknown error detected";

                    // Other errors only affect the specific device
                    await CallDirectMethod(alert.DeviceId, methodName, actionReason);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to process critical alert for {alert.DeviceId}: {ex.Message}");
                throw;
            }
        }

        private async Task CallDirectMethod(string deviceId, string methodName, string reason)
        {
            try
            {
                // Check if device is online first
                var twin = await registryManager.GetTwinAsync(deviceId);
                if (twin.ConnectionState != DeviceConnectionState.Connected)
                {
                    _logger.LogWarning($"Device {deviceId} is offline (ConnectionState: {twin.ConnectionState}) - skipping {methodName}");
                    return;
                }

                var method = new CloudToDeviceMethod(methodName);
                method.SetPayloadJson("{}");
                method.ResponseTimeout = TimeSpan.FromSeconds(10);

                var response = await serviceClient.InvokeDeviceMethodAsync(deviceId, method);

                if (response.Status == 200)
                {
                    _logger.LogInformation($"{methodName} executed successfully on {deviceId} - {reason}");
                }
                else
                {
                    _logger.LogError($"{methodName} failed on {deviceId}. Status: {response.Status} - {reason}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to execute {methodName} on {deviceId}: {ex.Message}");
            }
        }

        [Function("ProcessLineAlerts")]
        public async Task ProcessLineAlerts(
            [ServiceBusTrigger("line-alerts", Connection = "ServiceBusConnection")]
            ServiceBusReceivedMessage message)
        {
            try
            {
                var lineAlert = JsonConvert.DeserializeObject<LineErrorAlert>(message.Body.ToString());

                // Validate line alert data
                if (string.IsNullOrEmpty(lineAlert.LineId) || lineAlert.ErrorCount <= 0)
                {
                    _logger.LogWarning($"INVALID LINE ALERT: {lineAlert.LineId} - Missing LineId or ErrorCount. Skipping processing.");
                    return;
                }

                _logger.LogInformation($"LINE ALERT: {lineAlert.LineId} - {lineAlert.ErrorCount} errors, " +
                    $"MaxErrorCode: {lineAlert.MaxErrorCode}, Priority: {lineAlert.Priority}");

                await ProcessLineAlert(lineAlert);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing line alert: {ex.Message}");
                throw;
            }
        }

        private async Task ProcessLineAlert(LineErrorAlert alert)
        {
            _logger.LogWarning($"LINE-LEVEL RESET: {alert.LineId} - {alert.ErrorCount} errors detected");

            try
            {
                // Get all devices in the line
                var affectedDevices = await _deviceTwinService.GetDevicesInLineAsync(alert.LineId);

                string reason = $"Pattern detected: {alert.ErrorCount} errors in 1 minute, MaxErrorCode: {alert.MaxErrorCode}, AvgTemp: {alert.AvgTemperature:F1}°C";

                // Reset all devices in the line
                foreach (var deviceId in affectedDevices)
                {
                    await CallDirectMethod(deviceId, "ResetErrorStatus", reason);
                }

                _logger.LogInformation($"Reset commands sent to {affectedDevices.Count} devices on line {alert.LineId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to process line alert for {alert.LineId}: {ex.Message}");
                throw;
            }
        }

    }
}