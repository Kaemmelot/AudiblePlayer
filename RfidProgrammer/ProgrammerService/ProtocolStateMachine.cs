namespace RfidProgrammer.ProgrammerService
{
    internal class ProtocolStateMachine : DualTransitionStateMachine<ProgrammerEvent, ServiceEvent, ProgrammerState>
    {
        protected override ProgrammerState Initialize()
        {
            // failures
            AddGlobalTransition(ProgrammerState.Unknown, ServiceEvent.Failure);
            AddGlobalTransition(ProgrammerState.Unknown, ProgrammerEvent.Failure);

            // allow user actions in unknown
            AddLoopTransition(ProgrammerState.Unknown, ServiceEvent.ToggleByteMode);
            AddLoopTransition(ProgrammerState.Unknown, ServiceEvent.UserOperation);
            AddLoopTransition(ProgrammerState.Unknown, ProgrammerEvent.ErrorCheck);
            AddLoopTransition(ProgrammerState.Unknown, ProgrammerEvent.InvalidCommand);
            AddLoopTransition(ProgrammerState.Unknown, ProgrammerEvent.AuthFailed);
            AddLoopTransition(ProgrammerState.Unknown, ServiceEvent.Ack);
            AddLoopTransition(ProgrammerState.Unknown, ServiceEvent.Nack);
            AddLoopTransition(ProgrammerState.Unknown, ProgrammerEvent.CardChange);
            AddLoopTransition(ProgrammerState.Unknown, ProgrammerEvent.PartialResult);

            // just ignore comments
            AddGlobalIgnoreTransition(ProgrammerEvent.Comment);

            // connected: opening + response of programmer
            AddTransition(ProgrammerState.NotConnected, ProgrammerState.Connecting, ServiceEvent.Connect);
            AddTransition(ProgrammerState.Connecting, ProgrammerState.Connected, ProgrammerEvent.InitComplete);

            // disconnect
            AddGlobalTransition(ProgrammerState.NotConnected, ServiceEvent.Disconnect);

            // card changed
            AddLoopTransition(ProgrammerState.Connected, ProgrammerEvent.CardChange);
            AddTransition(ProgrammerState.OperationSuccess, ProgrammerState.Connected, ProgrammerEvent.CardChange);
            AddTransition(ProgrammerState.OperationFailed, ProgrammerState.Connected, ProgrammerEvent.CardChange);
            AddGlobalTransition(ProgrammerState.Unknown, ProgrammerEvent.CardChange); // fallback for all other states
            AddLoopTransition(ProgrammerState.Connected, ProgrammerEvent.AuthFailed); // initial test after card connect

            // prepare next operation
            AddTransition(ProgrammerState.OperationSuccess, ProgrammerState.Connected, ServiceEvent.NextOperation);
            AddTransition(ProgrammerState.OperationFailed, ProgrammerState.Connected, ServiceEvent.NextOperation);
            AddLoopTransition(ProgrammerState.Connected, ServiceEvent.NextOperation); // ignore

            // operation(s) start
            AddTransition(ProgrammerState.Connected, ProgrammerState.OperationInProgress, ServiceEvent.ReadCard, null, StartOperation);
            AddTransition(ProgrammerState.Connected, ProgrammerState.OperationInProgress, ServiceEvent.WriteCard, null, StartOperation);
            AddTransition(ProgrammerState.Connected, ProgrammerState.OperationInProgress, ServiceEvent.ChangeTrailers, null, StartOperation);
            AddTransition(ProgrammerState.Connected, ProgrammerState.OperationInProgress, ServiceEvent.CheckTrailers, null, StartOperation);
            AddTransition(ProgrammerState.Connected, ProgrammerState.OperationInProgress, ServiceEvent.SetTrailers, null, StartOperation);

            // error checking
            AddTransition(ProgrammerState.OperationInProgress, ProgrammerState.ErrorChecking, ProgrammerEvent.ErrorCheck, IsNonUserOperation);
            AddTransition(ProgrammerState.ErrorChecking, ProgrammerState.OperationInProgress, ServiceEvent.Ack); // non user operation by construction
            AddTransition(ProgrammerState.ErrorChecking, ProgrammerState.OperationInProgress, ServiceEvent.Nack); // non user operation by construction
            AddLoopTransition(ProgrammerState.OperationInProgress, ProgrammerEvent.ErrorCheck); // user operation by construction/order

            // ignore partial results (for now)
            AddLoopTransition(ProgrammerState.OperationInProgress, ProgrammerEvent.PartialResult);
            // record auth failed messages
            AddLoopTransition(ProgrammerState.OperationInProgress, ProgrammerEvent.AuthFailed, IsNonUserOperation, RecordAuthFailed);

            // operation(s) ended
            AddTransition(ProgrammerState.OperationInProgress, ProgrammerState.OperationSuccess, ProgrammerEvent.Ack, IsByteModeEnabled, EndOperation);
            AddTransition(ProgrammerState.OperationInProgress, ProgrammerState.OperationFailed, ProgrammerEvent.Nack, IsByteModeEnabled, EndOperation);
            AddTransition(ProgrammerState.OperationInProgress, ProgrammerState.OperationFailed, ProgrammerEvent.InvalidCommand, IsByteModeEnabled, EndOperation);

            // user operations
            AddTransition(ProgrammerState.Connected, ProgrammerState.OperationInProgress, ServiceEvent.ToggleByteMode, null, StartOperation);  // special cases, no ack from the programmer in both cases (possibly unsafe)
            AddTransition(ProgrammerState.OperationInProgress, ProgrammerState.Connected, ServiceEvent.ToggleByteMode, IsTextModeEnabled, EndOperation);
            AddTransition(ProgrammerState.Connected, ProgrammerState.OperationInProgress, ServiceEvent.UserOperation, null, StartOperation);
            AddLoopTransition(ProgrammerState.OperationInProgress, ServiceEvent.UserOperation, IsUserOperation); // for example Ack or Nack for error checking
            AddLoopTransition(ProgrammerState.OperationInProgress, ProgrammerEvent.AuthFailed, IsUserOperation);
            AddLoopTransition(ProgrammerState.OperationInProgress, ProgrammerEvent.Ack, IsTextModeEnabled); // stay here as long as text mode is enabled
            AddLoopTransition(ProgrammerState.OperationInProgress, ProgrammerEvent.Nack, IsTextModeEnabled);
            AddLoopTransition(ProgrammerState.OperationInProgress, ProgrammerEvent.InvalidCommand, IsTextModeEnabled);
            AddLoopTransition(ProgrammerState.OperationInProgress, ServiceEvent.NextOperation, IsUserOperation);

            return ProgrammerState.NotConnected; // Initial state
        }

        public ServiceEvent? CurrentOperation { get; private set; }

        public bool IsUserOperationRunning => CurrentOperation == ServiceEvent.UserOperation || CurrentOperation == ServiceEvent.ToggleByteMode;

        private void StartOperation(ProgrammerState currentState, ProgrammerState nextState, ServiceEvent @event)
        {
            CurrentOperation = @event;
        }

        private void EndOperation<T>(ProgrammerState currentState, ProgrammerState nextState, T @event)
        {
            CurrentOperation = null;
            // TODO reset auth failed flag or event?
        }

        private bool IsTextModeEnabled<T>(ProgrammerState currentState, ProgrammerState nextState, T @event)
        {
            return CurrentOperation == ServiceEvent.ToggleByteMode;
        }

        private bool IsByteModeEnabled<T>(ProgrammerState currentState, ProgrammerState nextState, T @event)
        {
            return CurrentOperation != ServiceEvent.ToggleByteMode;
        }

        private bool IsUserOperation<T>(ProgrammerState currentState, ProgrammerState nextState, T @event)
        {
            return IsUserOperationRunning;
        }

        private bool IsNonUserOperation<T>(ProgrammerState currentState, ProgrammerState nextState, T @event)
        {
            return !IsUserOperationRunning;
        }

        private void RecordAuthFailed(ProgrammerState currentState, ProgrammerState nextState, ProgrammerEvent @event)
        {
            // TODO auth failed flag or event?
        }
    }
}
