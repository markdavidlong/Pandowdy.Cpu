using Microsoft.Extensions.Hosting;
using Pandowdy.UI;

namespace Pandowdy
{
    public static class UiBootstrap
    {
        public static void IntegrateUiServiceProvider(IHost host)
        {
            DesktopServiceProvider.SetProvider(host.Services);
        }
    }
}
