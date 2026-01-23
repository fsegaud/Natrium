using System;
using System.IO;

namespace Hasm
{
    [ProtoBuf.ProtoContract]
    public class Program
    {
        [ProtoBuf.ProtoMember(1)]
        public BuildConfig BuildConfig;
        [ProtoBuf.ProtoMember(2)]
        public uint RequiredRegisters { get; internal set; }
        [ProtoBuf.ProtoMember(3)]
        public uint RequiredStack { get; internal set; }
        [ProtoBuf.ProtoMember(4)]
        public uint RequiredDevices { get; internal set; }
        
        [ProtoBuf.ProtoMember(5)]
        internal Instruction[] Instructions = Array.Empty<Instruction>();
        
        public string ToBase64()
        {
            if (Instructions == null)
                return string.Empty;

            using MemoryStream ms = new MemoryStream();
            ProtoBuf.Serializer.Serialize(ms, this);
            return Convert.ToBase64String(ms.GetBuffer(), 0, (int)ms.Length);
        }

        public void FromBase64(string base64)
        {
            if (string.IsNullOrEmpty(base64))
                return;    
            
            byte[] bytes = Convert.FromBase64String(base64);
            using MemoryStream ms = new MemoryStream(bytes);
            ProtoBuf.Serializer.Deserialize(ms, this);
        }
    }
}
