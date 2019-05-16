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
    partial class Program : MyGridProgram
    {
        #region Configuration
        //[General]
        Vector3D Home;
        Vector3D HomeApproach;
        #endregion

        MyIni ini;
        StateMachine stateMachine;
        
        public Program()
        {            
        }

        public void Load(string data, bool restore)
        {

        }

        public void Save()
        {
            ini.Clear();
        }

        public void Main(string argument, UpdateType updateSource)
        {
        }
    }
}