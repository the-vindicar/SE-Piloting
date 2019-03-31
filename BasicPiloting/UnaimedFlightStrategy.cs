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
        public class UnaimedFlightStrategy : BasePilotingStrategy
        {
            public double VelocityUsage = 0.9;
            public UnaimedFlightStrategy(Waypoint goal, IMyTerminalBlock reference = null) : base(goal, reference) { }
            public override bool Update(AutoPilot owner, ref Vector3D linearV, ref Vector3D angularV)
            {
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
                    double acceptable_velocity = Math.Min(VelocityUsage * accel * braking_time, MaxLinearVelocity);
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
