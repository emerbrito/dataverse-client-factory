using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.PowerPlatform.Dataverse.Client.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EmBrito.Dataverse.Extensions.ClientFactory
{
    public class DataverseClientFactory : IDisposable
    {

        #region PrivateDeclarations

        private readonly Uri _instanceUri;
        private readonly string _appId;
        private readonly string _appSecret;
        private readonly ConcurrentQueue<ServiceClient> _queue = new ConcurrentQueue<ServiceClient>();
        private ServiceClient _serviceClient;
        private ILogger<DataverseClientFactory> _log;
        private Func<ConnectionOptions, ServiceClient>? _serviceClientProvider = null;

        #endregion

        #region Public Properties

        public int AvailableConnections { get => _queue.Count; }

        public ServiceClient InnerServiceClient { get => _serviceClient; }

        public bool IsReady { get => _serviceClient?.IsReady ?? false; }

        public int RecommendedDegreeOfParallelism { get => _serviceClient?.RecommendedDegreesOfParallelism ?? 0; }

        #endregion

        #region Constructors

        public DataverseClientFactory(string appId, string appSecret, string dataverseInstanceUrl, ILogger<DataverseClientFactory> logger)
            : this(appId, appSecret, dataverseInstanceUrl, false, true, logger)
        {
        }

        public DataverseClientFactory(string appId, string appSecret, string dataverseInstanceUrl, bool deferConnection, ILogger<DataverseClientFactory> logger)
            : this(appId, appSecret, dataverseInstanceUrl, deferConnection, true, logger)
        {
        }

        public DataverseClientFactory(string appId, string appSecret, string dataverseInstanceUrl, bool deferConnection, bool enableAffinityCookie, ILogger<DataverseClientFactory> logger)
        {
            if (string.IsNullOrWhiteSpace(appId)) throw new ArgumentNullException(nameof(appId));
            if (string.IsNullOrWhiteSpace(appSecret)) throw new ArgumentNullException(nameof(appSecret));
            if (string.IsNullOrWhiteSpace(dataverseInstanceUrl)) throw new ArgumentNullException(nameof(dataverseInstanceUrl));

            _log = logger ?? throw new ArgumentNullException(nameof(logger));

            if (Uri.TryCreate(dataverseInstanceUrl, UriKind.Absolute, out Uri? uri))
            {
                _instanceUri = uri;
            }
            else
            {
                throw new ArgumentException($"Invalid or unexpected Dataverse instance url was passed as an argument to {nameof(DataverseClientFactory)}. Url: {dataverseInstanceUrl}");
            }

            _appId = appId;
            _appSecret = appSecret;

            _serviceClient = InitializeClient(appId, appSecret, dataverseInstanceUrl, deferConnection, enableAffinityCookie, logger);
        }

        #endregion

        #region Public Interface 

        public bool Connect()
        {
            return _serviceClient.Connect();
        }

        public IDataverseClient CreateClient()
        {
            IOrganizationServiceAsync2? orgSvc = null;

            if (_queue.TryDequeue(out ServiceClient? client))
            {
                orgSvc = client;
            }
            else
            {
                orgSvc = _serviceClient.Clone();
            }

            return DataverseServiceClientDispatch<IDataverseClient>.CreateProxy(orgSvc, this);
        }

        public void Dispose()
        {
            foreach (var entry in _queue.ToList())
            {
                entry.Dispose();
            }

            if (_serviceClient != null) _serviceClient.Dispose();
        }

        public void ServiceClientProvider(Func<ConnectionOptions, ServiceClient> provider)
        {
            _serviceClientProvider = provider;
        }

        #endregion

        #region Internal Methods

        internal void AddToPool(IOrganizationServiceAsync2 service)
        {
            if (service != null)
            {
                var client = (ServiceClient)service;

                if (client.IsReady)
                {
                    _queue.Enqueue(client);
                }
            }
        }

        #endregion

        #region Private Implementation

        private ServiceClient InitializeClient(string clientId, string clientSecret, string instanceUrl, bool deferConnection, bool enableAffinityCookie, ILogger logger)
        {
            var options = new ConnectionOptions
            {
                AuthenticationType = AuthenticationType.ClientSecret,
                ClientId = clientId,
                ClientSecret = clientSecret,
                Logger = logger,
                ServiceUri = new Uri(instanceUrl)
            };

            if (_serviceClientProvider != null)
            {
                return _serviceClientProvider(options);
            }

            return new ServiceClient(options, deferConnection: deferConnection)
            {
                EnableAffinityCookie = enableAffinityCookie
            };
        }

        #endregion

    }
}
