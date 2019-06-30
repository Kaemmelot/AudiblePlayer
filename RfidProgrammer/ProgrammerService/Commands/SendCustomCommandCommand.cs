using System.Text;
using System.Threading.Tasks;

namespace RfidProgrammer.ProgrammerService.Commands
{
    internal class SendCustomCommandCommand : ICommand
    {
        private ProgrammerService service;

        public SendCustomCommandCommand(ProgrammerService service)
        {
            this.service = service;
            Task = new Task(Action);
        }

        public Task Task { get; }

        public string Command { get; set; }

        private void Action()
        {
            var type = Command[0] == (char)ServiceEvent.ToggleByteMode ? ServiceEvent.ToggleByteMode : ServiceEvent.UserOperation;
            if (!service.stateMachine.MoveNext(type))
                return;
            service.lastCmd = Encoding.ASCII.GetBytes(Command);
            service.SerialWriteLine(service.lastCmd, true);
        }
    }
}
