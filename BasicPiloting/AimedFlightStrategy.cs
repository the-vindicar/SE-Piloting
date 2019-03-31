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
        public class AimedFlightStrategy : BasePilotingStrategy
        {
            public double VelocityUsage = 0.9;
            public AimedFlightStrategy(Waypoint goal, IMyTerminalBlock reference = null) : base(goal, reference) { }
            public override bool Update(AutoPilot owner, ref Vector3D linearV, ref Vector3D angularV)
            {
                bool distanceok = false;
                bool orientationok = false;
                IMyTerminalBlock reference = Reference ?? owner.Controller;
                MatrixD wm = reference.WorldMatrix;
                Goal.UpdateTime(owner.elapsedTime);
                Vector3D direction = Goal.CurrentPosition - wm.Translation;
                double distance = direction.Normalize();
                Vector3D facingdirection = direction;
                if (distance < Goal.TargetDistance)
                {
                    direction *= -1;
                    distance = Goal.TargetDistance - distance;
                }
                else
                    distance -= Goal.TargetDistance;
                if (distance > PositionEpsilon)
                {
                    double diff = owner.RotateToMatch(facingdirection,
                        wm.GetDirectionVector(Base6Directions.Direction.Forward),
                        wm.GetDirectionVector(Base6Directions.Direction.Up),
                        ref angularV);
                    if (diff > OrientationEpsilon)
                        linearV = Goal.Velocity;
                    else
                    {
                        orientationok = true;
                        //linear velocity
                        double accel = owner.GetMaxAccelerationFor(-direction);
                        double braking_time = Math.Sqrt(2 * distance / accel);
                        double acceptable_velocity = Math.Min(VelocityUsage * accel * braking_time, MaxLinearVelocity);
                        acceptable_velocity = Math.Min(acceptable_velocity, distance);//slow down when close
                        linearV = direction * acceptable_velocity + Goal.Velocity;
                        angularV = Vector3D.Zero;
                    }
                }
                else
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
