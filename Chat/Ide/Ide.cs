using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Actors.Client;
using Ide.Interfaces;
using Microsoft.ServiceFabric.Services.Remoting.Client;

namespace Ide
{
    [StatePersistence(StatePersistence.Persisted)]
    internal class Ide : Actor, IIde
    {
        private const string state = "zlonemisli";
        private IActorTimer ?tempo;
        private List<string> mile = new List<string> {
            "Иде Милее", "Лајковачком пруугом", "Лајковачком пруугом", "Идее Милеее",
            "Иде Милее", "са још једним другом", "са још једним друугом", "Иде Милеее",
            "суво сено коошено", "текла река кроз село", "а по реци риба плови", "нема кој да лови",
            "Иде Милее", "гори му цигаара", "гори му цигаара", "Иде Милеее",
            "ја познајем", "мојега друугара", "моје друугара", "ја поознајем",
            "суво сено коошено", "текла река кроз село", "а по реци риба плови", "нема кој да лови",
            "Немој Мииле", "да остављаш друуга", "да остављаш друуга", "немој Миилее",
            "дугаачка јее", "Лајковачка прууга", "Лајковачка прууга", "дугаачка јеее",
            "суво сено коошено", "текла река кроз село", "а по реци риба плоови", "нема коој да лооовии",
        };
        private int index;
        private int drama;
        private Microsoft.ServiceFabric.Services.Client.ServicePartitionKey ?partition;

        /// <summary>
        /// Initializes a new instance of Ide
        /// </summary>
        /// <param name="actorService">The Microsoft.ServiceFabric.Actors.Runtime.ActorService that will host this actor instance.</param>
        /// <param name="actorId">The Microsoft.ServiceFabric.Actors.ActorId for this actor instance.</param>
        public Ide(ActorService actorService, ActorId actorId) 
            : base(actorService, actorId)
        {
        }

        private async Task<Task> Pjevaj(object o)
        {
            // just because I'm paranoid, doesn't mean nobody's following me
            if (!await this.StateManager.GetStateAsync<bool>(state))
            {
                return Task.CompletedTask;
            }

            if ((index + 1) % 4 == 0)
            {
                if (++drama < 2)
                {
                    return Task.CompletedTask;
                }
                drama = 0;
            }
            var x = mile[index];
            if (++index == mile.Count) {
                index = 0;
                await this.StateManager.SetStateAsync(state, false);
                await this.StateManager.SaveStateAsync();
            }

            var user = ServiceProxy.Create<Comm.Incoming>(new Uri("fabric:/Chat/User"), partition);
            // TODO Microsoft.ServiceFabric.Client.ServicePartitionKey()
            var message = new Comm.Message { User = "Mile", Content = x };
            var apiKey = this.GetActorId<Ide>().GetStringId();
            await user.Message(apiKey, message);
            var history = ServiceProxy.Create<Comm.Incoming>(new Uri("fabric:/Chat/History"), partition);
            await history.Message(apiKey, message);

            return Task.CompletedTask;
        }
        public async Task<bool> Message(string message, CancellationToken cancellationToken)
        {
            var aid = this.GetActorId<Ide>();
            partition = Comm.Partitioning.FromApiKey(aid.GetStringId()); // TODO treba mu pravi apikey
            
            bool on = await this.StateManager.GetStateAsync<bool>(state, cancellationToken);
            if (!on && message.Contains("@mile"))
            {
                tempo = RegisterTimer(Pjevaj, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3));
                index = drama = 0;
                on = true;
            }
            if (on && message.Contains("@zasvirajpaizapojaszadeni"))
            {
                if (tempo != null)
                {
                    UnregisterTimer(tempo);
                    tempo = null;
                }
                on = false;
            }
            await this.StateManager.SetStateAsync(state, on);
            await this.StateManager.SaveStateAsync(cancellationToken);
            return on;
        }

        /// <summary>
        /// This method is called whenever an actor is activated.
        /// An actor is activated the first time any of its methods are invoked.
        /// </summary>
        protected override Task OnActivateAsync()
        {
            ActorEventSource.Current.ActorMessage(this, "Actor activated.");

            return this.StateManager.TryAddStateAsync(state, false);
        }

    }
}
