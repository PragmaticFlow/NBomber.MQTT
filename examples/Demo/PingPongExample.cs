using MQTTnet;
using MQTTnet.Protocol;
using NBomber;
using NBomber.CSharp;
using NBomber.Data;
using MqttClient = NBomber.MQTT.MqttClient;

new PingPongExample().Run();

public class PingPongExample
{
    public void Run()
    {
        var clientPool = new ClientPool<MqttClient>();
        var payload = Data.GenerateRandomBytes(200);

        var scenario = Scenario.Create("mqtt_scenario", async ctx =>
        {
            var mqttClient = clientPool.GetClient(ctx.ScenarioInfo);

            var publish = await Step.Run("publish", ctx, async () =>
            {
                var topic = $"/clients/{ctx.ScenarioInfo.InstanceId}";
                var msg = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(payload)
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                    .Build();

                return await mqttClient.Publish(msg);
            });

            var receive = await Step.Run("receive", ctx, async () =>
            {
                var response = await mqttClient.Receive(ctx.ScenarioCancellationToken);
                return response;
            });

            return Response.Ok();
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(Simulation.KeepConstant(10, TimeSpan.FromSeconds(5)))
        .WithInit(async context =>
        {
            for (var i = 0; i < 100; i++)
            {
                var topic = $"/clients/mqtt_scenario_{i}";
                var clientId = $"mqtt_client_{i}";
                var options = new MqttClientOptionsBuilder()
                    .WithTcpServer("localhost")
                    .WithClientId(clientId)
                    .Build();

                var mqttClient = new MqttClient(new MqttClientFactory().CreateMqttClient());
                var connectResult = await mqttClient.Connect(options);

                if (!connectResult.IsError)
                {
                    await mqttClient.Subscribe(topic, MqttQualityOfServiceLevel.AtMostOnce);
                    clientPool.AddClient(mqttClient);
                }
                else
                    throw new Exception("client can't connect to the MQTT broker");

                await Task.Delay(10);
            }
        })
        .WithClean(ctx =>
        {
            clientPool.DisposeClients(client => client.Dispose());
            return Task.CompletedTask;
        });

        NBomberRunner
            .RegisterScenarios(scenario)
            .Run();
    }
}

