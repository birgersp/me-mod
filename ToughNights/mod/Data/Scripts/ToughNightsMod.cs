﻿using Sandbox.Definitions;
using Sandbox.Game.Entities;
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
using VRage.ObjectBuilder;
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
        private static readonly double MIN_TIME_BETWEEN_ATTACKS_SEC = 120.0;
        private static readonly double ATTACKS_TIME_WINDOW_SEC = 480.0;
        private static readonly double MIN_SOLAR_ELEVATION = -5.0;

        private readonly Dictionary<MyPlayer.PlayerId, uint> playerTargetTimestamps = new Dictionary<MyPlayer.PlayerId, uint>();

        [Automatic]
        private readonly MySectorWeatherComponent weather = null;

        private Action serverAction;
        private static uint currentTime_sec;
        private static Boolean fastForward = false;
        private static readonly MyDefinitionId barbarianId = new MyDefinitionId(typeof(MyObjectBuilder_HumanoidBot), "BarbarianForestClubStudded");
        private static readonly List<MyDefinitionId> lightEntityDefinitionIds = new List<MyDefinitionId>();

        static ToughNightsMod()
        {
            if (MyObjectBuilderType.TryParse("Block", out var blockType))
            {
                lightEntityDefinitionIds.Add(new MyDefinitionId(blockType, "TorchWall"));
                lightEntityDefinitionIds.Add(new MyDefinitionId(blockType, "TorchStand"));
                lightEntityDefinitionIds.Add(new MyDefinitionId(blockType, "Brazier"));
                lightEntityDefinitionIds.Add(new MyDefinitionId(blockType, "Bonfire"));
                lightEntityDefinitionIds.Add(new MyDefinitionId(blockType, "BedWood"));
            }
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
                var position = player.ControlledEntity.GetPosition();
                if (positionHasNearbyLightSource(position))
                {
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

            if (!withinSolarElevationLimit(playerPosition))
                return;

            if (positionHasNearbyLightSource(playerPosition))
                return;

            spawnBarbarian(playerPosition);
            spawnBarbarian(playerPosition);
        }

        private bool withinSolarElevationLimit(Vector3D position)
        {
            var info = weather.CreateSolarObservation(weather.CurrentTime, position);
            var solarElevation = info.SolarElevation;
            if (solarElevation <= MIN_SOLAR_ELEVATION)
                return true;
            return false;
        }

        private static bool positionHasNearbyLightSource(Vector3D position)
        {
            var sphere = new BoundingSphereD(position, LIGHT_ENTITY_RADIUS);
            var entities = MyEntities.GetEntitiesInSphere(ref sphere);
            foreach (var entity in entities)
            {
                foreach (MyDefinitionId definitionId in lightEntityDefinitionIds)
                {
                    if (definitionId == entity.DefinitionId)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static uint createTargetTimestamp()
        {
            var random = new Random();
            return currentTime_sec + (uint)(random.NextDouble() * (ATTACKS_TIME_WINDOW_SEC) + MIN_TIME_BETWEEN_ATTACKS_SEC);
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
