using System;
using Castle.DynamicProxy;

namespace TvdP.SimpleInjector
{
    public static class Interceptor
    {
        public static object CreateProxy(
            Type type, 
            ProxyGenerationOptions options, 
            IInterceptor[] interceptors, 
            object target
        ) =>
            options == null
                ? InterceptorExtensions.generator.CreateInterfaceProxyWithTarget(type, target, interceptors)
                : InterceptorExtensions.generator.CreateInterfaceProxyWithTarget(type, target, options, interceptors)
        ;
    }
}
