using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace AspectOrientedProgramming
{
    public static class Extensions
    {
        public static T CreateProxy<T, TService, TProxy>(this TService instance, Action<TProxy, TService> action = null) where T : class where TService : T where TProxy : AspectDispatchProxy<T>
        {
            if (instance is TProxy)
            {
                return instance;
            }

            object proxy = DispatchProxy.Create<T, TProxy>();

            if (proxy is TProxy aspectProxy && action != null)
            {
                action(aspectProxy, instance);
            }

            return (T)proxy;
        }

        public static T[] GetAttributes<T>(this ICustomAttributeProvider attributeProvider) where T : Attribute
        {
            return attributeProvider.GetCustomAttributes(typeof(T), true).Select(x => (T)x).ToArray();
        }
    }

    public sealed class AspectProxy<T> where T : class
    {
        public T ProxyInstance { get; }

        private AspectProxy(T instance)
        {
            ProxyInstance = Create(instance);
        }

        public static T Create<TService>(TService instance) where TService : T
        {
            return instance.CreateProxy<T, TService, AspectDispatchProxy<T>>();
        }

        public static implicit operator T(AspectProxy<T> proxy)
        {
            return proxy.ProxyInstance;
        }

        public static implicit operator AspectProxy<T>(T instance)
        {
            return new AspectProxy<T>(instance);
        }
    }

    public class AspectDispatchProxy<T> : DispatchProxy where T : class
    {
        private T _aspectInstance;

        internal void SetAspectInstance(T instance)
        {
            _aspectInstance = instance;
        }

        private static IEnumerable<AspectAttribute> GetAspectAttributes(ICustomAttributeProvider attributeProvider)
        {
            if (attributeProvider == null)
            {
                return Array.Empty<AspectAttribute>();
            }

            var attrs = attributeProvider.GetAttributes<AspectAttribute>();

            return attrs.OrderBy(x => x.Order);
        }

        private AspectAttribute[] GetAspectAttributeBases(MethodInfo targetMethod)
        {
            var method = GetInstanceMethod(targetMethod);

            return GetAspectAttributes(targetMethod.ReflectedType).Concat(
                GetAspectAttributes(method?.ReflectedType)).Concat(
                GetAspectAttributes(targetMethod)).Concat(
                GetAspectAttributes(method)).ToArray();
        }

        protected virtual void SetAspectAttr(AspectAttribute attr) { }

        protected MethodInfo GetInstanceMethod(MethodInfo targetMethod)
        {
            var parameterTypes = targetMethod.GetParameters().Select(x => x.ParameterType).ToArray();

            var genericArguments = targetMethod.GetGenericArguments();

            var method = _aspectInstance.GetType().GetMethod(targetMethod.Name, genericArguments.Length, parameterTypes);

            if (method is { IsGenericMethod: true })
            {
                method = method.MakeGenericMethod(genericArguments);
            }

            return method;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {

            var attrs = GetAspectAttributeBases(targetMethod);

            if (!attrs.Any())
            {
                return targetMethod.Invoke(_aspectInstance, args);
            }

            AspectAttribute aspect = new AspectExecutor(_aspectInstance);

            foreach (var attr in attrs)
            {
                SetAspectAttr(attr);
                attr.Next = aspect;
                aspect = attr;
            }

            var context = new AspectProxyContext(GetInstanceMethod(targetMethod), args);

            attrs.First().Executing(context);

            return context.ReturnValue;
        }
    }

    public sealed class AspectProxyContext
    {
        internal AspectProxyContext(MethodInfo targetMethod, object[] args)
        {
            TargetMethod = targetMethod;
            Args = args;
        }

        public MethodInfo TargetMethod { get; }
        public object[] Args { get; }
        public object ReturnValue { get; set; }
    }

    internal sealed class AspectExecutor : AspectAttribute
    {
        private readonly object _instance;

        internal AspectExecutor(object instance)
        {
            _instance = instance;
        }

        protected override void Invoke(AspectProxyContext context)
        {
            context.ReturnValue = context.TargetMethod.Invoke(_instance, context.Args);
        }
    }

    public abstract class AspectAttribute : Attribute
    {
        public int Order { get; set; } = 100;

        internal AspectAttribute Next { get; set; }

        internal void Executing(AspectProxyContext context)
        {
            Invoke(context);
        }

        protected virtual void Invoke(AspectProxyContext context)
        {
            Next.Invoke(context);
        }

        #region sealed

        public sealed override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public sealed override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public sealed override bool IsDefaultAttribute()
        {
            return base.IsDefaultAttribute();
        }

        public sealed override bool Match(object obj)
        {
            return base.Match(obj);
        }

        public sealed override string ToString()
        {
            return base.ToString();
        }

        public sealed override object TypeId => base.TypeId;

        #endregion
    }

    public abstract class AspectAsyncAttribute : AspectAttribute
    {
        protected sealed override void Invoke(AspectProxyContext context)
        {
            var task = InvokeAsync(context);

            SpinWait.SpinUntil(() => task.IsCompleted);

            if (task.IsFaulted)
            {
                throw task.Exception ?? new Exception($"{GetType().FullName}.{nameof(InvokeAsync)} is faulted.");
            }
        }

        protected virtual async Task InvokeAsync(AspectProxyContext context)
        {
            base.Invoke(context);

            await Task.CompletedTask;
        }
    }
}
