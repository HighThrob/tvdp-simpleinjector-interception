using System;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using SimpleInjector;
using Castle.DynamicProxy;
using System.Runtime.CompilerServices;

namespace TvdP.SimpleInjector
{
    // Extension methods for interceptor registration
    // NOTE: These extension methods can only intercept interfaces, not abstract types.
    public static class InterceptorExtensions
    {
        /// <summary>
        /// Registers an interceptor in the container
        /// </summary>
        /// <typeparam name="TInterceptor">The type of the interceptor. The container will be required to provide an instance of this type.</typeparam>
        /// <param name="container">The container to register an interceptor into.</param>
        /// <param name="predicate">Predicate that determines which service registrations will be intercepted using this interceptor.</param>
        /// <param name="order">gives the order in which the interceptor is applied. Higher order numbers are applied before lower order numbers. When order numbers are equal the order of registration determines the order of application.</param>
        public static void InterceptWith<TInterceptor>(
            this Container container,
            Func<Type, bool> predicate, 
            int order = 0
        )
            where TInterceptor : class, IInterceptor
        {
            if (container == null) throw new ArgumentNullException(nameof(container));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            new InterceptionHelper
            {
                BuildInterceptorExpression =
                    e => BuildInterceptorExpression<TInterceptor>(container),
                Predicate = type => predicate(type),
                Order = order
            }
                .AttachTo(container);
        }

        /// <summary>
        /// Registers an interceptor in the container.
        /// </summary>
        /// <typeparam name="TInterceptor">The type of the interceptor.</typeparam>
        /// <param name="container">The container to register an interceptor into.</param>
        /// <param name="interceptorFactory">The factory function that will provide an instance of the interceptor.</param>
        /// <param name="predicate">Predicate that determines which service registrations will be intercepted using this interceptor.</param>
        /// <param name="order">gives the order in which the interceptor is applied. Higher order numbers are applied before lower order numbers. When order numbers are equal the order of registration determines the order of application.</param>
        public static void InterceptWith(
            this Container container,
            Func<IInterceptor> interceptorFactory, 
            Func<Type, bool> predicate, 
            int order = 0
        )
        {
            if (container == null) throw new ArgumentNullException(nameof(container));
            if (interceptorFactory == null) throw new ArgumentNullException(nameof(interceptorFactory));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            new InterceptionHelper
            {
                BuildInterceptorExpression =
                    e => Expression.Invoke(Expression.Constant(interceptorFactory)),
                Predicate = type => predicate(type),
                Order = order
            }
                .AttachTo(container);
        }

        /// <summary>
        /// Registers an interceptor in the container.
        /// </summary>
        /// <typeparam name="TInterceptor">The type of the interceptor.</typeparam>
        /// <param name="container">The container to register an interceptor into.</param>
        /// <param name="interceptorFactory">The factory function that will provide an instance of the interceptor.</param>
        /// <param name="predicate">Predicate that determines which service registrations will be intercepted using this interceptor.</param>
        /// <param name="order">gives the order in which the interceptor is applied. Higher order numbers are applied before lower order numbers. When order numbers are equal the order of registration determines the order of application.</param>
        public static void InterceptWith(
            this Container container,
            Func<ExpressionBuiltEventArgs, IInterceptor> interceptorFactory,
            Func<Type, bool> predicate, 
            int order = 0
        )
        {
            if (container == null) throw new ArgumentNullException(nameof(container));
            if (interceptorFactory == null) throw new ArgumentNullException(nameof(interceptorFactory));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            new InterceptionHelper
            {
                BuildInterceptorExpression =
                    e =>
                        Expression.Invoke(
                            Expression.Constant(interceptorFactory),
                            Expression.Constant(e)
                        ),
                Predicate = type => predicate(type),
                Order = order
            }
                .AttachTo(container);
        }

        /// <summary>
        /// Registers an interceptor in the container.
        /// </summary>
        /// <typeparam name="TInterceptor">The type of the interceptor.</typeparam>
        /// <param name="container">The container to register an interceptor into.</param>
        /// <param name="interceptor">The interceptor instance to register.</param>
        /// <param name="predicate">Predicate that determines which service registrations will be intercepted using this interceptor.</param>
        /// <param name="order">gives the order in which the interceptor is applied. Higher order numbers are applied before lower order numbers. When order numbers are equal the order of registration determines the order of application.</param>
        public static void InterceptWith(
            this Container container,
            IInterceptor interceptor, 
            Func<Type, bool> predicate, 
            int order = 0
        )
        {
            if (container == null) throw new ArgumentNullException(nameof(container));
            if (interceptor == null) throw new ArgumentNullException(nameof(interceptor));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            new InterceptionHelper
            {
                BuildInterceptorExpression = e => Expression.Constant(interceptor),
                Predicate = predicate,
                Order = order
            }
                .AttachTo(container);
        }

        /// <summary>
        /// Registers a proxy options modifier in the container. 
        /// </summary>
        /// <param name="container">The container to register the proxy options modifier into.</param>
        /// <param name="optionsModifier">The modifier to be registered.</param>
        /// <param name="predicate">Predicate that determines which interception proxies will have the options modified using this modifier.</param>
        public static void InterceptionProxyWithOptions(
            this Container container, 
            Func<ProxyGenerationOptions, ProxyGenerationOptions> optionsModifier, 
            Func<Type, bool> predicate
        )
        {
            if (container == null) throw new ArgumentNullException(nameof(container));
            if (optionsModifier == null) throw new ArgumentNullException(nameof(optionsModifier));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            new ProxyGenerationOptionsHelper
            {
                ProxyGenerationOptionsTransform = optionsModifier,
                Predicate = predicate
            }
                .AttachTo(container);
        }

        [DebuggerStepThrough]
        static Expression BuildInterceptorExpression<TInterceptor>(
            Container container
        )
            where TInterceptor : class
        {
            var interceptorRegistration = container.GetRegistration(typeof(TInterceptor));

            if (interceptorRegistration == null)
            {
                // This will throw an ActivationException
                container.GetInstance<TInterceptor>();
            }

            return interceptorRegistration.BuildExpression();
        }

        class ProxyInfo
        {
            public (Expression expression, int order)[] interceptorExpressions { get; set; }
            public ProxyGenerationOptions ProxyOptions { get; set; }
            public Expression targetExpression { get; set; }
        }

        static ConditionalWeakTable<Expression, ProxyInfo> ProxyInfoMap = new ConditionalWeakTable<Expression, ProxyInfo>();

        class ProxyGenerationOptionsHelper
        {
            internal Func<ProxyGenerationOptions, ProxyGenerationOptions> ProxyGenerationOptionsTransform { get; set; }
            internal Func<Type, bool> Predicate { get; set; }

            [DebuggerStepThrough]
            internal void OnExpressionBuilt(object sender, ExpressionBuiltEventArgs e)
            {
                if (this.Predicate(e.RegisteredServiceType))
                {
                    ThrowIfServiceTypeNotAnInterface(e);
                    ModifyProxyGenerationOptionsForBuilderExpression(e);
                }
            }

            [DebuggerStepThrough]
            void ModifyProxyGenerationOptionsForBuilderExpression(ExpressionBuiltEventArgs e) =>
                BuildProxyExpression(
                    e,
                    proxyInfo =>
                        proxyInfo.ProxyOptions = ProxyGenerationOptionsTransform(proxyInfo.ProxyOptions ?? new ProxyGenerationOptions())
                );

            internal void AttachTo(Container container) =>
                container.ExpressionBuilt += OnExpressionBuilt;
        }

        class InterceptionHelper
        {
            public int Order { get; internal set; }

            internal Func<ExpressionBuiltEventArgs, Expression> BuildInterceptorExpression { get; set; }

            internal Func<Type, bool> Predicate { get; set; }

            [DebuggerStepThrough]
            internal void OnExpressionBuilt(object sender, ExpressionBuiltEventArgs e)
            {
                if (Predicate(e.RegisteredServiceType))
                {
                    ThrowIfServiceTypeNotAnInterface(e);
                    AddInterceptorToBuilderExpression(e);
                }
            }

            [DebuggerStepThrough]
            void AddInterceptorToBuilderExpression(ExpressionBuiltEventArgs e) =>
                BuildProxyExpression(
                    e,
                    proxyInfo =>
                        proxyInfo.interceptorExpressions =
                            new[] { (expression: this.BuildInterceptorExpression(e), order: Order) }
                                .Concat(proxyInfo.interceptorExpressions)
                                .OrderBy(t => t.order)
                                .ToArray()
                );

            internal void AttachTo(Container container) =>
                container.ExpressionBuilt += OnExpressionBuilt;
        }

        [DebuggerStepThrough]
        static void ThrowIfServiceTypeNotAnInterface(ExpressionBuiltEventArgs e)
        {
            // NOTE: We can only handle interfaces, because
            // System.Runtime.Remoting.Proxies.RealProxy only supports interfaces.
            if (!e.RegisteredServiceType.GetTypeInfo().IsInterface)
                throw new NotSupportedException(
                    $"Can't intercept type {e.RegisteredServiceType.Name} because it is not an interface."
                );
        }

        static readonly MethodInfo NonGenericInterceptorCreateProxyMethod =
            typeof(Interceptor)
                .GetMethods()
                .Where(method => method.Name == "CreateProxy" && method.GetParameters().Length == 4)
                .Single();

        [DebuggerStepThrough]
        static void BuildProxyExpression(ExpressionBuiltEventArgs e, Action<ProxyInfo> modifyProxyInfo)
        {
            var proxyInfo = 
                ProxyInfoMap.GetValue(
                    e.Expression, 
                    exp => 
                        new ProxyInfo { 
                            targetExpression = exp, 
                            interceptorExpressions = new (Expression, int)[0] 
                        }
                );

            modifyProxyInfo(proxyInfo);

            // Create call to
            // (ServiceType)Interceptor.CreateProxy(Type, IInterceptor, object)
            Expression proxyExpression =
                Expression.Convert(
                    Expression.Call(NonGenericInterceptorCreateProxyMethod,
                        Expression.Constant(e.RegisteredServiceType, typeof(Type)),
                        Expression.Constant(proxyInfo.ProxyOptions, typeof(ProxyGenerationOptions)),
                        Expression.NewArrayInit(typeof(IInterceptor), proxyInfo.interceptorExpressions.Select(t => t.expression)),
                        proxyInfo.targetExpression),
                    e.RegisteredServiceType);

            if (
                proxyInfo.targetExpression is ConstantExpression 
                && proxyInfo.interceptorExpressions.Length == 1 
                && proxyInfo.interceptorExpressions[0].expression is ConstantExpression
            )
                proxyExpression = Expression.Constant(CreateInstance(proxyExpression), e.RegisteredServiceType);

            ProxyInfoMap.Add(proxyExpression, proxyInfo);

            e.Expression = proxyExpression;
        }

        [DebuggerStepThrough]
        static object CreateInstance(Expression expression) =>
            Expression.Lambda<Func<object>>(
                expression,
                new ParameterExpression[0]
            )
            .Compile()
            .Invoke();

        /// <summary>
        /// Creates a SimpleInjector registration for a fake, not callable instance of a service.
        /// </summary>
        /// <param name="lifestyle">The lifestyle for the registration.</param>
        /// <param name="serviceType">The service type to create a fake registration for.</param>
        /// <param name="container">The container where the registration in for.</param>
        /// <returns></returns>
        public static Registration CreateRegistrationForFake(this Lifestyle lifestyle, Type serviceType, Container container)
        {
            if (lifestyle == null) throw new ArgumentNullException(nameof(lifestyle));
            if (container == null) throw new ArgumentNullException(nameof(container));
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));

            var instance = generator.CreateInterfaceProxyWithoutTarget(serviceType);
            return lifestyle.CreateRegistration(serviceType, () => instance, container);
        }

        /// <summary>
        /// Registers a fake, not callable instance of the service.
        /// </summary>
        /// <param name="container">The container to register the fake implementation in.</param>
        /// <param name="serviceType">The type of the service.</param>
        /// <param name="lifestyle">The lifestyle for the registration.</param>
        public static void RegisterFake(this Container container, Type serviceType, Lifestyle lifestyle)
        {
            if (container == null) throw new ArgumentNullException(nameof(container));
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            if (lifestyle == null) throw new ArgumentNullException(nameof(lifestyle));

            container.AddRegistration(serviceType, lifestyle.CreateRegistrationForFake(serviceType, container));
        }

        /// <summary>
        /// Registers a singleton fake, not callable instance of the service.
        /// </summary>
        /// <param name="container">The container to register the fake implementation in.</param>
        /// <param name="serviceType">The type of the service.</param>
        public static void RegisterFake(this Container container, Type serviceType) =>
            container.RegisterFake(serviceType, Lifestyle.Singleton);

        /// <summary>
        /// Registers a singleton fake, not callable instance of the service.
        /// </summary>
        /// <typeparam name="T">The service type.</typeparam>
        /// <param name="container">The container to register the fake implementation in.</param>
        /// <param name="lifestyle">The lifestyle for the registration.</param>
        public static void RegisterFake<T>(this Container container, Lifestyle lifestyle)
            where T : class =>
            container.RegisterFake(typeof(T), lifestyle);

        /// <summary>
        /// Registers a singleton fake, not callable instance of the service.
        /// </summary>
        /// <typeparam name="T">The service type.</typeparam>
        /// <param name="container">The container to register the fake implementation in.</param>
        public static void RegisterFake<T>(this Container container)
            where T : class =>
            container.RegisterFake(typeof(T), Lifestyle.Singleton);

        internal static readonly ProxyGenerator generator = new ProxyGenerator();
    }

}
