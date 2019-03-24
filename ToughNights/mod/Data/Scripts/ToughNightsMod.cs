using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Gui;
using Sandbox.Game.Players;
using Sandbox.Game.SessionComponents;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage;
using VRage.Components;
using VRage.Game;
using VRage.Game.Input;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.AI.Bot;
using VRage.Network;
using VRage.Session;
using VRage.Utils;
using VRageMath;

namespace ToughNights
{
    [StaticEventOwner]
    [MyDependency(typeof(MySectorWeatherComponent))]
    [MySessionComponent(AlwaysOn = true)]
    public class ToughNightsMod : MySessionComponent, IMyEventProxy
    {
        private static readonly double LIGHT_ENTITY_RADIUS = 15.0;

        private readonly Dictionary<MyPlayer.PlayerId, uint> playerTargetTimestamps = new Dictionary<MyPlayer.PlayerId, uint>();

        [Automatic]
        private readonly MySectorWeatherComponent weather = null;

        private Action serverAction;
        private static uint currentTime_sec;
        private static Boolean fastForward = false;
        private static readonly MyDefinitionId barbarianId = new MyDefinitionId(typeof(MyObjectBuilder_HumanoidBot), "BarbarianForestClubStudded");
        private static readonly List<String> lightEntityDefinitionIds = new List<String>();

        static ToughNightsMod()
        {
            lightEntityDefinitionIds.Add("Block:TorchWall");
            lightEntityDefinitionIds.Add("Block:TorchStand");
            lightEntityDefinitionIds.Add("Block:Brazier");
            lightEntityDefinitionIds.Add("Block:Bonfire");
            lightEntityDefinitionIds.Add("Block:BedWood");
        }

        // (Only for offline-testing)
        MyInputContext inputContext = new MyInputContext("ToughNightsControl");

        // (Only for offline-testing)
        protected override void OnLoad()
        {
            base.OnLoad();
            inputContext.RegisterAction(MyStringHash.GetOrCompute("ToughNights_Test"), () => { invokeServerAction(invokeTest); });
            inputContext.RegisterAction(MyStringHash.GetOrCompute("ToughNights_FastForward"), setFastForward);
            inputContext.RegisterAction(MyStringHash.GetOrCompute("ToughNights_Normal"), setNormal);
            inputContext.Push();
        }

        // (Only for offline-testing)
        protected override void OnUnload()
        {
            inputContext.Pop();
            base.OnUnload();
        }

        // (Only for offline-testing)
        private void invokeTest()
        {
            var players = MyPlayers.Static.GetAllPlayers();
            foreach (MyPlayer player in players.Values)
            {
                var sphere = new BoundingSphereD(player.ControlledEntity.GetPosition(), LIGHT_ENTITY_RADIUS);
                var entities = MyEntities.GetEntitiesInSphere(ref sphere);
                foreach (var entity in entities)
                {
                    broadcastNotification(entity.DefinitionId.ToString());
                }
            }
        }

        // (Only for offline-testing)
        private void setFastForward()
        {
            fastForward = true;
        }

        // (Only for offline-testing)
        private void setNormal()
        {
            fastForward = false;
        }

        // (Only for offline-testing)
        private void invokeServerAction(Action action)
        {
            serverAction = action;
            MyMultiplayer.RaiseEvent(this, x => x.ServerMethodInvokedByClient);
        }

        // (Only for offline-testing)
        [Event, Reliable, Server]
        private void ServerMethodInvokedByClient()
        {
            serverAction();
        }

        [FixedUpdate]
        private void Update()
        {
            if (!MyMultiplayerModApi.Static.IsServer)
                return;

            currentTime_sec = DateTime.Now.ToUnixTimestamp();

            if (fastForward)
            {
                var nextOffset = weather.DayOffset + TimeSpan.FromSeconds(600 * MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS);
                weather.DayOffset = TimeSpan.FromMinutes(nextOffset.TotalMinutes % weather.DayDurationInMinutes);
            }

            var players = MyPlayers.Static.GetAllPlayers();
            foreach (MyPlayer player in players.Values)
            {
                checkPlayerTargetTimestamp(player);
            }
        }

        private void checkPlayerTargetTimestamp(MyPlayer player)
        {
            uint targetTimestamp;
            if (!playerTargetTimestamps.TryGetValue(player.Id, out targetTimestamp))
            {
                targetTimestamp = createTargetTimestamp();
                playerTargetTimestamps[player.Id] = targetTimestamp;
            }

            if (currentTime_sec > targetTimestamp)
            {
                processPlayer(player);
                playerTargetTimestamps[player.Id] = createTargetTimestamp();
            }
        }

        private void processPlayer(MyPlayer player)
        {
            var playerPosition = player.ControlledEntity.GetPosition();

            var info = weather.CreateSolarObservation(weather.CurrentTime, playerPosition);
            var solarElevation = info.SolarElevation;
            if (solarElevation > -5)
                return;

            if (positionHasNearbyLightSource(playerPosition))
            {
                return;
            }

            spawnBarbarian(playerPosition);
            spawnBarbarian(playerPosition);
        }

        private static bool positionHasNearbyLightSource(Vector3D position)
        {
            var sphere = new BoundingSphereD(position, LIGHT_ENTITY_RADIUS);
            var entities = MyEntities.GetEntitiesInSphere(ref sphere);
            foreach (var entity in entities)
            {
                if (lightEntityDefinitionIds.Contains(entity.DefinitionId.ToString()))
                    return true;
            }
            return false;
        }

        private static uint createTargetTimestamp()
        {
            var random = new Random();
            return currentTime_sec + (uint)(random.NextDouble() * (4.0 * 60.0)) + 60;
        }

        private static void spawnBarbarian(Vector3D position)
        {
            var newPos = MyEntities.FindFreePlace(position, 1f, 200, 5, 5f);
            if (!newPos.HasValue)
            {
                return;
            }
            var botPosition = newPos.Value;
            var botDefinition = (MyAgentDefinition)MyDefinitionManager.Get<MyBotDefinition>(barbarianId);
            var createdEntity = MySession.Static.Scene.CreateEntity(botDefinition.BotEntity);
            createdEntity.PositionComp.SetWorldMatrix(MatrixD.CreateWorld(botPosition));
            botDefinition.RaiseBeforeBotSpawned(createdEntity);
            MySession.Static.Scene.ActivateEntity(createdEntity);
            botDefinition.AfterBotSpawned(createdEntity);
        }

        private static void broadcastNotification(string message)
        {
            MyMultiplayerModApi.Static.RaiseStaticEvent(x => showNotificationToClients, message);
        }

        [Event]
        [Server]
        [Broadcast]
        private static void showNotificationToClients(string msg)
        {
            ((IMyUtilities)MyAPIUtilities.Static).ShowNotification(msg);
        }
    }
}
