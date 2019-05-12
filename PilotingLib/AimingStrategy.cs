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
            /// <summary>
            /// Constructs the strategy with given goal and (optional) reference block.
            /// </summary>
            /// <param name="goal">Goal to pursue.</param>
            /// <param name="reference">Reference block to use, or null to use ship controller.</param>
            public AimingStrategy(Waypoint goal, IMyTerminalBlock reference,
                Base6Directions.Direction forward = Base6Directions.Direction.Forward,
                Base6Directions.Direction up = Base6Directions.Direction.Up) : base (goal, reference, forward, up) { }
            /// <summary>
            /// Queries the strategy on which linear and angular velocities the ship should have.
            /// </summary>
            /// <param name="owner">AutoPilot instance that queries the strategy.</param>
            /// <param name="linearV">Initial value - current linear velocity. Is set to desired linear velocity.</param>
            /// <param name="angularV">Initial value - current rotation. Is set to desired rotation.</param>
            /// <returns>True if goal is considered achieved.</returns>
            public override bool Update(AutoPilot owner, ref Vector3D linearV, ref Vector3D angularV)
            {
                if (Goal == null) return false;
                IMyTerminalBlock reference = Reference ?? owner.Controller;
                Vector3D pos = Goal.CurrentPosition;
                MatrixD wm = reference.WorldMatrix;
                Vector3D direction = pos - wm.Translation;
                double distance = direction.Normalize();
                linearV.X = linearV.Y = linearV.Z = 0;
                double diff = owner.RotateToMatch(direction, Vector3D.Zero,
                    wm.GetDirectionVector(ReferenceForward),
                    wm.GetDirectionVector(ReferenceUp),
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
