using System;
using System.Linq;
using System.Threading;

namespace DistributedSystems
{
    public class TokenRing : DistributedCalculation
    {
        // --- Constructor -----------------------------------------------
        public TokenRing() : base(false)
        {
        }

        // --- Public Methods --------------------------------------------        
        public override void Acquire(Tuple<long, string> receivedLC = null)
        {
            if (!HasToken)
            {
                HasToken = true;
                if (NeedsToAccessCriticalSection)
                    Pool.Release();
                //else
                 //   Release();
            }
            else
                Console.WriteLine("Should not reach this line of code.");
        }
        public override void Release()
        {
            if (HasToken)
            {
                if (Node.Instance.Network.Count > 1)
                {
                    HasToken = false;
                    // skip all Nodes until you find the next after Address
                    // will return empty if there is no other node
                    int index = Node.Instance.Network.IndexOf(Node.Instance.Address);
                    string IP = Node.Instance.Network[(index + 1) % Node.Instance.Network.Count];

                    IRPCOperations API = Node.Instance.ConnectTo(IP);
                    if (API != null && !string.IsNullOrEmpty(IP))
                    {
                        API.take_token();
                        // if there are no other Nodes that want take the Token
                        // we wait a little so that we don't try to pass the token very fast
                        Thread.Sleep(50);
                    }
                    else
                        Console.WriteLine("Method: Release(). Problem trying to get the API");

                }

            }
        }
        public override void Run(int Value)
        {
            CurrentValue = Value;
            while (!((DateTime.Now - StartTime).TotalSeconds > 3.0)) // predefined period of 3 seconds
            {
                Console.WriteLine((DateTime.Now - StartTime).TotalSeconds);

                if (!HasToken)
                {
                    NeedsToAccessCriticalSection = true;
                    Pool.WaitOne();
                }
                // We have the token, enter critical section
                Update((MathOp)Enum.GetValues(typeof(MathOp)).GetValue(random.Next(Enum.GetValues(typeof(MathOp)).Length)),
                    (int)(random.NextDouble() * 100) + 1);
                PropagateState();
                // Done with critical section, release token
                NeedsToAccessCriticalSection = false;
                Release();                

                try
                {
                    int sleepInterval = 50 + (int)(random.NextDouble() * 200);
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
            Console.WriteLine("DONE!!!");
            HasToken = false;
            Console.WriteLine("Final result: " + CurrentValue);
        }
    }
}
