using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Remoting;

namespace Comm
{
    public interface Incoming : IService
    {
        Task<bool> Message(string apiKey, Message msg);
    }
}
