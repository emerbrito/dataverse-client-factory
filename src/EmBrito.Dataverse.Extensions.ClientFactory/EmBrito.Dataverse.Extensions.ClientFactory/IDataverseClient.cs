using Microsoft.PowerPlatform.Dataverse.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EmBrito.Dataverse.Extensions.ClientFactory
{
    public interface IDataverseClient : IOrganizationServiceAsync2, IDisposable
    {
    }
}
