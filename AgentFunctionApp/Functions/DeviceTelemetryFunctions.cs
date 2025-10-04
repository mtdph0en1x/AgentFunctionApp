using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using System.Net;
using AgentFunctionApp.Models;

namespace AgentFunctionApp.Functions
{
    public class DeviceTelemetryFunctions
    {
        private readonly ILogger _logger;
        private readonly CosmosClient _cosmosClient;
        private readonly Container _container;

        public DeviceTelemetryFunctions(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<DeviceTelemetryFunctions>();
            
            var connectionString = Environment.GetEnvironmentVariable("COSMOS_CONNECTION_STRING");
            _cosmosClient = new CosmosClient(connectionString);
            _container = _cosmosClient.GetContainer("IIoTMonitoring", "Telemetry");
        }

        [Function("GetDevices")]
        public async Task<HttpResponseData> GetDevices(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "devices")] HttpRequestData req)
        {
            _logger.LogInformation("Getting latest device telemetry");

            try
            {
                var query = new QueryDefinition(
                    @"SELECT * FROM c
                      WHERE c.DocumentType IN ('telemetry-compressor', 'telemetry-press', 'telemetry-conveyor', 'telemetry-quality')
                      ORDER BY c.WindowEnd DESC");

                var iterator = _container.GetItemQueryIterator<DeviceTelemetry>(query);
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

                var httpResponse = req.CreateResponse(HttpStatusCode.OK);
                await httpResponse.WriteAsJsonAsync(latestByDevice);
                return httpResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching devices");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
                return errorResponse;
            }
        }

        [Function("GetDeviceDetail")]
        public async Task<HttpResponseData> GetDeviceDetail(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "devices/{deviceId}")] HttpRequestData req,
            string deviceId)
        {
            _logger.LogInformation($"Getting details for device: {deviceId}");

            try
            {
                var query = new QueryDefinition(
                    @"SELECT * FROM c
                      WHERE c.DeviceId = @deviceId
                        AND c.DocumentType LIKE 'telemetry-%'
                      ORDER BY c.WindowEnd DESC")
                    .WithParameter("@deviceId", deviceId);

                var iterator = _container.GetItemQueryIterator<DeviceTelemetry>(query);
                var items = new List<DeviceTelemetry>();

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    items.AddRange(response);
                }

                if (!items.Any())
                {
                    var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFoundResponse.WriteAsJsonAsync(new { error = "Device not found" });
                    return notFoundResponse;
                }

                var latest = items.First();
                var historical = items.Select(item => new
                {
                    timestamp = item.WindowEnd,
                    temperature = item.AvgTemperature,
                    productionRate = item.AvgProductionRate,
                    availability = item.AvailabilityPercentage * 100
                }).Reverse().ToList();

                var result = new
                {
                    current = latest,
                    historical = historical
                };

                var httpResponse = req.CreateResponse(HttpStatusCode.OK);
                await httpResponse.WriteAsJsonAsync(result);
                return httpResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching device detail");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
                return errorResponse;
            }
        }
    }
}
