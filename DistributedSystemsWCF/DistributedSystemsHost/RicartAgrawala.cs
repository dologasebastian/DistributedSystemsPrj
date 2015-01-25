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
        private long SentTimeStamp;
        private bool IsLocked = false;
        //private CountdownEvent Latch;
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
                Pool = new Semaphore(0, 1);
            }
        }

        public bool ShouldStop()
        {
            return (DateTime.Now - StartTime).TotalSeconds > DURATION;
        }

        public override void Run(int Value)
        {
            CurrentValue = Value;

            while (!ShouldStop()) // predefined period of seconds
            {
                Console.WriteLine((DateTime.Now - StartTime).TotalSeconds);

                NeedsToAccessCriticalSection = true;
                RequestToAll();
    
                MathOp op = (MathOp)Enum.GetValues(typeof(MathOp)).GetValue(random.Next(Enum.GetValues(typeof(MathOp)).Length));
                int arg = (int)(random.NextDouble() * 100) + 1; // never divide by zero
                Update(op, arg);
                PropagateState(op, arg);
    
                NeedsToAccessCriticalSection = false;
                Release();

                // consider this as extra work it has to do that is not in the critical section
                SleepCurrentThread();
            }
            //Finished = true;
            Done();
        }

        public void RequestToAll()
        {
            if (IsLocked) return;
            System.Diagnostics.Debug.Assert(!Acquiring);
            Acquiring = true;
            Clock.EventSend();
    
            // Should receive a response from every other node in the network
            //Latch = new CountdownEvent(Node.Instance.Network.Count - 1);
            Pool = new Semaphore(0, Node.Instance.Network.Count < 2 ? 1 : (Node.Instance.Network.Count - 1));
            Replied.Clear();
    
            SentTimeStamp = Clock.ToTuple().Item1;
            foreach (string ip in Node.Instance.Network)
            {
                if (ip != Node.Instance.Address)
                {
                    IRPCOperations API = Node.Instance.ConnectTo(ip);
                    API.raRequest(Node.Instance.Address, SentTimeStamp);
                }
            }
    
            try
            {
                // When all other nodes reply this will move forward
                while (Pool.Equals(Node.Instance.Network.Count - 1))//(!Latch.Wait(3000))
                {
                    Thread.Sleep(3000);
                    Console.WriteLine("Received replies from: " + String.Concat(Replied, ", "));
                    Console.WriteLine("Current count: " + Pool.ToString());
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
            }
            Acquiring = false;
            IsLocked = true;
            Pool = null;
        }
    
    
        public void MessageReceived(string ip, long k)
        {
            Clock.EventReceive(new Tuple<long, string>(k, ip));
    
            // If not interested in critical section reply with 'OK'
            if (!NeedsToAccessCriticalSection)
            {
                Node.Instance.ConnectTo(ip).raReply(Node.Instance.Address);
            // Wants critical section but doesn't have the lock yet
            }
            else if (Acquiring)
            {
                // I win (the result of this comparison should be the same on the sender as well
                if (new LamportClock(SentTimeStamp, Node.Instance.Address).Compare(new Tuple<long, string>(k, ip)) < 0)
                {
                    PendingReplies.Enqueue(ip);
                // I lose
                }
                else
                {
                    Node.Instance.ConnectTo(ip).raReply(Node.Instance.Address);
                }
            // In the resource
            }
            else
            {
                PendingReplies.Enqueue(ip);
            }
        }

        public override void Acquire(string ip)
        {
            if (!Acquiring)
            {
                return;
            }
            Clock.EventLocal();
            Replied.Add(ip);

            if (Pool != null)
            {
                Pool.Release();
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
            if (Pool != null) Pool.Dispose();
            Pool = new Semaphore(0, 1);
            StartTime = DateTime.Now;
            HasStarted = false;

            Clock = new LamportClock(Node.Instance.Address);
            Acquiring = false;
            IsLocked = false;
            //Latch; ??
            Replied = new HashSet<string>();
            PendingReplies = new Queue<string>();
        }
    }
}
