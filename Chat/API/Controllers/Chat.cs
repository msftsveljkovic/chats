using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Actors.Client;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Services.Client;

namespace API.Controllers
{
    [ApiController]
    [Route("v1")]
    public class Chat : ControllerBase
    {
        //Meter meter = new Meter();

        [HttpGet]
        [Route("get")]
        public async Task<Comm.MessageList> Get([FromQuery] string apiKey, [FromQuery] string user, [FromQuery] long fromTS)
        {
            var meter = HttpContext.RequestServices.GetService<Meter>();
            var partition = Comm.Partitioning.FromApiKey(apiKey);
            var istory = ServiceProxy.Create<Comm.Istory>(new Uri("fabric:/Chat/History"), partition);
            var rslt = await istory.Read(apiKey, fromTS);
            var id = ServiceProxy.Create<Comm.Id>(new Uri("fabric:/Chat/User"), partition);
            await id.Ping(apiKey, user);
            _ = meter?.Sample(2);
            return rslt;
        }

        [HttpGet]
        [Route("who")]
        public async Task<List<string>> Who([FromQuery] string apiKey)
        {
            var meter = HttpContext.RequestServices.GetService<Meter>();
            var partition = Comm.Partitioning.FromApiKey(apiKey);
            var proxy = ServiceProxy.Create<Comm.Id>(new Uri("fabric:/Chat/User"), partition);
            var rslt = await proxy.Who(apiKey);
            _ = meter?.Sample(1);
            return rslt;
        }

        [HttpPost]
        [Route("pub")]
        public async Task<bool> Pub([FromQuery] string apiKey, [FromBody] Comm.Message msg)
        {
            var meter = HttpContext.RequestServices.GetService<Meter>();
            var partition = Comm.Partitioning.FromApiKey(apiKey);
            var user = ServiceProxy.Create<Comm.Incoming>(new Uri("fabric:/Chat/User"), partition);
            var result = await user.Message(apiKey, msg);
            var history = ServiceProxy.Create<Comm.Incoming>(new Uri("fabric:/Chat/History"), partition);
            var aid = new ActorId(apiKey);
            var ide = ActorProxy.Create<Ide.Interfaces.IIde>(aid, new Uri("fabric:/Chat/IdeActorService"));
            await ide.Message(msg.Content, new CancellationToken());
            _ = meter?.Sample(3);
            return await history.Message(apiKey, msg) || result;
        }
    }
}
