using System.Threading.Tasks;

namespace RfidProgrammer.ProgrammerService.Commands
{
    internal class CheckKeysCommand : ICommand
    {
        private ProgrammerService service;

        public CheckKeysCommand(ProgrammerService service)
        {
            this.service = service;
            Task = new Task(Action);
        }

        public Task Task { get; }

        private void Action()
        {
            if (service.WriteCommand(ServiceEvent.CheckTrailers, new byte[0]))
                service.WaitForEndOfOperation();
        }
    }
}
