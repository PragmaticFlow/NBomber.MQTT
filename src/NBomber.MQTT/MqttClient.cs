using System.Threading.Channels;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using NBomber.Contracts;
using NBomber.CSharp;

namespace NBomber.MQTT;

public class MqttClient : IDisposable
{
    public IMqttClient Client { get; }

    private readonly Channel<Response<MqttApplicationMessage>> _channel;
    
    public MqttClient (IMqttClient client)
    {
        Client = client;
        _channel = Channel.CreateUnbounded<Response<MqttApplicationMessage>>();
        
        Client.ApplicationMessageReceivedAsync += msg =>
        {
            var response = Response.Ok(sizeBytes:msg.ApplicationMessage.PayloadSegment.Count, payload:msg.ApplicationMessage);
            return _channel.Writer.WriteAsync(response).AsTask();
        };
    }
    
    public async Task<Response<MqttClientConnectResult>> Connect(MqttClientOptions options, CancellationToken cancellationToken = default)
    {
        var result = await Client.ConnectAsync(options, cancellationToken);
        
        return result.ResultCode == MqttClientConnectResultCode.Success
            ? Response.Ok(statusCode: result.ResultCode.ToString(), payload: result)
            
            : Response.Fail(payload: result, statusCode: result.ResultCode.ToString(), 
                message: $"Reason string: {result.ReasonString}\nResponse information: {result.ResponseInformation}");
    }

    public async Task<Response<MqttClientSubscribeResult>> Subscribe(
        string topic,
        MqttQualityOfServiceLevel qualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce,
        CancellationToken cancellationToken = default)
    {
        var result = await Client.SubscribeAsync(topic, qualityOfServiceLevel, cancellationToken);
        return Response.Ok(payload: result);
    }
    
    public async Task<Response<MqttClientPublishResult>> Publish(MqttApplicationMessage applicationMessage, CancellationToken cancellationToken = default)
    {
        var result = await Client.PublishAsync(applicationMessage, cancellationToken);
        
        return result.IsSuccess
            ? Response.Ok(payload: result, statusCode: result.ReasonCode.ToString(), sizeBytes: applicationMessage.PayloadSegment.Count)
            : Response.Fail(payload: result, statusCode: result.ReasonCode.ToString(), message: result.ReasonString);
    }

    public ValueTask<Response<MqttApplicationMessage>> Receive() => _channel.Reader.ReadAsync();
    
    public async Task<Response<object>> Disconnect(
        MqttClientDisconnectOptionsReason reason = MqttClientDisconnectOptionsReason.NormalDisconnection,
        string? reasonString = null,
        uint sessionExpiryInterval = 0,
        List<MqttUserProperty>? userProperties = null,
        CancellationToken cancellationToken = default)
    {
        await Client.DisconnectAsync(reason, reasonString, sessionExpiryInterval, userProperties, cancellationToken);
        return Response.Ok();
    }
    
    public void Dispose()
    {   
        Client.Dispose();
    }
}