using System;
using System.Threading.Tasks;

namespace RfidProgrammer.ProgrammerService.Commands
{
    internal class EraseContentCommand : ICommand
    {
        private ProgrammerService service;

        public EraseContentCommand(ProgrammerService service)
        {
            this.service = service;
            Task = new Task(Action);
        }

        public Task Task { get; }

        public uint Start { get; set; }

        private void Action()
        {
            var block = service.GetBlockByContentIndex(Start);
            var args = new byte[18]; // default: zero
            var success = true;
            // first block might be different (if start is set to non zero)
            if (Start % 16 != 0 && Start < service.CurrentCard.Content.Length) // we need to keep a few bytes intact
            {
                var a = new byte[18];
                a[0] = (byte)(block / 4); // sector
                a[1] = (byte)(block % 4); // block
                Array.Copy(service.CurrentCard.Content, 0, a, 2, (int)Start % 16);
                if (success = service.WriteCommand(ServiceEvent.WriteCard, a))
                {
                    block++;
                    if (block % 4 == 3)
                        block++;
                    success = service.WaitForEndOfOperation();
                }
                else
                    service.stateMachine.MoveNext(ServiceEvent.Failure);
            }
            while (success && block < ProgrammerService.Blocks)
            {
                // prepare new args
                args[0] = (byte)(block / 4); // sector
                args[1] = (byte)(block % 4); // block
                if (success = service.WriteCommand(ServiceEvent.WriteCard, args))
                {
                    block++;
                    if (block % 4 == 3)
                        block++;
                    success = service.WaitForEndOfOperation();
                }
                else
                    service.stateMachine.MoveNext(ServiceEvent.Failure);
            }
            if (success && Start < service.CurrentCard.Content.Length) // we cut the current content
            {
                var content = new byte[Start];
                Array.Copy(service.CurrentCard.Content, content, Start);
                service.CurrentCard = service.rfidFactory.CreateRfidCard(service.CurrentCard.Id, service.TrimContent(content));
            }
        }
    }
}
