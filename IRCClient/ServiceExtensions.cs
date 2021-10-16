using System;
using Microsoft.Extensions.DependencyInjection;

namespace IRCClient
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddIrcClient(this IServiceCollection service, Func<IServiceProvider, IConnection> factory, ServiceLifetime lifetime = ServiceLifetime.Singleton)
        {
            service.AddTransient(factory);
            service.Add(ServiceDescriptor.Describe(typeof(IrcClient), typeof(IrcClient), lifetime));
            return service;
        }

        public static IServiceCollection AddIrcClient(this IServiceCollection service, Func<IServiceProvider, IConnectionSupplier> factory, ServiceLifetime lifetime = ServiceLifetime.Transient)
        {
            service.AddSingleton(factory);
            service.Add(ServiceDescriptor.Describe(typeof(IrcClient), typeof(IrcClient), lifetime));
            return service;
        }
    }
}