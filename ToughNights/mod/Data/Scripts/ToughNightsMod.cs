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

        MyInputContext inputContext = new MyInputContext("ToughNightsControl");
        private readonly Dictionary<MyPlayer.PlayerId, uint> playerTargetTimestamps = new Dictionary<MyPlayer.PlayerId, uint>();

        [Automatic]
        private readonly MySectorWeatherComponent weather = null;

        private uint currentTime_sec;

        private Action serverAction;
        private Boolean fastForward = false;

        private readonly MyDefinitionId barbarianId = new MyDefinitionId(typeof(MyObjectBuilder_HumanoidBot), "BarbarianForestClubStudded");

        private static readonly List<String> lightEntityDefinitionIds = new List<String>();

        static ToughNightsMod()
        {
            lightEntityDefinitionIds.Add("Block:TorchWall");
            lightEntityDefinitionIds.Add("Block:TorchStand");
            lightEntityDefinitionIds.Add("Block:Brazier");
            lightEntityDefinitionIds.Add("Block:Bonfire");
        }

        protected override void OnLoad()
        {
            base.OnLoad();
            inputContext.RegisterAction(MyStringHash.GetOrCompute("ToughNights_Test"), () => { invokeServerAction(invokeTest); });
            inputContext.RegisterAction(MyStringHash.GetOrCompute("ToughNights_FastForward"), setFastForward);
            inputContext.RegisterAction(MyStringHash.GetOrCompute("ToughNights_Normal"), setNormal);
            inputContext.Push();
        }

        protected override void OnUnload()
        {
            inputContext.Pop();
            base.OnUnload();
        }

        private void invokeTest()
        {
            var players = MyPlayers.Static.GetAllPlayers();
            foreach (MyPlayer player in players.Values)
            {
                playerTargetTimestamps[player.Id] = 0;
            }
        }

        private void setFastForward()
        {
            fastForward = true;
        }

        private void setNormal()
        {
            fastForward = false;
        }

        private void invokeServerAction(Action action)
        {
            serverAction = action;
            MyMultiplayer.RaiseEvent(this, x => x.ServerMethodInvokedByClient);
        }

        private void log(string msg)
        {
            MyHud.Notifications.Add(new MyHudNotificationDebug(msg));
        }

        [Event, Reliable, Server]
        private void ServerMethodInvokedByClient()
        {
            try
            {
                serverAction();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        [FixedUpdate]
        private void Update()
        {
            if (!MyMultiplayerModApi.Static.IsServer)
                return;

            if (fastForward)
            {
                var nextOffset = weather.DayOffset + TimeSpan.FromSeconds(600 * MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS);
                weather.DayOffset = TimeSpan.FromMinutes(nextOffset.TotalMinutes % weather.DayDurationInMinutes);
            }

            currentTime_sec = DateTime.Now.ToUnixTimestamp();
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

            var info = weather.CreateSolarObservation(weather.CurrentTime, player.ControlledEntity.GetPosition());
            var solarElevation = info.SolarElevation;
            if (solarElevation > -5)
                return;

            if (positionHasNearbyLightSource(playerPosition))
            {
                return;
            }

            var position = player.ControlledEntity.GetPosition();
            spawnBarbarian(position);
        }

        private bool positionHasNearbyLightSource(Vector3D position)
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

        private uint createTargetTimestamp()
        {
            var random = new Random();
            return currentTime_sec + (uint)(random.NextDouble() * (4.0 * 60.0)) + 60;
        }

        private void spawnBarbarian(Vector3D position)
        {
            var newPos = MyEntities.FindFreePlace(position, 1f, 200, 5, 0.5f);
            if (!newPos.HasValue)
                newPos = MyEntities.FindFreePlace(position, 1f, 200, 5, 5f);
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
    }
}
