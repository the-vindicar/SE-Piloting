using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        const string AntennaName = "Antenna";
        const string TransmitTag = "SalvageDRone";

        List<IMySensorBlock> sensors = new List<IMySensorBlock>();
        List<MyDetectedEntityInfo> entities = new List<MyDetectedEntityInfo>();
        AutoPilot pilot;
        public Program()
        {
            pilot = new AutoPilot(GridTerminalSystem, Me);
            GridTerminalSystem.GetBlocksOfType(sensors, (b) => b.CubeGrid.EntityId == Me.CubeGrid.EntityId);
        }

        void Done()
        {
            pilot.DisableOverrides();
            pilot.Controller.DampenersOverride = true;
            Me.Enabled = false;
        }

        public void Main(string argument, UpdateType updateSource)
        {
        }
    }
}