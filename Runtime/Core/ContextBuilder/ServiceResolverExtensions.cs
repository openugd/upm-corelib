using System;
using OpenUGD.Resolvers;
using OpenUGD.Services;

namespace OpenUGD.Core.ContextBuilder
{
    public static class ServiceResolverExtensions
    {
        public static void AddService(
            this IServiceRegister register,
            Func<Service> factory
        ) =>
            register.AddService(new ServiceResolver(_ => factory()));

        public static void AddService<TInterface>(
            this IServiceRegister register,
            Func<Service> factory
        ) =>
            register.AddService(new ServiceResolver(new[] {
                typeof(TInterface)
            }, _ => factory()));

        public static void AddService<TInterface1, TInterface2>(
            this IServiceRegister register,
            Func<Service> factory
        ) =>
            register.AddService(new ServiceResolver(new[] {
                typeof(TInterface1),
                typeof(TInterface2)
            }, _ => factory()));

        public static void AddService<TInterface1, TInterface2, TInterface3>(
            this IServiceRegister register,
            Func<Service> factory
        ) =>
            register.AddService(new ServiceResolver(new[] {
                typeof(TInterface1),
                typeof(TInterface2),
                typeof(TInterface3)
            }, _ => factory()));

        public static void AddService<TInterface, TImpl>(this IServiceRegister register) where TImpl : Service =>
            register.AddService(new ServiceResolver(new[] {
                typeof(TInterface)
            }, injector => {
                var resolver = new SingletonResolver(typeof(TImpl), false);
                var result = (TImpl)resolver.Resolve(injector, typeof(TImpl));
                resolver.OnUnRegister();
                return result;
            }));

        public static void AddService<TInterface1, TInterface2, TImpl>(this IServiceRegister register)
            where TImpl : Service =>
            register.AddService(new ServiceResolver(new[] {
                typeof(TInterface1),
                typeof(TInterface2)
            }, injector => {
                var resolver = new SingletonResolver(typeof(TImpl), false);
                var result = (TImpl)resolver.Resolve(injector, typeof(TImpl));
                resolver.OnUnRegister();
                return result;
            }));

        public static void AddService<TInterface1, TInterface2, TInterface3, TImpl>(this IServiceRegister register)
            where TImpl : Service =>
            register.AddService(new ServiceResolver(new[] {
                typeof(TInterface1),
                typeof(TInterface2),
                typeof(TInterface3)
            }, injector => {
                var resolver = new SingletonResolver(typeof(TImpl), false);
                var result = (TImpl)resolver.Resolve(injector, typeof(TImpl));
                resolver.OnUnRegister();
                return result;
            }));
    }
}
