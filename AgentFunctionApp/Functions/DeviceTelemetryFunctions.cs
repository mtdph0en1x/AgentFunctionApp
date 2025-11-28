using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using System.Net;
using AgentFunctionApp.Models;
using AgentFunctionApp.Services;
using Newtonsoft.Json;
using System.Text.Json;

namespace AgentFunctionApp.Functions
{
    public class DeviceTelemetryFunctions
    {
        private readonly ILogger _logger;
        private readonly CosmosClient _cosmosClient;
        private readonly Container _container;
        private readonly DeviceTwinService _deviceTwinService;

        public DeviceTelemetryFunctions(ILoggerFactory loggerFactory, DeviceTwinService deviceTwinService)
        {
            _logger = loggerFactory.CreateLogger<DeviceTelemetryFunctions>();
            _deviceTwinService = deviceTwinService;

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
                // Get optional querry parameters
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

        [Function("UpdateDeviceTwin")]
        public async Task<HttpResponseData> UpdateDeviceTwin(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "devices/{deviceId}/twin")] HttpRequestData req,
            string deviceId)
        {
            _logger.LogInformation($"Updating device twin for: {deviceId}");

            try
            {
                // Parse request body
                string requestBody;
                using (var reader = new StreamReader(req.Body))
                {
                    requestBody = await reader.ReadToEndAsync();
                }

                var updateRequest = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(requestBody);

                if (!updateRequest.TryGetProperty("propertyName", out var propertyNameElement) ||
                    !updateRequest.TryGetProperty("propertyValue", out var propertyValueElement))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { error = "Request must contain propertyName and propertyValue" });
                    return badRequest;
                }

                var propertyName = propertyNameElement.GetString();

                // Extract the value based on its type
                object propertyValue;
                switch (propertyValueElement.ValueKind)
                {
                    case System.Text.Json.JsonValueKind.Number:
                        propertyValue = propertyValueElement.GetDouble();
                        break;
                    case System.Text.Json.JsonValueKind.String:
                        propertyValue = propertyValueElement.GetString();
                        break;
                    case System.Text.Json.JsonValueKind.True:
                    case System.Text.Json.JsonValueKind.False:
                        propertyValue = propertyValueElement.GetBoolean();
                        break;
                    default:
                        propertyValue = propertyValueElement.ToString();
                        break;
                }

                _logger.LogInformation($"Sending to device twin - Property: {propertyName}, Value: {propertyValue}, Type: {propertyValue?.GetType().Name}");

                // Update device twin desired property
                await _deviceTwinService.UpdateDeviceTwinDesiredPropertyAsync(deviceId, propertyName, propertyValue);

                var httpResponse = req.CreateResponse(HttpStatusCode.OK);
                await httpResponse.WriteAsJsonAsync(new {
                    success = true,
                    message = $"Device twin updated: {propertyName} = {propertyValue}"
                });
                return httpResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating device twin for {deviceId}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
                return errorResponse;
            }
        }

        [Function("GetErrors")]
        public async Task<HttpResponseData> GetErrors(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "errors")] HttpRequestData req)
        {
            _logger.LogInformation("Getting error events");

            try
            {
                // Get query parameters
                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                var deviceId = query["deviceId"];
                var lineId = query["lineId"];
                var daysBack = int.TryParse(query["daysBack"], out var days) ? days : 7;

                var cutoffDate = DateTime.UtcNow.AddDays(-daysBack);

                // Build query based on filters
                string queryText;
                if (!string.IsNullOrEmpty(deviceId))
                {
                    queryText = @"SELECT * FROM c
                                  WHERE c.DocumentType = 'error-event'
                                  AND c.DeviceId = @deviceId
                                  AND c.Timestamp >= @cutoffDate
                                  ORDER BY c.Timestamp DESC";
                }
                else if (!string.IsNullOrEmpty(lineId))
                {
                    queryText = @"SELECT * FROM c
                                  WHERE c.DocumentType = 'error-event'
                                  AND c.LineId = @lineId
                                  AND c.Timestamp >= @cutoffDate
                                  ORDER BY c.Timestamp DESC";
                }
                else
                {
                    queryText = @"SELECT * FROM c
                                  WHERE c.DocumentType = 'error-event'
                                  AND c.Timestamp >= @cutoffDate
                                  ORDER BY c.Timestamp DESC";
                }

                var queryDefinition = new QueryDefinition(queryText)
                    .WithParameter("@cutoffDate", cutoffDate);

                if (!string.IsNullOrEmpty(deviceId))
                {
                    queryDefinition.WithParameter("@deviceId", deviceId);
                }
                if (!string.IsNullOrEmpty(lineId))
                {
                    queryDefinition.WithParameter("@lineId", lineId);
                }

                var iterator = _container.GetItemQueryIterator<ErrorEvent>(queryDefinition);
                var errors = new List<ErrorEvent>();

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    errors.AddRange(response);
                }

                _logger.LogInformation($"Retrieved {errors.Count} error events");

                var httpResponse = req.CreateResponse(HttpStatusCode.OK);
                await httpResponse.WriteAsJsonAsync(errors);
                return httpResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving error events");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
                return errorResponse;
            }
        }
    }
}
