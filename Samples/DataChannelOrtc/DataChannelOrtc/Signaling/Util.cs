using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace DataChannelOrtc.Signaling
{
    /// <summary>
    /// The connection state.
    /// </summary>
    public enum State
    {
        NOT_CONNECTED,
        RESOLVING, // Note: State not used
        SIGNING_IN,
        CONNECTED,
        SIGNING_OUT_WAITING, // Note: State not used
        SIGNING_OUT,
    };

    public static class ExtensionMethods
    {
        public static async void WriteStringAsync(this StreamSocket socket, string str)
        {
            try
            {
                var writer = new DataWriter(socket.OutputStream);
                writer.WriteString(str);
                await writer.StoreAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Error] Singnaling: Couldn't write to socket : " + ex.Message);
            }
        }

        public static int ParseLeadingInt(this string str)
        {
            return int.Parse(Regex.Match(str, "\\d+").Value);
        }
    }
}
