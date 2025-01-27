using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Comm
{
    [DataContract]
    public class MessageList
    {
        [DataMember]
        public long tstamp {  get; set; }

        [DataMember]
        public List<Message> msgs { get; set; } = new List<Message>();
    }
}
