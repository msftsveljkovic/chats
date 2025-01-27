using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace User
{
    using Active = IReliableDictionary<string, DateTime>;

    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    internal sealed class User : StatefulService, Comm.Incoming, Comm.Id
    {
        private const int InactivityTimeout_sec = 60;

        public User(StatefulServiceContext context)
            : base(context)
        { }

        public async Task<bool> Message(string apiKey, Comm.Message msg)
        {
            return await Ping(apiKey, msg.User);
        }

        public async Task<List<string>> Who(string apiKey)
        {
            var rslt = new List<string>();
            var toRemove = new List<string>();
            var act = await this.StateManager.GetOrAddAsync<Active>(apiKey);
            using (var tx = this.StateManager.CreateTransaction())
            {
                var johny = await act.CreateEnumerableAsync(tx);
                var walker = johny.GetAsyncEnumerator();
                while (await walker.MoveNextAsync(default))
                {
                    var elapsed = DateTime.Now - walker.Current.Value;
                    if (elapsed.Seconds <= InactivityTimeout_sec)
                    {
                        rslt.Add(walker.Current.Key);
                    }
                    else
                    {
                        toRemove.Add(walker.Current.Key);
                    }
                }
                foreach (var i in toRemove)
                {
                    await act.TryRemoveAsync(tx, i);
                }
                await tx.CommitAsync();
            }
            return rslt;
        }

        public async Task<bool> Ping(string apiKey, string user)
        {
            var act = await this.StateManager.GetOrAddAsync<Active>(apiKey);
            using (var tx = this.StateManager.CreateTransaction())
            {
                await act.AddOrUpdateAsync(tx, user, DateTime.Now, (_, _) => DateTime.Now);
                await tx.CommitAsync();
            }
            return true;
        }

        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            return this.CreateServiceRemotingReplicaListeners();
        }

        /// <summary>
        /// This is the main entry point for your service replica.
        /// This method executes when this replica of your service becomes primary and has write status.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service replica.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            // TODO: Replace the following sample code with your own logic 
            //       or remove this RunAsync override if it's not needed in your service.

            var myDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, long>>("myDictionary");

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using (var tx = this.StateManager.CreateTransaction())
                {
                    var result = await myDictionary.TryGetValueAsync(tx, "Counter");

                    ServiceEventSource.Current.ServiceMessage(this.Context, "Current Counter Value: {0}",
                        result.HasValue ? result.Value.ToString() : "Value does not exist.");

                    await myDictionary.AddOrUpdateAsync(tx, "Counter", 0, (key, value) => ++value);

                    // If an exception is thrown before calling CommitAsync, the transaction aborts, all changes are 
                    // discarded, and nothing is saved to the secondary replicas.
                    await tx.CommitAsync();
                }

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
    }
}
