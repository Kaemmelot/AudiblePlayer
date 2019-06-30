namespace RfidProgrammer.ProgrammerService
{
    public enum ServiceEvent
    {
        ReadCard = 'R',
        WriteCard = 'W',
        SetTrailers = 'T',
        ChangeTrailers = 't',
        CheckTrailers = 'C',
        //SelfTest = 'S', // currently unused
        //Reset = 'X', // currently unused
        ToggleByteMode = 'b', // user only action
        Ack = 'A',
        Nack = 'N',

        // following commands are not send and are just for internal state switching
        Connect = 256,
        Failure, // state switching not possible (i.e. different states)
        NextOperation, // switch state from finished operation
        Disconnect,
        UserOperation
    }
}
