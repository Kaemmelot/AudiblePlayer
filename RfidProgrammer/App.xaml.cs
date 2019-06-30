using System.Windows;

namespace RfidProgrammer
{
    public partial class App : Application
    {
        public App()
        {
            var bootstrapper = new AppBootstrapper();
            bootstrapper.Run();
        }
    }
}
