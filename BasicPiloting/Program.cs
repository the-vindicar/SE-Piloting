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
        IMyTextPanel screen = null;
        List<IMySensorBlock> sensors = new List<IMySensorBlock>();
        List<MyDetectedEntityInfo> entities = new List<MyDetectedEntityInfo>();
        AutoPilot pilot;
        public Program()
        {
            GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(null, (p) =>
            {
                if (p.IsSameConstructAs(Me)) screen = p;
                return false;
            });
            screen?.WritePublicText("", false);
            GridTerminalSystem.GetBlocksOfType(sensors);
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            pilot = new AutoPilot(GridTerminalSystem, Me);
            pilot.Log += (msg) => screen?.WritePublicText(msg+"\n", true);
            pilot.RepeatLastTask = true;
            IMyShipConnector b = GridTerminalSystem.GetBlockWithName("Connector") as IMyShipConnector;
            Waypoint goal = new Waypoint("GPS:Docking Connector:32.50:13.28:55.01:");
            Vector3D approach = new Vector3D(-1, 0, 0);
            goal.TargetDistance = 0.0;
            var strategy = new DockingStrategy(goal, approach, b);
            pilot.Tasks.Add(strategy);
        }

        void Done()
        {
            pilot.DisableOverrides();
            pilot.Controller.DampenersOverride = true;
            Me.Enabled = false;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            screen?.WritePublicText("", false);
            if (updateSource != UpdateType.Update10 && argument == "stop")
            {
                Done();
                return;
            }
            if (pilot.Update(Runtime.TimeSinceLastRun.TotalSeconds))
            {
                Done();
                return;
            }
        }
    }
}