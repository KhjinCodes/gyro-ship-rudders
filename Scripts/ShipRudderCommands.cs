using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.ModAPI;

namespace Khjin.ShipRudder
{
    public class ShipRudderCommands : ModManager
    {
        private ShipRudderSettings settings = null;
        private ShipRudderMessaging messaging = null;
        private List<IMyPlayer> currentPlayers = new List<IMyPlayer>();

        // Replies text
        private const string REPLY_NO_ACCESS = "Only the Owner and Admins can change settings.";
        private const string REPLY_GET_VALUE = "{0} is currently set to {1}.";
        private const string REPLY_SET_VALUE = "{0} has been updated to {1}.";
        private const string REPLY_INVALID_COMMAND = "Unknown command.";
        private const string REPLY_INVAILD_VALUE_BOOL = "Invalid settings for {0}. Value must be True or False.";
        private const string REPLY_INVALID_VALUE = "Invalid setting for {0}. Value must be within {1} to {2}.";
        private const string REPLY_RESET = "All settings have been reset.";

        public ShipRudderCommands()
        {
            currentPlayers = new List<IMyPlayer>();
        }

        public override void LoadData()
        {
            settings = ShipRudderSession.Instance.Settings;
            messaging = ShipRudderSession.Instance.Messaging;
        }

        public override void UnloadData()
        {
            settings = null;
            messaging = null;
            currentPlayers.Clear();
        }

        public void HandleCommand(string message, ulong senderId, bool fromLocal)
        {
            // Extract the command
            string cleanMessage = message.Replace("/r", string.Empty).Trim();
            string[] parts = cleanMessage.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            CommandDetails command = new CommandDetails();
            command.Message = message;
            command.SenderId = senderId;
            command.FromLocal = fromLocal;
            command.Command = parts[0];
            command.Value = parts.Length == 2 ? parts[1] : string.Empty;
            command.IsGet = (parts.Length == 1);

            if(!command.FromLocal)
            {
                ModUtil.NotifyMessage($"Remote message arrived:\n{command.Message}", "Green");
            }

            if(command.Command == "reset")
            {
                HandleSettingsReset(command);
            }
            else if(command.Command == "commands")
            {
                HandleGetCommandList(command);
            }
            else if (command.IsGet)
            {
                HandleGetSetting(command);
            }
            else if(!command.IsGet && command.Value != string.Empty)
            {
                HandleUpdateSetting(command);
            }
        }

        private void HandleSettingsReset(CommandDetails command)
        {
            if (ModUtil.IsServer())
            {
                if (HasAccessRights(command.SenderId))
                {
                    settings.ResetSettings();
                    SendResponse(REPLY_RESET, command);
                }
                else
                {
                    SendResponse(REPLY_NO_ACCESS, command);
                }
            }
            else
            {
                messaging.MessageServer(command.Message);
            }
        }

        private void HandleGetCommandList(CommandDetails command)
        {
            if (ModUtil.IsServer())
            {
                string commandList = "Available commands: " +
                                     "/rcommands, " +
                                     "/rreset, " +
                                     settings.GetAvailableSettings();
                SendResponse(commandList, command);
            }
            else
            {
                messaging.MessageServer(command.Message);
            }
        }

        private void HandleGetSetting(CommandDetails command)
        {
            if (ModUtil.IsServer())
            {
                string value = settings.GetSetting(command.Command);
                if (value != string.Empty)
                {
                    SendResponse(string.Format(REPLY_GET_VALUE, command.Command, value), command);
                }
                else
                {
                    SendResponse(REPLY_INVALID_COMMAND, command);
                }
            }
            else
            {
                messaging.MessageServer(command.Message);
            }
        }

        private void HandleUpdateSetting(CommandDetails command)
        {
            if (ModUtil.IsServer())
            {
                if (HasAccessRights(command.SenderId))
                {
                    string value = settings.GetSetting(command.Command);
                    if (value != string.Empty)
                    {
                        if (settings.UpdateSetting(command.Command, command.Value))
                        {
                            SendResponse(string.Format(REPLY_SET_VALUE, command.Command, command.Value), command);
                        }
                        else
                        {
                            SettingLimits limits = settings.GetLimits(command.Command);
                            if(limits is BoolLimits)
                            {
                                BoolLimits bl = limits as BoolLimits;
                                SendResponse(string.Format(REPLY_INVAILD_VALUE_BOOL, command.Command), command);
                            }
                            else if(limits is FloatLimits)
                            {
                                FloatLimits fl = limits as FloatLimits;
                                SendResponse(string.Format(REPLY_INVALID_VALUE, command.Command, fl.MinValue, fl.MaxValue), command);
                            }
                            else if (limits is DoubleLimits)
                            {
                                DoubleLimits dl = limits as DoubleLimits;
                                SendResponse(string.Format(REPLY_INVALID_VALUE, command.Command, dl.MinValue, dl.MaxValue), command);
                            }
                        }
                    }
                    else
                    {
                        SendResponse(REPLY_INVALID_COMMAND, command);
                    }
                }
                else
                {
                    SendResponse(REPLY_NO_ACCESS, command);
                }
            }
            else
            {
                messaging.MessageServer(command.Message);
            }
        }

        private void SendResponse(string message, CommandDetails command)
        {
            if(command.FromLocal)
            {
                messaging.ChatPlayer(message);
            }
            else
            {
                messaging.MessagePlayer(message, command.SenderId);
            }
        }

        private bool HasAccessRights(ulong playerId)
        {
            var promoteLevel = GetPromoteLevel(playerId);
            if ((promoteLevel & (MyPromoteLevel.Owner | MyPromoteLevel.Admin)) != 0)
                return true;
            else
                return false;
        }

        private MyPromoteLevel GetPromoteLevel(ulong playerId)
        {
            MyAPIGateway.Players.GetPlayers(currentPlayers);
            foreach(var player in currentPlayers)
            {
                if (player.IsBot)
                    continue;
                else
                {
                    return player.PromoteLevel;
                }
            }

            return MyPromoteLevel.None;
        }

        private class CommandDetails
        {
            public ulong SenderId;
            public string Message;
            public string Command;
            public string Value;
            public bool IsGet = true;
            public bool FromLocal;
        }
    }
}
