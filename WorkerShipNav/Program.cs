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
using VRage.Game.GUI.TextPanel;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        MyCommandLine Cmd = new MyCommandLine();
        StringBuilder ShipInfo = new StringBuilder();
        StringBuilder StorageInfo = new StringBuilder();
        MyDetectedEntityInfo ScanResult = new MyDetectedEntityInfo();
        StringBuilder ScanInfo = new StringBuilder();

        AutoPilot Pilot;
        IMyCockpit Cockpit;
        IMyTextSurface Middle;
        IMyTextSurface Left;
        IMyTextSurface Right;

        IMyCameraBlock Front;
        IMyShipConnector Connector;
        List<IMyTerminalBlock> StorageBlocks = new List<IMyTerminalBlock>();
        List<IMyBatteryBlock> Batteries = new List<IMyBatteryBlock>();

        public Program()
        {
            Pilot = new AutoPilot(GridTerminalSystem, Me);
            GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(null, (b) => 
            {
                if (b.CubeGrid != Me.CubeGrid)
                    return false;
                if (b is IMyCockpit && b.IsFunctional)
                    Cockpit = b as IMyCockpit;
                if ((b is IMyCameraBlock)
                    && b.IsFunctional
                    && (Pilot.Controller.WorldMatrix.Forward.Dot(b.WorldMatrix.Forward) > 0.9))
                    Front = b as IMyCameraBlock;
                else if (b.BlockDefinition.TypeIdString == "MyObjectBuilder_ShipConnector"
                    && b.BlockDefinition.SubtypeName != "ConnectorSmall"
                    && b.IsFunctional)
                    Connector = b as IMyShipConnector;
                else if (b is IMyBatteryBlock)
                    Batteries.Add(b as IMyBatteryBlock);
                if (b.InventoryCount > 0 
                    && !(b is IMyConveyorSorter) 
                    && b.BlockDefinition.SubtypeName != "ConnectorSmall")
                    StorageBlocks.Add(b);
                return false;
            });
            if (Cockpit == null) throw new Exception("Cockpit not found.");
            if (Connector == null) throw new Exception("Connector not found.");
            if (Front == null) throw new Exception("Front camera not found.");

            StorageBlocks.Sort((a, b) => ((double)b.GetInventory(0).MaxVolume).CompareTo((double)a.GetInventory(0).MaxVolume));

            Front.EnableRaycast = true;

            Middle = Cockpit.GetSurface(0);
            Left = Cockpit.GetSurface(1);
            Right = Cockpit.GetSurface(2);

            Middle.ContentType = ContentType.TEXT_AND_IMAGE;
            Middle.Font = "Monospace";
            Middle.FontSize = 1.5f;

            Left.ContentType = ContentType.TEXT_AND_IMAGE;
            Left.Font = "Debug";
            Left.FontSize = 2.0f;

            Right.ContentType = ContentType.TEXT_AND_IMAGE;
            Right.Font = "Debug";
            Right.FontSize = 2.0f;

            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Log(string msg)
        {
            Echo(msg);
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if ((updateSource & ~(UpdateType.Update1 | UpdateType.Update10 | UpdateType.Update100)) != 0)
            {
                if (Cmd.TryParse(argument))
                    switch (Cmd.Argument(0))
                    {
                        case "fly": FlyForward(); break;
                        case "dock": PerformDocking(); break;
                        case "scan": QuickScan(); break;
                        case "halt": QuickHalt(); break;
                        default:
                            Log($"Unknown command: {Cmd.Argument(0)}");break;
                    }
            }
            if ((updateSource & (UpdateType.Update1 | UpdateType.Update10 | UpdateType.Update100)) != 0)
            {
                if (Pilot.Tasks.Count > 0)
                    Pilot.Update(Runtime.TimeSinceLastRun.TotalSeconds);
                if (Cockpit.IsUnderControl)
                {
                    UpdateShipInfo();
                    UpdateStorageInfo();
                }
            }
        }

        private void PerformDocking()
        {
            if (Pilot.Tasks.Count > 0)
                QuickHalt();
            Vector3D dock, approach, orientation;
            if (!Front.EnableRaycast)
                Front.EnableRaycast = true;
            if (!Connector.IsFunctional || !GetApproachFromScan(Front, out dock, out approach, out orientation))
            {
                Echo("Can't dock.");
            }
            else
            {
                var task = new DockingStrategy(dock, Connector, approach, orientation);
                Pilot.Tasks.Add(task);
            }
        }

        private void FlyForward()
        {
            if (Pilot.Tasks.Count > 0)
                QuickHalt();
            var task = new FlyForwardStrategy(20.0, Front);
            Pilot.Tasks.Add(task);
        }

        private void QuickHalt()
        {
            Pilot.Tasks.Clear();
            Pilot.DisableOverrides();
            Pilot.Controller.DampenersOverride = true;
        }

        private void QuickScan()
        {
            double limit;
            if (Front.RaycastDistanceLimit < 0)
                limit = Front.AvailableScanRange;
            else
                limit = Math.Min(Front.AvailableScanRange, Front.RaycastDistanceLimit);
            limit = Math.Min(limit, 1e5);
            ScanResult = Front.Raycast(limit);
            UpdateScanInfo();
        }

        private bool GetApproachFromScan(IMyCameraBlock camera, out Vector3D position, out Vector3D approach, out Vector3D orientation)
        {
            Vector3D scan = 100 * camera.WorldMatrix.Forward + camera.WorldMatrix.Translation;
            MyDetectedEntityInfo info = camera.CanScan(scan) ? camera.Raycast(scan) : new MyDetectedEntityInfo();
            if (info.IsEmpty() || !info.HitPosition.HasValue || (info.Type != MyDetectedEntityType.LargeGrid && info.Type != MyDetectedEntityType.SmallGrid))
            {
                position = Vector3D.Zero;
                approach = Vector3D.Zero;
                orientation = Vector3D.Zero;
                return false;
            }
            Base6Directions.Direction dir = info.Orientation.GetClosestDirection(camera.GetPosition() - info.HitPosition.Value);
            approach = info.Orientation.GetDirectionVector(dir);
            position = info.HitPosition.Value;
            Base6Directions.Direction odir = info.Orientation.GetClosestDirection(Connector.WorldMatrix.Up);
            orientation = info.Orientation.GetDirectionVector(odir);
            return true;
        }

        private void UpdateScanInfo()
        {
            ScanInfo.Clear();
            if (!ScanResult.IsEmpty())
            {
                ScanInfo.Append(ScanResult.Name);
                ScanInfo.Append('\n');
                ScanInfo.Append(ScanResult.Type.ToString());
                ScanInfo.Append('\n');
                ScanInfo.Append(ScanResult.Relationship.ToString());
                ScanInfo.Append('\n');
                ScanInfo.Append($"Size: {ScanResult.BoundingBox.Size.Max():F0}m\n");
                ScanInfo.Append($"Speed: {ScanResult.Velocity.Length():F0}m/s\n");
                ScanInfo.Append($"GPS:{ScanResult.Name}:{ScanResult.Position.X:F1}:{ScanResult.Position.Y:F1}:{ScanResult.Position.Z:F1}:\n");
            }
            Right.WriteText(ScanInfo);
        }

        private void UpdateShipInfo()
        {
            ShipInfo.Clear();
            ShipInfo.Append("Autopilot: ");
            if (Pilot.Tasks.Count == 0)
                ShipInfo.Append("idle");
            else if (Pilot.Tasks[0] is DockingStrategy)
                ShipInfo.Append("dock");
            else
                ShipInfo.Append("route");
            ShipInfo.Append('\n');
            ShipInfo.Append($"Speed: {Cockpit.GetShipSpeed():F1}m/s\n");
            ShipInfo.Append("Dampeners: ");
            ShipInfo.Append(Cockpit.DampenersOverride ? "on\n" : "OFF\n");
            ShipInfo.Append("Connector: ");
            if (!Connector.IsFunctional)
                ShipInfo.Append("BROKEN\n");
            else switch (Connector.Status)
                {
                    case MyShipConnectorStatus.Connected: ShipInfo.Append("DOCKED\n"); break;
                    case MyShipConnectorStatus.Connectable: ShipInfo.Append("READY\n"); break;
                    default: ShipInfo.Append("n/a\n"); break;
                }
            double charge = Batteries.Average((b) => b.CurrentStoredPower / b.MaxStoredPower) * 100.0;
            ShipInfo.Append($"Batteries: {charge:F1}%\n");
            if (Front.AvailableScanRange < 1e5)
                ShipInfo.Append($"Scan: {Front.AvailableScanRange / 1000.0:F1}km\n");
            Left.WriteText(ShipInfo);
        }

        private void UpdateStorageInfo()
        {
            StorageInfo.Clear();
            int maxlen = StorageBlocks.Max((b) => b.CustomName.Length);

            for (int i = 0; i < StorageBlocks.Count; i++)
            {
                IMyTerminalBlock b = StorageBlocks[i];
                for (int j = b.CustomName.Length; j < maxlen; j++)
                    StorageInfo.Append(' ');
                StorageInfo.Append(b.CustomName);
                StorageInfo.Append(' ');
                IMyInventory inv = b.GetInventory(0);
                double ratio = 100 * (double)inv.CurrentVolume / (double)inv.MaxVolume;
                StorageInfo.AppendFormat("{0,5:F1}%", ratio);
                StorageInfo.Append("\n");
            }
            Middle.WriteText(StorageInfo, false);
        }
    }
}