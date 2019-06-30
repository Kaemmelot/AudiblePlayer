using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RfidProgrammer.ProgrammerService.Commands
{
    internal class ResetAccessAndKeysCommand : ICommand
    {
        private ProgrammerService service;

        public ResetAccessAndKeysCommand(ProgrammerService service)
        {
            this.service = service;
            Task = new Task(Action);
        }

        public Task Task { get; }

        private void Action()
        {
            var newAccess = service.rfidFactory.CreateRfidAccess();
            var args = newAccess.KeyA.Concat(newAccess.AccessBits).Concat(newAccess.KeyB).Concat(Encoding.ASCII.GetBytes(new char[] { (char)newAccess.SelectedKey })).ToArray();
            if (!service.WriteCommand(ServiceEvent.SetTrailers, args))
                return;
            if (service.WaitForEndOfOperation() && service.CurrentState == ProgrammerState.OperationSuccess)
                service.CurrentAccess = newAccess;
        }
    }
}
