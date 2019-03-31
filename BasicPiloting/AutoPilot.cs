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
        public class AutoPilot
        {
            public List<BasePilotingStrategy> Tasks = new List<BasePilotingStrategy>();
            public BasePilotingStrategy CurrentTask { get { return Tasks.Count > 0 ? Tasks.First() : null; } }
            public IMyShipController Controller { get; private set; }
            public double elapsedTime;
            public bool RepeatLastTask = false;

            public MyShipVelocities Velocities { get; private set; }
            public MyShipMass Mass { get; private set; }

            public MatrixD WorldMatrix { get { return Controller.WorldMatrix; } }
            public Vector3D Position { get { return Controller.WorldMatrix.Translation; } }
            public Vector3D Forward { get { return Controller.WorldMatrix.GetDirectionVector(Base6Directions.Direction.Forward); } }
            public Vector3D Up { get { return Controller.WorldMatrix.GetDirectionVector(Base6Directions.Direction.Up); } }
            public Vector3D Left { get { return Controller.WorldMatrix.GetDirectionVector(Base6Directions.Direction.Left); } }
            public Vector3D Velocity { get { return Controller.GetShipVelocities().LinearVelocity; } }
            public Vector3D Rotation { get { return Controller.GetShipVelocities().AngularVelocity; } }

            public delegate void LogFunction(string msg);
            public LogFunction Log = null;

            public AutoPilot(IMyShipController controller, List<IMyThrust> thrusters, List<IMyGyro> gyros)
            { Thrusters = thrusters; Gyros = gyros; Controller = controller; ThrustValues = new double[Thrusters.Count]; }
            public AutoPilot(IMyGridTerminalSystem gts, IMyShipController controller = null, IMyTerminalBlock reference = null)
            {
                if (controller == null)
                    gts.GetBlocksOfType<IMyShipController>(null, (b) => { if (b.IsFunctional) Controller = b; return false; });
                else
                    Controller = controller;
                if (Controller == null)
                    throw new Exception("No controller on this ship!");
                Thrusters = new List<IMyThrust>();
                gts.GetBlocksOfType(Thrusters, (t) => t.IsFunctional && t.CubeGrid.EntityId == Controller.CubeGrid.EntityId);
                ThrustValues = new double[Thrusters.Count];
                Gyros = new List<IMyGyro>();
                gts.GetBlocksOfType(Gyros, (g) => g.IsFunctional && g.CubeGrid.EntityId == Controller.CubeGrid.EntityId);
            }

            List<IMyThrust> Thrusters;
            double[] ThrustValues;
            List<IMyGyro> Gyros;

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
                    Vector3D LinearV = Velocities.LinearVelocity;
                    Vector3D AngularV = Velocities.AngularVelocity;
                    //task can always set respective velocities to 0 to avoid linear and/or angular motion
                    Controller.DampenersOverride = false; 
                    elapsedTime = elapsed;
                    bool done = Tasks[0].Update(this, ref LinearV, ref AngularV);
                    SetRotationVelocity(AngularV);
                    SetThrustVector(LinearV);
                    if (done && (!RepeatLastTask || (Tasks.Count > 1))) //current task has been complete
                    {
                        Tasks.RemoveAt(0); //remove current task
                        if (Tasks.Count == 0) //that was the last task
                        {
                            //set the ship to manual piloting
                            DisableOverrides();
                            SetInertialDampeners(true);
                        }
                    }
                    return done;
                }
            }

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

            public void SetInertialDampeners(bool val)
            {
                Controller.DampenersOverride = val;
            }

            void SetThrustVector(Vector3D targetvel)
            {
                MyShipVelocities vels = Controller.GetShipVelocities();
                Vector3D deltaV = targetvel - vels.LinearVelocity;
                //if (!Vector3D.IsZero(angularvel) && !Vector3D.IsZero(deltaV))
                //{
                //    MatrixD rotmatrix = MatrixD.CreateFromYawPitchRoll(-angularvel.Y * elapsedTime / 2, -angularvel.X * elapsedTime / 2, -angularvel.Z * elapsedTime / 2);
                //    deltaV = Vector3D.Rotate(deltaV, rotmatrix);
                //}
                if (!Vector3D.IsZero(deltaV))
                {
                    double deltavabs = deltaV.Normalize();
                    double maxaccel = deltavabs / elapsedTime;
                    MyShipMass mass = Controller.CalculateShipMass();
                    double requiredthrust = mass.TotalMass * maxaccel;
                    double totalthrustvalue = 0.0;
                    for (int i = Thrusters.Count - 1; i >= 0; i--)
                        if (Thrusters[i].IsWorking)
                        {
                            double projection = Thrusters[i].WorldMatrix.GetDirectionVector(Base6Directions.Direction.Backward).Dot(deltaV);
                            ThrustValues[i] = (projection > 0)
                                ? projection * Thrusters[i].MaxEffectiveThrust
                                : 0;
                            totalthrustvalue += ThrustValues[i];
                        }
                        else
                            ThrustValues[i] = 0.0;
                    for (int i = Thrusters.Count - 1; i >= 0; i--)
                        Thrusters[i].ThrustOverride = (float)(requiredthrust * ThrustValues[i] / totalthrustvalue);
                }
                else
                    foreach (IMyThrust t in Thrusters)
                        t.ThrustOverride = 0.0f;
            }

            void SetRotationVelocity(double pitch, double yaw, double roll)
            {
                SetRotationVelocity(new Vector3D(pitch, yaw, roll));
            }
            void SetRotationVelocity(Vector3D gridRotation)
            {
                if (Vector3D.IsZero(gridRotation))
                {
                    foreach (IMyGyro g in Gyros)
                    {
                        g.Pitch = 0.0f;
                        g.Yaw = 0.0f;
                        g.Roll = 0.0f;
                        g.GyroOverride = true;
                    }
                }
                else
                {
                    Vector3D fixedGridRotation = new Vector3D(-gridRotation.X, gridRotation.Y, gridRotation.Z);
                    Vector3D worldRotation = Vector3D.TransformNormal(gridRotation, Controller.WorldMatrix);
                    foreach (IMyGyro g in Gyros)
                    {
                        //transform rotation vector into gyro's block-space coordinates
                        Vector3D gyroRotation = Vector3D.TransformNormal(worldRotation, MatrixD.Transpose(g.WorldMatrix));
                        g.Pitch = (float)gyroRotation.X;
                        g.Yaw = (float)gyroRotation.Y;
                        g.Roll = (float)gyroRotation.Z;
                        g.GyroOverride = true;
                    }
                }
            }

            public double RotateToMatch(Vector3D target, Vector3D forward, Vector3D up, ref Vector3D vel)
            {
                double diff = forward.Dot(target);
                Vector3D left = up.Cross(forward);
                Vector3D movevector = forward - target;
                vel.Z = 0;
                vel.X = up.Dot(movevector);
                vel.Y = left.Dot(movevector);
                if (diff < 0)
                    vel.Y += Math.Sign(vel.Y);
                diff = 1 - diff;
                return diff;
            }
        }
    }
}
