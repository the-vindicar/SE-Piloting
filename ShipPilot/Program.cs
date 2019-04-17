using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRageMath;
using VRage.Game.GUI.TextPanel;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        AutoPilot pilot;
        IMyTextSurface Surface;
        List<IMyCameraBlock> Cameras = new List<IMyCameraBlock>();
        public Program()
        {
            pilot = new AutoPilot(GridTerminalSystem, Me);
            GridTerminalSystem.GetBlocksOfType(Cameras, (c) => c.IsSameConstructAs(Me));
            if (pilot.Controller is IMyCockpit)
                Surface = (pilot.Controller as IMyCockpit).GetSurface(0);
            else
                Surface = Me.GetSurface(0);
            Surface.ContentType = ContentType.TEXT_AND_IMAGE;
            Surface.BackgroundColor = Color.Black;
            Surface.FontSize = 1.5f;
            //pilot.Log = Log;
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Log(string msg)
        {
            Surface.WriteText(msg, true);
            Surface.WriteText("\n", true);
        }

        public void Main(string argument, UpdateType updateSource)
        {
            Surface.WriteText("", false);
            if ((updateSource & ~(UpdateType.Update1 | UpdateType.Update10 | UpdateType.Update100)) != 0)
            {
                if (argument == "start")
                {
                    var task = new FlyForwardStrategy(20.0, Cameras);
                    pilot.Tasks.Clear();
                    pilot.Tasks.Add(task);
                }
                else if (argument == "stop")
                {
                    pilot.Tasks.Clear();
                    pilot.DisableOverrides();
                    pilot.Controller.DampenersOverride = true;
                }
            }
            if ((updateSource & (UpdateType.Update1 | UpdateType.Update10 | UpdateType.Update100)) != 0)
            {
                if (pilot.Tasks.Count > 0)
                {
                    pilot.Update(Runtime.TimeSinceLastRun.TotalSeconds);
                }
                else
                    Log("No active tasks.");
            }
        }

        //public bool GetApproachFromScan(MyDetectedEntityInfo info, out Vector3D position, out Vector3D approach)
        //{
        //    if (info.IsEmpty() || !info.HitPosition.HasValue || (info.Type != MyDetectedEntityType.LargeGrid && info.Type != MyDetectedEntityType.SmallGrid))
        //    {
        //        position = Vector3D.Zero;
        //        approach = Vector3D.Zero;
        //        return false;
        //    }

        //}
    }
}