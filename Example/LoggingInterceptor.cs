using Castle.DynamicProxy;
using System;
using System.Collections.Generic;
using System.Text;

namespace TvdP
{
    public class LoggingInterceptor : IInterceptor
    {
        readonly ILogger logger;

        public LoggingInterceptor(ILogger logger)
        {
            this.logger = logger;
        }

        public string Prefix { get; set; }

        public void Intercept(IInvocation invocation)
        {
            logger.LogMessage($"{Prefix ?? ""}: starting {invocation.Method.Name}");
            invocation.Proceed();
            logger.LogMessage($"{Prefix ?? ""}: finished {invocation.Method.Name}");
        }
    }
}
