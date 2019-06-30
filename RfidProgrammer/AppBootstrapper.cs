using Prism.Mef;
using Prism.Modularity;
using RfidProgrammer.MainView;
using RfidProgrammer.ProgrammerService;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;

namespace RfidProgrammer
{
    internal class AppBootstrapper : MefBootstrapper
    {
        protected override void ConfigureAggregateCatalog()
        {
            base.ConfigureAggregateCatalog();

            AggregateCatalog.Catalogs.Add(new AssemblyCatalog(GetType().Assembly));
        }

        protected override void ConfigureModuleCatalog()
        {
            base.ConfigureModuleCatalog();

            var programmerServiceModule = typeof(ProgrammerServiceModule);
            ModuleCatalog.AddModule(new ModuleInfo()
            {
                ModuleName = programmerServiceModule.Name,
                ModuleType = programmerServiceModule.AssemblyQualifiedName,
                Ref = programmerServiceModule.Assembly.CodeBase
            });
            var mainViewModule = typeof(MainViewModule);
            ModuleCatalog.AddModule(new ModuleInfo()
            {
                ModuleName = mainViewModule.Name,
                ModuleType = mainViewModule.AssemblyQualifiedName,
                Ref = mainViewModule.Assembly.CodeBase,
                DependsOn = { "ProgrammerServiceModule" }
            });
        }

        protected override CompositionContainer CreateContainer()
        {
            var container = base.CreateContainer();
            container.ComposeExportedValue(container);
            return container;
        }
    }
}
