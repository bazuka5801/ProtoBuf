using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using SilentOrbit.Code;

namespace SilentOrbit.ProtocolBuffers
{
    static class MessageSerializer
    {
        public static void GenerateClassSerializer(ProtoMessage m, CodeWriter cw, Options options)
        {
            if (m.OptionExternal || m.OptionType == "interface")
            {
                //Don't make partial class of external classes or interfaces
                //Make separate static class for them
                cw.Bracket(m.OptionAccess + " static class " + m.SerializerType);
            }
            else
            {
                cw.Bracket(m.OptionAccess + " partial " + m.OptionType + " " + m.SerializerType);
            }

            if (m.OptionType == "class")
            {
                GeneratePooled(m, cw, options);
            }

            GenerateReader(m, cw, options);
            GenerateWriter(m, cw, options);
            
            foreach (ProtoMessage sub in m.Messages.Values)
            {
                cw.WriteLine();
                GenerateClassSerializer(sub, cw, options);
            }
            cw.EndBracket();
            cw.WriteLine();
            return;
        }

        static void GeneratePooled(ProtoMessage m, CodeWriter cw, Options options)
        {
            cw.WriteLine("#region [Methods] Pooled");
            cw.WriteLine("private bool _disposed;");
            cw.WriteLine("public bool ShouldPool = true;");
            cw.WriteLine();
            
            #region [Method] Dispose
            cw.Bracket("public virtual void Dispose()");
                cw.WriteLine("if (this._disposed)");
                    cw.WriteIndent("return;");
                cw.WriteLine("this.ResetToPool();");
                cw.WriteLine("this._disposed = true;");
            cw.EndBracketSpace();
            #endregion

            #region [Method] ResetToPool
            cw.Bracket("public void ResetToPool()");
                cw.WriteLine("ResetToPool(this);");
            cw.EndBracketSpace();
            #endregion
            
            #region [Method] [Static] ResetToPool
            cw.Bracket($"public static void ResetToPool({m.CsType} instance)");
                cw.WriteLine("if (!instance.ShouldPool)");
                    cw.WriteIndent("return;");


            string GetFieldType(Field field)
            {
                string csType = field.ProtoType.FullCsType;
                if (field.OptionCodeType != null)
                    csType = field.OptionCodeType;
                return csType;
            }
            
            
            // Generate Default Structs
            List<string> defStructs = new List<string>();
            
            foreach (var field in m.Fields.Values)
            {
                string csFullType = GetFieldType(field);
                string csType     = field.ProtoType.CsType;
                string fName      = field.CsName;
                cw.Comment($"[{csType}] {fName}");
                
                // Generate Default Structs
                if (field.Rule != FieldRule.Repeated && field.ProtoType.OptionType == "struct" &&
                    string.IsNullOrEmpty(field.OptionDefault) &&
                    defStructs.Contains(csFullType) == false)
                {
                    cw.WriteLine($"{csFullType} def{csType} = new {csFullType}();");
                    defStructs.Add(csFullType);
                }
                
                if (field.Rule == FieldRule.Repeated)
                {
                    cw.Bracket($"if (instance.{field.CsName} != null)");
                    if (field.ProtoType is ProtoMessage)
                    {
                        cw.ForBracket($"int i = 0; i < instance.{fName}.Count; i++");
                            cw.Bracket($"if (instance.{fName}[i] != null)");
                                cw.WriteLine($"instance.{fName}[i].ResetToPool();");
                                cw.WriteLine($"instance.{fName}[i] = null;");
                            cw.EndBracket();
                        cw.EndBracket();
                    }

                    cw.WriteLine($"List<{csFullType}> ins{fName} = instance.{fName};");
                    cw.WriteLine($"Pool.FreeList<{csFullType}>(ref ins{fName});");
                    cw.WriteLine($"instance.{fName} = ins{fName};");
                    cw.EndBracket();
                }
                else
                {
                    if (field.ProtoType is ProtoMessage && field.ProtoType.OptionType != "struct")
                    {
                        cw.Bracket($"if (instance.{fName} != null)");
                            cw.WriteLine($"instance.{fName}.ResetToPool();");
                            cw.WriteLine($"instance.{fName} = null;");
                        cw.EndBracket();
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(field.OptionDefault) == false)
                        {
                            cw.WriteLine($"instance.{fName} = {field.FormatForTypeAssignment()};");
                        }
                        else if (field.ProtoType.OptionType == "struct")
                        {
                            cw.WriteLine($"instance.{fName} = def{csType};");
                        }
                        else
                        {
                            cw.WriteLine($"instance.{fName} = default({csFullType});");
                        }
                    }
                }
                
                cw.WriteLine();
            }
            
            defStructs.Clear();
            
            cw.WriteLine($"Pool.Free<{m.FullCsType}>(ref instance);");
            cw.EndBracketSpace();
            #endregion

            #region [Method] EnterPool
            cw.Bracket("public virtual void EnterPool()");
                cw.WriteLine("this._disposed = true;");
            cw.EndBracketSpace();
            #endregion
            
            #region [Method] LeavePool
            cw.Bracket("public virtual void LeavePool()");
                cw.WriteLine("this._disposed = false;");
            cw.EndBracketSpace();
            #endregion
            
            cw.WriteLine("#endregion");
        }
        
        static void GenerateDefaults(ProtoMessage m, CodeWriter cw, Options options)
        {
            foreach (Field f in m.Fields.Values)
            {
                if (f.Rule == FieldRule.Repeated)
                {
                    //Initialize lists of the custom DateTime or TimeSpan type.
                    string csType = f.ProtoType.FullCsType;
                    if (f.OptionCodeType != null)
                        csType = f.OptionCodeType;

                    cw.WriteLine("if (instance." + f.CsName + " == null)");
                    cw.WriteIndent("instance." + f.CsName + " = Pool.GetList<" + csType + ">();");
                }
                else if (f.OptionDefault != null)
                {
                    if (options.Properties)
                    {
                        if (f.ProtoType is ProtoEnum)
                            cw.WriteLine("instance." + f.CsName + " = " + f.ProtoType.FullCsType + "." +
                                         f.OptionDefault + ";");
                        else
                            cw.WriteLine("instance." + f.CsName + " = " + f.FormatForTypeAssignment() + ";");
                    }
                }
                else if (f.Rule == FieldRule.Optional)
                {
                    if (f.ProtoType is ProtoEnum)
                    {
                        ProtoEnum pe = f.ProtoType as ProtoEnum;
                        //the default value is the first value listed in the enum's type definition
                        foreach (var kvp in pe.Enums)
                        {
                            cw.WriteLine("instance." + f.CsName + " = " + pe.FullCsType + "." + kvp.Name + ";");
                            break;
                        }
                    }
                }
            }
        }

        static void GenerateReader(ProtoMessage m, CodeWriter cw, Options options)
        {
            cw.WriteLine("#region [Methods] Reader");
            
            #region [Method] ReadFromStream
            if (m.OptionExternal == false)
            {
                cw.Bracket("public void ReadFromStream(Stream stream, int size)");
                {
                    cw.WriteLine("DeserializeLength(stream, size, this);");
                }
                cw.EndBracketSpace();
            }
            #endregion
            
            #region Helper Deserialize Methods
            string refstr = (m.OptionType == "struct") ? "ref " : "";
            if (m.OptionType != "interface")
            {
                var newInstance = m.OptionType == "struct" ? $"new {m.CsType}();" : $"Pool.Get<{m.CsType}>();";
                
                cw.Summary("Helper: create a new instance to deserializing into");
                cw.Bracket(m.OptionAccess + " static " + m.CsType + " Deserialize(Stream stream)");
                {
                    cw.WriteLine(m.CsType + " instance = " + newInstance);
                    cw.WriteLine("Deserialize(stream, " + refstr + "instance);");
                    cw.WriteLine("return instance;");
                }
                cw.EndBracketSpace();

                cw.Summary("Helper: create a new instance to deserializing into");
                cw.Bracket(m.OptionAccess + " static " + m.CsType + " DeserializeLengthDelimited(Stream stream)");
                {
                    cw.WriteLine(m.CsType + " instance = " + newInstance);
                    cw.WriteLine("DeserializeLengthDelimited(stream, " + refstr + "instance);");
                    cw.WriteLine("return instance;");
                }
                cw.EndBracketSpace();

                cw.Summary("Helper: create a new instance to deserializing into");
                cw.Bracket(m.OptionAccess + " static " + m.CsType + " DeserializeLength(Stream stream, int length)");
                {
                    cw.WriteLine(m.CsType + " instance = " + newInstance);
                    cw.WriteLine("DeserializeLength(stream, length, " + refstr + "instance);");
                    cw.WriteLine("return instance;");
                }
                cw.EndBracketSpace();

                cw.Summary("Helper: put the buffer into a MemoryStream and create a new instance to deserializing into");
                cw.Bracket(m.OptionAccess + " static " + m.CsType + " Deserialize(byte[] buffer)");
                {
                    cw.WriteLine(m.CsType + " instance = " + newInstance);
                    cw.WriteLine("var ms = Pool.Get<MemoryStream>();");
                    cw.WriteLine("ms.Write(buffer, 0 ,buffer.Length);");
                    cw.WriteLine("ms.Position = 0;");
                    cw.WriteLine("Deserialize(ms, " + refstr + "instance);");
                    cw.WriteLine("Pool.FreeMemoryStream(ref ms);");
                    cw.WriteLine("return instance;");
                }
                cw.EndBracketSpace();
            }

            cw.Summary("Helper: put the buffer into a MemoryStream before deserializing");
            cw.Bracket(m.OptionAccess + " static " + m.FullCsType + " Deserialize(byte[] buffer, " + refstr + m.FullCsType + " instance)");
            cw.WriteLine("var ms = Pool.Get<MemoryStream>();");
            cw.WriteLine("ms.Write(buffer, 0 ,buffer.Length);");
            cw.WriteLine("ms.Position = 0;");
            cw.WriteLine("Deserialize(ms, " + refstr + "instance);");
            cw.WriteLine("Pool.FreeMemoryStream(ref ms);");
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
            foreach (string method in methods)
            {
                if (method == "Deserialize")
                {
                    cw.Summary("Takes the remaining content of the stream and deserialze it into the instance.");
                    cw.Bracket(m.OptionAccess + " static " + m.FullCsType + " " + method + "(Stream stream, " + refstr + m.FullCsType + " instance)");
                }
                else if (method == "DeserializeLengthDelimited")
                {
                    cw.Summary("Read the VarInt length prefix and the given number of bytes from the stream and deserialze it into the instance.");
                    cw.Bracket(m.OptionAccess + " static " + m.FullCsType + " " + method + "(Stream stream, " + refstr + m.FullCsType + " instance)");
                }
                else if (method == "DeserializeLength")
                {
                    cw.Summary("Read the given number of bytes from the stream and deserialze it into the instance.");
                    cw.Bracket(m.OptionAccess + " static " + m.FullCsType + " " + method + "(Stream stream, int length, " + refstr + m.FullCsType + " instance)");
                }
                else
                    throw new NotImplementedException();

                GenerateDefaults(m, cw, options);

                if (method == "DeserializeLengthDelimited")
                {
                    //Important to read stream position after we have read the length field
                    cw.WriteLine("long limit = global::SilentOrbit.ProtocolBuffers.ProtocolParser.ReadUInt32(stream);");
                    cw.WriteLine("limit += stream.Position;");
                }
                if (method == "DeserializeLength")
                {
                    //Important to read stream position after we have read the length field
                    cw.WriteLine("long limit = stream.Position + length;");
                }

                cw.WhileBracket("true");

                if (method == "DeserializeLengthDelimited" || method == "DeserializeLength")
                {
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
                foreach (Field f in m.Fields.Values)
                {
                    if (f.ID < 16)
                    {
                        hasLowID = true;
                        break;
                    }
                }

                if (hasLowID)
                {
                    cw.Comment("Optimized reading of known fields with field ID < 16");
                    cw.Switch("keyByte");
                    foreach (Field f in m.Fields.Values)
                    {
                        if (f.ID >= 16)
                            continue;
                        cw.Dedent();
                        cw.Comment("Field " + f.ID + " " + f.WireType);
                        cw.Indent();
                        cw.Case(((f.ID << 3) | (int)f.WireType));
                        if (FieldSerializer.FieldReader(f, cw))
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
                foreach (Field f in m.Fields.Values)
                {
                    if (f.ID < 16)
                        continue;
                    cw.Case(f.ID);
                    //Makes sure we got the right wire type
                    cw.WriteLine("if(key.WireType != global::SilentOrbit.ProtocolBuffers.Wire." + f.WireType + ")");
                    cw.WriteIndent("break;"); //This can be changed to throw an exception for unknown formats.
                    if (FieldSerializer.FieldReader(f, cw))
                        cw.WriteLine("continue;");
                }
                cw.CaseDefault();
                if (m.OptionPreserveUnknown)
                {
                    cw.WriteLine("if (instance.PreservedFields == null)");
                    cw.WriteIndent("instance.PreservedFields = new List<global::SilentOrbit.ProtocolBuffers.KeyValue>();");
                    cw.WriteLine("instance.PreservedFields.Add(new global::SilentOrbit.ProtocolBuffers.KeyValue(key, global::SilentOrbit.ProtocolBuffers.ProtocolParser.ReadValueBytes(stream, key)));");
                }
                else
                {
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
            cw.WriteLine("#endregion");
            return;
        }

        /// <summary>
        /// Generates code for writing a class/message
        /// </summary>
        static void GenerateWriter(ProtoMessage m, CodeWriter cw, Options options)
        {
            cw.WriteLine("#region [Methods] Writer");

            #region [Method] Serialize
            cw.Summary("Serialize the instance into the stream");
            cw.Bracket(m.OptionAccess + " static void Serialize(Stream stream, " + m.CsType + " instance)");
            {
                if (m.OptionTriggers)
                {
                    cw.WriteLine("instance.BeforeSerialize();");
                    cw.WriteLine();
                }

                //Shared memorystream for all fields
                cw.WriteLine("var msField = Pool.Get<MemoryStream>();");

                foreach (Field f in m.Fields.Values)
                    FieldSerializer.FieldWriter(m, f, cw, false);

                cw.WriteLine("Pool.FreeMemoryStream(ref msField);");

                if (m.OptionPreserveUnknown)
                {
                    cw.IfBracket("instance.PreservedFields != null");
                    cw.ForeachBracket("var kv in instance.PreservedFields");
                    cw.WriteLine("global::SilentOrbit.ProtocolBuffers.ProtocolParser.WriteKey(stream, kv.Key);");
                    cw.WriteLine("stream.Write(kv.Value, 0, kv.Value.Length);");
                    cw.EndBracket();
                    cw.EndBracket();
                }
            }
            cw.EndBracketSpace();
            #endregion

            #region [Method] SerializeToBytes
            cw.Summary("Helper: Serialize into a MemoryStream and return its byte array");
            cw.Bracket(m.OptionAccess + " static byte[] SerializeToBytes(" + m.CsType + " instance)");
            {
                cw.WriteLine("var ms = Pool.Get<MemoryStream>();");
                cw.WriteLine("Serialize(ms, instance);");
                cw.WriteLine("var arr = ms.ToArray();");
                cw.WriteLine("Pool.FreeMemoryStream(ref ms);");
                cw.WriteLine("return arr;");
            }
            cw.EndBracket();
            #endregion
            
            #region [Method] SerializeLengthDelimited
            cw.Summary("Helper: Serialize with a varint length prefix");
            cw.Bracket(m.OptionAccess + " static void SerializeLengthDelimited(Stream stream, " + m.CsType + " instance)");
            {
                cw.WriteLine("var data = SerializeToBytes(instance);");
                cw.WriteLine("global::SilentOrbit.ProtocolBuffers.ProtocolParser.WriteUInt32(stream, (uint)data.Length);");
                cw.WriteLine("stream.Write(data, 0, data.Length);");
            }
            cw.EndBracketSpace();
            #endregion

            #region [Method] SerializeDelta
            cw.Bracket($"public static void SerializeDelta(Stream stream, {m.CsType} instance, {m.CsType} previous)");
            {
                cw.WriteLine("var msField = Pool.Get<MemoryStream>();");

                foreach (Field f in m.Fields.Values)
                {
                    cw.IfBracket($"instance.{f.CsName} != previous.{f.CsName}");
                    FieldSerializer.FieldWriter(m, f, cw, true);
                    cw.EndBracket();
                }
                
                cw.WriteLine("Pool.FreeMemoryStream(ref msField);");
            }
            cw.EndBracket();
            #endregion

            #region [Method] WriteToStream
            if (m.OptionExternal == false)
            {
                cw.Bracket("public void WriteToStream(Stream stream)");
                {
                    cw.WriteLine("Serialize(stream, this);");
                }
                cw.EndBracket();
            }
            #endregion

            #region [Method] WriteToStreamDelta
            if (m.OptionExternal == false)
            {
                cw.Bracket($"public void WriteToStreamDelta(Stream stream, {m.CsType} previous)");
                {
                    cw.IfBracket("previous != null");
                    {
                        cw.WriteLine("SerializeDelta(stream, this, previous);");
                    }
                    cw.EndBracket();
                    cw.Bracket("else");
                    {
                        cw.WriteLine("Serialize(stream, this);");
                    }
                    cw.EndBracket();
                }
                cw.EndBracket();
            }
            #endregion
            
            cw.WriteLine("#endregion");
        }
    }
}

