using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Gui;
using Sandbox.Game.Players;
using System;
using VRage;
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
    [MySessionComponent(AlwaysOn = true)]
    public class ToughNightsMod : MySessionComponent, IMyEventProxy
    {
        MyInputContext m_inputContext = new MyInputContext("ExampleInputContext");

        protected override void OnLoad()
        {
            base.OnLoad();
            m_inputContext.RegisterAction(MyStringHash.GetOrCompute("ExampleControlName"), HandleInput);
            m_inputContext.Push();
        }

        protected override void OnUnload()
        {
            m_inputContext.Pop();
            base.OnUnload();
        }

        private void HandleInput(ref MyInputContext.ActionEvent action)
        {
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
                tryFunction();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private void tryFunction()
        {
            var players = MyPlayers.Static.GetAllPlayers();
            var id = new MyDefinitionId(typeof(MyObjectBuilder_HumanoidBot), "BarbarianForestClubStudded");
            foreach (MyPlayer player in players.Values)
            {
                var position = player.ControlledEntity.GetPosition();
                SpawnBot(id, position);
            }
        }

        public void SpawnBot(MyDefinitionId botId, Vector3D position)
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
