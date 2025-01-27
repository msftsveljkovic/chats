using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Comm;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace History
{
    using Log = IReliableQueue<Entry>;

    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    internal sealed class History : StatefulService, Comm.Incoming, Comm.Istory
    {
        private const long MaxEntries = 100;

        public History(StatefulServiceContext context)
            : base(context)
        { }

        public async Task<bool> Message(string apiKey, Message msg)
        {
            var log = await this.StateManager.GetOrAddAsync<Log>(apiKey);
            using (var tx = this.StateManager.CreateTransaction())
            {
                while (await log.GetCountAsync(tx) >= MaxEntries)
                {
                    var r = await log.TryDequeueAsync(tx);
                    if (!r.HasValue)
                    {
                        break;
                    }
                }
                Entry entry = new() { ts = DateTime.Now.Ticks, message = msg };
                await log.EnqueueAsync(tx, entry);
                await tx.CommitAsync();
            }
            return true;

        }

        public async Task<MessageList> Read(string apiKey, long fromTS)
        {
            var rslt = new MessageList();
            var log = await this.StateManager.GetOrAddAsync<Log>(apiKey);
            using (var tx = this.StateManager.CreateTransaction())
            {
                var johny = await log.CreateEnumerableAsync(tx);
                var walker = johny.GetAsyncEnumerator();
                while (await walker.MoveNextAsync(default))
                {
                    rslt.tstamp = walker.Current.ts + 1;
                    if (walker.Current.ts >= fromTS)
                    {
                        rslt.msgs.Add(walker.Current.message);
                    }
                }
            }
            return rslt;
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
