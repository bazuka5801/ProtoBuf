using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Despunch.Data;
using UnityEngine;
using Random = System.Random;

namespace TestNew
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            Random rand = new Random();
            InputState[] inputs = new InputState[1024];
            for (var i = 0; i < inputs.Length; i++)
            {
                inputs[i] = new InputState()
                { 
                    aimAngles = new Vector3(rand.Next(-1000,1000),rand.Next(-1000,1000),rand.Next(-1000,1000)),
                    mouseDelta = new Vector3(rand.Next(-1000,1000),rand.Next(-1000,1000),rand.Next(-1000,1000)),
                    buttons = rand.Next()
                };
            }
            Stopwatch sw = new Stopwatch();
            sw.Start();
            var ms = Pool.Get<MemoryStream>();
            for (int j = 0; j < 1000; j++)
            {
                for (int i = 0; i < inputs.Length; i++)
                {
                    InputState.Serialize(ms, inputs[i]);
                    InputState.Deserialize(ms, inputs[i]);
                }
            }
            Pool.FreeMemoryStream(ref ms);

            sw.Stop();
            Console.WriteLine("MS: "+sw.ElapsedMilliseconds);
            Console.ReadKey();
        }
    }
}