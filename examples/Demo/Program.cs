using MQTTnet;
using MQTTnet.Client;
using NBomber.CSharp;
using MqttClient = NBomber.MQTT.MqttClient;

new MqttHelloTest().Run();

public class MqttHelloTest
{
    public void Run()
    {
        var payload = "hello";

        var scenario = Scenario.Create("mqtt_hello_scenario", async ctx =>
        {
            var topic = $"/clients/{ctx.ScenarioInfo.ThreadId}";
            var mqttClient = new MqttClient(new MqttFactory().CreateMqttClient());

            var connect = await Step.Run("connect", ctx, async () =>
            {
                var options = new MqttClientOptionsBuilder().WithConnectionUri("ws://localhost:8083/mqtt").Build();
                return await mqttClient.Connect(options);
            });

            var subscribe = await Step.Run("subscribe", ctx, async () =>
                await mqttClient.Subscribe(topic));

            var publish = await Step.Run("publish", ctx, async () => 
                await mqttClient.Publish(topic, payload));

            var receive = await Step.Run("receive", ctx, async () => 
                await mqttClient.Receive());

            var disconnect = await Step.Run("disconnect", ctx, async () =>
                await mqttClient.Disconnect());

            return Response.Ok();
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.KeepConstant(1, TimeSpan.FromSeconds(30))
        );

        NBomberRunner
            .RegisterScenarios(scenario)
            .Run();
    }
}

