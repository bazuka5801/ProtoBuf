using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace TestNew
{
    public static class Pool
    {
        public static Queue<MemoryStream> mss = new Queue<MemoryStream>();
        
        public static MemoryStream Get<T>()
            where T : class, new()
        {
            if (mss.Count > 0)
                return mss.Dequeue();
            else return new MemoryStream();
        }

        public static void Free<T>(ref T t)
        {
            
        }

        public static void FreeMemoryStream(ref MemoryStream ms)
        {
            ms.SetLength(0);
            mss.Enqueue(ms);
        }

        public static void FreeList<T>(ref List<T> obj)
        {
        }
    }
}