using System.Threading.Channels;
using MQTTnet;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using NBomber.Contracts;
using NBomber.CSharp;

namespace NBomber.MQTT;

/// <summary>
/// Provides a wrapper around an <see cref="IMqttClient"/> for managing MQTT communication,
/// including connecting, subscribing, publishing, and receiving messages via a channel-based model.
/// </summary>
public class MqttClient : IDisposable
{
    private readonly Channel<Response<MqttApplicationMessage>> _channel = Channel.CreateUnbounded<Response<MqttApplicationMessage>>();
    private long _msgReceivedCount;

    /// <summary>
    /// Gets the underlying MQTT client used for communication with the MQTT broker.
    /// </summary>
    public IMqttClient Client { get; }

    /// <summary>
    /// Gets the total number of messages received by the client.
    /// </summary>
    public long MsgReceivedCount => _msgReceivedCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="MqttClient"/> class with the specified MQTT client.
    /// Registers a message handler that queues incoming messages for consumption.
    /// </summary>
    /// <param name="client">The MQTT client instance to wrap.</param>
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
    /// <param name="options">The client options for connecting to the broker.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>
    /// A <see cref="Response{T}"/> containing the result of the connection attempt.
    /// Returns a failed response if the result code indicates an error.
    /// </returns>
    public async Task<Response<MqttClientConnectResult>> Connect(MqttClientOptions options, CancellationToken cancellationToken = default)
    {
        var result = await Client.ConnectAsync(options, cancellationToken);
        
        return result.ResultCode == MqttClientConnectResultCode.Success
            ? Response.Ok(statusCode: result.ResultCode.ToString(), payload: result)
            
            : Response.Fail(payload: result, statusCode: result.ResultCode.ToString(), 
                message: $"Reason string: {result.ReasonString}\nResponse information: {result.ResponseInformation}");
    }

    /// <summary>
    /// Asynchronously subscribes the MQTT client to a topic with a given QoS level.
    /// </summary>
    /// <param name="topic">The topic to subscribe to.</param>
    /// <param name="qualityOfServiceLevel">The quality of service level (default is AtMostOnce).</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>
    /// A <see cref="Response{T}"/> containing the subscription result.
    /// </returns>
    public async Task<Response<MqttClientSubscribeResult>> Subscribe(
        string topic,
        MqttQualityOfServiceLevel qualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce,
        CancellationToken cancellationToken = default)
    {
        var result = await Client.SubscribeAsync(topic, qualityOfServiceLevel, cancellationToken);
        return Response.Ok(payload: result);
    }

    /// <summary>
    /// Asynchronously publishes an MQTT application message to the broker.
    /// </summary>
    /// <param name="applicationMessage">The application message to publish.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>
    /// A <see cref="Response{T}"/> containing the result of the publish operation.
    /// Returns a failed response if the publish was unsuccessful.
    /// </returns>
    public async Task<Response<MqttClientPublishResult>> Publish(MqttApplicationMessage applicationMessage, CancellationToken cancellationToken = default)
    {
        var result = await Client.PublishAsync(applicationMessage, cancellationToken);
        
        return result.IsSuccess
            ? Response.Ok(payload: result, statusCode: result.ReasonCode.ToString(), sizeBytes: applicationMessage.Payload.Length)
            : Response.Fail(payload: result, statusCode: result.ReasonCode.ToString(), message: result.ReasonString);
    }

    /// <summary>
    /// Asynchronously receives a queued MQTT message from the broker.
    /// </summary>
    /// <param name="token">Token used to cancel the operation.</param>
    /// <returns>
    /// A <see cref="Response{T}"/> containing the received <see cref="MqttApplicationMessage"/>.
    /// </returns>
    /// <exception cref="IgnoreMeasurementException">
    /// Thrown when the operation is cancelled by the token.
    /// </exception>
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
    /// Asynchronously disconnects the client from the MQTT broker, providing optional details.
    /// </summary>
    /// <param name="reason">The reason for disconnection.</param>
    /// <param name="reasonString">An optional textual reason for the disconnection.</param>
    /// <param name="sessionExpiryInterval">Optional session expiry interval in seconds.</param>
    /// <param name="userProperties">Optional list of user properties for the disconnect packet.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A success response indicating disconnection.</returns>
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