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
        var result = await client.PublishStringAsync(topic, text);
        var payload = FSharpOption<MqttClientPublishResult>.Some(result); 
        return new Response<MqttClientPublishResult>(result.ReasonCode.ToString(), false, text.Length, "", payload) ;
    }
    
    public async Task<Response<MqttClientPublishResult>> Publish(string topic, byte[] payload)
    {
        var result = await client.PublishBinaryAsync(topic, payload);
        var payloadForResponse = FSharpOption<MqttClientPublishResult>.Some(result); 
        return new Response<MqttClientPublishResult>(result.ReasonCode.ToString(), false, payload.Length, "", payloadForResponse) ;
    }
    
    public void Dispose()
    {   
        client.Dispose();
    }
}