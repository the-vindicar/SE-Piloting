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
        public class Waypoint
        {
            double _ElapsedTime;
            Vector3D _Position;
            /// <summary>
            /// Entity we are tracking.
            /// </summary>
            public MyDetectedEntityInfo Entity { get; private set; }
            /// <summary>
            /// Name of the waypoint: name of the entity or GPS point.
            /// </summary>
            public string Name { get; private set; }
            /// <summary>
            /// Current position of the target, based on last known position and velocity.
            /// Or static position, if velocity is zero.
            /// </summary>
            public Vector3D CurrentPosition
            {
                get
                {
                    return Vector3D.IsZero(Velocity) ? _Position
                        : (_Position + (_ElapsedTime / 1000.0) * Velocity);
                }
            }
            /// <summary>
            /// Last known velocity of the target.
            /// </summary>
            public Vector3D Velocity { get; private set; }
            /// <summary>
            /// Distance to the target that ought to be maintained.
            /// </summary>
            public double TargetDistance = 0.0;
            static readonly char[] gpsSep = new char[] { ':' };
            public Waypoint(string gps)
            {
                //"GPS: Slam Here:59.55:-11.63:-22.81:"
                if (gps.StartsWith("GPS:"))
                    gps = gps.Substring(4, gps.Length - 4);
                else
                    throw new ArgumentException($"'${gps}' is not a valid GPS string.");
                //" Slam Here:59.55:-11.63:-22.81"
                string[] parts = gps.Split(gpsSep, 5);
                if (parts.Length != 5)
                    throw new ArgumentException($"'${gps}' is not a valid GPS string.");
                double x = double.Parse(parts[parts.Length - 4]);
                double y = double.Parse(parts[parts.Length - 3]);
                double z = double.Parse(parts[parts.Length - 2]);
                Entity = new MyDetectedEntityInfo();
                _Position = new Vector3D(x, y, z);
                Velocity = Vector3D.Zero;
                Name = parts[0];
            }
            public Waypoint(Vector3D worldpos)
            {
                Entity = new MyDetectedEntityInfo();
                _Position = worldpos;
                Velocity = Vector3D.Zero;
                Name = "";
            }
            public Waypoint(Vector3D worldpos, Vector3D vel, string name)
            {
                Entity = new MyDetectedEntityInfo();
                _ElapsedTime = 0;
                _Position = worldpos;
                Velocity = vel;
                Name = name;
            }
            public Waypoint(MyDetectedEntityInfo entity)
            {
                SetNewEntityInfo(entity);
            }

            private void SetNewEntityInfo(MyDetectedEntityInfo entity)
            {
                Entity = entity;
                _ElapsedTime = 0;
                Name = Entity.Name;
                _Position = Entity.Position;
                Velocity = Entity.Velocity;
            }

            public static implicit operator Waypoint(string gps) { return new Waypoint(gps); }
            public static implicit operator Waypoint(Vector3D worldpos) { return new Waypoint(worldpos); }
            public static implicit operator Waypoint(MyDetectedEntityInfo entity) { return new Waypoint(entity); }

            public override string ToString()
            {
                Vector3D p = CurrentPosition;
                return $"GPS:{Name}:{p.X:F2}:{p.Y:F2}:{p.Z:F2}:";
            }
            bool EntityMatches(MyDetectedEntityInfo entity)
            {
                return Entity.IsEmpty() || (Entity.EntityId == entity.EntityId);
            }
            List<MyDetectedEntityInfo> Detected = new List<MyDetectedEntityInfo>();
            public bool UpdateEntity(List<IMySensorBlock> sensors, Func<MyDetectedEntityInfo, bool> selector = null)
            {
                if (selector == null) selector = EntityMatches;
                foreach (IMySensorBlock sensor in sensors)
                {
                    Detected.Clear();
                    sensor.DetectedEntities(Detected);
                    foreach (MyDetectedEntityInfo entity in Detected)
                        if (selector(entity))
                        {
                            SetNewEntityInfo(entity);
                            return true;
                        }
                }
                return false;
            }
            public bool UpdateEntity(List<IMyLargeTurretBase> turrets, Func<MyDetectedEntityInfo, bool> selector = null)
            {
                if (selector == null) selector = EntityMatches;
                MyDetectedEntityInfo entity;
                foreach (IMyLargeTurretBase turret in turrets)
                {
                    entity = turret.GetTargetedEntity();
                    if (selector(entity))
                    {
                        SetNewEntityInfo(entity);
                        return true;
                    }
                }
                return false;
            }
            public bool UpdateEntity(List<IMyCameraBlock> cameras, Func<MyDetectedEntityInfo, bool> selector = null)
            {
                Vector3D estimate = CurrentPosition;
                if (selector == null) selector = EntityMatches;
                MyDetectedEntityInfo entity;
                foreach (IMyCameraBlock camera in cameras)
                {
                    camera.EnableRaycast = true;
                    if (camera.CanScan(estimate))
                    {
                        entity = camera.Raycast(estimate);
                        if (selector(entity))
                        {
                            SetNewEntityInfo(entity);
                            return true;
                        }
                    }
                }
                return false;
            }
            public void UpdateTime(double milliseconds)
            {
                _ElapsedTime += milliseconds;
            }
        }
    }
}
