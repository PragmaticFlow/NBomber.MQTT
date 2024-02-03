using Microsoft.FSharp.Core;
using MQTTnet.Client;
using NBomber.Contracts;

namespace NBomber.MQTT;

public class MqttClient(IMqttClient client) : IDisposable
{
    public IMqttClient Client { get; } = client;

    public async Task<Response<MqttClientConnectResult>> Connect(MqttClientOptions options,
        CancellationToken cancellationToken = default)
    {
        var result = await client.ConnectAsync(options, cancellationToken);
        if (result.ResultCode == MqttClientConnectResultCode.Success)
        {
            return new Response<MqttClientConnectResult>(statusCode: result.ResultCode.ToString(), isError: false,
                0, message: string.Empty, payload: result);
        }
        
        return new Response<MqttClientConnectResult>(statusCode: result.ResultCode.ToString(), isError: true,
            0, message: $"Reason string: {result.ReasonString}\nResponse information: {result.ResponseInformation}", 
            payload: result);
    }
    
    public async Task<Response<MqttClientConnectResult>> Connect(string socketServer, 
        CancellationToken cancellationToken = default)
    {
        var options = new MqttClientOptionsBuilder().WithWebSocketServer(optionsBuilder => 
        {
            optionsBuilder.WithUri(socketServer);
        }).WithCleanSession().Build();
        
        var result = await client.ConnectAsync(options, cancellationToken);
        if (result.ResultCode == MqttClientConnectResultCode.Success)
        {
            return new Response<MqttClientConnectResult>(statusCode: result.ResultCode.ToString(), isError: false,
                0, message: string.Empty, payload: result);
        }
        
        return new Response<MqttClientConnectResult>(statusCode: result.ResultCode.ToString(), isError: true,
            0, message: $"Reason string: {result.ReasonString}\nResponse information: {result.ResponseInformation}", 
            payload: result);
    }

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

    public async Task<Response<object>> Disconnect()
    {
        await client.DisconnectAsync();
        return new Response<object>(statusCode: string.Empty, isError: !client.IsConnected, sizeBytes: 0,
            message: string.Empty, payload: null);
    }
    
    public async Task<Response<object>> Disconnect(MqttClientDisconnectOptions options, 
        CancellationToken cancellationToken = default)
    {
        await client.DisconnectAsync(options, cancellationToken);
        return new Response<object>(statusCode: string.Empty, isError: !client.IsConnected, sizeBytes: 0,
            message: string.Empty, payload: null);
    }
    
    public async Task<Response<object>> Disconnect(MqttClientDisconnectOptionsReason reason, string reasonString, 
        CancellationToken cancellationToken = default)
    {
        var options = new MqttClientDisconnectOptions();
        options.ReasonString = reasonString;
        options.Reason = reason;
        
        await client.DisconnectAsync(options, cancellationToken);
        return new Response<object>(statusCode: string.Empty, isError: !client.IsConnected, sizeBytes: 0,
            message: string.Empty, payload: null);
    }
    
    public void Dispose()
    {   
        client.Dispose();
    }
}