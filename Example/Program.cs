using System;
using SimpleInjector;
using TvdP.SimpleInjector;

namespace TvdP
{
    class Program
    {
        static void Main(string[] args)
        {
            var container = new Container();

            container.RegisterSingleton<ILogger, Logger>();
            container.Register<IService, ServiceImplementation>();
            container.InterceptWith(() => new LoggingInterceptor(container.GetInstance<ILogger>()) { Prefix = "2" }, type => type == typeof(IService), 20);
            container.InterceptWith(() => new LoggingInterceptor(container.GetInstance<ILogger>()) { Prefix = "1" }, type => type == typeof(IService), 10);
            container.Verify();

            var service = container.GetInstance<IService>();
            service.DoSomething("this");

            Console.WriteLine("Press <enter> to finish.");
            Console.ReadLine();
        }
    }
}
