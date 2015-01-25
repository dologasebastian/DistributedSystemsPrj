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
        public override void Acquire(string ip = null)
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
        public void ReleaseAll()
        {
            
        }
        public override void Run(int Value)
        {
            CurrentValue = Value;
            while (true) 
            {
                if (!HasToken)
                {
                    NeedsToAccessCriticalSection = true;
                    Pool.WaitOne();
                }
                if (!((DateTime.Now - StartTime).TotalSeconds > DURATION)) // predefined period of seconds
                {
                    Console.WriteLine((DateTime.Now - StartTime).TotalSeconds);

                    // We have the token, enter critical section
                    MathOp op = (MathOp)Enum.GetValues(typeof(MathOp)).GetValue(random.Next(Enum.GetValues(typeof(MathOp)).Length));
                    int arg = (int)(random.NextDouble() * 100) + 1;
                    Update(op, arg);
                    PropagateState(op, arg);
                    // Done with critical section, release token
                    NeedsToAccessCriticalSection = false;
                    Release();

                    // consider this as extra work it has to do that is not in the critical section
                    SleepCurrentThread();
                }
                else
                {
                    // we put sleep to make sure other nodes will have finished the calculation period
                    // and will not start anything new. When we do Release() they will all just print the resut.
                    SleepCurrentThread(200);
                    Release();
                    Done();
                    break;
                }
            }
        }
        protected override void Reset()
        {
            CurrentValue = 0;
            NeedsToAccessCriticalSection = false;
            this.HasToken = false;
            Pool = new Semaphore(0, 1);
            StartTime = DateTime.Now;
            HasStarted = false;
        }
    }
}
