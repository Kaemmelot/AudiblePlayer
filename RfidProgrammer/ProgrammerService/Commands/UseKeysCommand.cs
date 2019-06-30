using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RfidProgrammer.ProgrammerService.Commands
{
    internal class UseKeysCommand : ICommand
    {
        private ProgrammerService service;

        public UseKeysCommand(ProgrammerService service)
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
            if (!service.WriteCommand(ServiceEvent.SetTrailers, KeyA.Concat(AccessBits).Concat(KeyB).Concat(Encoding.ASCII.GetBytes(new char[] { (char)SelectedKey })).ToArray()))
                return;
            if (service.WaitForEndOfOperation() && service.CurrentState == ProgrammerState.OperationSuccess)
                service.CurrentAccess = service.rfidFactory.CreateRfidAccess(KeyA, KeyB, AccessBits, SelectedKey);
        }
    }
}
