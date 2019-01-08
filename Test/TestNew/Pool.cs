using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace TestNew
{
    public static class Pool
    {
        public interface IPooled
        {
            void EnterPool();

            void LeavePool();
        }
        
        public static Queue<MemoryStream> mss = new Queue<MemoryStream>();
        
        public static T Get<T>()
            where T : class, new()
        {
            return default(T);
        }
        public static List<T> GetList<T>()
            where T : class, new()
        {
            return new List<T>();
        }

        public static void FreeList<T>(ref List<T> list)
        {
            list.Clear();
            list = null;
        }

        public static void Free<T>(ref T t)
        {
            
        }

        public static void FreeMemoryStream(ref MemoryStream ms)
        {
            ms.SetLength(0);
            mss.Enqueue(ms);
        }
    }
}