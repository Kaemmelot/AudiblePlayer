namespace RfidProgrammer.ProgrammerService
{
    internal enum ProgrammerEvent
    {
        Comment = '#',
        InitComplete = 'I',
        CardChange = 'C',
        PartialResult = 'P',
        ErrorCheck = 'E',
        AuthFailed = 'x',
        InvalidCommand = 'X',
        Ack = 'A',
        Nack = 'N',

        // following commands are not send and are just for internal state switching
        Failure = 256, // unknown byte received

        Nothing
    }
}
