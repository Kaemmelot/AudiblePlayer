using System;
using System.Collections.Generic;
using System.Linq;

namespace RfidProgrammer.ProgrammerService
{
    public abstract class DualTransitionStateMachine<TTransitionEvent1, TTransitionEvent2, TState>
        where TState : struct
        where TTransitionEvent1 : struct
        where TTransitionEvent2 : struct
    {
        private sealed class Transition<TEvent>
        {
            public TState? CurrentState { get; }

            public TState? NextState { get; }

            public TEvent Event { get; }

            public Func<TState, TState, TEvent, bool> Guard { get; }

            public Action<TState, TState, TEvent> OnTransition { get; }

            public Transition(TState? currentState, TState? nextState, TEvent @event, Func<TState, TState, TEvent, bool> guard = null, Action<TState, TState, TEvent> onTransition = null)
            {
                CurrentState = currentState;
                NextState = nextState;
                Event = @event;
                Guard = guard;
                OnTransition = onTransition;
            }
        }

        public sealed class StateChangedEventArgs
        {
            public StateChangedEventArgs(TState newState)
            {
                NewState = newState;
            }

            public TState NewState { get; }
        }

        public delegate void StateChangedEventHandler(StateChangedEventArgs args);

        public event StateChangedEventHandler StateChanged;

        private TState state;

        public TState CurrentState
        {
            get
            {
                return state;
            }
            private set
            {
                if (!EqualityComparer<TState>.Default.Equals(state, value))
                {
                    state = value;
                    StateChanged?.Invoke(new StateChangedEventArgs(value));
                }
            }
        }

        public DualTransitionStateMachine()
        {
            CurrentState = Initialize();
        }

        private Transition<T> GetTransition<T>(List<Transition<T>> transitions, T @event)
        {
            // first with matching state, event and if guard returns true
            return transitions.FirstOrDefault(transition =>
                (transition.CurrentState == null || transition.CurrentState.Equals(CurrentState)) &&
                transition.Event.Equals(@event) &&
                (transition.Guard == null || transition.Guard.Invoke(CurrentState, transition.NextState ?? CurrentState, @event))
            );
        }

        private List<Transition<TTransitionEvent1>> transitions1 = new List<Transition<TTransitionEvent1>>();
        private List<Transition<TTransitionEvent2>> transitions2 = new List<Transition<TTransitionEvent2>>();

        public bool HasNext(TTransitionEvent1 @event)
        {
            return GetTransition(transitions1, @event) != null;
        }

        public bool HasNext(TTransitionEvent2 @event)
        {
            return GetTransition(transitions2, @event) != null;
        }

        public bool MoveNext(TTransitionEvent1 @event)
        {
            var transition = GetTransition(transitions1, @event);
            if (transition != null)
            {
                var nextState = transition.NextState ?? CurrentState;
                transition.OnTransition?.Invoke(CurrentState, nextState, @event);
                CurrentState = nextState;
            }
            return transition != null;
        }

        public bool MoveNext(TTransitionEvent2 @event)
        {
            var transition = GetTransition(transitions2, @event);
            if (transition != null && transition.NextState != null)
            {
                var nextState = transition.NextState ?? CurrentState;
                transition.OnTransition?.Invoke(CurrentState, nextState, @event);
                CurrentState = nextState;
            }
            return transition != null;
        }

        public void MoveNextOrFail(TTransitionEvent1 @event)
        {
            if (!MoveNext(@event))
                throw new InvalidOperationException();
        }

        public void MoveNextOrFail(TTransitionEvent2 @event)
        {
            if (!MoveNext(@event))
                throw new InvalidOperationException();
        }

        protected abstract TState Initialize();

        protected void AddTransition(TState currentState, TState nextState, TTransitionEvent1 @event,
            Func<TState, TState, TTransitionEvent1, bool> guard = null, Action<TState, TState, TTransitionEvent1> onTransition = null)
        {
            transitions1.Add(new Transition<TTransitionEvent1>(currentState, nextState, @event, guard, onTransition));
        }

        protected void AddTransition(TState currentState, TState nextState, TTransitionEvent2 @event,
            Func<TState, TState, TTransitionEvent2, bool> guard = null, Action<TState, TState, TTransitionEvent2> onTransition = null)
        {
            transitions2.Add(new Transition<TTransitionEvent2>(currentState, nextState, @event, guard, onTransition));
        }

        protected void AddLoopTransition(TState state, TTransitionEvent1 @event,
            Func<TState, TState, TTransitionEvent1, bool> guard = null, Action<TState, TState, TTransitionEvent1> onTransition = null)
        {
            transitions1.Add(new Transition<TTransitionEvent1>(state, state, @event, guard, onTransition));
        }

        protected void AddLoopTransition(TState state, TTransitionEvent2 @event,
            Func<TState, TState, TTransitionEvent2, bool> guard = null, Action<TState, TState, TTransitionEvent2> onTransition = null)
        {
            transitions2.Add(new Transition<TTransitionEvent2>(state, state, @event, guard, onTransition));
        }

        protected void AddGlobalTransition(TState nextState, TTransitionEvent1 @event,
            Func<TState, TState, TTransitionEvent1, bool> guard = null, Action<TState, TState, TTransitionEvent1> onTransition = null)
        {
            transitions1.Add(new Transition<TTransitionEvent1>(null, nextState, @event, guard, onTransition));
        }

        protected void AddGlobalTransition(TState nextState, TTransitionEvent2 @event,
            Func<TState, TState, TTransitionEvent2, bool> guard = null, Action<TState, TState, TTransitionEvent2> onTransition = null)
        {
            transitions2.Add(new Transition<TTransitionEvent2>(null, nextState, @event, guard, onTransition));
        }

        protected void AddGlobalIgnoreTransition(TTransitionEvent1 @event, Func<TState, TState, TTransitionEvent1, bool> guard = null)
        {
            transitions1.Add(new Transition<TTransitionEvent1>(null, null, @event, guard, null));
        }

        protected void AddGlobalIgnoreTransition(TTransitionEvent2 @event, Func<TState, TState, TTransitionEvent2, bool> guard = null)
        {
            transitions2.Add(new Transition<TTransitionEvent2>(null, null, @event, guard, null));
        }
    }
}
