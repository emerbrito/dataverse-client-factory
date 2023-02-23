using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EmBrito.Dataverse.Extensions.ClientFactory
{
    public static class ServiceCollectionExtensions
    {

        public static IServiceCollection AddDataverseClientFactory(this IServiceCollection services, Action<ClientFactoryOptions> configureFacotry)
        {
            services.Configure(configureFacotry);
            services.AddSingleton(serviceProvider =>
            {
                var factoryOptions = serviceProvider.GetRequiredService<IOptions<ClientFactoryOptions>>()?.Value;
                ArgumentNullException.ThrowIfNull(factoryOptions, nameof(factoryOptions));

                return new DataverseClientFactory(
                    factoryOptions.ClientId,
                    factoryOptions.ClientSecret,
                    factoryOptions.DataverseInstanceUri,
                    factoryOptions.DeferConnection,
                    factoryOptions.EnableAffinityCookie,
                    factoryOptions.Logger ?? serviceProvider.GetRequiredService<ILogger<DataverseClientFactory>>());

            });

            return services;
        }

    }
}
