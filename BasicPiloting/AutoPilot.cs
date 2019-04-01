﻿using Sandbox.Game.EntityComponents;
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
        /// Maintains a list of piloting tasks to complete, and performs them in sequence.
        /// Call Update() method often to ensure corrections can be made on time.
        /// </summary>
        public class AutoPilot
        {
            /// <summary>
            /// List of tasks to complete, in order of completion.
            /// </summary>
            public List<BasePilotingStrategy> Tasks = new List<BasePilotingStrategy>();
            /// <summary>
            /// Current task, or null if there is none.
            /// </summary>
            public BasePilotingStrategy CurrentTask { get { return Tasks.Count > 0 ? Tasks.First() : null; } }
            /// <summary>
            /// Ship controller (cockpit, flight station or remote controller) used by the autopilot. 
            /// The ship MUST have at least one installed.
            /// </summary>
            public IMyShipController Controller { get; private set; }
            /// <summary>
            /// Time elapsed since last update.
            /// </summary>
            public double elapsedTime { get; private set; }
            /// <summary>
            /// If true, then last task in the list will not be discarded even when it's completed.
            /// Useful for maintaining position or keeping pursuit of the target.
            /// </summary>
            public bool RepeatLastTask = false;
            /// <summary>
            /// Ship velocities just before last task update.
            /// </summary>
            public MyShipVelocities Velocities { get; private set; }
            /// <summary>
            /// Ship mass jsut before last task update.
            /// </summary>
            public MyShipMass Mass { get; private set; }
            
            public MatrixD WorldMatrix { get { return Controller.WorldMatrix; } }
            public Vector3D Position { get { return Controller.WorldMatrix.Translation; } }
            public Vector3D Forward { get { return Controller.WorldMatrix.GetDirectionVector(Base6Directions.Direction.Forward); } }
            public Vector3D Up { get { return Controller.WorldMatrix.GetDirectionVector(Base6Directions.Direction.Up); } }
            public Vector3D Left { get { return Controller.WorldMatrix.GetDirectionVector(Base6Directions.Direction.Left); } }
            public Vector3D Velocity { get { return Controller.GetShipVelocities().LinearVelocity; } }
            public Vector3D Rotation { get { return Controller.GetShipVelocities().AngularVelocity; } }

            public delegate void LogFunction(string msg);
            /// <summary>
            /// This function can be used for debug logging, but it's up to user to provide actual log output.
            /// Like Echo() or a screen.
            /// </summary>
            public LogFunction Log = null;
            /// <summary>
            /// Create autopilot using specific set of blocks.
            /// </summary>
            /// <param name="controller">Ship controller to use.</param>
            /// <param name="thrusters">Thrusters to use.</param>
            /// <param name="gyros">Gyros to use.</param>
            public AutoPilot(IMyShipController controller, List<IMyThrust> thrusters, List<IMyGyro> gyros)
            { Thrusters = thrusters; Gyros = gyros; Controller = controller; ThrustValues = new double[Thrusters.Count]; }
            /// <summary>
            /// Create autopilot and let it find the required blocks.
            /// </summary>
            /// <param name="gts">GridTerminalSystem to use for block lookup.</param>
            /// <param name="basis">Only blocks on the exact same grid as this one will be used.</param>
            public AutoPilot(IMyGridTerminalSystem gts, IMyTerminalBlock basis)
            {
                Controller = basis as IMyShipController;
                if (Controller == null)
                    gts.GetBlocksOfType<IMyShipController>(null, (b) => 
                    {
                        if (b.IsFunctional && b.CubeGrid.EntityId == basis.CubeGrid.EntityId)
                            Controller = b;
                        return false;
                    });
                if (Controller == null)
                    throw new Exception("No controller found on this grid!");
                Thrusters = new List<IMyThrust>();
                gts.GetBlocksOfType(Thrusters, (t) => t.IsFunctional && t.CubeGrid.EntityId == Controller.CubeGrid.EntityId);
                ThrustValues = new double[Thrusters.Count];
                Gyros = new List<IMyGyro>();
                gts.GetBlocksOfType(Gyros, (g) => g.IsFunctional && g.CubeGrid.EntityId == Controller.CubeGrid.EntityId);
            }

            List<IMyThrust> Thrusters;
            double[] ThrustValues;
            List<IMyGyro> Gyros;
            /// <summary>
            /// Determines how quickly the ship can accelerate in a given direction without changing its orientation.
            /// Useful for determining how quickly you can slow down to a full stop.
            /// </summary>
            /// <param name="direction">Direction vector in world-space.</param>
            /// <returns>Acceleration in m/s^2</returns>
            public double GetMaxAccelerationFor(Vector3D direction)
            {
                double thrust = 0.0;
                direction.Normalize();
                foreach (IMyThrust t in Thrusters)
                    if (t.IsWorking)
                    {
                        double projection = t.WorldMatrix.GetDirectionVector(Base6Directions.Direction.Backward).Dot(direction);
                        if (projection > 0)
                            thrust += t.MaxEffectiveThrust * projection;
                    }
                return thrust / Mass.TotalMass;
            }
            /// <summary>
            /// Queries the current task for suggestions on linear and angular velocities, and sets up thrusters and gyros accordingly.
            /// </summary>
            /// <param name="elapsed">How many seconds elapsed since the last update.</param>
            /// <returns>True if all tasks have been completed.</returns>
            public bool Update(double elapsed)
            {
                if (Tasks.Count == 0) //if there is nothing to do, drift
                    return true; //report that we are done
                else //there is something to do
                {
                    if (Tasks.Count > 0)
                    {
                        Log?.Invoke($"Task (total {Tasks.Count}): {Tasks[0].GetType().Name}");
                        Log?.Invoke(Tasks[0].Goal.ToString());
                    }
                    Velocities = Controller.GetShipVelocities();
                    Mass = Controller.CalculateShipMass();
                    // current velocities
                    Vector3D LinearV = Velocities.LinearVelocity;
                    Vector3D AngularV = Velocities.AngularVelocity;
                    //task can always set respective velocities to 0 to avoid linear and/or angular motion
                    Controller.DampenersOverride = false;
                    elapsedTime = elapsed;
                    //query the task.
                    bool done = Tasks[0].Update(this, ref LinearV, ref AngularV);
                    //whether its done or not, apply changes to thrust/rotation
                    SetRotationVelocity(AngularV, Tasks[0].Reference, Tasks[0].ReferenceForward, Tasks[0].ReferenceUp);
                    SetThrustVector(LinearV);
                    if (done && (!RepeatLastTask || (Tasks.Count > 1)))
                    {   //current task has been completed, and we shouldn't keep it
                        Tasks.RemoveAt(0); //remove current task
                        if (Tasks.Count == 0) //that was the last task
                        {
                            //set the ship to manual piloting
                            Controller.DampenersOverride = true;
                            DisableOverrides();
                        }
                    }
                    return done;
                }
            }
            /// <summary>
            /// Disables overrides on used thrusters/gyros. 
            /// Useful if you need to quickly switch the ship to manual piloting.
            /// Does not change the state of inertia dampeners!
            /// </summary>
            public void DisableOverrides()
            {
                foreach (IMyThrust t in Thrusters)
                    t.ThrustOverride = 0.0f;
                foreach (IMyGyro g in Gyros)
                {
                    g.Yaw = 0.0f;
                    g.Pitch = 0.0f;
                    g.Roll = 0.0f;
                    g.GyroOverride = false;
                }
            }
            /// <summary>
            /// Calculates and thrust required to reach target velocity and configures thrusters to do so.
            /// </summary>
            /// <param name="targetvel">Desired velocity in world-space.</param>
            void SetThrustVector(Vector3D targetvel)
            {
                //how much our current velocity differs from desired one?
                Vector3D deltaV = targetvel - Velocities.LinearVelocity; 
                //if (!Vector3D.IsZero(angularvel) && !Vector3D.IsZero(deltaV))
                //{
                //    MatrixD rotmatrix = MatrixD.CreateFromYawPitchRoll(-angularvel.Y * elapsedTime / 2, -angularvel.X * elapsedTime / 2, -angularvel.Z * elapsedTime / 2);
                //    deltaV = Vector3D.Rotate(deltaV, rotmatrix);
                //}
                if (!Vector3D.IsZero(deltaV, 1e-3)) // it does, significantly.
                {
                    double deltavabs = deltaV.Normalize();
                    //Estimate thrust required to stop before the next update.
                    //Prevents the ship from oscillating back and forth around the target.
                    double maxaccel = deltavabs / elapsedTime; 
                    MyShipMass mass = Controller.CalculateShipMass();
                    double requiredthrust = mass.TotalMass * maxaccel;
                    //Calculate how much thrust each thruster can contribute to pushing the ship in required direction.
                    //It depends on thruster overall power, its effectiveness (ion/atmo) and direction.
                    double totalthrustvalue = 0.0;
                    for (int i = Thrusters.Count - 1; i >= 0; i--)
                        if (Thrusters[i].IsWorking) //disabled/damaged/unfueled thrusters are ignored
                        {
                            double projection = Thrusters[i].WorldMatrix.GetDirectionVector(Base6Directions.Direction.Backward).Dot(deltaV);
                            ThrustValues[i] = (projection > 0)
                                ? projection * Thrusters[i].MaxEffectiveThrust
                                : 0;
                            totalthrustvalue += ThrustValues[i];
                        }
                        else
                            ThrustValues[i] = 0.0;
                    //Set thruster overrides accordingly.
                    for (int i = Thrusters.Count - 1; i >= 0; i--)
                        Thrusters[i].ThrustOverride = (float)(requiredthrust * ThrustValues[i] / totalthrustvalue);
                }
                else //We have already achieved target velocity
                    foreach (IMyThrust t in Thrusters)
                        t.ThrustOverride = 0.0f;
            }
            /// <summary>
            /// Configures gyros to apply required rotation.
            /// </summary>
            /// <param name="gridRotation">Angular velocity of the ship in grid-space.</param>
            /// <param name="reference">Reference block used (controller otherwise).</param>
            void SetRotationVelocity(Vector3D gridRotation, 
                IMyTerminalBlock reference, 
                Base6Directions.Direction forward,
                Base6Directions.Direction up)
            {
                if (Vector3D.IsZero(gridRotation)) //do we need to stop?
                {   //well, that's easy
                    foreach (IMyGyro g in Gyros)
                    {
                        g.Pitch = 0.0f;
                        g.Yaw = 0.0f;
                        g.Roll = 0.0f;
                        g.GyroOverride = true;
                    }
                }
                else //no, we need to rotate
                {
                    //a bit of a mess with X axis sign... no idea why, but it's necessary to flip it.
                    Vector3D fixedGridRotation = new Vector3D(-gridRotation.X, gridRotation.Y, gridRotation.Z);
                    //Translate rotation vector from grid-space to world-space.
                    IMyTerminalBlock refblock = reference ?? Controller;
                    MatrixD wm = refblock.WorldMatrix;
                    if (forward != Base6Directions.Direction.Forward || up != Base6Directions.Direction.Up)
                    {
                        Vector3D old_forward = wm.GetDirectionVector(forward);
                        Vector3D old_up = wm.GetDirectionVector(up);
                        wm.SetDirectionVector(Base6Directions.Direction.Forward, old_forward);
                        wm.SetDirectionVector(Base6Directions.Direction.Up, old_up);
                    }
                    Vector3D worldRotation = Vector3D.TransformNormal(gridRotation, wm);
                    foreach (IMyGyro g in Gyros)
                    {
                        //transform rotation vector into this gyro's block-space
                        Vector3D gyroRotation = Vector3D.TransformNormal(worldRotation, MatrixD.Transpose(g.WorldMatrix));
                        g.Pitch = (float)gyroRotation.X;
                        g.Yaw = (float)gyroRotation.Y;
                        g.Roll = (float)gyroRotation.Z;
                        g.GyroOverride = true;
                    }
                }
            }
        }
    }
}
