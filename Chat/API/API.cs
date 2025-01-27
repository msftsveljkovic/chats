using System;
using System.Collections.Generic;
using System.Fabric;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ServiceFabric.Services.Communication.AspNetCore;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Microsoft.ServiceFabric.Data;
using System.Fabric.Description;
using System.Collections.ObjectModel;
using System.Timers;

namespace API
{
    /// <summary>
    /// The FabricRuntime creates an instance of this class for each service type instance.
    /// </summary>
    internal sealed class API : StatelessService
    {
        System.Timers.Timer timer;
        Meter meter = new Meter { };

        public API(StatelessServiceContext context)
            : base(context)
        { }

        /// <summary>
        /// Optional override to create listeners (like tcp, http) for this service instance.
        /// </summary>
        /// <returns>The collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return new ServiceInstanceListener[]
            {
                new ServiceInstanceListener(serviceContext =>
                    new KestrelCommunicationListener(serviceContext, "ServiceEndpoint", (url, listener) =>
                    {
                        ServiceEventSource.Current.ServiceMessage(serviceContext, $"Starting Kestrel on {url}");

                        var builder = WebApplication.CreateBuilder();

                        builder.Services.AddSingleton<StatelessServiceContext>(serviceContext);
                        builder.WebHost
                                    .UseKestrel(opt =>
                                    {
                                        int port = serviceContext.CodePackageActivationContext.GetEndpoint("ServiceEndpoint").Port;
                                        opt.Listen(IPAddress.IPv6Any, port, listenOptions =>
                                        {
                                            //listenOptions.UseHttps(GetCertificateFromStore());
                                        });
                                    })
                                    .UseContentRoot(Directory.GetCurrentDirectory())
                                    .UseServiceFabricIntegration(listener, ServiceFabricIntegrationOptions.None)
                                    .UseUrls(url);
                        builder.Services.AddSingleton(meter);
                        builder.Services.AddControllers();
                        builder.Services.AddEndpointsApiExplorer();
                        builder.Services.AddSwaggerGen();
                        var app = builder.Build();
                        if (app.Environment.IsDevelopment())
                        {
                        app.UseSwagger();
                        app.UseSwaggerUI();
                        }
                        //app.UseHttpsRedirection();
                        //app.UseAuthorization();
                        app.MapControllers();
                        return app;

                    }))
            };
        }

        /// <summary>
        /// Finds the ASP .NET Core HTTPS development certificate in development environment. Update this method to use the appropriate certificate for production environment.
        /// </summary>
        /// <returns>Returns the ASP .NET Core HTTPS development certificate</returns>
        private static X509Certificate2 GetCertificateFromStore()
        {
            string aspNetCoreEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (string.Equals(aspNetCoreEnvironment, "Development", StringComparison.OrdinalIgnoreCase))
            {
                const string aspNetHttpsOid = "1.3.6.1.4.1.311.84.1.1";
                const string CNName = "CN=localhost";
                using (X509Store store = new X509Store(StoreName.My, StoreLocation.LocalMachine))
                {
                    store.Open(OpenFlags.ReadOnly);
                    var certCollection = store.Certificates;
                    var currentCerts = certCollection.Find(X509FindType.FindByExtension, aspNetHttpsOid, true);
                    currentCerts = currentCerts.Find(X509FindType.FindByIssuerDistinguishedName, CNName, true);
                    return currentCerts.Count == 0 ? null : currentCerts[0];
                }
            }
            else
            {
                throw new NotImplementedException("GetCertificateFromStore should be updated to retrieve the certificate for non Development environment");
            }
        }

        protected override async Task OnOpenAsync(CancellationToken cancellationToken)
        {
            await DefineDescription();
            timer = new System.Timers.Timer(30000);
            timer.Elapsed += OnTimer;
            timer.AutoReset = true;
            timer.Start();

            //ServiceEventSource.Current.ServiceMessage(this.Context, $"{this.Context.NodeContext.NodeName} opened with service: YambGame");
            await base.OnOpenAsync(cancellationToken);
        }

        private async Task DefineDescription()
        {
            FabricClient fabricClient = new FabricClient();
            StatelessServiceUpdateDescription updateDescription = new StatelessServiceUpdateDescription();

            AddMetrics(updateDescription);
            AddAutoscaling(updateDescription);

            await fabricClient.ServiceManager.UpdateServiceAsync(Context.ServiceName, updateDescription);
        }

        private void AddMetrics(StatelessServiceUpdateDescription updateDescription)
        {
            var userLoadMetric = new StatelessServiceLoadMetricDescription
            {
                Name = "ApiLoad",
                DefaultLoad = 0,
                Weight = ServiceLoadMetricWeight.High
            };
            updateDescription.Metrics = new CustomMetrics();
            updateDescription.Metrics.Add(userLoadMetric);
        }

        private void AddAutoscaling(StatelessServiceUpdateDescription updateDescription)
        {
            PartitionInstanceCountScaleMechanism scaleMechanism = new PartitionInstanceCountScaleMechanism
            {
                MinInstanceCount = 1,
                MaxInstanceCount = 5,
                ScaleIncrement = 1
            };

            AveragePartitionLoadScalingTrigger trigger = new AveragePartitionLoadScalingTrigger
            {
                MetricName = "ApiLoad",
                LowerLoadThreshold = 10,
                UpperLoadThreshold = 20,
                ScaleInterval = TimeSpan.FromSeconds(30)
            };

            ScalingPolicyDescription scalingPolicyDescription = new ScalingPolicyDescription(scaleMechanism, trigger);

            updateDescription.ScalingPolicies ??= new List<ScalingPolicyDescription>();
            updateDescription.ScalingPolicies.Add(scalingPolicyDescription);
        }
        public void ReportMetric(string name, int val)
        {
            var loadMetrics = new List<LoadMetric>
            {
                new LoadMetric(name, val)
            };

            //ServiceEventSource.Current.ServiceMessage(this.Context, $"Reported custom metric: {name} with value {val}");
            Partition.ReportLoad(loadMetrics);
        }

        private void OnTimer(Object source, ElapsedEventArgs e)
        {
            ReportMetric("ApiLoad", meter.Refresh());
        }
    }
}
