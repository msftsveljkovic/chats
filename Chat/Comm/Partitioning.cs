using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.ServiceFabric.Services.Client;

namespace Comm
{
    public class Partitioning
    {
        public static ServicePartitionKey FromApiKey(string apiKey)
        {
            return new ServicePartitionKey(apiKey.GetHashCode());
        }
    }
}
