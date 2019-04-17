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
        /// Represents a goal for the autopilot: a point in world-space to reach, or an entity to pursue.
        /// </summary>
        public class Waypoint
        {
            /// <summary>
            /// How much time passed since last target update.
            /// </summary>
            double _ElapsedTime;
            /// <summary>
            /// Position of the target at the time of last update.
            /// </summary>
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
            /// <summary>
            /// Creates a stationary waypoint from GPS string.
            /// </summary>
            /// <param name="gps">GPS string. Typical format is "GPS:Point name:59.55:-11.63:-22.81:"</param>
            public Waypoint(string gps)
            {
                Entity = new MyDetectedEntityInfo();
                Velocity = Vector3D.Zero;
                string name;
                if (TryParseGPS(gps, out _Position, out name))
                    Name = name;
                else
                    throw new ArgumentException($"'{gps}' is not a valid GPS string");
            }
            /// <summary>
            /// Creates a stationary or moving waypoint, using given data.
            /// </summary>
            /// <param name="worldpos">Target position at the moment, world-space.</param>
            /// <param name="vel">Target velocity at the moment, world-space.</param>
            /// <param name="name">Target name.</param>
            public Waypoint(Vector3D worldpos, Vector3D vel, string name)
            {
                Entity = new MyDetectedEntityInfo();
                _ElapsedTime = 0;
                _Position = worldpos;
                Velocity = vel;
                Name = name;
            }
            /// <summary>
            /// Creates a waypoint from entity description.
            /// </summary>
            /// <param name="entity">Entity description.</param>
            public Waypoint(MyDetectedEntityInfo entity)
            {
                SetNewEntityInfo(entity);
            }

            public void SetNewEntityInfo(MyDetectedEntityInfo entity)
            {
                Entity = entity;
                _ElapsedTime = 0;
                Name = Entity.Name;
                _Position = Entity.Position;
                Velocity = Entity.Velocity;
            }

            public static implicit operator Waypoint(string gps) { return new Waypoint(gps); }
            public static implicit operator Waypoint(Vector3D worldpos) { return new Waypoint(worldpos, Vector3D.Zero, "Location"); }
            public static implicit operator Waypoint(MyDetectedEntityInfo entity) { return new Waypoint(entity); }

            public override string ToString()
            {
                Vector3D p = CurrentPosition;
                return $"GPS:{Name}:{p.X:F2}:{p.Y:F2}:{p.Z:F2}:";
            }
            bool EntityMatches(MyDetectedEntityInfo entity)
            {
                return !Entity.IsEmpty() && (Entity.EntityId == entity.EntityId);
            }
            List<MyDetectedEntityInfo> Detected = new List<MyDetectedEntityInfo>();
            /// <summary>
            /// Queries given sensor blocks, attempting to find the entity to pursue.
            /// </summary>
            /// <param name="sensors">List of sensors to query.</param>
            /// <param name="selector">Selector function. Should return true for the entity the ship should pursue.
            /// If null, then the ship will search for the entity with the same EntityId as current entity.</param>
            /// <returns>True if the entity was detected, and approved by the selector.</returns>
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
            /// <summary>
            /// Queries given turret blocks, attempting to find the entity to pursue.
            /// </summary>
            /// <param name="turrets">List of turrets to query. Entities targeted by them will be candidates.</param>
            /// <param name="selector">Selector function. Should return true for the entity the ship should pursue.
            /// If null, then the ship will search for the entity with the same EntityId as current entity.</param>
            /// <returns>True if the entity was detected and approved by the selector.</returns>
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
            /// <summary>
            /// Attempts to scan estimated position of the target using given camera blocks.
            /// </summary>
            /// <param name="cameras">Cameras to raycast with. Will be set to raycast by the method.</param>
            /// <param name="selector">Selector function. Should return true for the entity the ship should pursue.
            /// If null, then the ship will search for the entity with the same EntityId as current entity.</param>
            /// <returns>True if the entity was detected and approved by the selector.</returns>
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
            /// <summary>
            /// Increases time since the target was last updated.
            /// </summary>
            /// <param name="milliseconds">How much time passed since last call to UpdateTime() or UpdateEntity().</param>
            public void UpdateTime(double milliseconds)
            {
                _ElapsedTime += milliseconds;
            }

            public static bool TryParseGPS(string gps, out Vector3D vec, out string name)
            {
                //"GPS: Slam Here:59.55:-11.63:-22.81:"
                vec = Vector3D.Zero;
                name = "";
                if (gps.StartsWith("GPS:"))
                    gps = gps.Substring(4, gps.Length - 4);
                else
                    return false;
                //" Slam Here:59.55:-11.63:-22.81"
                string[] parts = gps.Split(gpsSep, 5);
                if (parts.Length != 5)
                    return false;
                double x, y, z;
                if (double.TryParse(parts[parts.Length - 4], out x)
                    && double.TryParse(parts[parts.Length - 3], out y)
                    && double.TryParse(parts[parts.Length - 2], out z))
                {
                    vec = new Vector3D(x, y, z);
                    name = parts[1];
                    return true;
                }
                else
                    return false;
            }
        }
    }
}
