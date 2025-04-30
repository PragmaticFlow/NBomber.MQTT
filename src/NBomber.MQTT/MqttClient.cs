using System.Threading.Channels;
using MQTTnet;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using NBomber.Contracts;
using NBomber.CSharp;

namespace NBomber.MQTT;

public class MqttClient : IDisposable
{
    private readonly Channel<Response<MqttApplicationMessage>> _channel = Channel.CreateUnbounded<Response<MqttApplicationMessage>>();
    private long _msgReceivedCount;

    /// <summary>
    /// Gets the underlying MQTT channel used for communication with the message broker.
    /// </summary>
    public IMqttClient Client { get; }

    /// <summary>
    /// Gets the total number of messages received by the client.
    /// </summary>
    public long MsgReceivedCount => _msgReceivedCount;

    public MqttClient(IMqttClient client)
    {
        Client = client;
        Client.ApplicationMessageReceivedAsync += msg =>
        {
            Interlocked.Increment(ref _msgReceivedCount);
                
            var response = Response.Ok(sizeBytes: msg.ApplicationMessage.Payload.Length, payload: msg.ApplicationMessage);
            _channel.Writer.TryWrite(response);
            return Task.CompletedTask;
        };
    }

    /// <summary>
    /// Asynchronously connects the MQTT client to a broker using the specified options.
    /// </summary>
    public async Task<Response<MqttClientConnectResult>> Connect(MqttClientOptions options, CancellationToken cancellationToken = default)
    {
        var result = await Client.ConnectAsync(options, cancellationToken);
        
        return result.ResultCode == MqttClientConnectResultCode.Success
            ? Response.Ok(statusCode: result.ResultCode.ToString(), payload: result)
            
            : Response.Fail(payload: result, statusCode: result.ResultCode.ToString(), 
                message: $"Reason string: {result.ReasonString}\nResponse information: {result.ResponseInformation}");
    }

    /// <summary>
    /// Asynchronously subscribes to the specified MQTT topic with the given quality of service (QoS) level.
    /// </summary>
    public async Task<Response<MqttClientSubscribeResult>> Subscribe(
        string topic,
        MqttQualityOfServiceLevel qualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce,
        CancellationToken cancellationToken = default)
    {
        var result = await Client.SubscribeAsync(topic, qualityOfServiceLevel, cancellationToken);
        return Response.Ok(payload: result);
    }

    /// <summary>
    /// Asynchronously publishes an MQTT message to the broker.
    /// </summary>
    public async Task<Response<MqttClientPublishResult>> Publish(MqttApplicationMessage applicationMessage, CancellationToken cancellationToken = default)
    {
        var result = await Client.PublishAsync(applicationMessage, cancellationToken);
        
        return result.IsSuccess
            ? Response.Ok(payload: result, statusCode: result.ReasonCode.ToString(), sizeBytes: applicationMessage.Payload.Length)
            : Response.Fail(payload: result, statusCode: result.ReasonCode.ToString(), message: result.ReasonString);
    }

    /// <summary>
    /// Asynchronously receives an MQTT application message from the channel.
    /// </summary>
    public async ValueTask<Response<MqttApplicationMessage>> Receive(CancellationToken token)
    {
        try
        {
            var response = await _channel.Reader.ReadAsync(token);
            return response;
        }
        catch (OperationCanceledException)
        {
            throw new IgnoreMeasurementException();
        }
    }

    /// <summary>
    /// Asynchronously disconnects the MQTT client from the broker with optional disconnection details.
    /// </summary>
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

    /// <summary>
    /// Releases resources used by the MQTT client, including disposing of the client instance.
    /// </summary>
    public void Dispose()
    {   
        Client.Dispose();
    }
}