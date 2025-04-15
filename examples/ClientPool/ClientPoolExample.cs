using Microsoft.Extensions.Configuration;
using MQTTnet;
using MQTTnet.Client;
using NBomber;
using NBomber.CSharp;
using NBomber.Data;
using MqttClient = NBomber.MQTT.MqttClient;

new ClientPoolExample().Run();

public class CustomScenarioSettings
{
    public string MqttServerUrl { get; set; }
    public int ClientCount { get; set; }
    public int MsgSizeBytes { get; set; }
    public bool UsePersistence { get; set; }
}

public class ClientPoolExample
{
    public void Run()
    {
        var clientPool = new ClientPool<MqttClient>();
        var message = Data.GenerateRandomBytes(200);
        var usePersistence = false;

        var scenario = Scenario.Create("mqtt_scenario", async ctx =>
        {
            var mqttClient = clientPool.GetClient(ctx.ScenarioInfo);

            var publish = await Step.Run("publish", ctx, async () =>
            {
                var topic = $"/clients/{ctx.ScenarioInfo.InstanceId}";
                var msg = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(message)
                    .WithRetainFlag(usePersistence)
                    .Build();

                return await mqttClient.Publish(msg);
            });

            var receive = await Step.Run("receive", ctx, async () =>
                await mqttClient.Receive(ctx.ScenarioCancellationToken));

            return Response.Ok();
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(3))
        .WithLoadSimulations(Simulation.KeepConstant(copies: 1, during: TimeSpan.FromSeconds(30)))
        .WithInit(async context =>
        {
            var config = context.CustomSettings.Get<CustomScenarioSettings>();
            message = Data.GenerateRandomBytes(config.MsgSizeBytes);
            usePersistence = config.UsePersistence;                    

            for (var i = 0; i < config.ClientCount; i++)
            {
                var topic = $"/clients/mqtt_scenario_{i}";
                var clientId = $"mqtt_client_{i}";
                var options = new MqttClientOptionsBuilder()
                    .WithWebSocketServer(options => { options.WithUri(config.MqttServerUrl); })
                    .WithClientId(clientId)
                    .WithCleanSession(!usePersistence)
                    .Build();

                var mqttClient = new MqttClient(new MqttFactory().CreateMqttClient());
                var connectResult = await mqttClient.Connect(options);                

                if (!connectResult.IsError)
                {
                    await mqttClient.Subscribe(topic);
                    clientPool.AddClient(mqttClient);
                }
                else
                    throw new Exception("client can't connect to the MQTT broker");
            }
        })
        .WithClean(ctx =>
        {
            clientPool.DisposeClients(async client => await client.Disconnect());
            return Task.CompletedTask;
        });

        NBomberRunner
            .RegisterScenarios(scenario)
            .LoadConfig("config.json")
            .Run();
    }
}

