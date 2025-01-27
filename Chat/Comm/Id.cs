using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Remoting;

namespace Comm
{
    public interface Id: IService
    {
        Task<List<string>> Who(string apiKey);

        Task<bool> Ping(string apiKey, string user);
    }
}
