using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Gui;
using Sandbox.Game.Players;
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

            //Something along the likes of
            //MyDefinitionId definitionId = default;
            //Vector3D worldPosition = Vector3D.Zero;

            //var createdEntity = MySession.Static.Scene.CreateEntity(definitionId);
            //createdEntity.PositionComp.SetWorldMatrix(MatrixD.CreateWorld(worldPosition));
            //MySession.Static.Scene.ActivateEntity(createdEntity);

            var botDefinition = (MyAgentDefinition)MyDefinitionManager.Get<MyBotDefinition>(botId);
            var newPos = MyEntities.FindFreePlace(position, 1f, 200, 5, 0.5f);
            if (!newPos.HasValue)
                MyEntities.FindFreePlace(position, 1f, 200, 5, 5f);
            if (newPos.HasValue)
                position = newPos.Value;

            var gravity = VRage.Entities.Gravity.MyGravityProviderSystem.CalculateTotalGravityInPoint(position);
            if (!Vector3.IsZero(gravity))
                gravity.Normalize();
            else
                gravity = Vector3.Down;

            var spawnWorldMatrix = MatrixD.CreateWorld(position, Vector3.Forward, -gravity);
            var botEntityOb = (MyObjectBuilder_EntityBase)MyObjectBuilderSerializer.CreateNewObject(botDefinition.BotEntity);
            botEntityOb.PositionAndOrientation = new MyPositionAndOrientation(spawnWorldMatrix);
            botEntityOb.PersistentFlags |= MyPersistentEntityFlags2.InScene;
            var botEntity = MyEntities.CreateFromObjectBuilder(botEntityOb);
            botDefinition.RaiseBeforeBotSpawned(botEntity);

            // Try without first, uncomment if it doesn't work
            //            var botName = GetRandomCharacterName();
            //            var botIdentity = MyIdentities.Static.CreateIdentity(botName);
            //            MyIdentities.Static.SetControlledEntity(botIdentity, botEntity);

            MyEntities.Add(botEntity);

            botDefinition.AfterBotSpawned(botEntity);

            // may cause issues in MP
            //            var eventProxyOwner = MyExternalReplicable.FindByObject(botEntity);
            //            if (eventProxyOwner != null)
            //                MyMultiplayer.ReplicateImmediately(eventProxyOwner);

            //log("BOT SPAWNED");
        }
    }
}
