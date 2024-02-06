using Microsoft.FSharp.Core;
using MQTTnet.Client;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using NBomber.Contracts;
using System.Threading.Channels;
using MQTTnet;

namespace NBomber.MQTT;

public class MqttClient : IDisposable
{
    public IMqttClient Client { get; }

    private readonly Channel<Response<MqttApplicationMessage>> _channel;
    
    public MqttClient (IMqttClient client)
    {
        Client = client;
        _channel = Channel.CreateUnbounded<Response<MqttApplicationMessage>>();
        Client.ApplicationMessageReceivedAsync +=  msg => 
            _channel.Writer.WriteAsync(new Response<MqttApplicationMessage>(statusCode: string.Empty, isError: false,
                sizeBytes: 0, message: string.Empty, payload: msg.ApplicationMessage))
                .AsTask();
    }
    
    public async Task<Response<MqttClientConnectResult>> Connect(MqttClientOptions options,
        CancellationToken cancellationToken = default)
    {
        var result = await Client.ConnectAsync(options, cancellationToken);
        if (result.ResultCode == MqttClientConnectResultCode.Success)
        {
            return new Response<MqttClientConnectResult>(statusCode: result.ResultCode.ToString(), isError: false,
                0, message: string.Empty, payload: result);
        }
        
        return new Response<MqttClientConnectResult>(statusCode: result.ResultCode.ToString(), isError: true,
            0, message: $"Reason string: {result.ReasonString}\nResponse information: {result.ResponseInformation}", 
            payload: result);
    }

    public async Task<Response<MqttClientSubscribeResult>> Subscribe(
        string topic,
        MqttQualityOfServiceLevel qualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce,
        CancellationToken cancellationToken = default)
    {
        var result = await Client.SubscribeAsync(topic, qualityOfServiceLevel, cancellationToken);
        
        return new Response<MqttClientSubscribeResult>(statusCode: string.Empty, isError: false, sizeBytes: 0,
            message: string.Empty, payload: result);
    }
    
    public async Task<Response<MqttClientPublishResult>> Publish(MqttApplicationMessage applicationMessage,
        CancellationToken cancellationToken = default)
    {
        var result = await Client.PublishAsync(applicationMessage, cancellationToken);
        var payloadForResponse = FSharpOption<MqttClientPublishResult>.Some(result);
        if (result.IsSuccess)
        {
            return new Response<MqttClientPublishResult>(statusCode: result.ReasonCode.ToString(), isError: false,
                sizeBytes: applicationMessage.PayloadSegment.Count, message: string.Empty, payload: payloadForResponse);
        }
            
        return new Response<MqttClientPublishResult>(statusCode: result.ReasonCode.ToString(), isError: true,
            sizeBytes: 0, message: result.ReasonString, payload: payloadForResponse);
    }

    public async ValueTask<Response<MqttApplicationMessage>> Receive() =>  await _channel.Reader.ReadAsync();
    
    public async Task<Response<object>> Disconnect(
        MqttClientDisconnectOptionsReason reason = MqttClientDisconnectOptionsReason.NormalDisconnection,
        string reasonString = null,
        uint sessionExpiryInterval = 0,
        List<MqttUserProperty> userProperties = null,
        CancellationToken cancellationToken = default)
    {
        await Client.DisconnectAsync(reason, reasonString, sessionExpiryInterval, userProperties, cancellationToken);
        
        return new Response<object>(statusCode: string.Empty, isError: Client.IsConnected, sizeBytes: 0,
            message: string.Empty, payload: null);
    }
    
    public void Dispose()
    {   
        Client.Dispose();
    }
}