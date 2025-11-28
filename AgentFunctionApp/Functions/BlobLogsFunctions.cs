using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using Azure.Storage.Blobs;
using Newtonsoft.Json;
using AgentFunctionApp.Models;

namespace AgentFunctionApp.Functions
{
    public class BlobLogsFunctions
    {
        private readonly ILogger _logger;
        private readonly BlobServiceClient _blobServiceClient;

        public BlobLogsFunctions(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<BlobLogsFunctions>();
            var connectionString = Environment.GetEnvironmentVariable("BLOB_STORAGE_CONNECTION_STRING");
            _blobServiceClient = new BlobServiceClient(connectionString);
        }

        [Function("GetBlobLogs")]
        public async Task<HttpResponseData> GetBlobLogs(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "logs")] HttpRequestData req)
        {
            _logger.LogInformation("Getting blob logs");

            try
            {
                // Parse query parameters
                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                var deviceId = query["deviceId"];
                var date = query["date"]; // format: 2025-10-04
                var container = query["container"] ?? "telemetry-qcs"; // default container

                var containerClient = _blobServiceClient.GetBlobContainerClient(container);
                var logs = new List<DeviceTelemetry>();

                // Blob structure: deviceId/YYYY/MM/DD/HH.json
                string prefix = "";
                if (!string.IsNullOrEmpty(deviceId) && !string.IsNullOrEmpty(date))
                {
                    var dateParts = date.Split('-'); // 2025-10-04 -> [2025, 10, 04]
                    prefix = $"{deviceId}/{dateParts[0]}/{dateParts[1]}/{dateParts[2]}/";
                }
                else if (!string.IsNullOrEmpty(deviceId))
                {
                    prefix = $"{deviceId}/";
                }

                await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: prefix))
                {
                    var blobClient = containerClient.GetBlobClient(blobItem.Name);
                    var downloadResult = await blobClient.DownloadContentAsync();
                    var content = downloadResult.Value.Content.ToString();

                    // Parse JSONL
                    var lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            var log = JsonConvert.DeserializeObject<DeviceTelemetry>(line);
                            if (log != null)
                            {
                                logs.Add(log);
                            }
                        }
                    }
                }

                var httpResponse = req.CreateResponse(HttpStatusCode.OK);
                await httpResponse.WriteAsJsonAsync(logs);
                return httpResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching blob logs");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
                return errorResponse;
            }
        }

        [Function("ListBlobDates")]
        public async Task<HttpResponseData> ListBlobDates(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "logs/dates")] HttpRequestData req)
        {
            _logger.LogInformation("Listing available log dates");

            try
            {
                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                var deviceId = query["deviceId"];
                var container = query["container"] ?? "telemetry-qcs";

                var containerClient = _blobServiceClient.GetBlobContainerClient(container);
                var dates = new HashSet<string>();

                string prefix = string.IsNullOrEmpty(deviceId) ? "" : $"{deviceId}/";

                await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: prefix))
                {
                    // Extract date
                    var parts = blobItem.Name.Split('/');
                    if (parts.Length >= 4)
                    {
                        // Reconstruct date as YYYY-MM-DD
                        var dateStr = $"{parts[1]}-{parts[2]}-{parts[3]}";
                        dates.Add(dateStr);
                    }
                }

                var httpResponse = req.CreateResponse(HttpStatusCode.OK);
                await httpResponse.WriteAsJsonAsync(dates.OrderByDescending(d => d).ToList());
                return httpResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing blob dates");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
                return errorResponse;
            }
        }
    }
}
