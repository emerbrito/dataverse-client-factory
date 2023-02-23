using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EmBrito.Dataverse.Extensions.ClientFactory
{
    public class ClientFactoryOptions
    {

        public string ClientId { get; set; } = String.Empty;
        public string ClientSecret { get; set; } = String.Empty;
        public string DataverseInstanceUri { get; set; } = String.Empty;
        public bool DeferConnection { get; set; } = false;
        public bool EnableAffinityCookie { get; set; } = true;
        public ILogger<DataverseClientFactory>? Logger { get; set; }

    }
}
