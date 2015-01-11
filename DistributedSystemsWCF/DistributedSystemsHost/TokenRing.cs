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
                {
                    Pool.Release();
                }
                else
                    Release();
            }
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
                        API.TakeToken();
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
                //lock (ThisLock)
                //{
                    if (!this.HasToken)
                    {
                        NeedsToAccessCriticalSection = true;
                        Pool.WaitOne();
                    }
                    // We have the token, enter critical section
                    MathOp op = (MathOp)Enum.GetValues(typeof(MathOp)).GetValue(random.Next(Enum.GetValues(typeof(MathOp)).Length));
                    int arg = (int)(random.NextDouble() * 100) + 1; // never divide by zero
                    Update(op, arg);
                    PropagateState();
                    // Done with critical section, release token
                    Release();
                    NeedsToAccessCriticalSection = false;
                //}

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
