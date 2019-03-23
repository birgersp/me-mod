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
using VRage.ObjectBuilders;
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
        MyInputContext m_inputContext = new MyInputContext("ExampleInputContext");
        private readonly Dictionary<MyPlayer.PlayerId, uint> playerTargetTimestamps = new Dictionary<MyPlayer.PlayerId, uint>();

        [Automatic]
        private readonly MySectorWeatherComponent weather = null;

        private uint currentTime_sec;
        private uint prevUpdateTime_sec;

        private Action serverAction;
        private Boolean fastForward = false;

        protected override void OnLoad()
        {
            base.OnLoad();
            m_inputContext.RegisterAction(MyStringHash.GetOrCompute("ToughNights_Test"), () => { invokeServerAction(invokeTest); });
            m_inputContext.RegisterAction(MyStringHash.GetOrCompute("ToughNights_FastForward"), setFastForward);
            m_inputContext.RegisterAction(MyStringHash.GetOrCompute("ToughNights_Normal"), setNormal);
            m_inputContext.Push();
        }

        protected override void OnUnload()
        {
            m_inputContext.Pop();
            base.OnUnload();
        }

        private void invokeTest()
        {
            var players = MyPlayers.Static.GetAllPlayers();
            var id = new MyDefinitionId(typeof(MyObjectBuilder_HumanoidBot), "BarbarianForestClubStudded");
            foreach (MyPlayer player in players.Values)
            {
                var position = player.ControlledEntity.GetPosition();
                SpawnBot(id, position);
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

            currentTime_sec = DateTime.Now.ToUnixTimestamp();
            if (currentTime_sec - prevUpdateTime_sec > 2)
            {
                prevUpdateTime_sec = currentTime_sec;
                //log("Yo waddup");
            }

            if (fastForward)
            {
                var nextOffset = weather.DayOffset + TimeSpan.FromSeconds(1200 * MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS);
                weather.DayOffset = TimeSpan.FromMinutes(nextOffset.TotalMinutes % weather.DayDurationInMinutes);
            }

            var players = MyPlayers.Static.GetAllPlayers();
            foreach (MyPlayer player in players.Values)
            {
                processPlayer(player);
            }
        }

        private void processPlayer(MyPlayer player)
        {
            uint timestamp;
            if (!playerTargetTimestamps.TryGetValue(player.Id, out timestamp))
            {
                timestamp = createTargetTimestamp();
            }
        }

        private uint createTargetTimestamp()
        {
            return 0;
        }

        private void SpawnBot(MyDefinitionId botId, Vector3D position)
        {
            var newPos = MyEntities.FindFreePlace(position, 1f, 200, 5, 0.5f);
            if (!newPos.HasValue)
                newPos = MyEntities.FindFreePlace(position, 1f, 200, 5, 5f);
            var botPosition = newPos.Value;
            var botDefinition = (MyAgentDefinition)MyDefinitionManager.Get<MyBotDefinition>(botId);
            var createdEntity = MySession.Static.Scene.CreateEntity(botDefinition.BotEntity);
            createdEntity.PositionComp.SetWorldMatrix(MatrixD.CreateWorld(botPosition));
            botDefinition.RaiseBeforeBotSpawned(createdEntity);
            MySession.Static.Scene.ActivateEntity(createdEntity);
            botDefinition.AfterBotSpawned(createdEntity);
        }
    }
}
