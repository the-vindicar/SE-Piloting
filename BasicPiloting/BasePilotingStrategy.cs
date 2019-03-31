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
    partial class Program
    {
        public abstract class BasePilotingStrategy
        {
            public IMyTerminalBlock Reference;
            public Waypoint Goal { get; set; }
            public double MaxAngularVelocity = 6;
            public double OrientationEpsilon = 1e-5;
            public double MaxLinearVelocity = 100.0;
            public double PositionEpsilon = 1e-1;
            public BasePilotingStrategy(Waypoint goal, IMyTerminalBlock reference = null) { Goal = goal; Reference = reference; }
            public abstract bool Update(AutoPilot owner, ref Vector3D linearV, ref Vector3D angularV);
            public static Vector3D ParseGPS(string gps)
            {
                //"GPS: Slam Here:59.55:-11.63:-22.81:"
                if (gps.StartsWith("GPS:"))
                    gps = gps.Substring(4, gps.Length - 5);
                else
                    throw new ArgumentException($"'${gps}' is not a valid GPS string.");
                //" Slam Here:59.55:-11.63:-22.81"
                string[] parts = gps.Split(new char[] { ':' });
                if (parts.Length < 4)
                    throw new ArgumentException($"'${gps}' is not a valid GPS string.");
                double x = double.Parse(parts[parts.Length - 3]);
                double y = double.Parse(parts[parts.Length - 2]);
                double z = double.Parse(parts[parts.Length - 1]);
                return new Vector3D(x, y, z);
            }
        }
    }
}
