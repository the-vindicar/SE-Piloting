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
        /// Points the ship in the direction of the goal and flies straight there.
        /// It won't slow down before the target, and it will ignore potential collisions.
        /// </summary>
        public class RammingStrategy : BasePilotingStrategy
        {
            public RammingStrategy(Waypoint goal, IMyTerminalBlock reference = null) : base(goal, reference) { }
            public override bool Update(AutoPilot owner, ref Vector3D linearV, ref Vector3D angularV)
            {
                if (Goal == null) return false;
                IMyTerminalBlock reference = Reference ?? owner.Controller;
                MatrixD wm = reference.WorldMatrix;
                Goal.UpdateTime(owner.elapsedTime);
                Vector3D direction = Goal.CurrentPosition - wm.Translation;
                double distance = direction.Normalize();
                //linear velocity
                linearV = direction * MaxLinearSpeed + Goal.Velocity;
                //angular velocity
                double diff = RotateToMatch(direction, Vector3D.Zero,
                    wm.GetDirectionVector(Base6Directions.Direction.Forward),
                    wm.GetDirectionVector(Base6Directions.Direction.Up),
                    ref angularV);
                if (diff < OrientationEpsilon)
                    angularV = Vector3D.Zero;
                return (diff < OrientationEpsilon) && (distance < PositionEpsilon);
            }
        }
    }
}
