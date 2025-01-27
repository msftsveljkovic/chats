using System.Runtime.Serialization;

namespace History
{
    [DataContract]
    public class Entry
    {
        [DataMember]
        public long ts;

        [DataMember]
        public Comm.Message message = new() { User = "", Content = "" };
    }
}
