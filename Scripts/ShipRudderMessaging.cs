using ProtoBuf;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace Khjin.ShipRudder
{
    public class ShipRudderMessaging : ModManager
    {
        private const ushort channelId = 11492; // Unique ID for this mod
        private Networking networking = null;
        private bool isWelcomeDone = false;

        public ShipRudderMessaging() { }

        public override void LoadData()
        {
            networking = new Networking(channelId, new Action<ulong, MessagePacket, bool>(ProcessMessage));
            networking.Register();
        }

        public override void UnloadData()
        {
            networking.Unregister();
            networking = null;
        }

        public void WelcomePlayer()
        {
            if (!isWelcomeDone)
                isWelcomeDone = true;
            else
                return;

            string message = "Ship Rudders Mod by Khjin. To view the list of available commands, enter /rcommands in chat.";
            ChatPlayer(message);
        }

        public void MessageServer(string message)
        {
            networking.SendToServer(new MessagePacket(message));
        }

        public void MessagePlayer(string message, ulong recipientId)
        {
            MessagePacket messagePacket = new MessagePacket(message);
            networking.SendToPlayer(messagePacket, recipientId);
        }

        public void ChatPlayer(string message)
        {
            networking.ChatLocalPlayer(message);
        }

        public void NotifyPlayer(string message, string fontColor = "White")
        {
            ModUtil.NotifyMessage(message, fontColor);
        }

        private void ProcessMessage(ulong senderId, MessagePacket packet, bool isArrivedFromServer)
        {
            if (isArrivedFromServer)
            {
                ProcessAsClient(senderId, packet);
            }
            else
            {
                if (ModUtil.IsServer())
                {
                    ProcessAsServer(senderId, packet);
                }
            }
        }
    
        private void ProcessAsServer(ulong senderId, MessagePacket packet)
        {
            if (packet.Message.StartsWith("/r"))
            {
                // Set fromLocal as false as this always comes from clients
                ShipRudderSession.Instance.Commands.HandleCommand(packet.Message, senderId, false);
            }
        }

        private void ProcessAsClient(ulong senderId, MessagePacket packet)
        {
            ChatPlayer(packet.Message);
        }
    }

    public class Networking
    {
        public readonly ushort channelId;
        private Action<ushort, byte[], ulong, bool> messageHandler;
        public Action<ulong, MessagePacket, bool> PacketHandler { get; private set; }

        private List<IMyPlayer> currentPlayers = null;

        public Networking(ushort channelId, Action<ulong, MessagePacket, bool> packetHandler)
        {
            this.channelId = channelId;
            PacketHandler = packetHandler;
        }

        public void Register()
        {
            messageHandler = new Action<ushort, byte[], ulong, bool>(HandleMessage);
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(channelId, messageHandler);
        }

        public void Unregister()
        {
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(channelId, messageHandler);
        }

        public void HandleMessage(ushort channelId, byte[] messageBytes, ulong senderId, bool isArrivedFromServer)
        {
            try
            {
                // Only recognize messages from this mod
                if (channelId == this.channelId)
                {
                    var packet = MyAPIGateway.Utilities.SerializeFromBinary<PacketBase>(messageBytes);

                    if (packet != null && packet is MessagePacket)
                    {
                        MessagePacket messagePacket = packet as MessagePacket;
                        PacketHandler(senderId, messagePacket, isArrivedFromServer);
                    }
                }
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLineAndConsole($"{ex.Message}\n{ex.StackTrace}");
                ModUtil.NotifyMessage($"[ERROR: {GetType().FullName}: {ex.Message} |" +
                                      " Send SpaceEngineers.Log to mod author]",
                                      MyFontEnum.Red);
            }
        }

        public void SendToServer(MessagePacket packet)
        {
            if (!ModUtil.IsServer())
            {
                var bytes = MyAPIGateway.Utilities.SerializeToBinary(packet);
                MyAPIGateway.Multiplayer.SendMessageToServer(channelId, bytes);
            }
        }

        public void SendToPlayer(MessagePacket packet, ulong recipientId)
        {
            if (ModUtil.IsServer())
            {
                var bytes = MyAPIGateway.Utilities.SerializeToBinary(packet);
                MyAPIGateway.Multiplayer.SendMessageTo(channelId, bytes, recipientId);
            }
        }

        public void ChatLocalPlayer(string message)
        {
            ModUtil.ChatMessage(ShipRudderSession.MOD_NAME, message);
        }

        public void BroadCastToPlayers(MessagePacket packet)
        {
            if (!ModUtil.IsServer()) { return; }

            if (currentPlayers == null)
                currentPlayers = new List<IMyPlayer>(MyAPIGateway.Session.SessionSettings.MaxPlayers);
            else
                currentPlayers.Clear();

            MyAPIGateway.Players.GetPlayers(currentPlayers);
            foreach (var p in currentPlayers)
            {
                if (p.IsBot)
                    continue;

                if (p.SteamUserId == MyAPIGateway.Multiplayer.ServerId)
                    continue;

                if (p.SteamUserId == packet.SenderId)
                    continue;

                byte[] rawData = MyAPIGateway.Utilities.SerializeToBinary(packet);
                MyAPIGateway.Multiplayer.SendMessageTo(channelId, rawData, p.SteamUserId);
            }

            currentPlayers.Clear();
        }
       
    }

    [ProtoContract]
    public class MessagePacket : PacketBase
    {
        [ProtoMember(1)]
        public readonly string Message;

        // Required for deserialization (Digi)
        public MessagePacket() { }

        public MessagePacket(string message)
        {
            Message = message;
        }

        public override bool Received()
        {
            return false;
        }
    }

    [ProtoInclude(1000, typeof(MessagePacket))]
    [ProtoContract]
    public abstract class PacketBase
    {
        [ProtoMember(1)]
        public readonly ulong SenderId;

        public PacketBase()
        {
            SenderId = MyAPIGateway.Multiplayer.MyId;
        }

        public abstract bool Received();
    }
}
