using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Messaging.ServiceBus;
using Newtonsoft.Json;
using AgentFunctionApp.Models;

namespace AgentFunctionApp.Functions
{
    public class LineCoordinationFunctions
    {
        private readonly ILogger<LineCoordinationFunctions> _logger;
        private static readonly string ServiceBusConnectionString = Environment.GetEnvironmentVariable("ServiceBusConnection") ?? "";
        private static readonly ServiceBusClient serviceBusClient = new ServiceBusClient(ServiceBusConnectionString);

        public LineCoordinationFunctions(ILogger<LineCoordinationFunctions> logger)
        {
            _logger = logger;
        }

        [Function("CoordinateProductionLine")]
        public async Task CoordinateProductionLine(
            [ServiceBusTrigger("line-coordination", Connection = "ServiceBusConnection")]
            ServiceBusReceivedMessage message)
        {
            var lineCommand = JsonConvert.DeserializeObject<LineCoordinationMessage>(
                message.Body.ToString());

            _logger.LogInformation($"LINE AGENT: {lineCommand.LineId} - {lineCommand.Action} - {lineCommand.Reason}");

            switch (lineCommand.Action)
            {
                case "EmergencyStop":
                    await HandleEmergencyStop(lineCommand);
                    break;

                case "Optimize":
                    await HandleLineOptimization(lineCommand);
                    break;

                case "Balance":
                    await HandleLineBalance(lineCommand);
                    break;

                case "Reset":
                    await HandleReset(lineCommand);
                    break;

                default:
                    _logger.LogWarning($"Unknown line action: {lineCommand.Action}");
                    break;
            }
        }

        private async Task HandleEmergencyStop(
            LineCoordinationMessage command)
        {
            _logger.LogError($"EMERGENCY STOP: Line {command.LineId} - {command.Reason}");

            var sender = serviceBusClient.CreateSender("device-commands");

            try
            {
                foreach (var deviceId in command.AffectedDevices)
                {
                    var deviceCommand = new DeviceCommandMessage
                    {
                        DeviceId = deviceId,
                        Command = "EmergencyStop",
                        Parameters = new Dictionary<string, object>
                        {
                            ["Reason"] = command.Reason,
                            ["LineId"] = command.LineId,
                            ["Timestamp"] = DateTime.UtcNow
                        },
                        Priority = 5,
                        SenderId = "LineCoordinationAgent"
                    };

                    await sender.SendMessageAsync(new ServiceBusMessage(JsonConvert.SerializeObject(deviceCommand)));
                    _logger.LogInformation($"Emergency stop command sent to {deviceId}");
                }
            }
            finally
            {
                await sender.DisposeAsync();
            }
        }

        private async Task HandleLineOptimization(
            LineCoordinationMessage command)
        {
            _logger.LogInformation($"OPTIMIZING LINE: {command.LineId}");

            var sender = serviceBusClient.CreateSender("device-commands");

            try
            {
                var actionType = command.Parameters.GetValueOrDefault("Action", "").ToString();

                if (actionType == "ReduceLoad")
                {
                    await HandleOverheatingOptimization(command, sender);
                }
                else if (actionType == "Compensate")
                {
                    await HandleErrorCompensation(command, sender);
                }
            }
            finally
            {
                await sender.DisposeAsync();
            }
        }

        private async Task HandleOverheatingOptimization(
            LineCoordinationMessage command,
            ServiceBusSender sender)
        {
            var overheatingDevice = command.Parameters.GetValueOrDefault("OverheatingDevice", "").ToString();
            var temperature = Convert.ToDouble(command.Parameters.GetValueOrDefault("Temperature", 0));

            foreach (var deviceId in command.AffectedDevices)
            {
                int targetRate;
                string reason;

                if (deviceId == overheatingDevice)
                {
                    targetRate = 40; // Reduce overheating device significantly
                    reason = $"Reducing load due to temperature: {temperature}°C";
                }
                else
                {
                    targetRate = 65; // Increase others to compensate
                    reason = $"Compensating for overheating device {overheatingDevice}";
                }

                var deviceCommand = new DeviceCommandMessage
                {
                    DeviceId = deviceId,
                    Command = "AdjustProductionRate",
                    Parameters = new Dictionary<string, object>
                    {
                        ["TargetRate"] = targetRate,
                        ["Reason"] = reason
                    },
                    SenderId = "LineOptimizationAgent"
                };

                await sender.SendMessageAsync(new ServiceBusMessage(JsonConvert.SerializeObject(deviceCommand)));
                _logger.LogInformation($"Optimization command sent to {deviceId}: target rate {targetRate}");
            }
        }

        private async Task HandleErrorCompensation(
            LineCoordinationMessage command,
            ServiceBusSender sender)
        {
            var problematicDevice = command.Parameters.GetValueOrDefault("ProblematicDevice", "").ToString();
            var errorCount = Convert.ToInt32(command.Parameters.GetValueOrDefault("ErrorCount", 0));

            foreach (var deviceId in command.AffectedDevices)
            {
                int targetRate;
                string deviceAction;

                if (deviceId == problematicDevice)
                {
                    targetRate = 45; // Reduce problematic device
                    deviceAction = "ReduceRate";
                }
                else
                {
                    targetRate = 70; // Increase others to compensate
                    deviceAction = "AdjustProductionRate";
                }

                var deviceCommand = new DeviceCommandMessage
                {
                    DeviceId = deviceId,
                    Command = deviceAction,
                    Parameters = new Dictionary<string, object>
                    {
                        ["TargetRate"] = targetRate,
                        ["Reason"] = $"Compensating for device {problematicDevice} with {errorCount} errors"
                    },
                    SenderId = "LineOptimizationAgent"
                };

                await sender.SendMessageAsync(new ServiceBusMessage(JsonConvert.SerializeObject(deviceCommand)));
                _logger.LogInformation($"Compensation command sent to {deviceId}: target rate {targetRate}");
            }
        }

        private async Task HandleLineBalance(
            LineCoordinationMessage command)
        {
            _logger.LogInformation($"BALANCING LINE: {command.LineId}");

            var sender = serviceBusClient.CreateSender("device-commands");

            try
            {
                var slowDevice = command.Parameters.GetValueOrDefault("SlowDevice", "").ToString();
                var currentRate = Convert.ToInt32(command.Parameters.GetValueOrDefault("CurrentRate", 0));
                var targetRate = Convert.ToInt32(command.Parameters.GetValueOrDefault("TargetRate", 60));

                foreach (var deviceId in command.AffectedDevices)
                {
                    int newTargetRate;
                    string reason;

                    if (deviceId == slowDevice)
                    {
                        newTargetRate = Math.Min(targetRate, currentRate + 15); // Boost slow device gradually
                        reason = "Boosting slow device for line balance";
                    }
                    else
                    {
                        newTargetRate = targetRate - 5; // Slightly reduce others to maintain balance
                        reason = $"Adjusting for line balance due to slow device {slowDevice}";
                    }

                    var deviceCommand = new DeviceCommandMessage
                    {
                        DeviceId = deviceId,
                        Command = "AdjustProductionRate",
                        Parameters = new Dictionary<string, object>
                        {
                            ["TargetRate"] = newTargetRate,
                            ["Reason"] = reason
                        },
                        SenderId = "LineBalancingAgent"
                    };

                    await sender.SendMessageAsync(new ServiceBusMessage(JsonConvert.SerializeObject(deviceCommand)));
                    _logger.LogInformation($"Balance command sent to {deviceId}: target rate {newTargetRate}");
                }
            }
            finally
            {
                await sender.DisposeAsync();
            }
        }

        private async Task HandleReset(
            LineCoordinationMessage command)
        {
            _logger.LogInformation($"RESETTING DEVICES: {command.LineId}");

            var sender = serviceBusClient.CreateSender("device-commands");

            try
            {
                foreach (var deviceId in command.AffectedDevices)
                {
                    var deviceCommand = new DeviceCommandMessage
                    {
                        DeviceId = deviceId,
                        Command = "Reset",
                        Parameters = new Dictionary<string, object>
                        {
                            ["Reason"] = command.Reason
                        },
                        SenderId = "LineResetAgent"
                    };

                    await sender.SendMessageAsync(new ServiceBusMessage(JsonConvert.SerializeObject(deviceCommand)));
                    _logger.LogInformation($"Reset command sent to {deviceId}");
                }
            }
            finally
            {
                await sender.DisposeAsync();
            }
        }
    }
}