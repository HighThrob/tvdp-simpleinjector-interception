using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using SimpleInjector;
using TvdP.SimpleInjector;

namespace TvdP
{    
    class Program
    {
        static void Main(string[] args)
        {
            {
                Console.WriteLine("Explicit order of interception. Inner interceptor has order=20 and outer had order=10.");

                var container = new Container();

                container.RegisterSingleton<ILogger, Logger>();
                container.Register<IService, ServiceImplementation>();
                container.InterceptWith(() => new LoggingInterceptor(container.GetInstance<ILogger>()) { Prefix = "Outer interceptor" }, type => type == typeof(IService), 10);
                container.InterceptWith(() => new LoggingInterceptor(container.GetInstance<ILogger>()) { Prefix = "Inner interceptor" }, type => type == typeof(IService), 20);
                container.Verify();

                var service = container.GetInstance<IService>();
                service.DoSomething("this");
                Console.WriteLine();
            }

            {                
                Console.WriteLine("Implicit order of interception. Later registrations wrap earlier registrations.");

                var container = new Container();

                container.RegisterSingleton<ILogger, Logger>();
                container.Register<IService, ServiceImplementation>();
                container.InterceptWith(() => new LoggingInterceptor(container.GetInstance<ILogger>()) { Prefix = "Inner interceptor" }, type => type == typeof(IService));
                container.InterceptWith(() => new LoggingInterceptor(container.GetInstance<ILogger>()) { Prefix = "Outer interceptor" }, type => type == typeof(IService));
                container.Verify();

                var service = container.GetInstance<IService>();
                service.DoSomething("this");

                Console.WriteLine();
            }

            {                
                Console.WriteLine("Adding DataObjectAttribute to proxy.");

                var container = new Container();

                container.RegisterSingleton<ILogger, Logger>();
                container.Register<IService, ServiceImplementation>();
                container.InterceptWith(() => new LoggingInterceptor(container.GetInstance<ILogger>()) { Prefix = "Interceptor" }, type => type == typeof(IService));

                container.InterceptionProxyWithOptions(
                    pgo =>
                    {
                        pgo.AdditionalAttributes.Add(
                            new Castle.DynamicProxy.CustomAttributeInfo(
                                typeof(System.ComponentModel.DataObjectAttribute).GetConstructor(Type.EmptyTypes),
                                new object[0],
                                new PropertyInfo[0] ,
                                new object[0] 
                            )
                        );

                        return pgo;
                    },
                    type => type == typeof(IService)
                );

                container.Verify();

                var service = container.GetInstance<IService>();
                service.DoSomething("this");

                var dataObjectAttribute = (System.ComponentModel.DataObjectAttribute)Attribute.GetCustomAttribute(service.GetType(), typeof(System.ComponentModel.DataObjectAttribute));

                Console.WriteLine($"IService proxy has DataObjectAttribute: { (dataObjectAttribute != null ? "Yes" : "No") }.");
                Console.WriteLine();
            }

            {
                Console.WriteLine("Registering a Fake implementation. All functionality should be fully handled by interceptors but expect exception now.");

                var container = new Container();

                container.RegisterSingleton<ILogger, Logger>();
                container.RegisterFake<IService>();
                container.InterceptWith(() => new LoggingInterceptor(container.GetInstance<ILogger>()) { Prefix = "Interceptor" }, type => type == typeof(IService));
                container.Verify();

                var service = container.GetInstance<IService>();

                try
                {
                    service.DoSomething("this");
                }
                catch (NotImplementedException)
                {
                    Console.WriteLine($"Cought expected NotImplementedException.");
                }

                Console.WriteLine();
            }


            Console.WriteLine("Press <enter> to finish.");
            Console.ReadLine();
        }
    }
}
