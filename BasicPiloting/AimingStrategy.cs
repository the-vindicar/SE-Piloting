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
        /// <summary>
        /// Points the ship in the direction of the goal. Doesn't move the ship aside from rotation.
        /// </summary>
        public class AimingStrategy : BasePilotingStrategy
        {
            public AimingStrategy(Waypoint goal, IMyTerminalBlock reference = null) : base (goal, reference) { }
            public override bool Update(AutoPilot owner, ref Vector3D linearV, ref Vector3D angularV)
            {
                IMyTerminalBlock reference = Reference ?? owner.Controller;
                MatrixD wm = reference.WorldMatrix;
                Vector3D direction = Goal.CurrentPosition - wm.Translation;
                double distance = direction.Normalize();
                linearV.X = linearV.Y = linearV.Z = 0;
                double diff = owner.RotateToMatch(direction,
                    wm.GetDirectionVector(Base6Directions.Direction.Forward),
                    wm.GetDirectionVector(Base6Directions.Direction.Up),
                    ref angularV);
                if (diff < OrientationEpsilon)
                {
                    angularV = Vector3D.Zero;
                    return true;
                }
                else
                    return false;
            }
        }
    }
}
