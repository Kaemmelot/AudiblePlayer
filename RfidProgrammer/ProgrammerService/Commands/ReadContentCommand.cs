using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RfidProgrammer.ProgrammerService.Commands
{
    internal class ReadContentCommand : ICommand
    {
        private ProgrammerService service;

        public ReadContentCommand(ProgrammerService service)
        {
            this.service = service;
            Task = new Task(Action);
        }

        public Task Task { get; }

        public uint Start { get; set; }

        public uint Length { get; set; }

        private void Action()
        {
            var block = service.GetBlockByContentIndex(Start);
            var lastBlock = service.GetBlockByContentIndex(Length != 0 ? Start + Length - 1 : Start + ProgrammerService.UsableBytes - 1);

            var args = new byte[3]; // sector, block, #blocks to read
            var tmpContent = new List<byte>();

            var success = true;
            while (success && block < lastBlock)
            {
                // read sector for sector
                args[0] = (byte)(block / 4); // sector
                args[1] = (byte)(block % 4); // block
                args[2] = (byte)Math.Min(3 - (block % 4), lastBlock - block + 1); // number of blocks: 3 or 2 in the first sector
                if (!service.WriteCommand(ServiceEvent.ReadCard, args))
                    return;
                if (success = (service.WaitForEndOfOperation() && service.CurrentState == ProgrammerState.OperationSuccess && service.result != null))
                {
                    block += (byte)(args[2] + 1); // ignore access bits
                    tmpContent.AddRange(service.result);
                    if (Length == 0 && service.result.Contains(service.EndMarker))
                        break;
                }
            }

            // cut from start to length or trim if no length was given
            if (success)
            {
                byte[] result;
                if (Length != 0)
                {
                    result = new byte[Length];
                    Array.Copy(tmpContent.ToArray(), (int)Start % 16, result, 0, Length);
                }
                else
                    result = service.TrimContent(tmpContent.ToArray());
                service.CurrentCard = service.rfidFactory.CreateRfidCard(service.CurrentCard.Id, result);
            }
        }
    }
}
