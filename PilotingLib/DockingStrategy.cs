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
            /// How close (in meters) we should be to the target to engage autolock.
            /// Non-positive values disable this behaviour.
            /// If docking block does not support autolock, it's ignored.
            /// </summary>
            public double AutoLockDistance = 0.0;
            /// <summary>
            /// Vector pointing directly away from the dock, world-space. 
            /// If non-zero, ship will orient itself to face opposite of this vector, and will approach the dock riding on it.
            /// If zero, ship will fly directly towards the dock, which may result in crooked or failed docking.
            /// </summary>
            public Vector3D Approach;
            public Vector3D Facing;
            /// <summary>
            /// Constructs docking strategy for a ship connector.
            /// </summary>
            /// <param name="goal">Location of matching connector block, world-space.</param>
            /// <param name="connector">Connector block to use.</param>
            /// <param name="approach">Direction in which connector's working end should be facing.</param>
            /// <param name="facing">Direction in which connector's 'top' should be facing.</param>
            public DockingStrategy(Waypoint goal, IMyShipConnector connector, Vector3D approach, Vector3D? facing = null) 
                : this(goal, connector, Base6Directions.Direction.Forward, Base6Directions.Direction.Up, approach, facing)
            { }
            /// <summary>
            /// Constructs docking strategy for a merge block.
            /// </summary>
            /// <param name="goal">Location of matching merge block, world-space.</param>
            /// <param name="merger">Merge block to use.</param>
            /// <param name="approach">Direction in which merger's working end should be facing.</param>
            /// <param name="facing">Direction in which merger's top should be facing.</param>
            public DockingStrategy(Waypoint goal, IMyShipMergeBlock merger, Vector3D approach, Vector3D? facing = null) 
                : this(goal, merger, Base6Directions.Direction.Right, Base6Directions.Direction.Up, approach, facing)
            { }
            /// <summary>
            /// Constructs docking strategy for a landing gear.
            /// </summary>
            /// <param name="goal">Location of the landing zone, world-space.</param>
            /// <param name="gear">Landing gear to use.</param>
            /// <param name="approach">Direction in which landing gear's working end should be facing.</param>
            /// <param name="facing">Direction in which landing gear's forward vector should be facing.</param>
            public DockingStrategy(Waypoint goal, IMyLandingGear gear, Vector3D approach, Vector3D? facing = null)
                : this(goal, gear, Base6Directions.Direction.Down, Base6Directions.Direction.Forward, approach, facing)
            { }
            /// <summary>
            /// Constructs docking strategy for a rotor stator (on ship's side).
            /// </summary>
            /// <param name="goal">Location of the landing zone, world-space.</param>
            /// <param name="rotor">Rotor to use.</param>
            /// <param name="approach">Direction in which rotor's working end should be facing.</param>
            /// <param name="facing">Direction in which rotor's forward vector should be facing.</param>
            public DockingStrategy(Waypoint goal, IMyMotorStator rotor, Vector3D approach, Vector3D? facing = null)
                : this(goal, rotor, Base6Directions.Direction.Up, Base6Directions.Direction.Forward, approach, facing)
            { }
            /// <summary>
            /// Constructs docking strategy for a rotor top (on ship's side).
            /// Remember, top attachment needs to be done on accepting side!
            /// </summary>
            /// <param name="goal">Location of the landing zone, world-space.</param>
            /// <param name="rotor">Rotor to use.</param>
            /// <param name="approach">Direction in which rotor's working end should be facing.</param>
            /// <param name="facing">Direction in which rotor's forward vector should be facing.</param>
            public DockingStrategy(Waypoint goal, IMyMotorRotor rotor, Vector3D approach, Vector3D? facing = null)
                : this(goal, rotor, Base6Directions.Direction.Down, Base6Directions.Direction.Forward, approach, facing)
            { }

            private DockingStrategy(
                Waypoint goal, 
                IMyCubeBlock clamp, 
                Base6Directions.Direction forward, 
                Base6Directions.Direction up, 
                Vector3D approach, Vector3D? facing = null)
                : base(goal, clamp, forward, up)
            {
                MaxLinearSpeed = 2.0;
                Approach = approach;
                Approach.Normalize();
                Facing = facing.HasValue ? facing.Value : Vector3D.Zero;
                if (Reference is IMyShipConnector)
                {
                    TryLockIn = TryLockInConnector;
                    Unlock = UnlockConnector;
                }
                else if (Reference is IMyLandingGear)
                {
                    TryLockIn = TryLockInLGear;
                    Unlock = UnlockLGear;
                }
                else if (Reference is IMyShipMergeBlock)
                {
                    TryLockIn = TryLockInMerge;
                    Unlock = UnlockMerge;
                }
                else if (Reference is IMyMotorStator)
                {
                    TryLockIn = TryLockInStator;
                    Unlock = UnlockStator;
                }
                else if (Reference is IMyMotorRotor)
                {
                    TryLockIn = TryLockInRotor;
                    Unlock = UnlockRotor;
                }
                else
                    throw new Exception("Somehow, reference block is not a lockable one!");

            }
            /// <summary>
            /// Calculates position and approach vector to dock on specific ship connector.
            /// </summary>
            /// <param name="connector">Connector to use.</param>
            /// <param name="pos">Connector position.</param>
            /// <param name="approach">Approach direction.</param>
            public static void CalculateApproach(IMyShipConnector connector, out Vector3D pos, out Vector3D approach)
            {
                MatrixD wm = connector.WorldMatrix;
                pos = wm.Translation;
                approach = -wm.GetDirectionVector(Base6Directions.Direction.Forward);
            }
            /// <summary>
            /// Calculates position and approach vector to dock on specific merge block.
            /// </summary>
            /// <param name="merger">Merge block to use.</param>
            /// <param name="pos">Merge block's position.</param>
            /// <param name="approach">Approach direction.</param>
            public static void CalculateApproach(IMyShipMergeBlock merger, out Vector3D pos, out Vector3D approach)
            {
                MatrixD wm = merger.WorldMatrix;
                pos = wm.Translation;
                approach = -wm.GetDirectionVector(Base6Directions.Direction.Right);
            }
            /// <summary>
            /// Calculates position and approach vector to dock on specific rotor top.
            /// </summary>
            /// <param name="top">Rotor top to use.</param>
            /// <param name="pos">Rotor top's position.</param>
            /// <param name="approach">Approach direction.</param>
            public static void CalculateApproach(IMyMotorRotor top, out Vector3D pos, out Vector3D approach)
            {
                MatrixD wm = top.WorldMatrix;
                pos = wm.Translation;
                approach = -wm.GetDirectionVector(Base6Directions.Direction.Down);
            }

            public override bool Update(AutoPilot owner, ref Vector3D linearV, ref Vector3D angularV)
            {
                if (Goal == null) return false;
                IMyCubeBlock reference = Reference ?? owner.Controller;
                MatrixD wm = reference.WorldMatrix;
                Goal.UpdateTime(owner.elapsedTime);
                Vector3D currentGoalPos = Goal.CurrentPosition;
                Vector3D direction = currentGoalPos - wm.Translation;
                double distance = direction.Normalize();
                double target_distance = distance;
                double diff;

                if (!Vector3D.IsZero(Approach))
                {
                    Vector3D minusApproach = -Approach;
                    diff = owner.RotateToMatch(Approach, Facing,
                        wm.GetDirectionVector(ReferenceForward),
                        wm.GetDirectionVector(ReferenceUp),
                        ref angularV);
                    PlaneD alignment = new PlaneD(wm.Translation, minusApproach);
                    Vector3D alignedPos = alignment.Intersection(ref currentGoalPos, ref minusApproach);
                    Vector3D correction = alignedPos - wm.Translation;
                    if (!Vector3D.IsZero(correction, PositionEpsilon)) //are we on approach vector?
                    {   //no - let's move there
                        direction = correction;
                        distance = direction.Normalize();
                    }
                    //otherwise, we can keep our current direction
                }
                else
                    diff = owner.RotateToMatch(direction, Facing,
                        wm.GetDirectionVector(ReferenceForward),
                        wm.GetDirectionVector(ReferenceUp),
                        ref angularV);
                //rotate the ship to face it
                if (diff > OrientationEpsilon) //we still need to rotate
                    linearV = Goal.Velocity; //match velocities with our target, then.
                else //we are good
                {
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
                return TryLockIn(distance) || (target_distance < PositionEpsilon);
            }

            public Func<double, bool> TryLockIn { get; private set; }
            public Action Unlock { get; private set; }

            void UnlockConnector()
            {
                IMyShipConnector clamp = Reference as IMyShipConnector;
                clamp.Disconnect();
            }

            void UnlockMerge()
            {
                IMyShipMergeBlock clamp = Reference as IMyShipMergeBlock;
                clamp.Enabled = false;
            }

            void UnlockLGear()
            {
                IMyLandingGear clamp = Reference as IMyLandingGear;
                clamp.Unlock();
            }

            void UnlockStator()
            {
                IMyMotorStator clamp = Reference as IMyMotorStator;
                clamp.Detach();
            }

            void UnlockRotor()
            {
                IMyMotorRotor clamp = Reference as IMyMotorRotor;
                if (clamp.IsAttached)
                    clamp.Base.Detach();
            }

            bool TryLockInConnector(double distance)
            {
                IMyShipConnector clamp = Reference as IMyShipConnector;
                clamp.Enabled = true;
                if (clamp.Status == MyShipConnectorStatus.Connectable)
                {
                    clamp.Connect();
                    return clamp.Status == MyShipConnectorStatus.Connected;
                }
                else
                    return false;
            }

            bool TryLockInLGear(double distance)
            {
                IMyLandingGear clamp = Reference as IMyLandingGear;
                clamp.Enabled = true;
                if (AutoLockDistance > 0)
                    clamp.AutoLock = distance < AutoLockDistance;
                if (clamp.LockMode == LandingGearMode.ReadyToLock)
                    clamp.Lock();
                return clamp.LockMode == LandingGearMode.Locked;
            }

            bool TryLockInMerge(double distance)
            {
                IMyShipMergeBlock clamp = Reference as IMyShipMergeBlock;
                clamp.Enabled = true;
                return clamp.IsConnected;
            }

            bool TryLockInStator(double distance)
            {
                IMyMotorStator clamp = Reference as IMyMotorStator;
                clamp.Enabled = true;
                clamp.RotorLock = true;
                clamp.Attach();
                return clamp.IsAttached;
            }

            bool TryLockInRotor(double distance)
            {
                IMyMotorRotor clamp = Reference as IMyMotorRotor;
                return clamp.IsAttached;
            }
        }
    }
}
