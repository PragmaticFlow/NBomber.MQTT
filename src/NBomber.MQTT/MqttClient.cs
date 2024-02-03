using Microsoft.FSharp.Core;
using MQTTnet.Client;
using NBomber.Contracts;
using Microsoft.IO;

namespace NBomber.MQTT;

public class MqttClient(IMqttClient client) : IDisposable
{
    private static readonly RecyclableMemoryStreamManager MsStreamManager = new();
    
    public IMqttClient Client { get; } = client;

    public async Task<Response<MqttClientPublishResult>> Publish(string topic, string text)
    {
        try
        {
            var result = await client.PublishStringAsync(topic, text);
            var payload = FSharpOption<MqttClientPublishResult>.Some(result);
            if (result.IsSuccess)
            {
                return new Response<MqttClientPublishResult>(statusCode: result.ReasonCode.ToString(), isError: false,
                    sizeBytes: text.Length, message: "", payload: payload);
            }
            
            return new Response<MqttClientPublishResult>(statusCode: result.ReasonCode.ToString(), isError: true,
                sizeBytes: 0, message: result.ReasonString, payload: FSharpOption<MqttClientPublishResult>.Some(null));
        }
        catch (Exception e)
        {
            return new Response<MqttClientPublishResult>(statusCode: "128", isError: true,
                sizeBytes: 0, message: e.Message, FSharpOption<MqttClientPublishResult>.Some(null));
        }
    }
    
    public async Task<Response<MqttClientPublishResult>> Publish(string topic, byte[] payload)
    {
        try
        {
            var result = await client.PublishBinaryAsync(topic, payload);
            var payloadForResponse = FSharpOption<MqttClientPublishResult>.Some(result);
            if (result.IsSuccess)
            {
                return new Response<MqttClientPublishResult>(statusCode: result.ReasonCode.ToString(), isError: false,
                    sizeBytes: payload.Length, message: "", payload: payloadForResponse);
            }
            
            return new Response<MqttClientPublishResult>(statusCode: result.ReasonCode.ToString(), isError: true,
                sizeBytes: 0, message: result.ReasonString, payload: FSharpOption<MqttClientPublishResult>.Some(null));
        }
        catch (Exception e)
        {
            return new Response<MqttClientPublishResult>(statusCode: "128", isError: true,
                sizeBytes: 0, message: e.Message, FSharpOption<MqttClientPublishResult>.Some(null));
        }
    }
    
    public void Dispose()
    {   
        client.Dispose();
    }
}