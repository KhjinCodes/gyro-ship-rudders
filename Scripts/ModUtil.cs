using Sandbox.ModAPI;
using VRage.Utils;

namespace Khjin.ShipRudder
{
    public abstract class ModManager
    {
        public abstract void LoadData();

        public abstract void UnloadData();
    }

    public class ModUtil
    {
        public static bool IsServer()
        {
            return (MyAPIGateway.Multiplayer.IsServer || MyAPIGateway.Utilities.IsDedicated);
        }

        public static void Log(string message)
        {
            MyLog.Default.WriteLineAndConsole(message);
        }

        public static void ChatMessage(string senderName, string message)
        {
            if(MyAPIGateway.Session?.Player != null)
            {
                MyAPIGateway.Utilities.ShowMessage(senderName, message);
            }
        }

        public static void NotifyMessage(string message, string fontColor = "White", int durationMs = 1500)
        {
            if (MyAPIGateway.Session?.Player != null)
            {
                MyAPIGateway.Utilities.ShowNotification(message, durationMs, fontColor);
            }
        }
    }
}
