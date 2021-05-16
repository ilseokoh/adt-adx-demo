using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Azure;
using Azure.Core.Pipeline;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ADTADXDemo.Function
{
    public static class ADTPropChangeProcessor
    {
        private static string adtServiceUrl = Environment.GetEnvironmentVariable("ADT_SERVICE_URL");
        private static readonly HttpClient httpClient = new HttpClient();

        [FunctionName("ADTPropChangeProcessor")]
        public static async Task Run([EventHubTrigger("adt-sensor-prop-changed", Connection = "IoTMREventHubConnString")] EventData[] events, ILogger log)
        {
            var exceptions = new List<Exception>();

            DigitalTwinsClient client;

            // Authenticate on ADT APIs
            try
            {
                var credentials = new DefaultAzureCredential();
                client = new DigitalTwinsClient(new Uri(adtServiceUrl), credentials, new DigitalTwinsClientOptions { Transport = new HttpClientTransport(httpClient) });
                log.LogInformation("ADT service client connection created. ADTPropChangeToHL2Processor");
            }
            catch (Exception e)
            {
                log.LogError($"ADT service client connection failed. {e}");
                exceptions.Add(e);
                return;
            }
            if (client != null)
            {
                foreach (EventData eventData in events)
                {
                    try
                    {
                        JObject message = (JObject)JsonConvert.DeserializeObject(Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count));
                        log.LogInformation("Reading event:" + message.ToString());

                        // only particle and pressure sensor
                        var modelId = message["modelId"].ToString();
                        var twinId = eventData.Properties["cloudEvents:subject"].ToString();

                        var parentTwin = await AdtUtilities.FindParentByQueryAsync(client, twinId, log);
                        var parent = (JObject)JsonConvert.DeserializeObject(parentTwin.Contents["Parent"].ToString());

                        double valueMin, valueMax;
                        if (modelId == "dtmi:com:iloh:demo:sensor:particle;1")
                        {
                            // get parent to have min and max value 
                            valueMin = double.Parse(parent["ParticleMin"].ToString());
                            valueMax = double.Parse(parent["ParticleMax"].ToString());
                        }
                        else if (modelId == "dtmi:com:iloh:demo:sensor:pressure;1")
                        {
                            valueMin = double.Parse(parent["PressureMin"].ToString());
                            valueMax = double.Parse(parent["PressureMax"].ToString());
                        }
                        else
                        {
                            return;
                        }

                        foreach (var operation in message["patch"])
                        {
                            if (operation["op"].ToString() == "replace" || operation["op"].ToString() == "add")
                            {
                                string propertyPath = ((string)operation["path"]);

                                if (propertyPath.Equals("/Value"))
                                {
                                    // compare the value with min/max 
                                    var sensorValue = operation["value"].Value<double>();
                                    var sensorStatus = sensorValue >= valueMin && sensorValue <= valueMax;

                                    log.LogInformation($"sensorValue: {sensorValue}, sensorStatus: {sensorStatus}");

                                    // particleStatus 가 True라도 모든 센서가 True 여야 True
                                    // Retreive all the sensor values in the room
                                    var sensorValues = await GetAllSensorValuesInRoomAsync(client, parent["$dtId"].ToString(), modelId, log);
                                    var roomStatus = sensorValues.Where(x => x < valueMin || x > valueMax).Any();

                                    // update RoomStatus 
                                    if (modelId == "dtmi:com:iloh:demo:sensor:particle;1")
                                        await AdtUtilities.UpdateTwinPropertyAsync(client, parent["$dtId"].ToString(), "/ptstatus", !roomStatus, log);
                                    else if (modelId == "dtmi:com:iloh:demo:sensor:pressure;1")
                                        await AdtUtilities.UpdateTwinPropertyAsync(client, parent["$dtId"].ToString(), "/dpstatus", !roomStatus, log);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        // We need to keep processing the rest of the batch - capture this exception and continue.
                        // Also, consider capturing details of the message that failed processing so it can be processed again later.
                        exceptions.Add(e);
                    }
                }
            }

            // Once processing of the batch is complete, if any messages in the batch failed processing throw an exception so that there is a record of the failure.

            if (exceptions.Count > 1)
                throw new AggregateException(exceptions);

            if (exceptions.Count == 1)
                throw exceptions.Single();
        }
        public static async Task<IEnumerable<double>> GetAllSensorValuesInRoomAsync(DigitalTwinsClient client, string roomdtid, string sensormodelid, ILogger log)
        {
            var query = 
                "SELECT Sensor " +
                "FROM DIGITALTWINS Room " +
                "JOIN Sensor RELATED Room.hasCapability " +
                $"WHERE IS_OF_MODEL(Sensor, '{sensormodelid}') AND Room.$dtId = '{roomdtid}' AND IS_PRIMITIVE(Sensor.Value)";

            try
            {
                List<double> values = new List<double>();
                AsyncPageable<BasicDigitalTwin> twins = client.QueryAsync<BasicDigitalTwin>(query);
                await foreach(BasicDigitalTwin twin in twins)
                {
                    var val = (JObject)JsonConvert.DeserializeObject(twin.Contents["Sensor"].ToString());
                    values.Add(double.Parse(val["Value"].ToString()));
                }
                return values;
            }
            catch (RequestFailedException exc)
            {
                log.LogError($"*** Error in retrieving parent:{exc.Status}/{exc.Message}");
                throw exc;
            }
        }
    }
}
