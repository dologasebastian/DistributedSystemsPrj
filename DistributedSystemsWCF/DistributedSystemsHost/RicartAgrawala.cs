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
        // --- Private Properties ----------------------------------------
        private LamportClock Clock;
        private bool Acquiring = false;
        private int SentTimeStamp;
        private bool IsLocked = false;
        private CountdownLatch Latch;
        private HashSet<string> Replied = new HashSet<string>();
        private Queue<string> PendingReplies = new Queue<string>();

        public RicartAgrawala()
            : base(false)
        {
            Clock = new LamportClock(Node.Instance.Address);
        }

        public RicartAgrawala(bool initiallyLocked)
            : base(false)
        {
            Clock = new LamportClock(Node.Instance.Address);
            IsLocked = initiallyLocked;
            if (initiallyLocked)
            {
                Latch = new CountdownLatch(0);
            }
        }

        public override void Run(int Value)
        {
            CurrentValue = Value;

            while (true) // predefined period of seconds
            {
                NeedsToAccessCriticalSection = true;
                RequestToAll();

                if (!((DateTime.Now - StartTime).TotalSeconds > DURATION))
                {
                    Console.WriteLine((DateTime.Now - StartTime).TotalSeconds);

                    MathOp op = (MathOp)Enum.GetValues(typeof(MathOp)).GetValue(random.Next(Enum.GetValues(typeof(MathOp)).Length));
                    int arg = (int)(random.NextDouble() * 100) + 1; // never divide by zero
                    Update(op, arg);
                    PropagateState(op, arg);

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

        public void RequestToAll()
        {
            if (IsLocked) return;
            System.Diagnostics.Debug.Assert(!Acquiring);
            Acquiring = true;
            Clock.EventSend();
    
            // Should receive a response from every other node in the network
            Latch = new CountdownLatch(Node.Instance.Network.Count - 1);
            Replied.Clear();
    
            SentTimeStamp = Clock.ToTuple().Item1;
            foreach (string ip in Node.Instance.Network)
            {
                if (ip != Node.Instance.Address)
                {
                    IRPCOperations API = Node.Instance.ConnectTo(ip);
                    Console.WriteLine("RPC: Send request to " + ip + " (Clock: " + SentTimeStamp + ")...");
                    API.raRequest(Node.Instance.Address, SentTimeStamp);
                }
            }
    
            try
            {
                // When all other nodes reply this will move forward
                if (Node.Instance.Network.Count > 1)
                {
                    Latch.Wait();
                }
                /*while (!Pool.Equals(Node.Instance.Network.Count - 1))
                {
                    Thread.Sleep(2);
                    Console.WriteLine("Received replies from: " + String.Concat(Replied, ", "));
                    Console.WriteLine("Current count: " + Pool.ToString());
                }*/
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
            }
            Acquiring = false;
            IsLocked = true;
            Latch = null;
        }


        public void MessageReceived(string ip, int k)
        {
            Clock.EventReceive(new Tuple<int, string>(k, ip));
    
            // If not interested in critical section reply with 'OK'
            if (!NeedsToAccessCriticalSection)
            {
                Console.WriteLine("RPC: Sending OK reply to " + ip + "...");
                Node.Instance.ConnectTo(ip).raReply(Node.Instance.Address);
            // Wants critical section but doesn't have the lock yet
            }
            else if (Acquiring)
            {
                // I win (the result of this comparison should be the same on the sender as well
                if (new LamportClock(SentTimeStamp, Node.Instance.Address).Compare(new Tuple<int, string>(k, ip)) < 0)
                {
                    PendingReplies.Enqueue(ip);
                // I lose
                }
                else
                {
                    Console.WriteLine("RPC: Sending OK reply to " + ip + "...");
                    Node.Instance.ConnectTo(ip).raReply(Node.Instance.Address);
                }
            // In the resource
            }
            else
            {
                PendingReplies.Enqueue(ip);
            }

            // TODO: Unlock the node after the first received request
            IsLocked = false;
        }

        public override void Acquire(string ip)
        {
            if (!Acquiring)
            {
                return;
            }
            Clock.EventLocal();
            Replied.Add(ip);

            if (Latch != null && Latch.GetCount() > 0)
            {
                Latch.CountDown();
            }
            else
            {
                Console.WriteLine("Error");
            }
        }
    
        public override void Release()
        {
            // send all pending messages
            IsLocked = false;
            while (PendingReplies.Count > 0)
            {
                string ip = PendingReplies.Dequeue();
                Node.Instance.ConnectTo(ip).raReply(Node.Instance.Address);
            }
        }
    
        public bool HasLock()
        {
            return IsLocked;
        }

        protected override void Reset()
        {
            CurrentValue = 0;
            NeedsToAccessCriticalSection = false;
            this.HasToken = false;
            StartTime = DateTime.Now;
            HasStarted = false;

            Clock = new LamportClock(Node.Instance.Address);
            Acquiring = false;
            IsLocked = false;
            Latch = new CountdownLatch(0);
            Replied = new HashSet<string>();
            PendingReplies = new Queue<string>();
        }
    }
}
