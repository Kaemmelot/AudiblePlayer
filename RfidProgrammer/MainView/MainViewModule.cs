using Prism.Mef.Modularity;
using Prism.Modularity;
using Prism.Mvvm;
using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Windows;

namespace RfidProgrammer.MainView
{
    [ModuleExport(typeof(MainViewModule))]
    public class MainViewModule : IModule
    {
        [Import(typeof(CompositionContainer))]
        private CompositionContainer container;

        public void Initialize()
        {
            if (container == null)
                throw new ArgumentNullException();
            // select view model
            ViewModelLocationProvider.Register<MainWindow, RfidCardViewModel>();
            //ViewModelLocationProvider.Register<ConnectionStatusBar, IConnectionViewModel>(); // currently unneeded: doesn't work for usercontrols since data context is inherited
            ViewModelLocationProvider.Register<ConnectionWindow, IConnectionViewModel>();
            // create window/shell
            container.GetExportedValue<MainWindow>();
            Application.Current.MainWindow.Show();
        }
    }
}
