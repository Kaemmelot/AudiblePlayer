using System;
using System.Threading.Tasks;

namespace RfidProgrammer.ProgrammerService.Commands
{
    internal class WriteContentCommand : ICommand
    {
        private ProgrammerService service;

        public WriteContentCommand(ProgrammerService service)
        {
            this.service = service;
            Task = new Task(Action);
        }

        public Task Task { get; }

        public byte[] Content { get; set; }

        public uint Start { get; set; }

        public bool IgnorePreviousEnd { get; set; }

        public bool IgnoreEndMarker { get; set; }

        private void Action()
        {
            var contentEnd = Start + Content.Length;
            var newContent = IgnorePreviousEnd && Start == 0 ? Content : new byte[IgnorePreviousEnd ? contentEnd : Math.Max(contentEnd, service.CurrentCard.Content.Length)];
            //  add missing beginning
            if (Start > 0 && service.CurrentCard.Content.Length > 0) // copy current content if possible
                Array.Copy(service.CurrentCard.Content, newContent, Math.Min(Start, service.CurrentCard.Content.Length));
            for (var i = service.CurrentCard.Content.Length; i < Start; i++) // add zeros for the rest
                newContent[i] = 0;

            if (Content != newContent) // add content to new array
                Array.Copy(Content, 0, newContent, Start, Content.Length);

            if (!IgnorePreviousEnd && service.CurrentCard.Content.Length > contentEnd)
                Array.Copy(service.CurrentCard.Content, contentEnd, newContent, contentEnd, service.CurrentCard.Content.Length - contentEnd); // add trailing content from before

            var endMarkerNeeded = !IgnoreEndMarker && (IgnorePreviousEnd || (service.CurrentCard.Content.Length / 16) <= (contentEnd / 16)) && (newContent.Length == 0 || newContent[newContent.Length - 1] != service.EndMarker);
            var block = service.GetBlockByContentIndex(Start); // current (real) block

            if (endMarkerNeeded || block * 16 != Start || contentEnd % 16 != 0)
            {
                if (endMarkerNeeded)
                    contentEnd++;
                // content must be multiple of 16 bytes, we have to make it longer to transmit it
                // old length + missing bytes at start + missing bytes at end (possbibly one more for endmarker)
                var tmpContent = newContent;
                newContent = new byte[newContent.Length + (endMarkerNeeded ? 1 : 0) + (Start % 16) + (contentEnd % 16 != 0 ? 16 - (contentEnd % 16) : 0)];
                var s = (Start / 16) * 16;
                Array.Copy(tmpContent, 0, newContent, 0, Math.Min(tmpContent.Length, newContent.Length - (endMarkerNeeded ? 1 : 0))); // add content (adds missing start and end already)
                if (endMarkerNeeded)
                    newContent[contentEnd - 1] = service.EndMarker;
            }
            
            var b = Start / 16; // block pos in content
            var lastB = (contentEnd - 1) / 16; // last block in content
            var args = new byte[18];
            var success = true;
            while (success && b <= lastB)
            {
                // prepare new args
                args[0] = (byte)(block / 4); // sector
                args[1] = (byte)(block % 4); // block
                Array.Copy(newContent, b * 16, args, 2, 16);
                if (success = service.WriteCommand(ServiceEvent.WriteCard, args))
                {
                    b++;
                    block++;
                    if (block % 4 == 3)
                        block++; // skip access bits
                    success = service.WaitForEndOfOperation();
                }
                else
                    service.stateMachine.MoveNext(ServiceEvent.Failure);
            }
            if (success)
                service.CurrentCard = service.rfidFactory.CreateRfidCard(service.CurrentCard.Id, IgnoreEndMarker ? newContent : service.TrimContent(newContent));
        }
    }
}
