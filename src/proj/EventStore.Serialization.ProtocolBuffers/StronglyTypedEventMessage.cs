namespace EventStore.Serialization
{
    using System.Linq;
    using System.Collections.Generic;
    using System.Runtime.Serialization;
    using ProtoBuf;

    [DataContract]
    public class StronglyTypedEventMessage<T> : EventMessage
    {
        StronglyTypedEventMessage() { }

        public StronglyTypedEventMessage(Dictionary<string, object> headers, object body)
        {
            DenormalizedDictionary = headers.Select(x => new DictionaryPair(x.Key, x.Value.ToString())).ToList();
            StronglyTypedBody = (T)body;
        }

        [DataMember(Order = 1)]
        public List<DictionaryPair> DenormalizedDictionary { get; set; }


        [DataMember(Order = 2)]
        private T StronglyTypedBody
        {
            get { return (T)base.Body; }
            set { base.Body = value; }
        }

        public EventMessage ToEventMessage()
        {
            EventMessage result = this;
            DenormalizedDictionary.ForEach(pair => result.Headers.Add(pair.Key, pair.Value as object));
            return result;
        }
    }

    [DataContract]
    public class DictionaryPair
    {
        DictionaryPair() { }

        public DictionaryPair(string key, string value)
        {
            Key = key;
            Value = value;
        }

        [DataMember(Order = 1)]
        public string Key { get; set; }
        [DataMember(Order = 2)]
        public string Value { get; set; }

    }
}