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
            pilot = new AutoPilot(GridTerminalSystem);
            pilot.Log += (msg) => screen?.WritePublicText(msg+"\n", true);
            pilot.RepeatLastTask = true;
            //pilot.Tasks.Add(new AimedFlightStrategy("GPS:Test:20.71:23.77:37.54:"));
            //pilot.Tasks.Add(new AimedFlightStrategy("GPS:Test2:21.78:171.56:28.91:"));
            //pilot.Tasks.Add(new AimedFlightStrategy("GPS:Test:20.71:23.77:37.54:"));
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (updateSource != UpdateType.Update10 && argument == "stop")
            {
                pilot.DisableOverrides();
                pilot.SetInertialDampeners(true);
                Me.Enabled = false;
                return;
            }
            screen?.WritePublicText("", false);
            if (pilot.Tasks.Count == 0)
            {
                foreach (IMySensorBlock s in sensors)
                {
                    entities.Clear();
                    s.DetectedEntities(entities);
                    if (entities.Count > 0)
                    {
                        Waypoint goal = new Waypoint(entities[0]);
                        goal.TargetDistance = 5.0;
                        pilot.Tasks.Add(new AimedFlightStrategy(goal));
                        return;
                    }
                }
            }
            else
            {
                pilot.CurrentTask.Goal.UpdateEntity(sensors);
                pilot.Update(Runtime.TimeSinceLastRun.TotalSeconds);
            }
        }
    }
}