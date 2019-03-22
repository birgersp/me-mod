using Medieval.Entities.Components.Blocks;
using Sandbox.Game.Gui;
using Sandbox.ModAPI;
using VRage.Components;
using VRage.Game.Input;
using VRage.Game.ModAPI;
using VRage.Network;
using VRage.Session;
using VRage.Utils;
using VRageMath;

namespace BirgerMod
{
    [MySessionComponent(AlwaysOn = true)]
    public class BirgerMod : MySessionComponent, IMyEventProxy
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

        [Event, Reliable, Server]
        private void ServerMethodInvokedByClient()
        {
            MyHud.Notifications.Add(new MyHudNotificationDebug("Message called by client!"));
            log("Hello world!");
        }

        private void log(string msg)
        {
            ((IMyUtilities)MyAPIUtilities.Static).ShowNotification(msg, 1000, null, Color.White);
        }
    }
}
