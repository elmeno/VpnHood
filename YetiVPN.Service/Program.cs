using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting.WindowsServices;

namespace YetiVPN.Service
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
            .ConfigureLogging(configureLogging => configureLogging.AddFilter<EventLogLoggerProvider>(level => level >= LogLevel.Information))
                .ConfigureServices(services =>
                {
                    //if (WindowsServiceHelpers.IsWindowsService())
                        //services.AddSingleton<IHostLifetime, App>()
                    //else
                    services.AddHostedService<App>()
                    .Configure<EventLogSettings>(config =>
                    {
                        config.LogName = "YETI VPN Service";
                        config.SourceName = "YETI VPN Service Source";
                    }); ;
                }).UseWindowsService();
    }
}