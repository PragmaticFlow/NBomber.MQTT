using Microsoft.FSharp.Core;
using MQTTnet.Client;
using NBomber.Contracts;

namespace NBomber.MQTT;

public class MqttClient(IMqttClient client) : IDisposable
{
    public IMqttClient Client { get; } = client;

    public async Task<Response<MqttClientPublishResult>> Publish(string topic, string text)
    {
        var result = await client.PublishStringAsync(topic, text);
        var payload = FSharpOption<MqttClientPublishResult>.Some(result);
        if (result.IsSuccess)
        {
            return new Response<MqttClientPublishResult>(statusCode: result.ReasonCode.ToString(), isError: false,
                sizeBytes: text.Length, message: string.Empty, payload: payload);
        }
            
        return new Response<MqttClientPublishResult>(statusCode: result.ReasonCode.ToString(), isError: true,
            sizeBytes: 0, message: result.ReasonString, payload: payload);
    }
    
    public async Task<Response<MqttClientPublishResult>> Publish(string topic, byte[] payload)
    {
        var result = await client.PublishBinaryAsync(topic, payload);
        var payloadForResponse = FSharpOption<MqttClientPublishResult>.Some(result);
        if (result.IsSuccess)
        {
            return new Response<MqttClientPublishResult>(statusCode: result.ReasonCode.ToString(), isError: false,
                sizeBytes: payload.Length, message: string.Empty, payload: payloadForResponse);
        }
            
        return new Response<MqttClientPublishResult>(statusCode: result.ReasonCode.ToString(), isError: true,
            sizeBytes: 0, message: result.ReasonString, payload: payloadForResponse);
    }
    
    public void Dispose()
    {   
        client.Dispose();
    }
}