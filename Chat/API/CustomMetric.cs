using System.Collections.ObjectModel;
using System.Fabric.Description;

namespace API
{
    public class CustomMetrics : KeyedCollection<string, ServiceLoadMetricDescription>
    {
        protected override string GetKeyForItem(ServiceLoadMetricDescription item)
        {
            return item.Name;
        }
    }
}