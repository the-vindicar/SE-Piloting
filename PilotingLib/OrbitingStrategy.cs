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

namespace IngameScript
{
    partial class Program
    {
        /// <summary>
        /// Makes the ship orbit the specified location.
        /// </summary>
        public class OrbitingStrategy : BasePilotingStrategy
        {
            public Vector3D Normal;
            /// <summary>
            /// Constructs the strategy with given goal and (optional) reference block.
            /// </summary>
            /// <param name="goal">Goal to pursue. Make sure Distance property is set.</param>
            /// <param name="normal">Normal vector of the orbit.</param>
            /// <param name="reference">Reference block to use, or null to use ship controller.</param>
            /// <param name="forward">Direction on the reference block that is considered "forward".</param>
            /// <param name="up">Direction on the reference block that is considered "up".</param>
            public OrbitingStrategy(Waypoint goal, Vector3D normal, IMyTerminalBlock reference, Base6Directions.Direction forward = Base6Directions.Direction.Forward, Base6Directions.Direction up = Base6Directions.Direction.Up) : base(goal, reference, forward, up)
            {
                if (Goal.TargetDistance <= 0)
                    throw new ArgumentException("Goal.TargetDistance specifies orbit radius, and must be above 0.");
                if (Vector3D.IsZero(normal))
                    throw new ArgumentException("Normal vector must not be zero.");
                Normal = normal;
                MaxLinearSpeed = 10.0;
            }

            /// <summary>
            /// Queries the strategy on which linear and angular velocities the ship should have.
            /// </summary>
            /// <param name="owner">AutoPilot instance that queries the strategy.</param>
            /// <param name="linearV">Initial value - current linear velocity. Is set to desired linear velocity.</param>
            /// <param name="angularV">Initial value - current rotation. Is set to desired rotation.</param>
            /// <returns>False, since orbiter has no goal to achieve.</returns>
            public override bool Update(AutoPilot owner, ref Vector3D linearV, ref Vector3D angularV)
            {
                if (Goal == null) return false;
                IMyTerminalBlock reference = Reference ?? owner.Controller;
                MatrixD wm = reference.WorldMatrix;
                Vector3D radius = Goal.CurrentPosition - wm.Translation;
                double R = radius.Normalize();
                Vector3D vel = Normal.Cross(radius);
                vel.Normalize();
                linearV = Goal.Velocity + vel * MaxLinearSpeed + radius * (R - Goal.TargetDistance);
                double diff = owner.RotateToMatch(radius, Normal,
                    wm.GetDirectionVector(ReferenceForward),
                    wm.GetDirectionVector(ReferenceUp),
                    ref angularV);
                return false;
            }
        }
    }
}
