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
        /// Simple coroutine-based state machine.
        /// </summary>
        public class StateMachine
        {
            public delegate IEnumerable<string> State(); //state coroutine
            Func<string, State> StateMaker; //factory function for states
            /// <summary>
            /// Name of the currently active state.
            /// </summary>
            public string CurrentState
            {
                get
                {
                    return _CurrentStateName;
                }
                set
                {
                    _CurrentStateName = value;
                    _CurrentState = StateMaker(_CurrentStateName);
                    StateExecution?.Dispose();
                    StateExecution = null;
                }
            }
            private string _CurrentStateName;
            State _CurrentState; //currently running coroutine
            IEnumerator<string> StateExecution; //enumerator for currently running coroutine
            /// <summary>
            /// Creates state machine and initializes it to a specific state.
            /// </summary>
            /// <param name="state_maker">Factory function. Given state name, returns state coroutine.</param>
            /// <param name="starting_state">Initial state name.</param>
            public StateMachine(Func<string, State> state_maker, string starting_state)
            {
                StateMaker = state_maker;
                CurrentState = starting_state;
            }
            /// <summary>
            /// Updates state machine, letting current state coroutine run for a bit.
            /// </summary>
            /// <returns>True if state machine has reached final state, false otherwise.</returns>
            public bool Update()
            {
                if (_CurrentState == null) return true; //no state - no problem
                if (StateExecution == null) //if we need a new enumerator, we make one
                    StateExecution = _CurrentState().GetEnumerator();
                if (!StateExecution.MoveNext()) //if coroutine finished without yielding next state
                    throw new Exception($"Coroutine {CurrentState} has not provided next state.");
                if (StateExecution.Current != null) //if coroutine yielded not null
                    //then it's the name of the next state
                    CurrentState = StateExecution.Current;
                return _CurrentState == null; //if StateMaker returned null, then we are done
            }
        }
    }
}
