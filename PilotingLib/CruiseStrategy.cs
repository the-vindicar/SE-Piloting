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
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
        public class CruiseStrategy : BasePilotingStrategy
        {
            public double Clearance = 0.0;

            List<IMyCameraBlock> Cameras = null;
            List<IMySensorBlock> Sensors = null;
            List<MyDetectedEntityInfo> _entities = new List<MyDetectedEntityInfo>();
            List<MyDetectedEntityInfo> Collisions = new List<MyDetectedEntityInfo>();

            public CruiseStrategy(
                Waypoint goal,
                IMyCubeBlock reference, 
                List<IMyCameraBlock> cameras = null,
                List<IMySensorBlock> sensors = null,
                Base6Directions.Direction forward = Base6Directions.Direction.Forward, 
                Base6Directions.Direction up = Base6Directions.Direction.Up) 
                : base(goal, reference, forward, up)
            {
                Cameras = cameras ?? new List<IMyCameraBlock>();
                Sensors = sensors ?? new List<IMySensorBlock>();
            }

            public override bool Update(AutoPilot owner, ref Vector3D linearV, ref Vector3D angularV)
            {
                throw new Exception();
            }

            private double? CollisionDistance(ref MyDetectedEntityInfo e, ref BoundingSphereD volume, ref Vector3D myVel)
            {
                BoundingSphereD sphere = new BoundingSphereD(e.Position, e.BoundingBox.Size.Length() / 2 + volume.Radius + Clearance);
                Vector3D vel = myVel + e.Velocity;
                vel.Normalize();
                RayD ray = new RayD(ref volume.Center, ref vel);
                return sphere.Intersects(ray);
            }

            private void UpdateEntities()
            {

            }
        }
    }
}
