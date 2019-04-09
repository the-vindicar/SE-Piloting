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
        /// Provides basic facilities for piloting s strategies.
        /// </summary>
        public abstract class BasePilotingStrategy
        {
            /// <summary>
            /// Reference block. Its position & orientation are considered to be ship's position & orientation.
            /// If null, then ship controller block is used for that purpose.
            /// </summary>
            public IMyTerminalBlock Reference;
            public Base6Directions.Direction ReferenceForward;
            public Base6Directions.Direction ReferenceUp;
            /// <summary>
            /// Current goal: point in world-space to reach, or entity to pursue.
            /// If null, then the strategy will do absolutely nothing, but will still report its task as incomplete.
            /// </summary>
            public Waypoint Goal { get; set; }
            /// <summary>
            /// Maximum speed (relative to the goal) that ship is allowed to reach.
            /// </summary>
            public double MaxLinearSpeed = 100.0;
            public double OrientationEpsilon = 1e-4;
            public double PositionEpsilon = 1e-1;
            /// <summary>
            /// Constructs the strategy with given goal and (optional) reference block.
            /// </summary>
            /// <param name="goal">Goal to pursue.</param>
            /// <param name="reference">Reference block to use.</param>
            public BasePilotingStrategy(Waypoint goal, IMyTerminalBlock reference,
                Base6Directions.Direction forward = Base6Directions.Direction.Forward,
                Base6Directions.Direction up = Base6Directions.Direction.Up)
            {
                if (!Base6Directions.IsValidBlockOrientation(forward, up))
                    throw new ArgumentException("Invalid set of directions!");
                Goal = goal;
                Reference = reference;
                ReferenceForward = forward;
                ReferenceUp = up;
            }
            /// <summary>
            /// Queries the strategy on which linear and angular velocities the ship should have.
            /// </summary>
            /// <param name="owner">AutoPilot instance that queries the strategy.</param>
            /// <param name="linearV">Initial value - current linear velocity. Set it to desired linear velocity.</param>
            /// <param name="angularV">Initial value - current rotation. Set it to desired rotation.</param>
            /// <returns>True if goal is considered achieved.</returns>
            public abstract bool Update(AutoPilot owner, ref Vector3D linearV, ref Vector3D angularV);
            /// <summary>
            /// Calculates angular velocity required to point the ship in desired direction.
            /// All vectors must be unit-vectors in world-space cordinates.
            /// </summary>
            /// <param name="desired_forward">Desired direction for the ship to be pointing in, world-space.</param>
            /// <param name="desired_up">Desired direction for the top of the ship to be pointing in, world-space. Zero vector if roll doesn't matter.</param>
            /// <param name="forward">Current forward direction of the ship, world-space.</param>
            /// <param name="up">Current up direction for the ship.</param>
            /// <param name="vel">Suggested angular velocity.</param>
            /// <returns>Estimate of how wrong our orientation is. 0 means perfect match, 2 means opposite direction.</returns>
            public double RotateToMatch(Vector3D desired_forward, Vector3D desired_up, Vector3D forward, Vector3D up, ref Vector3D vel)
            {
                double diff = forward.Dot(desired_forward);
                double rolldiff;
                Vector3D left = up.Cross(forward);
                Vector3D movevector = forward - desired_forward;
                if (Vector3D.IsZero(desired_up))
                {
                    rolldiff = 0;
                    vel.Z = 0;
                }
                else
                {
                    rolldiff = desired_up.Dot(up);
                    Vector3D rollvector = up - desired_up;
                    vel.Z = left.Dot(rollvector);
                    if (rolldiff < 0)
                        vel.Z += Math.Sign(vel.Z);
                    rolldiff = 1 - rolldiff;
                }
                vel.X = up.Dot(movevector);
                vel.Y = left.Dot(movevector);
                if (diff < 0)
                    vel.Y += Math.Sign(vel.Y);
                diff = 1 - diff;
                return Math.Max(diff, rolldiff);
            }
        }
    }
}
