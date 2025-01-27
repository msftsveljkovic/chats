using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Remoting;

namespace Comm
{
    public interface Istory : IService
    {
        Task<MessageList> Read(string apiKey, long fromTS);
    }
}
