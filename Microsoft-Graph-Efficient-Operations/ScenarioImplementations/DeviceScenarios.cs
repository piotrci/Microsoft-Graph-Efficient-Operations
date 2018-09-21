using EfficientRequestHandling;
using EfficientRequestHandling.RequestBuilders;
using EfficientRequestHandling.RequestManagement;
using EfficientRequestHandling.ResponseHandlers;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScenarioImplementations
{
    public class DeviceScenarios
    {
        public static string GetDeviceReport(RequestManager requestManager)
        {
            IEnumerable<Device> devices;
            using (var builder = GraphRequestBuilder<Device>.GetBuilder<DeviceCollectionResponseHandler>(requestManager, out devices))
            {
                builder.Devices.Request().Select("operatingSystem, isManaged, isCompliant").Top(999).GetAsync().Wait();
                //foreach (var filter in GenericHelpers.GenerateFilterRangesForAlphaNumProperties("userPrincipalName"))
                //{
                //    builder.Users.Request().Top(999).Filter(filter).GetAsync().Wait();
                //}
            }
            var report = devices.GroupBy(d => $"OS: {d.OperatingSystem ?? "null"}, isManaged: {d.IsManaged ?? false}, isCompliant: {d.IsCompliant ?? false}")
                .Select(g => new { DeviceCategory = g.Key, DeviceCount = g.Count() });

            StringBuilder sb = new StringBuilder();

            foreach (var line in report.OrderByDescending(l => l.DeviceCount))
            {
                sb.AppendLine($"{line.DeviceCategory} - Count: {line.DeviceCount}");
            }
            return sb.ToString();
        }
    }
}
