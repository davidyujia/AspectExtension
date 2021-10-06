using System;
using System.Linq;
using AspectOrientedProgramming;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection
{
    public static class AspectExtension
    {
        public static void AddAspectSingleton<TService, TImplementation>(this IServiceCollection services) where TService : class where TImplementation : class, TService
        {
            AspectBase<TService, TImplementation>(func =>
            {
                services.AddSingleton<TImplementation>();
                services.AddSingleton(func);
            });
        }

        public static void AddAspectSingleton<TService, TImplementation>(this IServiceCollection services, Func<IServiceProvider, TImplementation> implementationFactory) where TService : class where TImplementation : class, TService
        {
            AspectBase<TService, TImplementation>(func =>
            {
                services.AddSingleton(implementationFactory);
                services.AddSingleton(func);
            });
        }

        public static void AddAspectScoped<TService, TImplementation>(this IServiceCollection services) where TService : class where TImplementation : class, TService
        {
            AspectBase<TService, TImplementation>(func =>
            {
                services.AddScoped<TImplementation>();
                services.AddScoped(func);
            });
        }

        public static void AddAspectScoped<TService, TImplementation>(this IServiceCollection services, Func<IServiceProvider, TImplementation> implementationFactory) where TService : class where TImplementation : class, TService
        {
            AspectBase<TService, TImplementation>(func =>
            {
                services.AddScoped(implementationFactory);
                services.AddScoped(func);
            });
        }

        public static void AddAspectTransient<TService, TImplementation>(this IServiceCollection services) where TService : class where TImplementation : class, TService
        {
            AspectBase<TService, TImplementation>(func =>
            {
                services.AddTransient<TImplementation>();
                services.AddTransient(func);
            });
        }

        public static void AddAspectTransient<TService, TImplementation>(this IServiceCollection services, Func<IServiceProvider, TImplementation> implementationFactory) where TService : class where TImplementation : class, TService
        {
            AspectBase<TService, TImplementation>(func =>
            {
                services.AddTransient(implementationFactory);
                services.AddTransient(func);
            });
        }

        private static void AspectBase<TService, TImplementation>(Action<Func<IServiceProvider, TService>> factory) where TService : class where TImplementation : class, TService
        {
            if (!typeof(TService).IsInterface)
            {
                throw new ArgumentException($"Generic parameter TService: \"{typeof(TService).FullName}\" must be Interface");
            }

            factory(CreateAspectProxy<TService, TImplementation>);
        }

        private static TService CreateAspectProxy<TService, TImplementation>(this IServiceProvider provider) where TService : class where TImplementation : class, TService
        {
            var instance = provider.GetRequiredService<TImplementation>();

            return instance.CreateProxy<TService, TImplementation, AspectServiceDispatchProxy<TService>>((proxy, service) =>
            {
                proxy.SetAspectInstance(service, provider);
            });
        }
    }

    public class AspectServiceDispatchProxy<T> : AspectDispatchProxy<T> where T : class
    {
        private IServiceProvider _provider;

        internal void SetAspectInstance(T instance, IServiceProvider provider)
        {
            SetAspectInstance(instance);

            _provider = provider;
        }

        protected override void SetAspectAttr(AspectAttribute attr)
        {
            var attrType = attr.GetType();

            foreach (var prop in attrType.GetProperties())
            {
                if (!prop.GetAttributes<FromAspectServiceAttribute>().Any())
                {
                    continue;
                }

                var service = _provider.GetRequiredService(prop.PropertyType);

                prop.SetValue(attr, service);
            }

            foreach (var field in attrType.GetFields())
            {
                if (!field.GetAttributes<FromAspectServiceAttribute>().Any())
                {
                    continue;
                }

                var service = _provider.GetRequiredService(field.FieldType);

                field.SetValue(attr, service);
            }
        }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class FromAspectServiceAttribute : Attribute { }
}