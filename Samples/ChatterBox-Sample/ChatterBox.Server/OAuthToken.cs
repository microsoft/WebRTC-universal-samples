using System.Runtime.Serialization;

namespace ChatterBox.Server
{
    [DataContract]
    public class OAuthToken
    {
        [DataMember(Name = "access_token")]
        public string AccessToken { get; set; }

        [DataMember(Name = "expires_in")]
        public double ExpireTime { get; set; }

        [DataMember(Name = "token_type")]
        public string TokenType { get; set; }
    }
}