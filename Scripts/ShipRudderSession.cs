using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace Khjin.ShipRudder
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    partial class ShipRudderSession : MySessionComponentBase
    {
        public static ShipRudderSession Instance;

        public const string MOD_VERSION = "1.1";
        public const string MOD_NAME = "Ship Rudders Mod";

        // Handles messages through chat input and from other sessions (players, server)
        public ShipRudderMessaging Messaging { get; private set; } = null;

        // Handles the loading, saving, and updating of settings
        public ShipRudderSettings Settings { get; private set; } = null;

        // Handles the commands parsing and execution
        public ShipRudderCommands Commands { get; private set; } = null;

        // Handles the management of ships and their components (gyros, controllers)
        private Dictionary<long, ShipRudderLogic> _ships;

        public ShipRudderSession()
        {
            Messaging = new ShipRudderMessaging();
            Settings = new ShipRudderSettings();
            Commands = new ShipRudderCommands();
            _ships = new Dictionary<long, ShipRudderLogic>();
        }

        public override void LoadData()
        {
            Instance = this;

            // Load the managers
            Messaging.LoadData();
            Settings.LoadData();
            Commands.LoadData();

            // Only servers process added entities
            if (ModUtil.IsServer())
            {
                MyAPIGateway.Entities.OnEntityAdd += EntityAdded;
            }

            // Listen to messages entered via chat
            MyAPIGateway.Utilities.MessageEntered += MessageEntered;
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Utilities.MessageEntered -= MessageEntered;

            // Clear references to ships
            if (ModUtil.IsServer())
            {
                MyAPIGateway.Entities.OnEntityAdd -= EntityAdded;
                foreach (var ship in _ships.Values)
                {
                    ship.UnloadData();
                }

                _ships.Clear();
            }

            // Unload the managers
            Commands.UnloadData();
            Settings.UnloadData(); // Saves settings before unload
            Messaging.UnloadData();

            Instance = null;
        }

        public override void UpdateAfterSimulation()
        {
            // Show welcome message
            Messaging.WelcomePlayer();
            if(!ModUtil.IsServer() || _ships.Count == 0) { return; }

            try
            {
                // Update tick timers on each instance
                foreach (var key in _ships.Keys)
                {
                    if (_ships[key].IsMarkedForClose)
                    { continue; }

                    MyAPIGateway.Parallel.Start(_ships[key].Update);
                    _ships[key].UpdateTicks();
                }
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLineAndConsole($"{e.Message}\n{e.StackTrace}");
                Messaging.NotifyPlayer($"[ ERROR: {GetType().FullName}: {e.Message} | Send SpaceEngineers.Log to mod author ]", "Red");
            }
        }

        private void EntityAdded(IMyEntity ent)
        {
            IMyCubeGrid grid = ent as IMyCubeGrid;
            if(grid != null && grid.Physics != null)
            {
                grid.OnMarkForClose += GridMarkedForClose;
                if(!_ships.ContainsKey(grid.EntityId))
                {
                    ShipRudderLogic ship = new ShipRudderLogic(grid);
                    ship.IsMarkedForClose = false;
                    ship.LoadData();
                    _ships.Add(grid.EntityId, ship);
                }
            }
        }

        private void GridMarkedForClose(IMyEntity ent)
        {
            IMyCubeGrid grid = ent as IMyCubeGrid;
            if(grid != null && _ships.ContainsKey(ent.EntityId))
            {
                var ship = _ships[ent.EntityId];
                ship.IsMarkedForClose = true;
                ship.UnloadData();
                _ships.Remove(ent.EntityId);
            }
        }

        private void MessageEntered(string messageText, ref bool sendToOthers)
        {
            if (messageText.StartsWith("/r"))
            {
                Commands.HandleCommand(messageText, MyAPIGateway.Multiplayer.MyId, true);
                sendToOthers = false;
            }
        }
    }
}
