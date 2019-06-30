using System.Threading.Tasks;

namespace RfidProgrammer.ProgrammerService
{
    internal interface ICommand
    {
        Task Task { get; }
    }
}
