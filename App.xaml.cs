using NAudio.MediaFoundation;
using System.Windows;

namespace UyghurASR
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            MediaFoundationApi.Startup();
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            MediaFoundationApi.Shutdown();
            base.OnExit(e);
        }
    }
}
