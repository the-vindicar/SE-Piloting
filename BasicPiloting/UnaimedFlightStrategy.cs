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
        /// Moves the ship in a straight line to the specified goal.
        /// Ship orientation is ignored.
        /// No collision avoidance is done.
        /// </summary>
        public class UnaimedFlightStrategy : BasePilotingStrategy
        {
            /// <summary>
            /// How close to the maximum safe speed are we allowed to get.
            /// </summary>
            public double VelocityUsage = 0.9;
            /// <summary>
            /// Constructs the strategy with given goal and (optional) reference block.
            /// </summary>
            /// <param name="goal">Goal to pursue.</param>
            /// <param name="reference">Reference block to use, or null to use ship controller.</param>
            public UnaimedFlightStrategy(Waypoint goal, IMyTerminalBlock reference) : base(goal, reference) { }
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
                MatrixD wm = reference.WorldMatrix;
                Goal.UpdateTime(owner.elapsedTime);
                Vector3D direction = Goal.CurrentPosition - wm.Translation;
                double distance = direction.Normalize();
                if (distance < Goal.TargetDistance)
                {
                    direction *= -1;
                    distance = Goal.TargetDistance - distance;
                }
                if (distance > PositionEpsilon)
                {
                    //linear velocity
                    double accel = owner.GetMaxAccelerationFor(-direction);
                    double braking_time = Math.Sqrt(2 * distance / accel);
                    double acceptable_velocity = Math.Min(VelocityUsage * accel * braking_time, MaxLinearSpeed);
                    acceptable_velocity = Math.Min(acceptable_velocity, distance);//slow down when close
                    Vector3D targetv = direction * acceptable_velocity;
                    linearV = targetv + Goal.Velocity;
                }
                else
                    linearV = Vector3D.Zero;
                angularV = Vector3D.Zero;
                if (distance < PositionEpsilon)
                    owner.Log?.Invoke("Target position reached.");
                return Vector3D.IsZero(linearV);
            }
        }
    }
}
