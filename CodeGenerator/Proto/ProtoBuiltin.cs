using System;

namespace SilentOrbit.ProtocolBuffers
{
    /// <summary>
    /// Representation of the build in data types
    /// </summary>
    class ProtoBuiltin : ProtoType
    {
        #region Const of build in proto types
        public const string Double = "double";
        public const string Float = "float";
        public const string Int32 = "int32";
        public const string Int64 = "int64";
        public const string UInt32 = "uint32";
        public const string UInt64 = "uint64";
        public const string Bool = "bool";
        public const string String = "string";
        public const string Bytes = "bytes";
        #endregion

        public ProtoBuiltin(string name, Wire wire, string csType)
        {
            ProtoName = name;
            wireType = wire;
            base.CsType = csType;
        }

        public override string CsType
        {
            get => base.CsType;
            set => throw new InvalidOperationException();
        }

        public override string CsNamespace => throw new InvalidOperationException();

        public override string FullCsType => CsType;

        readonly Wire wireType;

        public override Wire WireType => wireType;

        public override int WireSize
        {
            get
            {
                if (ProtoName == ProtoBuiltin.Bool)
                    return 1;
                return base.WireSize;
            }
        }
    }
}

