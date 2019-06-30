using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RfidProgrammer.ProgrammerService.Commands
{
    internal class ChangeKeysCommand : ICommand
    {
        private ProgrammerService service;

        public ChangeKeysCommand(ProgrammerService service)
        {
            this.service = service;
            Task = new Task(Action);
        }

        public Task Task { get; }

        public byte[] KeyA { get; set; }

        public byte[] KeyB { get; set; }

        public byte[] AccessBits { get; set; }

        public SelectedKey SelectedKey { get; set; }

        private void Action()
        {
            if (!service.WriteCommand(ServiceEvent.ChangeTrailers, KeyA.Concat(AccessBits).Concat(KeyB).Concat(Encoding.ASCII.GetBytes(new char[] { (char)SelectedKey })).ToArray()))
                return;
            if (service.WaitForEndOfOperation() && service.CurrentState == ProgrammerState.OperationSuccess && service.CurrentCard != null)
                service.CurrentAccess = service.rfidFactory.CreateRfidAccess(KeyA, KeyB, AccessBits, SelectedKey);
        }
    }
}
