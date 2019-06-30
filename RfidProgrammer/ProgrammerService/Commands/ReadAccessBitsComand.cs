using System;
using System.Threading.Tasks;

namespace RfidProgrammer.ProgrammerService.Commands
{
    internal class ReadAccessBitsCommand : ICommand
    {
        private ProgrammerService service;

        public ReadAccessBitsCommand(ProgrammerService service)
        {
            this.service = service;
            Task = new Task(Action);
        }

        public Task Task { get; }

        private void Action()
        {
            var success = true;
            var args = new byte[] { 0, 3, 1 };
            byte[] compare = null;
            for (var sector = 0; success && sector < ProgrammerService.UsableSectors; sector++)
            {
                args[0] = (byte)sector;
                success = service.WriteCommand(ServiceEvent.ReadCard, args) && service.WaitForEndOfOperation() && service.CurrentState == ProgrammerState.OperationSuccess
                        && service.CurrentCard != null && service.result != null;
                if (success)
                {
                    if (compare != null)
                    {
                        for (var i = 0; success && i < compare.Length; i++)
                            success = compare[i] == service.result[i];

                        if (!success)
                        {
                            service.AppendOutput("^--- ERROR: Access bits differ!", true);
                            service.stateMachine.MoveNext(ServiceEvent.Failure); // this is not allowed to happen, the user needs to fix it manually
                        }
                    }
                    else
                        compare = service.result.ToArray();
                }
            }
            if (success)
            {
                var keyA = new byte[6];
                Array.Copy(compare, 0, keyA, 0, 6);
                var accessBits = new byte[4];
                Array.Copy(compare, 6, accessBits, 0, 4);
                var keyB = new byte[6];
                Array.Copy(compare, 10, keyB, 0, 6);
                service.CurrentAccess = service.rfidFactory.CreateRfidAccess(keyA, keyB, accessBits, service.CurrentAccess.SelectedKey);
            }
        }
    }
}
