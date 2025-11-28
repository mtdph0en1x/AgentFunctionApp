using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using AgentFunctionApp.Models;
using AgentFunctionApp.Services;

namespace AgentFunctionApp.Functions
{
    public class AgentCoordinationFunctions
    {
        private readonly ILogger<AgentCoordinationFunctions> _logger;
        private readonly DeviceTwinService _deviceTwinService;
        private static readonly string IoTHubConnectionString = Environment.GetEnvironmentVariable("IoTHubConnectionString") ?? "";
        private static readonly ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(IoTHubConnectionString);
        private static readonly string ServiceBusConnectionString = Environment.GetEnvironmentVariable("ServiceBusConnection") ?? "";
        private static readonly ServiceBusClient serviceBusClient = new ServiceBusClient(ServiceBusConnectionString);
        private static readonly CosmosClient cosmosClient = new CosmosClient(Environment.GetEnvironmentVariable("COSMOS_CONNECTION_STRING"));
        private static readonly Container cosmosContainer = cosmosClient.GetContainer("IIoTMonitoring", "Telemetry");

        public AgentCoordinationFunctions(ILogger<AgentCoordinationFunctions> logger, DeviceTwinService deviceTwinService)
        {
            _logger = logger;
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
                throw; // Service Bus retry
            }
        }

    }
}