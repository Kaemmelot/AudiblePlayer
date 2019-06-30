using Prism.Mef.Modularity;
using Prism.Modularity;
using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;

namespace RfidProgrammer.ProgrammerService
{
    [ModuleExport(typeof(ProgrammerServiceModule))]
    public class ProgrammerServiceModule : IModule
    {
        [Import(typeof(CompositionContainer))]
        private CompositionContainer container;

        public void Initialize()
        {
            if (container == null)
                throw new ArgumentNullException(nameof(container));
            container.GetExportedValue<IProgrammerService>(); // start service for the first time
        }
    }
}
