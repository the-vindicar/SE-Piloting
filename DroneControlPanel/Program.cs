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
        const string ScreenName = "Retriever Status Screen";
        const string BroadcastTag = "RetrieverDrone";
        const string IGCIDMarker = "IGC ID: 0x";
        long DroneID = 0;
        IMyTextSurface Screen;
        StringBuilder Buffer = new StringBuilder();
        IMyBroadcastListener Listener;
        public Program()
        {
            IMyTerminalBlock b = GridTerminalSystem.GetBlockWithName(ScreenName);
            Screen = (b as IMyTextSurface) ?? (b as IMyTextSurfaceProvider)?.GetSurface(0);
            if (Screen == null)
                Screen = Me.GetSurface(0);
            Screen.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
            Listener = IGC.RegisterBroadcastListener(BroadcastTag);
            Listener.SetMessageCallback("incoming_message");
            if (Listener.IsActive)
                Echo("Listener registered. Callback set.");
            else
                Echo("Listener not registered!");
            if (!long.TryParse(Storage, out DroneID))
                DroneID = 0;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            switch (argument)
            {
                case "incoming_message": ProcessMessages(); break;
                case "execute_command": ExecuteCommand(); break;
                case "recall_now": RecallNow(); break;
                case "unlink_drone": UnlinkDrone(); break;
                default: WriteOnScreen("Unknown command: " + argument); break;
            }
        }

        private void UnlinkDrone()
        {
            DroneID = 0;
            WriteOnScreen("DroneID reset. Waiting for drone to trasmit the id.");
        }

        public void Save()
        {
            Storage = DroneID.ToString();
        }

        private void ProcessMessages()
        {
            while (Listener.HasPendingMessage)
            {
                MyIGCMessage msg = Listener.AcceptMessage();
                string data = msg.Data.ToString();
                if (DroneID == 0)
                    CheckForID(data);
                Echo(data);
                WriteOnScreen(data);
            }
        }

        private void ExecuteCommand()
        {
            Screen.ReadText(Buffer);
            RemoveLinesBefore(Buffer, 1);
            string command = Buffer.ToString().Trim();
            Echo("Command: " + command);
            if (DroneID != 0)
                IGC.SendUnicastMessage(DroneID, "command", command);
        }

        private void RecallNow()
        {
            if (DroneID != 0)
                IGC.SendUnicastMessage(DroneID, "command", "recall");
        }

        private void WriteOnScreen(string data)
        {
            Screen.ReadText(Buffer);
            if (Buffer.Length > 0 && Buffer[Buffer.Length-1] != '\n')
                Buffer.Append('\n');
            Buffer.Append(data);
            Buffer.Append('\n');
            RemoveLinesBefore(Buffer, 15);
            Screen.WriteText(Buffer, false);
        }

        private void RemoveLinesBefore(StringBuilder sb, int linecount)
        {
            int idx;
            int counter = linecount;
            for (idx = sb.Length-1; idx >= 0; idx--)
            {
                if (sb[idx] == '\n')
                    counter--;
                if (counter == 0) break;
            }
            if (idx >= 0)
                sb.Remove(0, idx + 1);
        }

        private void CheckForID(string s)
        {
            int idx = s.IndexOf(IGCIDMarker);
            long id;
            if (idx > 0)
            {
                string sid = s.Substring(idx + IGCIDMarker.Length);
                if (long.TryParse(sid, System.Globalization.NumberStyles.HexNumber,System.Globalization.CultureInfo.InvariantCulture, out id))
                {
                    DroneID = id;
                    WriteOnScreen($"Drone ID registered: {DroneID:X}");
                }
                else
                    WriteOnScreen($"Invalid drone ID: {sid}");
            }
        }

    }
}