using System;
using SimpleInjector;
using Castle.DynamicProxy;
using Xunit;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;

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

        [Fact]
        public void InterceptWith_has_no_problem_with_200_interceptors_on_a_service()
        {
            var container = new Container();

            int interceptorWasCalled = 0;

            container.Register<IService>(() => new Service());

            for(var i = 0; i < 200; ++i)
                container.InterceptWith(
                    new Interceptor(
                        ivc =>
                        {
                            interceptorWasCalled += 1;
                            ivc.Proceed();
                        }
                    ),
                    type => type == typeof(IService)
                );

            container.Verify();

            var service = container.GetInstance<IService>();
            service.DoSomething();

            Assert.Equal(200, interceptorWasCalled);
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

        static (Action<Container>, string)[] ArgumentNullCausingList = 
            new (Action<Container>,string)[]
            {
                (c => ((Container)null).InterceptWith<IInterceptor>(_ => true), "container"),
                (c => c.InterceptWith<IInterceptor>(null), "predicate"),

                (c => ((Container)null).InterceptWith(() => null, _ => true), "container"),
                (c => c.InterceptWith( (Func<IInterceptor>)null, _ => true), "interceptorFactory"),
                (c => c.InterceptWith(() => null, null), "predicate"),

                (c => ((Container)null).InterceptWith(_ => null, _ => true), "container"),
                (c => c.InterceptWith( (Func<ExpressionBuiltEventArgs, IInterceptor>)null, _ => true), "interceptorFactory"),
                (c => c.InterceptWith(_ => null, null), "predicate"),

                (c => ((Container)null).InterceptWith(new Interceptor(), _ => true), "container"),
                (c => c.InterceptWith( (IInterceptor)null, _ => true), "interceptor"),
                (c => c.InterceptWith(new Interceptor(), null), "predicate"),

                (c => ((Container)null).InterceptionProxyWithOptions(o => o, _ => true), "container"),
                (c => c.InterceptionProxyWithOptions(null, _ => true), "optionsModifier"),
                (c => c.InterceptionProxyWithOptions(o => o, null), "predicate"),

                (c => ((Lifestyle)null).CreateRegistrationForFake(typeof(IService), c), "lifestyle"),
                (c => Lifestyle.Singleton.CreateRegistrationForFake(null, c), "serviceType"),
                (c => Lifestyle.Singleton.CreateRegistrationForFake(typeof(IService),null), "container"),

                (c => ((Container)null).RegisterFake(typeof(IService), Lifestyle.Singleton), "container"),
                (c => c.RegisterFake(null, Lifestyle.Singleton), "serviceType"),
                (c => c.RegisterFake(typeof(IService), null), "lifestyle"),

                (c => ((Container)null).RegisterFake(typeof(IService)), "container"),
                (c => c.RegisterFake(null), "serviceType"),

                (c => ((Container)null).RegisterFake<IService>(Lifestyle.Singleton), "container"),
                (c => c.RegisterFake<IService>(null), "lifestyle"),

                (c => ((Container)null).RegisterFake<IService>(), "container"),
            };

        public static IEnumerable<object[]> GenerateArgumentNullExceptionCausingActions()
        {
            foreach((var nullCausingExpression, var parameterName) in ArgumentNullCausingList)
                yield return new object[] { nullCausingExpression, parameterName };
        }

        [Theory]
        [MemberData(nameof(GenerateArgumentNullExceptionCausingActions))]
        public void Expected_ArgumentNullException_in_logical_places(Action<Container> exceptionCausingAction, string parameterName)
        {
            Assert.Throws<ArgumentNullException>(parameterName, () => exceptionCausingAction(new Container()));
        }
    }
}