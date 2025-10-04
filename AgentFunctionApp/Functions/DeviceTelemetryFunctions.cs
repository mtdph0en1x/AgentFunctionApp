using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using System.Net;
using AgentFunctionApp.Models;
using Newtonsoft.Json;

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

        [Function("GetLineKPIs")]
        public async Task<HttpResponseData> GetLineKPIs(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "kpis")] HttpRequestData req)
        {
            _logger.LogInformation("Getting line KPI data");

            try
            {
                // Get optional query parameters
                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                var lineId = query["lineId"];
                var daysBack = int.TryParse(query["daysBack"], out var days) ? days : 30;

                var cutoffDate = DateTime.UtcNow.AddDays(-daysBack);

                QueryDefinition cosmosQuery;
                if (!string.IsNullOrEmpty(lineId))
                {
                    cosmosQuery = new QueryDefinition(
                        @"SELECT * FROM c
                          WHERE c.DocumentType = 'line-kpi'
                            AND c.LineId = @lineId
                            AND c.WindowEnd >= @cutoffDate
                          ORDER BY c.WindowEnd DESC")
                        .WithParameter("@lineId", lineId)
                        .WithParameter("@cutoffDate", cutoffDate);
                }
                else
                {
                    cosmosQuery = new QueryDefinition(
                        @"SELECT * FROM c
                          WHERE c.DocumentType = 'line-kpi'
                            AND c.WindowEnd >= @cutoffDate
                          ORDER BY c.WindowEnd DESC")
                        .WithParameter("@cutoffDate", cutoffDate);
                }

                var iterator = _container.GetItemQueryIterator<LineKPI>(cosmosQuery);
                var items = new List<LineKPI>();

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    items.AddRange(response);
                }

                var httpResponse = req.CreateResponse(HttpStatusCode.OK);
                await httpResponse.WriteAsJsonAsync(items);
                return httpResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching line KPIs");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
                return errorResponse;
            }
        }

        [Function("GetDeviceStatusHistory")]
        public async Task<HttpResponseData> GetDeviceStatusHistory(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "devices/{deviceId}/status-history")] HttpRequestData req,
            string deviceId)
        {
            _logger.LogInformation($"Getting status history for device: {deviceId}");

            try
            {
                var query = new QueryDefinition(
                    @"SELECT TOP 10 * FROM c
                      WHERE c.DocumentType = 'status-change'
                        AND c.DeviceId = @deviceId
                      ORDER BY c.Timestamp DESC")
                    .WithParameter("@deviceId", deviceId);

                var iterator = _container.GetItemQueryIterator<dynamic>(query);
                var items = new List<dynamic>();

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    items.AddRange(response);
                }

                var httpResponse = req.CreateResponse(HttpStatusCode.OK);
                await httpResponse.WriteAsJsonAsync(items);
                return httpResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching device status history");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
                return errorResponse;
            }
        }
    }
}
