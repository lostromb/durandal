using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Utils
{
    /// <summary>
    /// Represents a concurrent state machine in which each state is a single primitive value (usually an enum).
    /// Instance of StateMachine are (somewhat ironically) stateless. You have to pass a state in and then get a new state in response.
    /// This allows you to have shared static state machines to enforce well-known transition maps without allocating new machines each time.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class StateMachine<T>
    {
        private readonly IReadOnlyDictionary<T, T[]> _edges;

        public StateMachine(IReadOnlyDictionary<T, T[]> edges)
        {
            _edges = edges;
        }

        public void Transition(ref T currentState, T targetState)
        {
            T originalState = currentState;
            if (!_edges.ContainsKey(originalState))
            {
                throw new InvalidOperationException("There are no edges leading from state " + originalState + " in the state machine");
            }

            // Ensure the transition is valid
            T[] targets = _edges[originalState];

            bool valid = false;
            foreach (T target in targets)
            {
                if (target.Equals(targetState))
                {
                    valid = true;
                    break;
                }
            }

            if (!valid)
            {
                throw new InvalidOperationException("The state transition from " + originalState + " to " + targetState + " is illegal!");
            }

            currentState = targetState;
            OnStateChanged(originalState, targetState);
        }

        /// <summary>
        /// Transitions the state machine without caring about whether it's legal or not. Usually reserved for error recovery
        /// </summary>
        /// <param name="currentState">The current state of the machine</param>
        /// <param name="targetState">The target state of the machine</param>
        public void ForceTransition(ref T currentState, T targetState)
        {
            T originalState = currentState;
            currentState = targetState;
            OnStateChanged(originalState, targetState);
        }

        public void TryTransition(ref T currentState, T targetState)
        {
            T originalState = currentState;
            if (!_edges.ContainsKey(originalState))
            {
                return;
            }

            // Ensure the transition is valid
            T[] targets = _edges[originalState];

            bool valid = false;
            foreach (T target in targets)
            {
                if (target.Equals(targetState))
                {
                    valid = true;
                    break;
                }
            }

            if (!valid)
            {
                return;
            }
            
            originalState = currentState;
            currentState = targetState;
            OnStateChanged(originalState, targetState);
        }

        /// <summary>
        /// Fired whenever the state machine's state changes
        /// </summary>
        public event EventHandler<StateTransitionEventArgs<T>> StateChanged;

        protected virtual void OnStateChanged(T source, T dest)
        {
            EventHandler<StateTransitionEventArgs<T>> handler = StateChanged;
            if (handler != null)
            {
                handler(this, new StateTransitionEventArgs<T>(source, dest));
            }
        }
    }

    /// <summary>
    /// Event arguments which describe a transition occurring inside state machine
    /// </summary>
    /// <typeparam name="E"></typeparam>
    public class StateTransitionEventArgs<E> : EventArgs
    {
        public StateTransitionEventArgs(E source, E target)
        {
            SourceState = source;
            TargetState = target;
        }

        public E SourceState
        {
            get;
            private set;
        }

        public E TargetState
        {
            get;
            private set;
        }
    }
}
