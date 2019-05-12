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
        /// Aims the ship in the direction of the target, and flies directly there.
        /// The ship will fly as fast as possible, but will decelerate to zero in the end.
        /// Flight will only start after correct orientation has been achieved.
        /// </summary>
        public class AimedFlightStrategy : BasePilotingStrategy
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
            /// <param name="forward">Direction on the reference block that is considered "forward".</param>
            /// <param name="up">Direction on the reference block that is considered "up".</param>
            public AimedFlightStrategy(Waypoint goal, IMyTerminalBlock reference,
                Base6Directions.Direction forward = Base6Directions.Direction.Forward,
                Base6Directions.Direction up = Base6Directions.Direction.Up) : base(goal, reference, forward, up)
            {
            }
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
                bool distanceok = false;
                bool orientationok = false;
                IMyTerminalBlock reference = Reference ?? owner.Controller;
                MatrixD wm = reference.WorldMatrix;
                Goal.UpdateTime(owner.elapsedTime);
                Vector3D direction = Goal.CurrentPosition - wm.Translation;
                double distance = direction.Normalize();
                Vector3D facingdirection = direction; //we should face our goal, still.
                if (distance < Goal.TargetDistance) //Are we too close to the goal?
                {   // yep! better back off.
                    direction *= -1;
                    distance = Goal.TargetDistance - distance;
                }
                else //nah, we aren't there yet - just cut the distance we need to travel.
                    distance -= Goal.TargetDistance;
                if (distance > PositionEpsilon) //Are we too far from our desired position?
                {
                    //rotate the ship to face it
                    double diff = owner.RotateToMatch(facingdirection, Vector3D.Zero,
                        wm.GetDirectionVector(ReferenceForward),
                        wm.GetDirectionVector(ReferenceUp),
                        ref angularV);
                    if (diff > OrientationEpsilon) //we still need to rotate
                        linearV = Goal.Velocity; //match velocities with our target, then.
                    else //we are good
                    {
                        orientationok = true;
                        //how quickly can we go, assuming we still need to stop at the end?
                        double accel = owner.GetMaxAccelerationFor(-direction);
                        double braking_time = Math.Sqrt(2 * distance / accel); 
                        double acceptable_velocity = Math.Min(VelocityUsage * accel * braking_time, MaxLinearSpeed);
                        //extra slowdown when close to the target
                        acceptable_velocity = Math.Min(acceptable_velocity, distance);
                        //moving relative to the target
                        linearV = direction * acceptable_velocity + Goal.Velocity;
                        angularV = Vector3D.Zero;
                    }
                }
                else //we are close to our ideal position - attempting to rotate the ship is not a good idea.
                {
                    distanceok = true;
                    orientationok = true;
                    linearV = Goal.Velocity;
                    angularV = Vector3D.Zero;
                }
                return distanceok && orientationok;
            }
        }
    }
}
