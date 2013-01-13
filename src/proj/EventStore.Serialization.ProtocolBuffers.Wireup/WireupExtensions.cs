namespace EventStore
{
    using Serialization;
    using System.Reflection;
    using System;

    public static class WireupExtensions
    {
        public static SerializationWireup UsingProtocolBuffersSerialization(this PersistenceWireup wireup)
        {
            return wireup.UsingCustomSerialization(new ProtocolBufferSerializer());
        }

        public static SerializationWireup UsingProtocolBuffersSerialization(this PersistenceWireup wireup, params string[] contractAssemblyFileNamePatterns)
        {
            return wireup.UsingCustomSerialization(new ProtocolBufferSerializer(contractAssemblyFileNamePatterns));
        }

        public static SerializationWireup UsingProtocolBuffersSerialization(this PersistenceWireup wireup, params Assembly[] contractAssemblies)
        {
            return wireup.UsingCustomSerialization(new ProtocolBufferSerializer(contractAssemblies));
        }

        public static SerializationWireup UsingProtocolBuffersSerialization(this PersistenceWireup wireup, params Type[] dataContracts)
        {
            return wireup.UsingCustomSerialization(new ProtocolBufferSerializer(dataContracts));
        }
    }
}