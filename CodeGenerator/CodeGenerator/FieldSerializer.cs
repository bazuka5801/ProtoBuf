using System;
using System.Collections.Generic;
using System.Linq;
using SilentOrbit.Code;

namespace SilentOrbit.ProtocolBuffers
{
    class FieldSerializer
    {
        readonly CodeWriter cw;
        readonly Options options;

        public FieldSerializer(CodeWriter cw, Options options)
        {
            this.cw = cw;
            this.options = options;
        }

        #region Reader

        /// <summary>
        /// Return true for normal code and false if generated thrown exception.
        /// In the latter case a break is not needed to be generated afterwards.
        /// </summary>
        public bool FieldReader(Field f, ProtoMessage[] messageTables)
        {
            if (f.Rule == FieldRule.Repeated)
            {
                //Make sure we are not reading a list of interfaces
                if (f.ProtoType.OptionType == "interface" && !messageTables.Contains(f.ProtoType))
                {
                    cw.WriteLine("throw new NotSupportedException(\"Can't deserialize a list of interfaces\");");
                    return false;
                }

                if (f.OptionPacked == true)
                {
                    cw.Comment("repeated packed");
                    cw.WriteLine("long end" + f.ID + " = global::SilentOrbit.ProtocolBuffers.ProtocolParser.ReadUInt32(stream);");
                    cw.WriteLine("end" + f.ID + " += stream.Position;");
                    cw.WhileBracket("stream.Position < end" + f.ID);
                    cw.WriteLine("instance." + f.CsName + ".Add(" + FieldReaderType(f, messageTables, "stream", "br", null) + ");");
                    cw.EndBracket();

                    cw.WriteLine("if (stream.Position != end" + f.ID + ")");
                    cw.WriteIndent("throw new global::SilentOrbit.ProtocolBuffers.ProtocolBufferException(\"Read too many bytes in packed data\");");
                }
                else
                {
                    cw.Comment("repeated");
                    cw.WriteLine("instance." + f.CsName + ".Add(" + FieldReaderType(f, messageTables, "stream", "br", null) + ");");
                }
            }
            else
            {
                if (f.OptionReadOnly)
                {
                    //The only "readonly" fields we can modify
                    //We could possibly support bytes primitive too but it would require the incoming length to match the wire length
                    if (f.ProtoType is ProtoMessage)
                    {
                        cw.WriteLine(FieldReaderType(f, messageTables, "stream", "br", "instance." + f.CsName) + ";");
                        return true;
                    }
                    cw.WriteLine("throw new InvalidOperationException(\"Can't deserialize into a readonly primitive field\");");
                    return false;
                }

                if (f.ProtoType is ProtoMessage)
                {
                    if ( f.ProtoType.OptionType == "struct")
                    {
                        if ( f.OptionUseReferences )
                        {
                            cw.WriteLine( FieldReaderType( f, messageTables, "stream", "br", "ref instance." + f.CsName ) + ";" );
                        }
                        else
                        {
                            cw.WriteLine( "{" );
                            cw.WriteIndent( "var a = instance." + f.CsName + ";" );
                            cw.WriteIndent( "instance." + f.CsName + " = " + FieldReaderType( f, messageTables, "stream", "br", "ref a" ) + ";" );
                            cw.WriteLine( "}" );
                        }
                        
                        return true;
                    }

                    cw.WriteLine("if (instance." + f.CsName + " == null)");
                    if (f.ProtoType.OptionType == "interface" && !messageTables.Contains(f.ProtoType))
                        cw.WriteIndent("throw new InvalidOperationException(\"Can't deserialize into a interfaces null pointer\");");
                    else
                        cw.WriteIndent("instance." + f.CsName + " = " + FieldReaderType(f, messageTables, "stream", "br", null) + ";");
                    cw.WriteLine("else");
                    cw.WriteIndent(FieldReaderType(f, messageTables, "stream", "br", "instance." + f.CsName) + ";");
                    return true;
                }

                cw.WriteLine("instance." + f.CsName + " = " + FieldReaderType(f, messageTables, "stream", "br", "instance." + f.CsName) + ";");
            }
            return true;
        }

        public bool JsonFieldReader(Field f, ProtoMessage[] messageTables, string jToken)
        {
            if (f.Rule == FieldRule.Repeated) {
                //Make sure we are not reading a list of interfaces
                if (f.ProtoType.OptionType == "interface" && !messageTables.Contains(f.ProtoType)) {
                    cw.WriteLine("throw new NotSupportedException(\"Can't deserialize a list of interfaces\");");
                    return false;
                }
                
                cw.Comment("repeated");
                cw.ForeachBracket("var val in ((global::Newtonsoft.Json.Linq.JArray) " + jToken + ")");
                cw.WriteLine("instance." + f.CsName + ".Add(" + JsonFieldReaderType(f, messageTables, "val", null) + ");");
                cw.EndBracket();
            } else {
                if (f.OptionReadOnly) {
                    //The only "readonly" fields we can modify
                    //We could possibly support bytes primitive too but it would require the incoming length to match the wire length
                    if (f.ProtoType is ProtoMessage) {
                        cw.WriteLine(JsonFieldReaderType(f, messageTables, jToken, "instance." + f.CsName) + ";");
                        return true;
                    }
                    cw.WriteLine("throw new InvalidOperationException(\"Can't deserialize into a readonly primitive field\");");
                    return false;
                }

                if (f.ProtoType is ProtoMessage) {
                    if (f.ProtoType.OptionType == "struct") {
                        if (f.OptionUseReferences) {
                            cw.WriteLine(JsonFieldReaderType(f, messageTables, jToken, "ref instance." + f.CsName) + ";");
                        } else {
                            cw.WriteLine("{");
                            cw.WriteIndent("var a = instance." + f.CsName + ";");
                            cw.WriteIndent("instance." + f.CsName + " = " + JsonFieldReaderType(f, messageTables, jToken, "ref a") + ";");
                            cw.WriteLine("}");
                        }

                        return true;
                    }

                    cw.WriteLine("if (instance." + f.CsName + " == null)");
                    if (f.ProtoType.OptionType == "interface" && !messageTables.Contains(f.ProtoType))
                        cw.WriteIndent("throw new InvalidOperationException(\"Can't deserialize into a interfaces null pointer\");");
                    else
                        cw.WriteIndent("instance." + f.CsName + " = " + JsonFieldReaderType(f, messageTables, jToken, null) + ";");
                    cw.WriteLine("else");
                    cw.WriteIndent(JsonFieldReaderType(f, messageTables, jToken, "instance." + f.CsName) + ";");
                    return true;
                }

                cw.WriteLine("instance." + f.CsName + " = " + JsonFieldReaderType(f, messageTables, jToken, "instance." + f.CsName) + ";");
            }
            return true;
        }

        /// <summary>
        /// Read a primitive from the stream
        /// </summary>
        static string FieldReaderType(Field f, ProtoMessage[] messageTables, string stream, string binaryReader, string instance)
        {
            if (f.OptionCodeType != null)
            {
                switch (f.OptionCodeType)
                {
                    case "DateTime":
                        switch (f.ProtoType.ProtoName)
                        {
                            case ProtoBuiltin.UInt64:
                            case ProtoBuiltin.Int64:
                            case ProtoBuiltin.Fixed64:
                            case ProtoBuiltin.SFixed64:
                                return "new DateTime((long)" + FieldReaderPrimitive(f, messageTables, stream, binaryReader, instance) + ")";
                        }
                        throw new ProtoFormatException("Local feature, DateTime, must be stored in a 64 bit field", f.Source);

                    case "TimeSpan":
                        switch (f.ProtoType.ProtoName)
                        {
                            case ProtoBuiltin.UInt64:
                            case ProtoBuiltin.Int64:
                            case ProtoBuiltin.Fixed64:
                            case ProtoBuiltin.SFixed64:
                                return "new TimeSpan((long)" + FieldReaderPrimitive(f, messageTables, stream, binaryReader, instance) + ")";
                        }
                        throw new ProtoFormatException("Local feature, TimeSpan, must be stored in a 64 bit field", f.Source);

                    default:
                        //Assume enum
                        return "(" + f.OptionCodeType + ")" + FieldReaderPrimitive(f, messageTables, stream, binaryReader, instance);
                }
            }

            return FieldReaderPrimitive(f, messageTables, stream, binaryReader, instance);
        }

        static string JsonFieldReaderType(Field f, ProtoMessage[] messageTables, string jToken, string instance)
        {
            if (f.OptionCodeType != null) {
                switch (f.OptionCodeType) {
                    case "DateTime":
                        switch (f.ProtoType.ProtoName) {
                            case ProtoBuiltin.UInt64:
                            case ProtoBuiltin.Int64:
                            case ProtoBuiltin.Fixed64:
                            case ProtoBuiltin.SFixed64:
                                return "new DateTime((long)" + JsonFieldReaderPrimitive(f, messageTables, jToken, instance) + ")";
                        }
                        throw new ProtoFormatException("Local feature, DateTime, must be stored in a 64 bit field", f.Source);

                    case "TimeSpan":
                        switch (f.ProtoType.ProtoName) {
                            case ProtoBuiltin.UInt64:
                            case ProtoBuiltin.Int64:
                            case ProtoBuiltin.Fixed64:
                            case ProtoBuiltin.SFixed64:
                                return "new TimeSpan((long)" + JsonFieldReaderPrimitive(f, messageTables, jToken, instance) + ")";
                        }
                        throw new ProtoFormatException("Local feature, TimeSpan, must be stored in a 64 bit field", f.Source);

                    default:
                        //Assume enum
                        return "(" + f.OptionCodeType + ")" + JsonFieldReaderPrimitive(f, messageTables, jToken, instance);
                }
            }

            return JsonFieldReaderPrimitive(f, messageTables, jToken, instance);
        }

        static string FieldReaderPrimitive(Field f, ProtoMessage[] messageTables, string stream, string binaryReader, string instance)
        {
            if (f.ProtoType is ProtoMessage)
            {
                var m = (ProtoMessage) f.ProtoType;
                var messageTableParams = "";
                if (m.RequiredMessageTables.Any()) {
                    messageTableParams = string.Join("", m.RequiredMessageTables.Select(x => ", mTable" + Array.IndexOf(messageTables, x)));
                }

                if (f.ProtoType.OptionType == "interface" && messageTables.Contains(f.ProtoType)) {
                    var index = Array.IndexOf(messageTables, (ProtoMessage) f.ProtoType);
                    return "mTable" + index + ".Deserialize(" + stream + ")";
                }

                if (f.Rule == FieldRule.Repeated || instance == null) {
                    return m.FullSerializerType + ".DeserializeLengthDelimited(" + stream + messageTableParams + ")";
                } else {
                    return m.FullSerializerType + ".DeserializeLengthDelimited(" + stream + messageTableParams +  ", " + instance + ")";
                }
            }

            if (f.ProtoType is ProtoEnum)
                return "(" + f.ProtoType.FullCsType + ")global::SilentOrbit.ProtocolBuffers.ProtocolParser.ReadUInt64(" + stream + ")";

            if (f.ProtoType is ProtoBuiltin)
            {
                switch (f.ProtoType.ProtoName)
                {
                    case ProtoBuiltin.Double:
                        return binaryReader + ".ReadDouble()";
                    case ProtoBuiltin.Float:
                        return binaryReader + ".ReadSingle()";
                    case ProtoBuiltin.Int32: //Wire format is 64 bit varint
                        return "(int)global::SilentOrbit.ProtocolBuffers.ProtocolParser.ReadUInt64(" + stream + ")";
                    case ProtoBuiltin.Int64:
                        return "(long)global::SilentOrbit.ProtocolBuffers.ProtocolParser.ReadUInt64(" + stream + ")";
                    case ProtoBuiltin.UInt32:
                        return "global::SilentOrbit.ProtocolBuffers.ProtocolParser.ReadUInt32(" + stream + ")";
                    case ProtoBuiltin.UInt64:
                        return "global::SilentOrbit.ProtocolBuffers.ProtocolParser.ReadUInt64(" + stream + ")";
                    case ProtoBuiltin.SInt32:
                        return "global::SilentOrbit.ProtocolBuffers.ProtocolParser.ReadZInt32(" + stream + ")";
                    case ProtoBuiltin.SInt64:
                        return "global::SilentOrbit.ProtocolBuffers.ProtocolParser.ReadZInt64(" + stream + ")";
                    case ProtoBuiltin.Fixed32:
                        return binaryReader + ".ReadUInt32()";
                    case ProtoBuiltin.Fixed64:
                        return binaryReader + ".ReadUInt64()";
                    case ProtoBuiltin.SFixed32:
                        return binaryReader + ".ReadInt32()";
                    case ProtoBuiltin.SFixed64:
                        return binaryReader + ".ReadInt64()";
                    case ProtoBuiltin.Bool:
                        return "global::SilentOrbit.ProtocolBuffers.ProtocolParser.ReadBool(" + stream + ")";
                    case ProtoBuiltin.String:
                        return "global::SilentOrbit.ProtocolBuffers.ProtocolParser.ReadString(" + stream + ")";
                    case ProtoBuiltin.Bytes:
                        return "global::SilentOrbit.ProtocolBuffers.ProtocolParser.ReadBytes(" + stream + ")";
                    default:
                        throw new ProtoFormatException("unknown build in: " + f.ProtoType.ProtoName, f.Source);
                }

            }

            throw new NotImplementedException();
        }

        static string JsonFieldReaderPrimitive(Field f, ProtoMessage[] messageTables, string jToken, string instance)
        {
            if (f.ProtoType is ProtoMessage) {
                var m = (ProtoMessage) f.ProtoType;
                var messageTableParams = "";
                if (m.RequiredMessageTables.Any()) {
                    messageTableParams = string.Join("", m.RequiredMessageTables.Select(x => ", mTable" + Array.IndexOf(messageTables, x)));
                }

                if (f.ProtoType.OptionType == "interface" && messageTables.Contains(f.ProtoType)) {
                    var index = Array.IndexOf(messageTables, (ProtoMessage) f.ProtoType);
                    return "mTable" + index + ".Deserialize((global::Newtonsoft.Json.Linq.JObject) (" + jToken + "))";
                }

                if (f.Rule == FieldRule.Repeated || instance == null) {
                    return m.FullSerializerType + ".Deserialize((global::Newtonsoft.Json.Linq.JObject) (" + jToken +
                           ")" + messageTableParams + ")";
                } else {
                    return m.FullSerializerType + ".Deserialize((global::Newtonsoft.Json.Linq.JObject) (" + jToken +
                           ")" + messageTableParams + ", " + instance + ")";
                }
            }

            if (f.ProtoType is ProtoEnum)
                return "(" + f.ProtoType.FullCsType + ") (ulong) (" + jToken + ")";

            if (f.ProtoType is ProtoBuiltin) {
                switch (f.ProtoType.ProtoName) {
                    case ProtoBuiltin.Double:
                        return "(double) (" + jToken + ")";
                    case ProtoBuiltin.Float:
                        return "(float) (" + jToken + ")";
                    case ProtoBuiltin.Int32: //Wire format is 64 bit varint
                        return "(int) (" + jToken + ")";
                    case ProtoBuiltin.Int64:
                        return "(long) (" + jToken + ")";
                    case ProtoBuiltin.UInt32:
                        return "(uint) (" + jToken + ")";
                    case ProtoBuiltin.UInt64:
                        return "(ulong) (" + jToken + ")";
                    case ProtoBuiltin.SInt32:
                        return "(int) (" + jToken + ")";
                    case ProtoBuiltin.SInt64:
                        return "(long) (" + jToken + ")";
                    case ProtoBuiltin.Fixed32:
                        return "(uint) (" + jToken + ")";
                    case ProtoBuiltin.Fixed64:
                        return "(ulong) (" + jToken + ")";
                    case ProtoBuiltin.SFixed32:
                        return "(int) (" + jToken + ")";
                    case ProtoBuiltin.SFixed64:
                        return "(long) (" + jToken + ")";
                    case ProtoBuiltin.Bool:
                        return "(bool) (" + jToken + ")";
                    case ProtoBuiltin.String:
                        return "(string) (" + jToken + ")";
                    case ProtoBuiltin.Bytes:
                        return "global::System.Convert.FromBase64String((string) (" + jToken + "))";
                    default:
                        throw new ProtoFormatException("unknown build in: " + f.ProtoType.ProtoName, f.Source);
                }

            }

            throw new NotImplementedException();
        }

        #endregion

        #region Writer

        static void KeyWriter(string stream, int id, Wire wire, CodeWriter cw)
        {
            uint n = ((uint)id << 3) | ((uint)wire);
            cw.Comment("Key for field: " + id + ", " + wire);
            //cw.WriteLine("ProtocolParser.WriteUInt32(" + stream + ", " + n + ");");
            VarintWriter(stream, n, cw);
        }

        /// <summary>
        /// Generates writer for a varint value known at compile time
        /// </summary>
        static void VarintWriter(string stream, uint value, CodeWriter cw)
        {
            while (true)
            {
                byte b = (byte)(value & 0x7F);
                value = value >> 7;
                if (value == 0)
                {
                    cw.WriteLine(stream + ".WriteByte(" + b + ");");
                    break;
                }

                //Write part of value
                b |= 0x80;
                cw.WriteLine(stream + ".WriteByte(" + b + ");");
            }
        }

        /// <summary>
        /// Generates inline writer of a length delimited byte array
        /// </summary>
        static void BytesWriter(Field f, string stream, CodeWriter cw)
        {
            cw.Comment("Length delimited byte array");

            //Original
            //cw.WriteLine("ProtocolParser.WriteBytes(" + stream + ", " + memoryStream + ".ToArray());");

            //Much slower than original
            /*
            cw.WriteLine("ProtocolParser.WriteUInt32(" + stream + ", (uint)" + memoryStream + ".Length);");
            cw.WriteLine(memoryStream + ".Seek(0, System.IO.SeekOrigin.Begin);");
            cw.WriteLine(memoryStream + ".CopyTo(" + stream + ");");
            */

            //Same speed as original
            /*
            cw.WriteLine("ProtocolParser.WriteUInt32(" + stream + ", (uint)" + memoryStream + ".Length);");
            cw.WriteLine(stream + ".Write(" + memoryStream + ".ToArray(), 0, (int)" + memoryStream + ".Length);");
            */

            //10% faster than original using GetBuffer rather than ToArray
            cw.WriteLine("uint length" + f.ID + " = (uint)msField.Length;");
            cw.WriteLine("global::SilentOrbit.ProtocolBuffers.ProtocolParser.WriteUInt32(" + stream + ", length" + f.ID + ");");
            cw.WriteLine(stream + ".Write(msField.GetBuffer(), 0, (int)length" + f.ID + ");");
        }

        /// <summary>
        /// Generates code for writing one field
        /// </summary>
        public static void FieldWriter(ProtoMessage m, Field f, CodeWriter cw, Options options)
        {
            if (f.Rule == FieldRule.Repeated)
            {
                if (f.OptionPacked == true)
                {
                    //Repeated packed
                    cw.IfBracket("instance." + f.CsName + " != null");

                    KeyWriter("stream", f.ID, Wire.LengthDelimited, cw);
                    if (f.ProtoType.WireSize < 0)
                    {
                        //Un-optimized, unknown size
                        cw.WriteLine("msField.SetLength(0);");
                        if (f.IsUsingBinaryWriter)
                            cw.WriteLine("BinaryWriter bw" + f.ID + " = new BinaryWriter(ms" + f.ID + ");");

                        cw.ForeachBracket("var i" + f.ID + " in instance." + f.CsName);
                        cw.WriteLine(FieldWriterType(f, m.RequiredMessageTables, "msField", "bw" + f.ID, "i" + f.ID));
                        cw.EndBracket();

                        BytesWriter(f, "stream", cw);
                    }
                    else
                    {
                        //Optimized with known size
                        //No memorystream buffering, write size first at once

                        //For constant size messages we can skip serializing to the MemoryStream
                        cw.WriteLine("global::SilentOrbit.ProtocolBuffers.ProtocolParser.WriteUInt32(stream, " + f.ProtoType.WireSize + "u * (uint)instance." + f.CsName + ".Count);");

                        cw.ForeachBracket("var i" + f.ID + " in instance." + f.CsName);
                        cw.WriteLine(FieldWriterType(f, m.RequiredMessageTables, "stream", "bw", "i" + f.ID));
                        cw.EndBracket();
                    }
                    cw.EndBracket();
                }
                else
                {
                    //Repeated not packet
                    cw.IfBracket("instance." + f.CsName + " != null");
                    cw.ForeachBracket("var i" + f.ID + " in instance." + f.CsName);
                    KeyWriter("stream", f.ID, f.ProtoType.WireType, cw);
                    cw.WriteLine(FieldWriterType(f, m.RequiredMessageTables, "stream", "bw", "i" + f.ID));
                    cw.EndBracket();
                    cw.EndBracket();
                }
                return;
            }
            else if (f.Rule == FieldRule.Optional)
            {
                if (options.Nullable || 
                    f.ProtoType is ProtoMessage ||
                    f.ProtoType.ProtoName == ProtoBuiltin.String ||
                    f.ProtoType.ProtoName == ProtoBuiltin.Bytes)
                {
                    if (f.ProtoType.Nullable || options.Nullable) //Struct always exist, not optional
                        cw.IfBracket("instance." + f.CsName + " != null");
                    KeyWriter("stream", f.ID, f.ProtoType.WireType, cw);
                    var needValue = !f.ProtoType.Nullable && options.Nullable;
                    cw.WriteLine(FieldWriterType(f, m.RequiredMessageTables, "stream", "bw", "instance." + f.CsName + (needValue ? ".Value" : "")));
                    if (f.ProtoType.Nullable || options.Nullable) //Struct always exist, not optional
                        cw.EndBracket();
                    return;
                }
                if (f.ProtoType is ProtoEnum)
                {
                    if (f.OptionDefault != null)
                        cw.IfBracket("instance." + f.CsName + " != " + f.ProtoType.CsType + "." + f.OptionDefault);
                    KeyWriter("stream", f.ID, f.ProtoType.WireType, cw);
                    cw.WriteLine(FieldWriterType(f, m.RequiredMessageTables, "stream", "bw", "instance." + f.CsName));
                    if (f.OptionDefault != null)
                        cw.EndBracket();
                    return;
                }
                KeyWriter("stream", f.ID, f.ProtoType.WireType, cw);
                cw.WriteLine(FieldWriterType(f, m.RequiredMessageTables, "stream", "bw", "instance." + f.CsName));
                return;
            }
            else if (f.Rule == FieldRule.Required)
            {
                if (f.ProtoType is ProtoMessage && f.ProtoType.OptionType != "struct" ||
                    f.ProtoType.ProtoName == ProtoBuiltin.String ||
                    f.ProtoType.ProtoName == ProtoBuiltin.Bytes)
                {
                    cw.WriteLine("if (instance." + f.CsName + " == null)");
                    cw.WriteIndent("throw new ArgumentNullException(\"" + f.CsName + "\", \"Required by proto specification.\");");
                }
                KeyWriter("stream", f.ID, f.ProtoType.WireType, cw);
                cw.WriteLine(FieldWriterType(f, m.RequiredMessageTables, "stream", "bw", "instance." + f.CsName));
                return;
            }
            throw new NotImplementedException("Unknown rule: " + f.Rule);
        }

        /// <summary>
        /// Generates code for writing one field as a JSON string
        /// </summary>
        public static void FieldJsonWriter(ProtoMessage m, Field f, CodeWriter cw, Options options)
        {
            cw.WriteLine("writer.Write(\"\\\"" + f.ProtoName + "\\\":\");");
            if (f.Rule == FieldRule.Repeated) {
                cw.WriteLine();
                cw.IfBracket("instance." + f.CsName + " != null");
                cw.WriteLine("writer.Write(\"[\");");
                cw.WriteLine("var first = true;");
                cw.ForeachBracket("var i" + f.ID + " in instance." + f.CsName);
                cw.WriteLine("if (!first) writer.Write(\",\");");
                cw.WriteLine("else first = false;");
                cw.WriteLine(FieldWriterJsonType(f, m.RequiredMessageTables, "writer", "i" + f.ID));
                cw.EndBracket();
                cw.WriteLine("writer.Write(\"]\");");
                cw.ElseBracket();
                cw.WriteLine("writer.Write(\"null\");");
                cw.EndBracket();
                return;
            } else if (f.Rule == FieldRule.Optional) {
                if (options.Nullable ||
                    f.ProtoType is ProtoMessage ||
                    f.ProtoType.ProtoName == ProtoBuiltin.String ||
                    f.ProtoType.ProtoName == ProtoBuiltin.Bytes) {
                    var nullable = (f.ProtoType.Nullable || options.Nullable) && !(f.ProtoType.OptionType == "interface" && m.RequiredMessageTables.Contains(f.ProtoType));
                    if (nullable) //Struct always exist, not optional
                        cw.IfBracket("instance." + f.CsName + " != null");
                    var needValue = !f.ProtoType.Nullable && options.Nullable;
                    cw.WriteLine(FieldWriterJsonType(f, m.RequiredMessageTables, "writer", "instance." + f.CsName + (needValue ? ".Value" : "")));
                    if (nullable) { //Struct always exist, not optional
                        cw.ElseBracket();
                        cw.WriteLine("writer.Write(\"null\");");
                        cw.EndBracket();
                    }
                    return;
                }
                if (f.ProtoType is ProtoEnum) {
                    cw.WriteLine(FieldWriterJsonType(f, m.RequiredMessageTables, "writer", "instance." + f.CsName));
                    return;
                }
                cw.WriteLine(FieldWriterJsonType(f, m.RequiredMessageTables, "writer", "instance." + f.CsName));
                return;
            } else if (f.Rule == FieldRule.Required) {
                if (f.ProtoType is ProtoMessage && f.ProtoType.OptionType != "struct" ||
                    f.ProtoType.ProtoName == ProtoBuiltin.String ||
                    f.ProtoType.ProtoName == ProtoBuiltin.Bytes) {
                    cw.WriteLine("if (instance." + f.CsName + " == null)");
                    cw.WriteIndent("throw new ArgumentNullException(\"" + f.CsName + "\", \"Required by proto specification.\");");
                }
                cw.WriteLine(FieldWriterJsonType(f, m.RequiredMessageTables, "writer", "instance." + f.CsName));
                return;
            }
            throw new NotImplementedException("Unknown rule: " + f.Rule);
        }

        static string FieldWriterType(Field f, ProtoMessage[] messageTables, string stream, string binaryWriter, string instance)
        {
            if (f.OptionCodeType != null)
            {
                switch (f.OptionCodeType)
                {
                    case "DateTime":
                    case "TimeSpan":
                        return FieldWriterPrimitive(f, messageTables, stream, binaryWriter, instance + ".Ticks");
                    default: //enum
                        break;
                }
            }
            return FieldWriterPrimitive(f, messageTables, stream, binaryWriter, instance);
        }

        static string FieldWriterPrimitive(Field f, ProtoMessage[] messageTables, string stream, string binaryWriter, string instance)
        {

            if (f.ProtoType is ProtoEnum)
                return "global::SilentOrbit.ProtocolBuffers.ProtocolParser.WriteUInt64(" + stream + ",(ulong)" + instance + ");";

            if (f.ProtoType is ProtoMessage)
            {
                var pm = (ProtoMessage) f.ProtoType;
                var cw = new CodeWriter();
                var messageTableParams = "";
                if (pm.RequiredMessageTables.Any()) {
                    messageTableParams = string.Join("", pm.RequiredMessageTables.Select(x => ", mTable" + Array.IndexOf(messageTables, x)));
                }

                if (pm.OptionType == "interface" && messageTables.Contains(pm)) {
                    cw.WriteLine("mTable" + Array.IndexOf(messageTables, pm) + ".Serialize(" + stream + ", " + instance + ");");
                    return cw.Code;
                }

                cw.WriteLine("msField.SetLength(0);");
                cw.WriteLine(pm.FullSerializerType + ".Serialize(msField, " + instance + messageTableParams + ");");
                BytesWriter(f, stream, cw);
                return cw.Code;
            }

            switch (f.ProtoType.ProtoName)
            {
                case ProtoBuiltin.Double:
                case ProtoBuiltin.Float:
                case ProtoBuiltin.Fixed32:
                case ProtoBuiltin.Fixed64:
                case ProtoBuiltin.SFixed32:
                case ProtoBuiltin.SFixed64:
                    return binaryWriter + ".Write(" + instance + ");";
                case ProtoBuiltin.Int32: //Serialized as 64 bit varint
                    return "global::SilentOrbit.ProtocolBuffers.ProtocolParser.WriteUInt64(" + stream + ",(ulong)" + instance + ");";
                case ProtoBuiltin.Int64:
                    return "global::SilentOrbit.ProtocolBuffers.ProtocolParser.WriteUInt64(" + stream + ",(ulong)" + instance + ");";
                case ProtoBuiltin.UInt32:
                    return "global::SilentOrbit.ProtocolBuffers.ProtocolParser.WriteUInt32(" + stream + ",(uint)" + instance + ");";
                case ProtoBuiltin.UInt64:
                    return "global::SilentOrbit.ProtocolBuffers.ProtocolParser.WriteUInt64(" + stream + ", " + instance + ");";
                case ProtoBuiltin.SInt32:
                    return "global::SilentOrbit.ProtocolBuffers.ProtocolParser.WriteZInt32(" + stream + ", " + instance + ");";
                case ProtoBuiltin.SInt64:
                    return "global::SilentOrbit.ProtocolBuffers.ProtocolParser.WriteZInt64(" + stream + ", " + instance + ");";
                case ProtoBuiltin.Bool:
                    return "global::SilentOrbit.ProtocolBuffers.ProtocolParser.WriteBool(" + stream + ", " + instance + ");";
                case ProtoBuiltin.String:
                    return "global::SilentOrbit.ProtocolBuffers.ProtocolParser.WriteBytes(" + stream + ", Encoding.UTF8.GetBytes(" + instance + "));";
                case ProtoBuiltin.Bytes:
                    return "global::SilentOrbit.ProtocolBuffers.ProtocolParser.WriteBytes(" + stream + ", " + instance + ");";
            }

            throw new NotImplementedException();
        }

        static string FieldWriterJsonType(Field f, ProtoMessage[] messageTables, string writer, string instance)
        {
            if (f.OptionCodeType != null) {
                switch (f.OptionCodeType) {
                    case "DateTime":
                    case "TimeSpan":
                        return FieldWriterJsonPrimitive(f, messageTables, writer, instance + ".Ticks");
                }
            }
            return FieldWriterJsonPrimitive(f, messageTables, writer, instance);
        }
        
        static string FieldWriterJsonPrimitive(Field f, ProtoMessage[] messageTables, string writer, string instance)
        {
            if (f.ProtoType is ProtoEnum)
                return writer + ".Write(" + instance + ".ToString());";

            if (f.ProtoType is ProtoMessage) {
                var pm = (ProtoMessage) f.ProtoType;
                var cw = new CodeWriter();
                var messageTableParams = "";
                if (pm.RequiredMessageTables.Any()) {
                    messageTableParams = string.Join("", pm.RequiredMessageTables.Select(x => ", mTable" + Array.IndexOf(messageTables, x)));
                }

                if (pm.OptionType == "interface" && messageTables.Contains(pm)) {
                    cw.WriteLine(writer + ".Write(mTable" + Array.IndexOf(messageTables, pm) + ".SerializeJson(" + instance + "));");
                    return cw.Code;
                }

                cw.WriteLine(pm.FullSerializerType + ".SerializeJson(" + writer + ", " + instance + messageTableParams + ");");
                return cw.Code;
            }

            switch (f.ProtoType.ProtoName) {
                case ProtoBuiltin.Bool:
                    return writer + ".Write(" + instance + " ? \"true\" : \"false\");";
                case ProtoBuiltin.String:
                    return writer + ".Write(new global::Newtonsoft.Json.Linq.JValue(" + instance + ").ToString(global::Newtonsoft.Json.Formatting.None));";
                case ProtoBuiltin.Bytes:
                    return writer + ".Write(" + instance + " == null ? \"null\" : (\"\\\"\" + global::System.Convert.ToBase64String(" + instance + ") + \"\\\"\"));";
                default:
                    return writer + ".Write(" + instance + ".ToString());";
            }
        }

        #endregion
    }
}

