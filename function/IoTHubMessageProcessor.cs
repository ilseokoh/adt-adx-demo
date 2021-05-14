using IoTHubTrigger = Microsoft.Azure.WebJobs.EventHubTriggerAttribute;

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.EventHubs;
using System.Text;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using System;
using Azure.Core.Pipeline;
using Newtonsoft.Json;
using Azure;
using System.Threading.Tasks;

namespace ADTADXDemo.Function
{
    public static class IoTHubMessageProcessor
    {
        private static HttpClient httpClient = new HttpClient();
        private static string adtServiceUrl = Environment.GetEnvironmentVariable("ADT_SERVICE_URL");

        [FunctionName("IoTHubMessageProcessor")]
        public static async Task Run([IoTHubTrigger("messages/events", Connection = "IoTHubBuiltInConnection", ConsumerGroup = "function")]EventData message, ILogger log)
        {
            DigitalTwinsClient client;
            try
            {
                //Authenticate with Digital Twins
                var credentials = new DefaultAzureCredential();
                client = new DigitalTwinsClient(
                    new Uri(adtServiceUrl),
                    credentials,
                    new DigitalTwinsClientOptions { Transport = new HttpClientTransport(httpClient) }
                );

                log.LogInformation($"ADT service client connection created.");
            }
            catch (Exception e)
            {
                log.LogError($"ADT service client connection failed. {e}");
                return;
            }

            if (client != null)
            {
                if (message != null && message.Body != null)
                {
                    var msg = Encoding.UTF8.GetString(message.Body.Array);
                    log.LogInformation($"C# IoT Hub trigger function processed a message: {msg}");

                    // Get device Id
                    string deviceId = (string)message.SystemProperties["iothub-connection-device-id"];

                    try
                    {
                        var sensordata = JsonConvert.DeserializeObject<SensorMessage>(msg);

                        JsonPatchDocument updateTwinData = new JsonPatchDocument();
                        updateTwinData.AppendReplace<string>("/SensorName", sensordata.SensorName);
                        updateTwinData.AppendReplace<float>("/SensorValue", (float)sensordata.SensorValue);

                        await client.UpdateDigitalTwinAsync(deviceId, updateTwinData);   //deviceId == dtid 
                    }
                    catch(JsonException jex)
                    {
                        log.LogError("Message parsing error.", jex);
                    }
                    catch (RequestFailedException exc)
                    {
                        log.LogInformation($"{deviceId}*** Error:{exc.Status}/{exc.Message}");

                        // 최초 Update 할 경우 Replace를 쓰면 오류발생. Add를 써야함. 
                        var sensordata = JsonConvert.DeserializeObject<SensorMessage>(msg);

                        JsonPatchDocument addTwinData = new JsonPatchDocument();
                        addTwinData.AppendAdd<string>("/SensorName", sensordata.SensorName);
                        addTwinData.AppendAdd<float>("/SensorValue", (float)sensordata.SensorValue);

                        await client.UpdateDigitalTwinAsync(deviceId, addTwinData);
                        
                    }
                    catch(Exception ex)
                    {
                        log.LogError("Update Twin error.", ex);
                    }
                }
            }
        }
    }
}