namespace EventStore.Serialization
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Serialization;
    using System.Security.Cryptography;
    using System.Text;
    using ProtoBuf;
    using ProtoBuf.Meta;

    public class ProtocolBufferSerializer : ISerialize
    {
        private const int GuidLengthInBytes = 16;
        private static readonly MD5 TypeHasher = MD5.Create(); // creates 16-byte hashes
        private readonly Dictionary<Guid, Type> hashes = new Dictionary<Guid, Type>();
        private readonly Dictionary<Type, Guid> types = new Dictionary<Type, Guid>();
        private readonly Dictionary<Type, Func<Stream, object>> deserializers = new Dictionary<Type, Func<Stream, object>>();

        public ProtocolBufferSerializer(params string[] contractAssemblyFileNamePatterns)
            : this(contractAssemblyFileNamePatterns.LoadAssemblies())
        {
        }
        public ProtocolBufferSerializer(params Assembly[] contractAssemblies)
            : this(contractAssemblies.SelectMany(assembly => assembly.GetTypes().Where(x => x.GetCustomAttributes(false).Where(y => y.GetType().Name == "DataContractAttribute" || y.GetType().Name == "ProtoContractAttribute").Count() > 0)).ToArray())
        {
        }
        public ProtocolBufferSerializer(params Type[] dataContracts)
            : this()
        {
            foreach (var contract in dataContracts ?? new Type[] { })
            {
                RegisterContract(contract);
            }
        }
        public ProtocolBufferSerializer()
        {
            RegisterCommonTypes();
        }

        private void RegisterCommonTypes()
        {
            RegisterContract(typeof(Commit));
            //RegisterContract(typeof(EventMessage), typeof(StronglyTypedEventMessage<dynamic>));
            RegisterContract(typeof(Dictionary<string, object>));
            //RegisterContract(typeof(Dictionary<string, string>));
            //RegisterContract(typeof(List<object>));
            RegisterContract(typeof(List<EventMessage>), typeof(List<StronglyTypedEventMessage<dynamic>>));

            RegisterContract(typeof(Exception));
            RegisterContract(typeof(SerializationException));
        }

        public void RegisterContract(Type contract)
        {
            RegisterContract(contract, contract);
        }

        public void RegisterContract(Type contract, Type serializationDtoContract)
        {
            if (!this.CanRegisterContract(contract))
                return;

            RegisterHash(contract);
            RegisterDeserializer(contract, serializationDtoContract);
            RegisterToRuntimeModel(contract);
        }

        private void RegisterToRuntimeModel(Type contract)
        {
            if (CanRegisterContractToRuntimeModel(contract))
            {
                var hash = types[contract];
                RuntimeTypeModel.Default[typeof(object)].AddSubType(Math.Abs(hash.GetHashCode() / 4), contract);
            }
        }

        private bool CanRegisterContractToRuntimeModel(Type contract)
        {
            return !(typeof(System.Collections.IEnumerable).IsAssignableFrom(contract)
                || (typeof(string) == contract));
        }

        private bool CanRegisterContract(Type contract)
        {
            return contract != null
                && !types.ContainsKey(contract)
                && !string.IsNullOrEmpty(contract.FullName);
        }
        private void RegisterHash(Type contract)
        {
            var bytes = Encoding.Unicode.GetBytes(contract.FullName ?? string.Empty);
            var hash = new Guid(TypeHasher.ComputeHash(bytes));
            hashes[hash] = contract;
            types[contract] = hash;
        }

        private void RegisterDeserializer(Type contract, Type serializationDtoContract)
        {
            // TODO: make this faster by using reflection to create a delegate and then invoking the delegate
            var deserialize = typeof(Serializer).GetMethod("Deserialize").MakeGenericMethod(serializationDtoContract);
            deserializers[contract] = stream => deserialize.Invoke(null, new object[] { stream });
        }

        private void RegisterDeserializer(Type contract)
        {
            RegisterDeserializer(contract, contract);
        }

        public virtual void Serialize(Stream output, object graph)
        {
            if (null == graph)
                return;

            var contract = graph.GetType();
            WriteContractTypeToStream(output, contract);

            if (graph is List<EventMessage>)
            {
                var graphDto = ((List<EventMessage>)graph).Select(x => (EventMessage)Activator.CreateInstance(BuildSerializationWrapperType(x.Body.GetType()), new object[] { x.Headers, x.Body })).ToList();
                Serializer.Serialize(output, graphDto);
            }
            else
                Serializer.Serialize(output, graph);
        }

        public Type BuildSerializationWrapperType(Type contract)
        {
            var wrapperType = typeof(StronglyTypedEventMessage<>);
            Type[] typeArgs = { contract };
            return wrapperType.MakeGenericType(typeArgs);
        }

        private void WriteContractTypeToStream(Stream output, Type contract)
        {
            Guid hash;
            if (!types.TryGetValue(contract, out hash))
                throw new SerializationException(ExceptionMessages.UnableToSerialize.FormatWith(contract));

            var header = hash.ToByteArray();
            output.Write(header, 0, GuidLengthInBytes);
        }

        public virtual object Deserialize(Stream input)
        {
            if (input == null)
                return null;

            var contractType = ReadContractType(input);
            var deserialized = deserializers[contractType](input);
            if (deserialized is List<StronglyTypedEventMessage<dynamic>>)
                return ((List<StronglyTypedEventMessage<dynamic>>)deserialized).ConvertAll(x => x.ToEventMessage()).ToList();
            else
                return deserialized;
        }
        private Type ReadContractType(Stream serialized)
        {
            var header = new byte[GuidLengthInBytes];
            serialized.Read(header, 0, header.Length);
            var hash = new Guid(header);

            Type contract;
            if (!hashes.TryGetValue(hash, out contract))
                throw new SerializationException(ExceptionMessages.UnableToDeserialize.FormatWith(hash));

            return contract;
        }

        public T Deserialize<T>(Stream input)
        {
            return (T)Deserialize(input);
        }

        public void Serialize<T>(Stream output, T graph)
        {
            Serialize(output, (object)graph);
        }
    }
}