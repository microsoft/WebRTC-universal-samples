using System;
namespace DataChannelOrtc.Shared
{
    public class Util
    {
        public static string GetLocalPeerName()
        {
            //var hostname = System.Net.NetworkInformation.GetHostNames().FirstOrDefault(h => h.Type == HostNameType.DomainName);
            var hostname = System.Environment.MachineName;
            string ret = hostname ?? "<unknown host>";
            ret = ret + "-dual";
            return ret;
        }
    }
}
