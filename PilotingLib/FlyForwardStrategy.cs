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
        /// Flies the ship forward, sending raycasts in that direction.
        /// If raycast detects something in the way, the ship will slow down and stop before this obstacle.
        /// Warning: Only in that case the task will be considered completed! Otherwise, the ship will fly away into infinity.
        /// Consider checking cockpit controls to see if user has left the ship, and abort the task.
        /// </summary>
        public class FlyForwardStrategy : BasePilotingStrategy
        {
            /// <summary>
            /// How close to the maximum safe speed are we allowed to get.
            /// </summary>
            public double VelocityUsage = 0.9;
            private enum State { NoTarget, EnRoute, Destination }
            State CurrentState;
            Vector3D TargetVector;
            List<IMyCameraBlock> Cameras;
            double RaycastChargeSpeed = 0;
            MyDetectedEntityInfo Obstacle;
            double Distance;
            /// <summary>
            /// Constructs the strategy with the given stopping distance, reference block and camera list.
            /// </summary>
            /// <param name="distance">How far from the collision point we should stop. 
            /// Measured from the surface of ship's bounding sphere.</param>
            /// <param name="cameras">List of available cameras to use for raycasting.
            /// At least some of them should be facing in the forward direction.</param>
            /// <param name="reference">Reference block that will determine forward direction</param>
            /// <param name="forward">The direction to be considered forward, relative to reference block.</param>
            public FlyForwardStrategy(double distance, 
                List<IMyCameraBlock> cameras,
                IMyCubeBlock reference = null,
                Base6Directions.Direction forward = Base6Directions.Direction.Forward)
                : base(null, reference, forward)
            {
                Distance = distance;
                TargetVector = Vector3D.Zero;
                CurrentState = State.NoTarget;
                Cameras = cameras;
                Obstacle = new MyDetectedEntityInfo();
            }

            /// <summary>
            /// Constructs the strategy with the given stopping distance, reference block and camera list.
            /// </summary>
            /// <param name="distance">How far from the collision point we should stop. 
            /// Measured from the surface of ship's bounding sphere.</param>
            /// <param name="camera">Forward-facing camera to use for raycasting.</param>
            /// <param name="reference">Reference block that will determine forward direction</param>
            /// <param name="forward">The direction to be considered forward, relative to reference block.</param>
            public FlyForwardStrategy(double distance,
                IMyCameraBlock camera,
                IMyTerminalBlock reference = null,
                Base6Directions.Direction forward = Base6Directions.Direction.Forward) 
                : this(distance, new List<IMyCameraBlock> { camera }, reference, forward)
            {
            }

            public override bool Update(AutoPilot owner, ref Vector3D linearV, ref Vector3D angularV)
            {
                if (CurrentState == State.Destination) return true;
                angularV = Vector3D.Zero;

                IMyCubeBlock reference = Reference ?? owner.Controller;
                MatrixD wm = reference.WorldMatrix;
                TargetVector = wm.GetDirectionVector(ReferenceForward);
                BoundingSphereD bs = reference.CubeGrid.WorldVolume;
                Vector3D refpoint = bs.Center + TargetVector * (bs.Radius + Distance);

                RaycastChargeSpeed = 0;
                foreach (IMyCameraBlock cam in Cameras)
                    if (cam.IsWorking && (cam.WorldMatrix.Forward.Dot(TargetVector) > Math.Cos(cam.RaycastConeLimit)))
                    {
                        RaycastChargeSpeed += 2000.0;
                        cam.Enabled = true;
                        cam.EnableRaycast = true;
                    }
                if (RaycastChargeSpeed == 0) //we can't scan in that orientation (or at all)!
                    throw new Exception("Can't scan!");

                //owner.Log?.Invoke($"Direction: {TargetVector.X:F1}:{TargetVector.Y:F1}:{TargetVector.Z:F1}");
                double accel = owner.GetMaxAccelerationFor(TargetVector);
                double decel = owner.GetMaxAccelerationFor(-TargetVector);
                //owner.Log?.Invoke($"Accel: {accel:F1}m/s^2");
                //owner.Log?.Invoke($"Decel: {decel:F1}m/s^2");
                if (decel == 0) //we can't decelerate afterwards!
                    throw new Exception("Can't decelerate!");

                double speed = linearV.Length();
                //account for possible acceleration
                speed += accel * owner.elapsedTime; 
                //account for obstacle velocity relative to us
                double obstacle_speed = Obstacle.IsEmpty() ? 0.0
                    : Math.Max(0, Obstacle.Velocity.Dot(-TargetVector));
                speed += obstacle_speed;

                //time required to charge the camera enough to scan
                double scan_charge_time = (speed * speed) / (2 * decel * (RaycastChargeSpeed - speed));
                //this is reaction time - how much time will pass before we can re-scan
                scan_charge_time = Math.Max(scan_charge_time, owner.elapsedTime);
                //distance requried to decelerate + distance we will pass in time till the next scan
                double scan_dist = scan_charge_time * RaycastChargeSpeed;
                scan_dist = Math.Max(scan_dist, 100);

                if (ScanForObstacles(owner, scan_dist))
                {   //obstacle detected - we are done
                    linearV = Vector3D.Zero;
                    return true;
                }
                else
                {   //no obstacles detected - fly at top speed
                    //owner.Log?.Invoke("Miss");
                    linearV = TargetVector * MaxLinearSpeed;
                    return false;
                }
            }

            private bool ScanForObstacles(AutoPilot owner, double distance)
            {
                foreach (IMyCameraBlock cam in Cameras)
                    if (cam.EnableRaycast)
                    {
                        Vector3D scan = cam.GetPosition() + TargetVector * distance;
                        cam.Enabled = true;
                        if (cam.CanScan(scan))
                        {
                            //owner.Log?.Invoke("Scanning via " + cam.CustomName);
                            Obstacle = cam.Raycast(scan);
                            return !Obstacle.IsEmpty();
                        }
                    }
                //owner.Log?.Invoke("No cameras can scan at this time.");
                return false;
            }
        }
    }
}
