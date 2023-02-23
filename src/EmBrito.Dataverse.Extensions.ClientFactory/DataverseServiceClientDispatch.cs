using Microsoft.PowerPlatform.Dataverse.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EmBrito.Dataverse.Extensions.ClientFactory
{
    internal class DataverseServiceClientDispatch<T> : DispatchProxy where T : class
    {

        private IOrganizationServiceAsync2? _target;
        DataverseClientFactory? _factory;
        private bool _disposed;

        public static T CreateProxy(IOrganizationServiceAsync2 target, DataverseClientFactory factory)
        {
            var proxy = Create<T, DataverseServiceClientDispatch<T>>() as DataverseServiceClientDispatch<T>;

            proxy!._target = target;
            proxy._factory = factory;

            return (proxy as T)!;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod == null)
            {
                return null;
            }

            if (targetMethod.Name == "Dispose")
            {
                Dispose();
                return null;
            }

            return targetMethod.Invoke(_target, args);
        }

        public void Dispose()
        {
            if (!_disposed && _target != null)
            {
                _factory?.AddToPool(_target);
                _disposed = true;
            }
        }
    }
}
