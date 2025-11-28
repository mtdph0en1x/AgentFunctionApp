using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Logging;
using AgentFunctionApp.Models;
using Newtonsoft.Json;
using System.Collections.Concurrent;

namespace AgentFunctionApp.Services
{
    public class DeviceTwinService
    {
        private readonly ILogger<DeviceTwinService> _logger;
        private readonly RegistryManager _registryManager;
        private readonly ConcurrentDictionary<string, DeviceMetadata> _deviceCache;
        private readonly ConcurrentDictionary<string, List<string>> _lineDeviceCache;
        private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(30);

        public DeviceTwinService(ILogger<DeviceTwinService> logger)
        {
            _logger = logger;
            var iotHubConnectionString = Environment.GetEnvironmentVariable("IoTHubConnectionString") ?? "";
            _registryManager = RegistryManager.CreateFromConnectionString(iotHubConnectionString);
            _deviceCache = new ConcurrentDictionary<string, DeviceMetadata>();
            _lineDeviceCache = new ConcurrentDictionary<string, List<string>>();
        }

        public async Task<DeviceMetadata> GetDeviceMetadataAsync(string deviceId)
        {
            // Check cache first
            if (_deviceCache.TryGetValue(deviceId, out var cachedMetadata) &&
                !cachedMetadata.IsExpired(_cacheExpiry))
            {
                return cachedMetadata;
            }

            try
            {
                // Fetch from IoT Hub
                var twin = await _registryManager.GetTwinAsync(deviceId);
                var metadata = ExtractMetadataFromTwin(twin, deviceId);

                // Cache the result
                _deviceCache.AddOrUpdate(deviceId, metadata, (key, old) => metadata);

                _logger.LogInformation($"Retrieved device metadata for {deviceId}: Type={metadata.DeviceType}, Line={metadata.LineId}");
                return metadata;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to retrieve device twin for {deviceId}: {ex.Message}");

                // Return fallback metadata
                return GetFallbackMetadata(deviceId);
            }
        }

        public async Task<DeviceType> GetDeviceTypeAsync(string deviceId)
        {
            var metadata = await GetDeviceMetadataAsync(deviceId);
            return metadata.DeviceType;
        }

        public async Task<List<string>> GetDevicesInLineAsync(string lineId)
        {
            // Check cache first
            if (_lineDeviceCache.TryGetValue(lineId, out var cachedDevices))
            {
                return cachedDevices;
            }

            try
            {
                // Querry all devices and filter by lineId
                var query = _registryManager.CreateQuery("SELECT * FROM devices");
                var devices = new List<string>();

                while (query.HasMoreResults)
                {
                    var twins = await query.GetNextAsTwinAsync();
                    foreach (var twin in twins)
                    {
                        if (twin.Properties?.Reported?.Contains("lineId") == true)
                        {
                            var reportedLineId = twin.Properties.Reported["lineId"]?.ToString();
                            if (reportedLineId == lineId)
                            {
                                devices.Add(twin.DeviceId);
                            }
                        }
                    }
                }

                // Cache the result
                _lineDeviceCache.AddOrUpdate(lineId, devices, (key, old) => devices);

                _logger.LogInformation($"Retrieved {devices.Count} devices for line {lineId}");
                return devices;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to retrieve devices for line {lineId}: {ex.Message}");

                // Return fallback mapping
                return GetFallbackLineDevices(lineId);
            }
        }

        public void ClearCache()
        {
            _deviceCache.Clear();
            _lineDeviceCache.Clear();
            _logger.LogInformation("Device twin cache cleared");
        }

        public async Task UpdateDeviceTwinDesiredPropertyAsync(string deviceId, string propertyName, object propertyValue)
        {
            try
            {
                var twin = await _registryManager.GetTwinAsync(deviceId);

                var patch = new
                {
                    properties = new
                    {
                        desired = new Dictionary<string, object>
                        {
                            { propertyName, propertyValue }
                        }
                    }
                };

                var patchJson = JsonConvert.SerializeObject(patch);
                await _registryManager.UpdateTwinAsync(deviceId, patchJson, twin.ETag);

                _logger.LogInformation($"Updated device twin for {deviceId}: {propertyName} = {propertyValue}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to update device twin for {deviceId}: {ex.Message}");
                throw;
            }
        }

        private DeviceMetadata ExtractMetadataFromTwin(Twin twin, string deviceId)
        {
            var metadata = new DeviceMetadata
            {
                DeviceId = deviceId,
                LastUpdated = DateTime.UtcNow
            };

            // Extract lineId from reported properties
            if (twin.Properties?.Reported?.Contains("lineId") == true)
            {
                metadata.LineId = twin.Properties.Reported["lineId"]?.ToString();
            }

            // Extract lineName from reported properties
            if (twin.Properties?.Reported?.Contains("lineName") == true)
            {
                metadata.LineName = twin.Properties.Reported["lineName"]?.ToString();
            }

            // Determine device type from device name
            metadata.DeviceType = DetermineDeviceTypeFromName(deviceId);

            // Extract additional metadata 
            if (twin.Properties?.Reported?.Contains("status") == true)
            {
                var statusObj = twin.Properties.Reported["status"];
                if (statusObj is Newtonsoft.Json.Linq.JObject statusJson)
                {
                    metadata.ConnectionStatus = statusJson["connectionStatus"]?.ToString();
                    metadata.Health = statusJson["health"]?.ToString();
                    metadata.State = statusJson["state"]?.ToString();
                }
            }

            return metadata;
        }

        private DeviceType DetermineDeviceTypeFromName(string deviceId)
        {
            return deviceId.ToLower() switch
            {
                var id when id.Contains("press") => DeviceType.Press,
                var id when id.Contains("conveyor") => DeviceType.Conveyor,
                var id when id.Contains("quality") => DeviceType.QualityStation,
                var id when id.Contains("compressor") => DeviceType.Compressor,
                // Legacy mappings
                //"device1" => DeviceType.Press,
                //"device2" => DeviceType.Conveyor,
                //"device3" => DeviceType.QualityStation,
                //_ => DeviceType.Press // Default fallback
            };
        }

        private DeviceMetadata GetFallbackMetadata(string deviceId)
        {
            return new DeviceMetadata
            {
                DeviceId = deviceId,
                DeviceType = DetermineDeviceTypeFromName(deviceId),
                LineId = "ProductionLine1", // Default fallback
                LineName = "Primary Assembly Line",
                ConnectionStatus = "unknown",
                Health = "unknown",
                State = "unknown",
                LastUpdated = DateTime.UtcNow
            };
        }

        private List<string> GetFallbackLineDevices(string lineId)
        {

            _logger.LogWarning($"Using fallback device mapping for line {lineId} - device twin query failed");

            // Extract line number from lineId 
            var lineNumber = lineId.Replace("ProductionLine", "").Trim();

            if (!string.IsNullOrEmpty(lineNumber))
            {
                return new List<string>
                {
                    $"Press{lineNumber}",
                    $"Conveyor{lineNumber}",
                    $"QualityStation{lineNumber}",
                    $"Compressor{lineNumber}"
                };
            }

            _logger.LogError($"Cannot determine devices for line {lineId} - invalid line format");
            return new List<string>();
        }
    }

    public class DeviceMetadata
    {
        public string DeviceId { get; set; }
        public DeviceType DeviceType { get; set; }
        public string LineId { get; set; }
        public string LineName { get; set; }
        public string ConnectionStatus { get; set; }
        public string Health { get; set; }
        public string State { get; set; }
        public DateTime LastUpdated { get; set; }

        public bool IsExpired(TimeSpan expiry)
        {
            return DateTime.UtcNow - LastUpdated > expiry;
        }
    }
}