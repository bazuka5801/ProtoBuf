using System;
using System.Linq;
using SilentOrbit.Code;

namespace SilentOrbit.ProtocolBuffers
{
    class MessageSerializer
    {
        readonly CodeWriter cw;
        readonly Options options;
        readonly FieldSerializer fieldSerializer;

        public MessageSerializer(CodeWriter cw, Options options)
        {
            this.cw = cw;
            this.options = options;
            this.fieldSerializer = new FieldSerializer(cw, options);
        }

        public void GenerateClassSerializer(ProtoMessage m)
        {
            String identName = null;

            if (m.OptionIdentifier > 0) {
                identName = "MessageIdentifier";
                cw.Attribute("ProtoBuf.MessageIdent(" + identName + ")");
            }

            if (m.OptionExternal || m.OptionType == "interface") {
                //Don't make partial class of external classes or interfaces
                //Make separate static class for them
                cw.Bracket(m.OptionAccess + " partial class " + m.SerializerType);
            } else {
                cw.Attribute("System.Serializable()");
                cw.Bracket(m.OptionAccess + " partial " + m.OptionType + " " + m.SerializerType);
            }

            if (m.OptionIdentifier > 0) {
                cw.WriteLine(String.Format("public const uint {0} = 0x{1:x8};", identName, m.OptionIdentifier));
                cw.WriteLine();
            }

            GenerateReader(m);

            GenerateWriter(m);
            foreach (ProtoMessage sub in m.Messages.Values) {
                cw.WriteLine();
                GenerateClassSerializer(sub);
            }
            cw.EndBracket();
            cw.WriteLine();
            return;
        }

        private void GenerateDefaults(ProtoMessage m)
        {
            //Prepare List<> and default values
            foreach (Field f in m.Fields.Values) {
                if (f.Rule == FieldRule.Repeated) {
                    if (f.OptionReadOnly == false) {
                        //Initialize lists of the custom DateTime or TimeSpan type.
                        string csType = f.ProtoType.FullCsType;
                        if (f.OptionCodeType != null)
                            csType = f.OptionCodeType;

                        cw.WriteLine("if (instance." + f.CsName + " == null)");
                        cw.WriteIndent("instance." + f.CsName + " = new List<" + csType + ">();");
                    }
                } else if (f.OptionDefault != null) {
                    cw.WriteLine("instance." + f.CsName + " = " + f.FormatForTypeAssignment() + ";");
                } else if ((f.Rule == FieldRule.Optional) && !options.Nullable) {
                    if (f.ProtoType is ProtoEnum) {
                        ProtoEnum pe = f.ProtoType as ProtoEnum;
                        //the default value is the first value listed in the enum's type definition
                        foreach (var kvp in pe.Enums) {
                            cw.WriteLine("instance." + f.CsName + " = " + pe.FullCsType + "." + kvp.Name + ";");
                            break;
                        }
                    }
                }
            }
        }

        void FindMessageTableParams(ProtoMessage m, out string mTableParamDefs, out string mTableParams)
        {
            mTableParamDefs = "";
            mTableParams = "";
            if (m.RequiredMessageTables.Any()) {
                mTableParamDefs = string.Join("", m.RequiredMessageTables.Select(
                    (x, i) => string.Format(", ProtoBuf.MessageTable<{0}> mTable{1}", x.CsType, i)).ToArray());
                mTableParams = string.Join("", m.RequiredMessageTables.Select(
                    (x, i) => string.Format(", mTable{0}", i)).ToArray());
            }
        }

        void GenerateReader(ProtoMessage m)
        {
            string mTableParamDefs, mTableParams;
            FindMessageTableParams(m, out mTableParamDefs, out mTableParams);

            #region Helper Deserialize Methods
            string refstr = (m.OptionType == "struct") ? "ref " : "";
            if (m.OptionType != "interface") {
                if (!m.OptionNoInstancing) {
                    cw.Summary("Helper: create a new instance to deserializing into");
                    cw.Bracket(m.OptionAccess + " static " + m.CsType + " Deserialize(Stream stream" + mTableParamDefs + ")");
                    cw.WriteLine(m.CsType + " instance = new " + m.CsType + "();");
                    cw.WriteLine("Deserialize(stream" + mTableParams + ", " + refstr + "instance);");
                    cw.WriteLine("return instance;");
                    cw.EndBracketSpace();

                    cw.Summary("Helper: create a new instance to deserializing into");
                    cw.Bracket(m.OptionAccess + " static " + m.CsType + " DeserializeLengthDelimited(Stream stream" + mTableParamDefs + ")");
                    cw.WriteLine(m.CsType + " instance = new " + m.CsType + "();");
                    cw.WriteLine("DeserializeLengthDelimited(stream" + mTableParams + ", " + refstr + "instance);");
                    cw.WriteLine("return instance;");
                    cw.EndBracketSpace();

                    cw.Summary("Helper: create a new instance to deserializing into");
                    cw.Bracket(m.OptionAccess + " static " + m.CsType + " DeserializeLength(Stream stream" + mTableParamDefs + ", int length)");
                    cw.WriteLine(m.CsType + " instance = new " + m.CsType + "();");
                    cw.WriteLine("DeserializeLength(stream" + mTableParams + ", length, " + refstr + "instance);");
                    cw.WriteLine("return instance;");
                    cw.EndBracketSpace();

                    cw.Summary("Helper: put the buffer into a MemoryStream and create a new instance to deserializing into");
                    cw.Bracket(m.OptionAccess + " static " + m.CsType + " Deserialize(byte[] buffer" + mTableParamDefs + ")");
                    cw.WriteLine(m.CsType + " instance = new " + m.CsType + "();");
                    cw.WriteLine("using (var ms = new MemoryStream(buffer))");
                    cw.WriteIndent("Deserialize(ms" + mTableParams + ", " + refstr + "instance);");
                    cw.WriteLine("return instance;");
                    cw.EndBracketSpace();

                    cw.Summary("Helper: create a new instance when deserializing a JObject");
                    cw.Bracket(m.OptionAccess + " static " + m.CsType + " Deserialize(global::Newtonsoft.Json.Linq.JObject obj" + mTableParamDefs + ")");
                    cw.WriteLine(m.CsType + " instance = new " + m.CsType + "();");
                    cw.WriteLine("Deserialize(obj" + mTableParams + ", " + refstr + "instance);");
                    cw.WriteLine("return instance;");
                    cw.EndBracketSpace();

                    cw.Summary("Helper: create a new instance and deserialize JSON from a string");
                    cw.Bracket(m.OptionAccess + " static " + m.CsType + " Deserialize(string json" + mTableParamDefs + ")");
                    cw.WriteLine(m.CsType + " instance = new " + m.CsType + "();");
                    cw.WriteLine("Deserialize(global::Newtonsoft.Json.Linq.JObject.Parse(json)" + mTableParams + ", " + refstr + "instance);");
                    cw.WriteLine("return instance;");
                    cw.EndBracketSpace();
                }

                if (!m.OptionNoPartials) {
                    cw.Summary("Load this value from a proto buffer");
                    cw.Bracket(m.OptionAccess + " void FromProto(Stream stream" + mTableParamDefs + ")");
                    cw.WriteLine("Deserialize(stream" + mTableParams + ", this );");
                    cw.EndBracketSpace();

                    cw.Summary("Load this value from a json object");
                    cw.Bracket(m.OptionAccess + " void FromJson(global::Newtonsoft.Json.Linq.JObject obj" + mTableParamDefs + ")");
                    cw.WriteLine("Deserialize(obj" + mTableParams + ", this );");
                    cw.EndBracketSpace();
                }

            }

            cw.Summary("Helper: put the buffer into a MemoryStream before deserializing");
            cw.Bracket(m.OptionAccess + " static " + m.FullCsType + " Deserialize(byte[] buffer" + mTableParamDefs + ", " + refstr + m.FullCsType + " instance)");
            cw.WriteLine("using (var ms = new MemoryStream(buffer))");
            cw.WriteIndent("Deserialize(ms" + mTableParams + ", " + refstr + "instance);");
            cw.WriteLine("return instance;");
            cw.EndBracketSpace();
            #endregion

            string[] methods = new string[]
            {
                "Deserialize", //Default old one
                "DeserializeLengthDelimited", //Start by reading length prefix and stay within that limit
                "DeserializeLength", //Read at most length bytes given by argument
            };

            //Main Deserialize
            foreach (string method in methods) {
                if (method == "Deserialize") {
                    cw.Summary("Takes the remaining content of the stream and deserialze it into the instance.");
                    cw.Bracket(m.OptionAccess + " static " + m.FullCsType + " " + method + "(Stream stream" + mTableParamDefs + ", " + refstr + m.FullCsType + " instance)");
                } else if (method == "DeserializeLengthDelimited") {
                    cw.Summary("Read the VarInt length prefix and the given number of bytes from the stream and deserialze it into the instance.");
                    cw.Bracket(m.OptionAccess + " static " + m.FullCsType + " " + method + "(Stream stream" + mTableParamDefs + ", " + refstr + m.FullCsType + " instance)");
                } else if (method == "DeserializeLength") {
                    cw.Summary("Read the given number of bytes from the stream and deserialze it into the instance.");
                    cw.Bracket(m.OptionAccess + " static " + m.FullCsType + " " + method + "(Stream stream" + mTableParamDefs + ", int length, " + refstr + m.FullCsType + " instance)");
                } else
                    throw new NotImplementedException();

                if (m.IsUsingBinaryWriter)
                    cw.WriteLine("BinaryReader br = new BinaryReader(stream);");

                GenerateDefaults(m);

                if (method == "DeserializeLengthDelimited") {
                    //Important to read stream position after we have read the length field
                    cw.WriteLine("long limit = global::SilentOrbit.ProtocolBuffers.ProtocolParser.ReadUInt32(stream);");
                    cw.WriteLine("limit += stream.Position;");
                }
                if (method == "DeserializeLength") {
                    //Important to read stream position after we have read the length field
                    cw.WriteLine("long limit = stream.Position + length;");
                }

                cw.WhileBracket("true");

                if (method == "DeserializeLengthDelimited" || method == "DeserializeLength") {
                    cw.IfBracket("stream.Position >= limit");
                    cw.WriteLine("if (stream.Position == limit)");
                    cw.WriteIndent("break;");
                    cw.WriteLine("else");
                    cw.WriteIndent("throw new global::SilentOrbit.ProtocolBuffers.ProtocolBufferException(\"Read past max limit\");");
                    cw.EndBracket();
                }

                cw.WriteLine("int keyByte = stream.ReadByte();");
                cw.WriteLine("if (keyByte == -1)");
                if (method == "Deserialize")
                    cw.WriteIndent("break;");
                else
                    cw.WriteIndent("throw new System.IO.EndOfStreamException();");

                //Determine if we need the lowID optimization
                bool hasLowID = false;
                foreach (Field f in m.Fields.Values) {
                    if (f.ID < 16) {
                        hasLowID = true;
                        break;
                    }
                }

                if (hasLowID) {
                    cw.Comment("Optimized reading of known fields with field ID < 16");
                    cw.Switch("keyByte");
                    foreach (Field f in m.Fields.Values) {
                        if (f.ID >= 16)
                            continue;
                        cw.Dedent();
                        cw.Comment("Field " + f.ID + " " + f.WireType);
                        cw.Indent();
                        cw.Case(((f.ID << 3) | (int) f.WireType));
                        if (fieldSerializer.FieldReader(f, m.RequiredMessageTables))
                            cw.WriteLine("continue;");
                    }
                    cw.SwitchEnd();
                    cw.WriteLine();
                }
                cw.WriteLine("var key = global::SilentOrbit.ProtocolBuffers.ProtocolParser.ReadKey((byte)keyByte, stream);");

                cw.WriteLine();

                cw.Comment("Reading field ID > 16 and unknown field ID/wire type combinations");
                cw.Switch("key.Field");
                cw.Case(0);
                cw.WriteLine("throw new global::SilentOrbit.ProtocolBuffers.ProtocolBufferException(\"Invalid field id: 0, something went wrong in the stream\");");
                foreach (Field f in m.Fields.Values) {
                    if (f.ID < 16)
                        continue;
                    cw.Case(f.ID);
                    //Makes sure we got the right wire type
                    cw.WriteLine("if(key.WireType != global::SilentOrbit.ProtocolBuffers.Wire." + f.WireType + ")");
                    cw.WriteIndent("break;"); //This can be changed to throw an exception for unknown formats.
                    if (fieldSerializer.FieldReader(f, m.RequiredMessageTables))
                        cw.WriteLine("continue;");
                }
                cw.CaseDefault();
                if (m.OptionPreserveUnknown) {
                    cw.WriteLine("if (instance.PreservedFields == null)");
                    cw.WriteIndent("instance.PreservedFields = new List<global::SilentOrbit.ProtocolBuffers.KeyValue>();");
                    cw.WriteLine("instance.PreservedFields.Add(new global::SilentOrbit.ProtocolBuffers.KeyValue(key, global::SilentOrbit.ProtocolBuffers.ProtocolParser.ReadValueBytes(stream, key)));");
                } else {
                    cw.WriteLine("global::SilentOrbit.ProtocolBuffers.ProtocolParser.SkipKey(stream, key);");
                }
                cw.WriteLine("break;");
                cw.SwitchEnd();
                cw.EndBracket();
                cw.WriteLine();

                if (m.OptionTriggers)
                    cw.WriteLine("instance.AfterDeserialize();");
                cw.WriteLine("return instance;");
                cw.EndBracket();
                cw.WriteLine();
            }

            //JSON deserialize
            cw.Summary("Deserializes an instance from a JSON object.");
            cw.Bracket(m.OptionAccess + " static " + m.FullCsType + " Deserialize(global::Newtonsoft.Json.Linq.JObject obj" + mTableParamDefs + ", " + refstr + m.FullCsType + " instance)");

            GenerateDefaults(m);
      
            cw.WriteLine();
            cw.Bracket("foreach (var property in obj.Properties())");
            cw.Switch("property.Name");

            foreach (var f in m.Fields.Values) {
                cw.Case("\"" + f.CsName + "\"");
                fieldSerializer.JsonFieldReader(f, m.RequiredMessageTables, "property.Value");
                cw.WriteLine("break;");
            }

            cw.SwitchEnd();
            cw.EndBracket();
            cw.WriteLine();

            if (m.OptionTriggers)
                cw.WriteLine("instance.AfterDeserialize();");
            cw.WriteLine("return instance;");
            cw.EndBracket();
            cw.WriteLine();

            return;
        }

        /// <summary>
        /// Generates code for writing a class/message
        /// </summary>
        void GenerateWriter(ProtoMessage m)
        {
            string mTableParamDefs, mTableParams;
            FindMessageTableParams(m, out mTableParamDefs, out mTableParams);

            string stack = "global::SilentOrbit.ProtocolBuffers.ProtocolParser.Stack";
            if (options.ExperimentalStack != null) {
                cw.WriteLine("[ThreadStatic]");
                cw.WriteLine("static global::SilentOrbit.ProtocolBuffers.MemoryStreamStack stack = new " + options.ExperimentalStack + "();");
                stack = "stack";
            }

            cw.Summary("Serialize the instance into the stream");
            cw.Bracket(m.OptionAccess + " static void Serialize(Stream stream, " + m.CsType + " instance" + mTableParamDefs + ")");
            if (m.OptionTriggers) {
                cw.WriteLine("instance.BeforeSerialize();");
                cw.WriteLine();
            }
            if (m.IsUsingBinaryWriter)
                cw.WriteLine("BinaryWriter bw = new BinaryWriter(stream);");

            //Shared memorystream for all fields
            cw.WriteLine("var msField = " + stack + ".Pop();");

            foreach (Field f in m.Fields.Values)
                FieldSerializer.FieldWriter(m, f, cw, options);

            cw.WriteLine(stack + ".Push(msField);");

            if (m.OptionPreserveUnknown) {
                cw.IfBracket("instance.PreservedFields != null");
                cw.ForeachBracket("var kv in instance.PreservedFields");
                cw.WriteLine("global::SilentOrbit.ProtocolBuffers.ProtocolParser.WriteKey(stream, kv.Key);");
                cw.WriteLine("stream.Write(kv.Value, 0, kv.Value.Length);");
                cw.EndBracket();
                cw.EndBracket();
            }
            cw.EndBracket();
            cw.WriteLine();

            if (m.OptionType != "interface" && !m.OptionNoPartials) {
                cw.Summary("Serialize and return data as a byte array (use this sparingly)");
                cw.Bracket(m.OptionAccess + " byte[] ToProtoBytes(" + mTableParamDefs.TrimStart(',', ' ') + ")");
                cw.WriteLine("return SerializeToBytes( this" + mTableParams +" );");
                cw.EndBracketSpace();

                cw.Summary("Serialize to a Stream");
                cw.Bracket(m.OptionAccess + " void ToProto( Stream stream" + mTableParamDefs + " )");
                cw.WriteLine("Serialize( stream, this" + mTableParams + " );");
                cw.EndBracketSpace();

                cw.Summary("Serialize to a JSON string");
                cw.Bracket(m.OptionAccess + " string ToJson(" + mTableParamDefs.TrimStart(',', ' ') + ")");
                cw.WriteLine("var writer = new global::System.IO.StringWriter();");
                cw.WriteLine("SerializeJson(writer, this" + mTableParams + ");");
                cw.WriteLine("return writer.ToString();");
                cw.EndBracketSpace();
            }

            cw.Summary("Helper: Serialize into a MemoryStream and return its byte array");
            cw.Bracket(m.OptionAccess + " static byte[] SerializeToBytes(" + m.CsType + " instance" + mTableParamDefs + ")");
            cw.Using("var ms = new MemoryStream()");
            cw.WriteLine("Serialize(ms, instance" + mTableParams + ");");
            cw.WriteLine("return ms.ToArray();");
            cw.EndBracket();
            cw.EndBracket();

            cw.Summary("Helper: Serialize with a varint length prefix");
            cw.Bracket(m.OptionAccess + " static void SerializeLengthDelimited(Stream stream, " + m.CsType + " instance" + mTableParamDefs + ")");
            cw.WriteLine("var data = SerializeToBytes(instance" + mTableParams + ");");
            cw.WriteLine("global::SilentOrbit.ProtocolBuffers.ProtocolParser.WriteUInt32(stream, (uint)data.Length);");
            cw.WriteLine("stream.Write(data, 0, data.Length);");
            cw.EndBracket();

            cw.Summary("Serialize into a JSON string");
            cw.Bracket(m.OptionAccess + " static void SerializeJson(TextWriter writer, " + m.CsType + " instance" + mTableParamDefs + ")");
            cw.WriteLine("writer.Write(\"{\");");

            var first = true;
            foreach (Field f in m.Fields.Values) {
                if (!first) {
                    cw.WriteLine("writer.Write(\",\");");
                } else {
                    first = false;
                }
                FieldSerializer.FieldJsonWriter(m, f, cw, options);
            }

            cw.WriteLine("writer.Write(\"}\");");
            cw.EndBracket();
        }
    }
}

