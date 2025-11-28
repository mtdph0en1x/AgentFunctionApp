using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Azure.Messaging.ServiceBus;
using Newtonsoft.Json;
using System.Net;
using AgentFunctionApp.Models;

namespace AgentFunctionApp.Functions
{
    public class DeviceCommandFunctions
    {
        private readonly ILogger _logger;
        private static readonly string ServiceBusConnectionString = Environment.GetEnvironmentVariable("ServiceBusConnection") ?? "";
        private static readonly ServiceBusClient serviceBusClient = new ServiceBusClient(ServiceBusConnectionString);
        private static readonly ServiceBusSender commandSender = serviceBusClient.CreateSender("device-commands");

        public DeviceCommandFunctions(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<DeviceCommandFunctions>();
        }

        [Function("SendDeviceCommand")]
        public async Task<HttpResponseData> SendDeviceCommand(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "devices/{deviceId}/command")] HttpRequestData req,
            string deviceId)
        {
            _logger.LogInformation($"Sending command to device: {deviceId}");

            try
            {
                // Parse request body
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var commandRequest = JsonConvert.DeserializeObject<DeviceCommandRequest>(requestBody);

                if (commandRequest == null || string.IsNullOrEmpty(commandRequest.Command))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteStringAsync("Invalid command request");
                    return badRequest;
                }

                // Create device command message
                var commandMessage = new DeviceCommandMessage
                {
                    DeviceId = deviceId,
                    Command = commandRequest.Command,
                    Parameters = commandRequest.Parameters ?? new Dictionary<string, object>(),
                    Priority = commandRequest.Priority ?? 1,
                    SenderId = "PWA-UI",
                    RequiresAck = true
                };

                // Send to Service Bus queue
                var messageBody = JsonConvert.SerializeObject(commandMessage);
                var serviceBusMessage = new ServiceBusMessage(messageBody)
                {
                    MessageId = commandMessage.MessageId,
                    ContentType = "application/json"
                };

                await commandSender.SendMessageAsync(serviceBusMessage);

                _logger.LogInformation($"Command '{commandRequest.Command}' queued for device {deviceId}");

                // Return success response
                var response = req.CreateResponse(HttpStatusCode.Accepted);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    messageId = commandMessage.MessageId,
                    deviceId = deviceId,
                    command = commandRequest.Command,
                    status = "Command queued for execution"
                });

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending command to device {deviceId}: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new
                {
                    success = false,
                    error = ex.Message
                });
                return errorResponse;
            }
        }
    }

    // Request model for device commands
    public class DeviceCommandRequest
    {
        public string Command { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public int? Priority { get; set; }
    }
}
