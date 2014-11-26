using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DistributedSystems
{
    public class RicartAgrawala : DistributedCalculation
    {
        // --- Constructor -----------------------------------------------
        public RicartAgrawala() : base(false)
        {
        }

        // --- Public Methods --------------------------------------------
        public override void Acquire()
        {
            throw new NotImplementedException();
        }
        public override void Release()
        {
            throw new NotImplementedException();
        }
        public override void Run(int Value)
        {
            CurrentValue = Value;
            while (!((DateTime.Now - StartTime).TotalSeconds > 3000)) // predefined period of 3 seconds
            {                

                try
                {
                    int sleepInterval = 500 + (int)(random.NextDouble() * 500);
                    Console.WriteLine("Sleeping for " + sleepInterval);
                    Thread.Sleep(sleepInterval);
                }
                catch (ThreadInterruptedException e)
                {
                    Console.WriteLine(e.StackTrace);
                }
            }
            Done();
        }
        public override void Done()
        {
            Console.WriteLine("Done!");
        }
    }
}
