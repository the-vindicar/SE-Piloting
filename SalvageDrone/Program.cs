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
    partial class Program : MyGridProgram
    {
        #region Non-serialized variables
        IMyTextSurface logScreen;
        IMyRadioAntenna Antenna;
        IMyShipConnector Connector;
        IMyLandingGear Clamp;
        List<IMyBatteryBlock> Batteries = new List<IMyBatteryBlock>();
        List<IMySensorBlock> Sensors = new List<IMySensorBlock>();
        List<MyDetectedEntityInfo> Entities = new List<MyDetectedEntityInfo>();
        List<IMyThrust> Thrusters = new List<IMyThrust>();
        List<IMyGyro> Gyros = new List<IMyGyro>();
        IMyShipController Controller;
        AutoPilot Pilot;
        MyCommandLine Cmd = new MyCommandLine();
        MyIni ini = new MyIni();
        #endregion

        #region Serialized variables
        //[General]
        double AbortDistance;
        double ScanningDistance;
        double MaxSpeed;
        string TransmitTag;
        Vector3D DockLocation = Vector3D.Zero;
        Vector3D DockApproachVector = Vector3D.Zero;
        //[Approach]
        Dictionary<string, List<Vector3D>> Approaches = new Dictionary<string, List<Vector3D>>();
        //[Runtime] - Storage Only
        StateMachine State_Machine;
        Vector3D TargetLocation;
        string ChosenApproach;
        int ApproachIndex = -1;
        long TargetEntityID = 0;
        #endregion

        public Program()
        {
            GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(null, (b) =>
            {
                if (b.CubeGrid.EntityId == Me.CubeGrid.EntityId)
                {
                    if (b is IMyThrust)
                        Thrusters.Add(b as IMyThrust);
                    else if (b is IMyGyro)
                        Gyros.Add(b as IMyGyro);
                    else if (b is IMySensorBlock)
                        Sensors.Add(b as IMySensorBlock);
                    else if (b is IMyBatteryBlock)
                        Batteries.Add(b as IMyBatteryBlock);
                    else if (b is IMyShipController)
                        Controller = b as IMyShipController;
                    else if (b is IMyShipConnector)
                        Connector = b as IMyShipConnector;
                    else if (b is IMyRadioAntenna)
                        Antenna = b as IMyRadioAntenna;
                    else if (b is IMyLandingGear)
                        Clamp = b as IMyLandingGear;
                }
                return false;
            });
            if (Controller == null) throw new Exception("Controller not found.");
            if (Thrusters.Count == 0) throw new Exception("Thrusters not found.");
            if (Gyros.Count == 0) throw new Exception("Gyros not found.");
            if (Connector == null) throw new Exception("Connector not found.");
            if (Clamp == null) throw new Exception("Clamp not found.");
            if (Sensors.Count == 0) throw new Exception("Sensors not found.");
            Pilot = new AutoPilot(Controller, Thrusters, Gyros);
            try
            {
                Configure(Storage, true);
                Message("Retriever Drone state restored.");
            }
            catch (Exception)
            {
                Configure(Me.CustomData, false);
                Message("Retriever Drone initialized.");
            }
            Message($"Drone {Me.CubeGrid.CustomName} IGC ID: 0x{IGC.Me:X}");
            logScreen = Me.GetSurface(0);
            //Pilot.Log = (s) => logScreen?.WriteText(s, true);
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if ((updateSource & ~(UpdateType.Update1 | UpdateType.Update10 | UpdateType.Update100)) != 0)
            {   //we have been triggered by an external event.
                ProcessCommand(argument);
            }
            if ((updateSource & (UpdateType.Update1 | UpdateType.Update10 | UpdateType.Update100)) != 0)
            {
                if (State_Machine.Update())
                    Halt("Task completed.");
            }
        }

        public void Message(string message)
        {
            Echo(message);
            logScreen?.WriteText(message, true);
            logScreen?.WriteText("\n", true);
            if (!string.IsNullOrEmpty(TransmitTag) && (Antenna?.IsWorking ?? false))
                IGC.SendBroadcastMessage(TransmitTag, message);
        }
        #region Drone commands
        private void ProcessCommand(string command)
        {
            if (Cmd.TryParse(command) && Cmd.ArgumentCount > 0)
                switch (Cmd.Argument(0))
                {
                    case "id": Message($"Drone computer IGC ID: 0x{IGC.Me:X}"); break;
                    case "start": Start(Cmd.Argument(1)); break;
                    case "halt": Halt("Warning: drone halted."); break;
                    case "reload": Reload(); break;
                    case "recall": Recall(); break;
                    case "process_message": ProcessUnicast(); break;
                    default:
                        Start(command);  break;
                }
        }

        void ProcessUnicast()
        {
            while (IGC.UnicastListener.HasPendingMessage)
            {
                MyIGCMessage msg = IGC.UnicastListener.AcceptMessage();
                if (msg.Tag == "command" && msg.Data is string)
                {
                    string command = msg.Data as string;
                    if (command != "process_message")
                        ProcessCommand(command);
                }
            }
        }

        void Start(string arg)
        {
            if (State_Machine.CurrentState != "")
            {
                Message("Drone has already left the base.");
                return;
            }
            string _;
            if (!Waypoint.TryParseGPS(arg, out TargetLocation, out _) &&
                !Vector3D.TryParse(arg, out TargetLocation))
            {
                Message("Location must be a GPS string or a Vector3D string representation.");
                return;
            }
            ChosenApproach = "";
            ApproachIndex = -1;
            State_Machine.CurrentState = "Launch";
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        void Halt(string message = "")
        {
            Pilot.DisableOverrides();
            Pilot.Controller.DampenersOverride = true;
            State_Machine.CurrentState = "";
            Runtime.UpdateFrequency = UpdateFrequency.None;
            if (!string.IsNullOrEmpty(message))
                Message(message);
        }

        void Reload()
        {
            try
            {
                Storage = "";
                Configure(Me.CustomData, false);
                Message("Retriever Drone re-initialized.");
            }
            catch (Exception err)
            {
                Message("Failed to reload configuration!\n" + err.ToString());
            }
        }

        void Recall()
        {
            switch (State_Machine.CurrentState)
            {
                case "Launch":
                case "LeaveDock":
                case "ReturnHome":
                case "Dock":
                    Message("Cannot recall the drone at this stage."); break;
                default:
                    State_Machine.CurrentState = "ReturnHome"; break;
            }
        }
        #endregion
        #region Configuration saving and loading
        void Configure(string data, bool runtime)
        {
            MyIniParseResult result;
            ini.Clear();
            if (!ini.TryParse(data, out result))
                throw new FormatException($"Failed to parse configuration:\n{result.Error}\nLine: {result.LineNo}");
            AbortDistance = ini.Get("General", "AbortDistance").ToDouble(3000.0);
            ScanningDistance = ini.Get("General", "ScanningDistance").ToDouble(40.0);
            MaxSpeed = ini.Get("General", "MaxSpeed").ToDouble(100.0);
            TransmitTag = ini.Get("General", "TransmitTag").ToString("");
            if (!string.IsNullOrEmpty(TransmitTag))
                IGC.UnicastListener.SetMessageCallback("process_message");
            if (ini.ContainsKey("General", "Dock") && ini.ContainsKey("General", "DockApproach"))
            {
                if (!Vector3D.TryParse(ini.Get("General", "Dock").ToString(), out DockLocation))
                    throw new FormatException("Dock location information is not correct.");
                if (!Vector3D.TryParse(ini.Get("General", "DockApproach").ToString(), out DockApproachVector))
                    throw new FormatException("Dock approach information is not correct.");
            }
            else if (Connector.Status == MyShipConnectorStatus.Connected)
            {
                DockLocation = Connector.OtherConnector.GetPosition();
                DockApproachVector = Connector.OtherConnector.WorldMatrix.GetDirectionVector(Base6Directions.Direction.Forward);
            }
            if (Vector3D.IsZero(DockLocation) && Vector3D.IsZero(DockApproachVector))
                throw new Exception("Dock is not configured");
            #region Parse approach vectors
            List<MyIniKey> keys = new List<MyIniKey>();
            List<string> lines = new List<string>();
            string _;
            Vector3D vec;
            Approaches.Clear();
            ini.GetKeys("Approach", keys);
            foreach (MyIniKey key in keys)
            {
                List<Vector3D> approach = new List<Vector3D>();
                ini.Get(key).GetLines(lines);
                foreach (string line in lines)
                    if (Waypoint.TryParseGPS(line, out vec, out _))
                        approach.Add(vec);
                    else
                        throw new FormatException($"Approach {key.Name} waypoint not in GPS format:\n{line}");
                Approaches.Add(key.Name, approach);
            }
            if (Approaches.Count == 0)
                throw new Exception("No approaches configured!");
            #endregion
            #region Runtime items - restoring a saved state
            if (runtime)
            {
                string state = ini.Get("Runtime", "CurrentState").ToString("");
                if (string.IsNullOrEmpty(state))
                {
                    TargetLocation = Vector3D.Zero;
                    ChosenApproach = "";
                    ApproachIndex = -1;
                    TargetEntityID = 0;
                    State_Machine = new StateMachine(StateMaker, "");
                    Runtime.UpdateFrequency = UpdateFrequency.None;
                }
                else
                {
                    if (!Vector3D.TryParse(ini.Get("Runtime", "TargetLocation").ToString(""), out TargetLocation))
                        throw new Exception("Failed to restore target location");
                    ChosenApproach = ini.Get("Runtime", "ChosenApproach").ToString("");
                    ApproachIndex = ini.Get("Runtime", "ApproachIndex").ToInt32(-1);
                    TargetEntityID = ini.Get("Runtime", "TargetEntityID").ToInt64(0);
                    State_Machine = new StateMachine(StateMaker, state);
                    Runtime.UpdateFrequency = UpdateFrequency.Update10;
                }
            }
            else
            {
                TargetLocation = Vector3D.Zero;
                ChosenApproach = "";
                ApproachIndex = -1;
                TargetEntityID = 0;
                State_Machine = new StateMachine(StateMaker, "");
                Runtime.UpdateFrequency = UpdateFrequency.None;
            }
            #endregion
        }

        public void Save()
        {
            ini.Set("General", "AbortDistance", AbortDistance);
            ini.Set("General", "ScanningDistance", ScanningDistance);
            ini.Set("General", "MaxSpeed", MaxSpeed);
            ini.Set("General", "TransmitTag", TransmitTag);
            ini.Set("General", "Dock", DockLocation.ToString());
            ini.Set("General", "DockApproach", DockApproachVector.ToString());
            foreach (var kv in Approaches)
            {
                string data = string.Join("\n", kv.Value.Select<Vector3D, string>((v) => $"GPS:waypoint:{v.X:F3}:{v.Y:F3}:{v.Z:F3}:"));
                ini.Set("Approach", kv.Key, data);
            }
            ini.Set("Runtime", "CurrentState", State_Machine?.CurrentState ?? "");
            ini.Set("Runtime", "TargetLocation", TargetLocation.ToString());
            ini.Set("Runtime", "ChosenApproach", ChosenApproach);
            ini.Set("Runtime", "ApproachIndex", ApproachIndex);
            ini.Set("Runtime", "TargetEntityID", TargetEntityID);
            Storage = ini.ToString();
        }
        #endregion
        #region State machine implementation
        StateMachine.State StateMaker(string name)
        {
            switch (name)
            {
                case "Launch": return Launch;
                case "LeaveDock": return LeaveDock;
                case "ReachTargetArea": return ReachTargetArea;
                case "FindTarget": return FindTarget;
                case "CaptureTarget": return CaptureTarget;
                case "ReturnHome": return ReturnHome;
                case "Dock": return Dock;
                default: return null;
            }
        }

        IEnumerable<string> Launch()
        {
            logScreen?.WriteText("", false);
            Message($"Target: {TargetLocation.ToString()}");
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            if (ApproachIndex < 0)
            {
                var minkv = Approaches.MinBy((kv) => (float)(TargetLocation - kv.Value[0]).Length());
                ChosenApproach = minkv.Key;
                ApproachIndex = minkv.Value.Count - 1;
            }
            if ((Approaches[ChosenApproach][0] - TargetLocation).Length() > AbortDistance)
            {
                Message("Target location is too far from the dock!");
                yield return "";
            }
            else
            {
                Clamp.Unlock();
                Clamp.AutoLock = false;
                Vector3D detach = DockLocation + DockApproachVector * 2 * Me.CubeGrid.WorldVolume.Radius;
                Pilot.Tasks.Clear();
                var task = new UnaimedFlightStrategy(detach, Connector);
                task.MaxLinearSpeed = MaxSpeed;
                Pilot.Tasks.Add(task);
                Connector.Disconnect();
                Connector.Enabled = false;
                while (!Pilot.Update(Runtime.TimeSinceLastRun.TotalSeconds))
                {
                    yield return null;
                }
                yield return "LeaveDock";
            }
        }

        IEnumerable<string> LeaveDock()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            Message("Leaving dock via route "+ChosenApproach);
            while (ApproachIndex >= 0)
            {
                var task = new AimedFlightStrategy(Approaches[ChosenApproach][ApproachIndex], Pilot.Controller);
                task.MaxLinearSpeed = MaxSpeed;
                Pilot.Tasks.Add(task);
                while (!Pilot.Update(Runtime.TimeSinceLastRun.TotalSeconds))
                {
                    if (HasToAbort())
                        yield return "ReturnHome";
                    else
                        yield return null;
                }
                ApproachIndex--;
            }
            ChosenApproach = "";
            ApproachIndex = -1;
            yield return "ReachTargetArea";
        }

        IEnumerable<string> ReachTargetArea()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            Waypoint wp = new Waypoint(TargetLocation, Vector3D.Zero, "Target");
            wp.TargetDistance = ScanningDistance;
            if ((TargetLocation - Pilot.Controller.GetPosition()).LengthSquared() < (wp.TargetDistance * wp.TargetDistance))
                yield return "FindTarget";
            Message("Proceeding to target area.");
            var task = new AimedFlightStrategy(wp, Pilot.Controller);
            task.MaxLinearSpeed = MaxSpeed;
            Pilot.Tasks.Add(task);
            while (!Pilot.Update(Runtime.TimeSinceLastRun.TotalSeconds))
            {
                if (HasToAbort())
                    yield return "ReturnHome";
                else
                    yield return null;
            }
            yield return "FindTarget";
        }

        IEnumerable<string> FindTarget()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            foreach (IMySensorBlock sensor in Sensors)
                sensor.Enabled = true;
            yield return null;
            foreach (IMySensorBlock sensor in Sensors)
            {
                sensor.DetectedEntities(Entities);
                foreach (MyDetectedEntityInfo ent in Entities)
                    if (ent.Type == MyDetectedEntityType.SmallGrid 
                        || ent.Type == MyDetectedEntityType.LargeGrid)
                    {
                        TargetEntityID = ent.EntityId;
                        Message($"Target: {ent.Name} #{ent.EntityId}");
                        yield return "CaptureTarget";
                    }
            }
            var strategy = new AimedFlightStrategy(TargetLocation, Pilot.Controller);
            strategy.MaxLinearSpeed = MaxSpeed/10;
            Pilot.Tasks.Add(strategy);
            while (!Pilot.Update(Runtime.TimeSinceLastRun.TotalSeconds))
            {
                foreach (IMySensorBlock sensor in Sensors)
                {
                    sensor.DetectedEntities(Entities);
                    foreach (MyDetectedEntityInfo ent in Entities)
                        if (ent.Type == MyDetectedEntityType.SmallGrid || ent.Type == MyDetectedEntityType.LargeGrid)
                        {
                            TargetEntityID = ent.EntityId;
                            Message($"Target: {ent.Name} #{ent.EntityId}");
                            yield return "CaptureTarget";
                        }
                }
                if (HasToAbort())
                    yield return "ReturnHome";
                else
                    yield return null;
            }
            //we didn't find the target - abort mission
            Message("Aborting: no targets found.");
            yield return "ReturnHome";
        }

        IEnumerable<string> CaptureTarget()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            foreach (IMySensorBlock sensor in Sensors)
                sensor.Enabled = true;
            yield return null;
            MyDetectedEntityInfo entity = new MyDetectedEntityInfo();
            foreach (IMySensorBlock sensor in Sensors)
            {
                sensor.DetectedEntities(Entities);
                foreach (MyDetectedEntityInfo ent in Entities)
                    if (ent.EntityId == TargetEntityID)
                    {
                        entity = ent;
                        break;
                    }
            }
            if (entity.IsEmpty())
            {
                Message($"Failed to detect target with ID={TargetEntityID}");
                yield return "ReturnHome";
            }

            Message("Capturing target...");
            Pilot.Tasks.Clear();
            var goal = new Waypoint(entity);
            Vector3D approach = Clamp.GetPosition() - entity.Position;
            var strategy = new DockingStrategy(goal, approach, Clamp);
            strategy.ReferenceForward = Base6Directions.Direction.Down;
            strategy.ReferenceUp = Base6Directions.Direction.Forward;
            Pilot.Tasks.Add(strategy);
            while (!Pilot.Update(Runtime.TimeSinceLastRun.TotalSeconds))
            {
                if (!goal.UpdateEntity(Sensors))
                {
                    Message($"Aborting: target left sensor range.");
                    yield return "ReturnHome";
                }
                else if (HasToAbort())
                {
                    yield return "ReturnHome";
                }
                else
                    yield return null;
            }
            Message("Target captured.");
            yield return "ReturnHome";
        }

        IEnumerable<string> ReturnHome()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            Pilot.Tasks.Clear();
            foreach (IMySensorBlock sensor in Sensors)
                sensor.Enabled = false;
            if (ApproachIndex < 0)
            {
                var minkv = Approaches.MinBy((kv) => (float)(Pilot.Controller.GetPosition() - kv.Value[0]).Length());
                ChosenApproach = minkv.Key;
                ApproachIndex = 0;
            }
            Message("Returning to base via route " + ChosenApproach);
            while (ApproachIndex < Approaches[ChosenApproach].Count)
            {
                var strategy = new AimedFlightStrategy(Approaches[ChosenApproach][ApproachIndex], Pilot.Controller);
                strategy.PositionEpsilon = 1.0;
                Pilot.Tasks.Add(strategy);
                while (!Pilot.Update(Runtime.TimeSinceLastRun.TotalSeconds))
                {
                    if (TooFar())
                        Clamp.Unlock();
                    yield return null;
                }
                ApproachIndex++;
            }
            ChosenApproach = "";
            ApproachIndex = -1;
            yield return "Dock";
        }

        IEnumerable<string> Dock()
        {
            Connector.Enabled = true;
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            Message("Docking.");
            Pilot.Tasks.Add(new DockingStrategy(DockLocation, DockApproachVector, Connector));
            while (!Pilot.Update(Runtime.TimeSinceLastRun.TotalSeconds))
            {
                yield return null;
            }
            yield return "";
        }

        bool HasToAbort()
        {
            if (!Clamp.IsFunctional)
            {
                Message("Aborting: clamp malfunction.");
                return true;
            }
            if (TooFar())
            {
                Message("Aborting: too far from dock.");
                return true;
            }
            if (Batteries.Average((b) => (b.ChargeMode == ChargeMode.Recharge) ? 0.0f : (b.CurrentStoredPower / b.MaxStoredPower)) < 0.05)
            {
                Message("Aborting: battery charge below 5%.");
                return true;
            }
            return false;
        }

        bool TooFar()
        {
            return (Pilot.Controller.GetPosition() - DockLocation).LengthSquared() > AbortDistance * AbortDistance;
        }
        #endregion
    }
}