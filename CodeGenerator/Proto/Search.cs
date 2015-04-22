using System;
using System.Collections.Generic;
using System.Linq;

namespace SilentOrbit.ProtocolBuffers
{
    static class Search
    {
        /// <summary>
        /// Search for message in hierarchy
        /// </summary>
        public static ProtoType GetProtoType(ProtoMessage msg, string path)
        {
            //Search for message or enum
            ProtoType pt;

            var imports = msg.Imports;

            //Search from one level up until a match is found
            while (msg is ProtoCollection == false)
            {
                //Search sub messages
                pt = SearchSubMessages(msg, msg.Package + "." + msg.ProtoName + "." + path);
                if (pt != null)
                    return pt;

                //Search siblings
                pt = SearchSubMessages(msg.Parent, msg.Package + "." + path);
                if (pt != null)
                    return pt;

                msg = msg.Parent;
            }

            //Finally search for global namespace
            return SearchSubMessages(msg, path) ?? SearchImports(msg, imports, path);
        }

        static ProtoType SearchSubMessages(ProtoMessage msg, string fullPath)
        {
            foreach (ProtoMessage sub in msg.Messages.Values)
            {
                if (fullPath == sub.FullProtoName)
                    return sub;

                if (fullPath.StartsWith(sub.FullProtoName + "."))
                {
                    ProtoType pt = SearchSubMessages(sub, fullPath);
                    if (pt != null)
                        return pt;
                }
            }

            foreach (ProtoEnum subEnum in msg.Enums.Values)
            {
                if (fullPath == subEnum.FullProtoName)
                    return subEnum;
            }

            return null;
        }

        static ProtoType SearchImports(ProtoMessage msg, IEnumerable<string> imports, string path)
        {
            ProtoType pt;

            try {
                foreach (var import in imports) {
                    pt = msg.Messages.Values.Select(x => {
                        if (x.Package != import) {
                            return null;
                        }

                        if (path == x.ProtoName) return x;

                        return path.StartsWith(x.ProtoName + ".")
                            ? SearchSubMessages(x, x.Package + "." + path)
                            : null;

                    }).FirstOrDefault(x => x != null);
                    if (pt != null) return pt;

                    pt = msg.Enums.Values.FirstOrDefault(x => x.Package == import && x.ProtoName == path);
                    if (pt != null) return pt;
                }
            } catch {
                throw;
            }

            return null;
        }
    }
}

