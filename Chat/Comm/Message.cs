using System;
using System.Runtime.Serialization;

namespace Comm
{
    [DataContract]
    public class Message
    {
        [DataMember]
        public string User { get; set; } = string.Empty;

        [DataMember]
        public string Content { get; set; } = string.Empty;
    }
}
