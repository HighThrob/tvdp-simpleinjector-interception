using System;
using SimpleInjector;
using Castle.DynamicProxy;
using Xunit;
using System.Reflection;

namespace TvdP.SimpleInjector
{
    public class InterceptorExtensionsTests
    {
        public interface IService
        {
            void DoSomething();
        }

        public class Service : IService
        {
            private readonly Action doSomethingMock;

            public Service(Action doSomethingMock)
            {
                this.doSomethingMock = doSomethingMock;
            }

            public Service()
                : this(null)
            { }

            public void DoSomething() => doSomethingMock?.Invoke();
        }

        public class Interceptor : IInterceptor
        {
            readonly Action<IInvocation> InterceptMock;

            public Interceptor(Action<IInvocation> interceptMock)
            {
                this.InterceptMock = interceptMock;
            }

            public Interceptor()
                : this(null)
            { }

            public string Prefix { get; set; }

            public void Intercept(IInvocation invocation) =>
                (InterceptMock ?? (ivc => ivc.Proceed())).Invoke(invocation);
        }


        void InterceptWithX_registers_interceptor_for_service(Action<Container,Action> interceptorRegistration)
        {
            var container = new Container();

            bool interceptorWasCalled = false;

            container.Register<IService>(() => new Service());
            interceptorRegistration(container, ()=> { interceptorWasCalled = true; });
            container.Verify();

            var service = container.GetInstance<IService>();
            service.DoSomething();

            Assert.True(interceptorWasCalled);
        }

        [Fact]
        public void InterceptWith_registers_interceptor_using_factory_for_service() =>
            InterceptWithX_registers_interceptor_for_service(
                (container, callback) =>
                    container.InterceptWith(
                        () =>
                            new Interceptor(
                                ivc =>
                                {
                                    callback();
                                    ivc.Proceed();
                                }
                            ),
                        type => type == typeof(IService)
                    )
            );

        [Fact]
        public void InterceptWith_registers_interceptor_using_instance_for_service() =>
            InterceptWithX_registers_interceptor_for_service(
                (container, callback) =>
                {
                    var instance =
                        new Interceptor(
                            ivc =>
                            {
                                callback();
                                ivc.Proceed();
                            }
                        );

                    container.InterceptWith(
                        instance,
                        type => type == typeof(IService)
                    );
                }
            );

        [Fact]
        public void InterceptWith_registers_interceptor_using_container_resolution_for_service() =>
            InterceptWithX_registers_interceptor_for_service(
                (container, callback) =>
                {
                    var instance =
                        new Interceptor(
                            ivc =>
                            {
                                callback();
                                ivc.Proceed();
                            }
                        );

                    container.RegisterInstance<IInterceptor>(instance);

                    container.InterceptWith<IInterceptor>(
                        type => type == typeof(IService)
                    );
                }
            );

        [Fact]
        public void InterceptWith_registration_ignores_service_when_predicate_returns_false()
        {
            var container = new Container();

            bool interceptorWasCalled = false;

            container.Register<IService>(() => new Service());
            container.InterceptWith(
                () =>
                    new Interceptor(
                        ivc =>
                        {
                            interceptorWasCalled = true;
                            ivc.Proceed();
                        }
                    ),
                type => false
            );
            container.Verify();

            var service = container.GetInstance<IService>();
            service.DoSomething();

            Assert.False(interceptorWasCalled);
        }

        [Fact]
        public void InterceptWith_order_determines_order_of_interceptors()
        {
            var container = new Container();

            int callCount = 1;
            int interceptor1WasCalled = 0;
            int interceptor2WasCalled = 0;
            int interceptor3WasCalled = 0;

            container.Register<IService>(() => new Service());

            void RegisterInterceptor(Action callback, int order)
            {
                container.InterceptWith(
                    () =>
                        new Interceptor(
                            ivc =>
                            {
                                callback();
                                ivc.Proceed();
                            }
                        ),
                    type => type == typeof(IService),
                    order
                );
            }

            RegisterInterceptor(() => interceptor1WasCalled = callCount++, 10);
            RegisterInterceptor(() => interceptor2WasCalled = callCount++, 30);
            RegisterInterceptor(() => interceptor3WasCalled = callCount++, 20);

            container.Verify();

            var service = container.GetInstance<IService>();
            service.DoSomething();

            Assert.Equal(1, interceptor1WasCalled);
            Assert.Equal(3, interceptor2WasCalled);
            Assert.Equal(2, interceptor3WasCalled);
        }

        [Fact]
        public void InterceptWith_respects_registration_order_for_registrations_with_equal_order_number()
        {
            var container = new Container();

            int callCount = 1;
            int interceptor1WasCalled = 0;
            int interceptor2WasCalled = 0;
            int interceptor3WasCalled = 0;

            container.Register<IService>(() => new Service());

            void RegisterInterceptor(Action callback, int order)
            {
                container.InterceptWith(
                    () =>
                        new Interceptor(
                            ivc =>
                            {
                                callback();
                                ivc.Proceed();
                            }
                        ),
                    type => type == typeof(IService),
                    order
                );
            }

            RegisterInterceptor(() => interceptor1WasCalled = callCount++, 10);
            RegisterInterceptor(() => interceptor2WasCalled = callCount++, 10);
            RegisterInterceptor(() => interceptor3WasCalled = callCount++, 20);

            container.Verify();

            var service = container.GetInstance<IService>();
            service.DoSomething();

            Assert.Equal(2, interceptor1WasCalled);
            Assert.Equal(1, interceptor2WasCalled);
            Assert.Equal(3, interceptor3WasCalled);
        }

        [Fact]
        public void InterceptionProxyWithOptions_sets_options_for_proxy()
        {
            var container = new Container();

            var serviceInstance = new Service();

            container.RegisterInstance<IService>(serviceInstance);
            container.InterceptionProxyWithOptions(
                pgo =>
                {
                    pgo.AdditionalAttributes.Add(
                        new Castle.DynamicProxy.CustomAttributeInfo(
                            typeof(System.ComponentModel.DataObjectAttribute).GetConstructor(Type.EmptyTypes),
                            new object[0],
                            new PropertyInfo[0],
                            new object[0]
                        )
                    );

                    return pgo;
                },
                type => type == typeof(IService)
            );

            container.Verify();

            var service = container.GetInstance<IService>();

            var dataObjectAttribute = (System.ComponentModel.DataObjectAttribute)Attribute.GetCustomAttribute(service.GetType(), typeof(System.ComponentModel.DataObjectAttribute));

            Assert.NotNull(dataObjectAttribute);

            //ensure original doesn't have attribute
            dataObjectAttribute = (System.ComponentModel.DataObjectAttribute)Attribute.GetCustomAttribute(serviceInstance.GetType(), typeof(System.ComponentModel.DataObjectAttribute));

            Assert.Null(dataObjectAttribute);
        }

        void RegisterFakeX_registers_a_fake_implementation_of_a_service(Action<Container> register, Lifestyle expectedLifestyle)
        {
            var container = new Container();

            register(container);
            container.Verify();

            var service = container.GetInstance<IService>();

            Assert.Throws<NotImplementedException>(() => service.DoSomething());

            Assert.Same(container.GetRegistration(typeof(IService)).Lifestyle, expectedLifestyle);
        }

        [Fact]
        public void RegisterFake_registers_a_fake_implementation_of_a_service() =>
            RegisterFakeX_registers_a_fake_implementation_of_a_service(container => container.RegisterFake<IService>(), Lifestyle.Singleton);

        [Fact]
        public void RegisterFake_with_lifestyle_registers_a_fake_implementation_of_a_service_with_given_lifestyle() =>
            RegisterFakeX_registers_a_fake_implementation_of_a_service(container => container.RegisterFake<IService>(Lifestyle.Transient), Lifestyle.Transient);

        [Fact]
        public void Fake_registration_can_be_intercepted()
        {
            var container = new Container();

            var interceptorWasCalled = false;

            container.RegisterFake<IService>();
            container.InterceptWith(
                () =>
                    new Interceptor(
                        ivc =>
                        {
                            interceptorWasCalled = true;
                        }
                    ),
                type => type == typeof(IService)
            );
            container.Verify();

            var service = container.GetInstance<IService>();

            service.DoSomething();

            Assert.True(interceptorWasCalled);
        }
    }
}