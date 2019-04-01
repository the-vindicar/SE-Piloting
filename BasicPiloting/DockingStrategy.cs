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
        /// Approaches the target slowly, until the selected block locks in.
        /// </summary>
        public class DockingStrategy : BasePilotingStrategy
        {
            /// <summary>
            /// How close to the maximum safe speed are we allowed to get.
            /// </summary>
            public double VelocityUsage = 0.9;
            /// <summary>
            /// Vector pointing directly away from the dock, world-space. 
            /// If non-zero, ship will orient itself to face opposite of this vector, and will approach the dock riding on it.
            /// If zero, ship will fly directly towards the dock, which may result in crooked or failed docking.
            /// </summary>
            public Vector3D Approach;
            /// <summary>
            /// Constructs docking strategy for a ship connector.
            /// </summary>
            /// <param name="goal">Location of matching connector block, world-space.</param>
            /// <param name="approach"></param>
            /// <param name="connector"></param>
            public DockingStrategy(Waypoint goal, Vector3D approach, IMyShipConnector connector) 
                : base(goal, connector, Base6Directions.Direction.Forward, Base6Directions.Direction.Up)
            {
                MaxLinearSpeed = 2.0;
                Approach = approach;
                Approach.Normalize();
            }

            public DockingStrategy(Waypoint goal, Vector3D approach, IMyShipMergeBlock merger) 
                : base(goal, merger, Base6Directions.Direction.Right, Base6Directions.Direction.Up)
            {
                MaxLinearSpeed = 2.0;
                Approach = approach;
                Approach.Normalize();
            }

            public DockingStrategy(Waypoint goal, Vector3D approach, IMyLandingGear gear)
                : base(goal, gear, Base6Directions.Direction.Down, Base6Directions.Direction.Forward)
            {
                MaxLinearSpeed = 2.0;
                Approach = approach;
                Approach.Normalize();
            }

            public static void CalculateApproach(IMyShipConnector connector, out Vector3D pos, out Vector3D approach)
            {
                MatrixD wm = connector.WorldMatrix;
                pos = wm.Translation;
                approach = wm.GetDirectionVector(Base6Directions.Direction.Forward);
            }

            public static void CalculateApproach(IMyShipMergeBlock connector, out Vector3D pos, out Vector3D approach)
            {
                MatrixD wm = connector.WorldMatrix;
                pos = wm.Translation;
                approach = wm.GetDirectionVector(Base6Directions.Direction.Right);
            }

            public override bool Update(AutoPilot owner, ref Vector3D linearV, ref Vector3D angularV)
            {
                if (Goal == null) return false;
                if (TryLockIn())
                {
                    linearV = Goal.Velocity;
                    angularV = Vector3D.Zero;
                    return true;
                }
                bool distanceok = false;
                bool orientationok = false;
                IMyTerminalBlock reference = Reference ?? owner.Controller;
                MatrixD wm = reference.WorldMatrix;
                Goal.UpdateTime(owner.elapsedTime);
                Vector3D currentGoalPos = Goal.CurrentPosition;
                Vector3D direction = currentGoalPos - wm.Translation;
                double distance = direction.Normalize();
                double diff;

                if (!Vector3D.IsZero(Approach))
                {
                    diff = RotateToMatch(-Approach, Vector3D.Zero,
                        wm.GetDirectionVector(ReferenceForward),
                        wm.GetDirectionVector(ReferenceUp),
                        ref angularV);
                    PlaneD alignment = new PlaneD(wm.Translation, Approach);
                    Vector3D alignedPos = alignment.Intersection(ref currentGoalPos, ref Approach);
                    Vector3D correction = alignedPos - wm.Translation;
                    if (!Vector3D.IsZero(correction, PositionEpsilon)) //are we on approach vector?
                    {   //no - let's move there
                        direction = correction;
                        distance = direction.Normalize(); 
                    }
                    //otherwise, we can keep our current direction
                }
                else
                    diff = RotateToMatch(direction, Vector3D.Zero,
                        wm.GetDirectionVector(ReferenceForward),
                        wm.GetDirectionVector(ReferenceUp),
                        ref angularV);
                if (distance > PositionEpsilon) //Are we too far from our desired position?
                {
                    //rotate the ship to face it
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

            public bool TryLockIn()
            {
                if (Reference is IMyShipConnector)
                    return TryLockIn(Reference as IMyShipConnector);
                else if (Reference is IMyLandingGear)
                    return TryLockIn(Reference as IMyLandingGear);
                else if (Reference is IMyShipMergeBlock)
                    return TryLockIn(Reference as IMyShipMergeBlock);
                else
                    return false;
            }

            public void Unlock()
            {
                if (Reference is IMyShipConnector)
                    Unlock(Reference as IMyShipConnector);
                else if (Reference is IMyLandingGear)
                    Unlock(Reference as IMyLandingGear);
                else if (Reference is IMyShipMergeBlock)
                    Unlock(Reference as IMyShipMergeBlock);
            }

            void Unlock(IMyShipConnector clamp)
            {
                clamp.Disconnect();
            }

            void Unlock(IMyShipMergeBlock clamp)
            {
                clamp.Enabled = false;
            }

            void Unlock(IMyLandingGear clamp)
            {
                clamp.Unlock();
            }

            bool TryLockIn(IMyShipConnector clamp)
            {
                clamp.Enabled = true;
                if (clamp.Status == MyShipConnectorStatus.Connectable)
                {
                    clamp.Connect();
                    return clamp.Status == MyShipConnectorStatus.Connected;
                }
                else
                    return false;
            }

            bool TryLockIn(IMyLandingGear clamp)
            {
                clamp.Enabled = true;
                clamp.AutoLock = true;
                if (clamp.LockMode == LandingGearMode.ReadyToLock)
                    clamp.Lock();
                return clamp.LockMode == LandingGearMode.Locked;
            }

            bool TryLockIn(IMyShipMergeBlock clamp)
            {
                clamp.Enabled = true;
                return clamp.IsConnected;
            }

        }
    }
}
