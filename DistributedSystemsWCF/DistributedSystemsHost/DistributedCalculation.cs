using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DistributedSystems
{
    public abstract class DistributedCalculation : MutualExclusionAlgorithm
    {
        // --- Protected Properties -------------------------------------------
        protected static readonly int DURATION = 3;
        protected bool HasStarted = false;
        protected static Random random = new Random();
        protected Object ThisLock = new Object();
        protected DateTime StartTime;
        //{
        //    get { lock (ThisLock) { return this.StartTime; } }
        //    set { lock (ThisLock) { this.StartTime = value; } }
        //}
        protected Semaphore Pool = new Semaphore(0, 1);

        // --- Public Properties ----------------------------------------------
        public int CurrentValue { get; set; }   // The current value known by this Node that is calculated and passed through the network
        public bool NeedsToAccessCriticalSection {get;set;}
        //{ 
        //    get { lock (ThisLock) { return this.NeedsToAccessCriticalSection; } }
        //    set { lock (ThisLock) { this.NeedsToAccessCriticalSection = value; } }
        //}                             // Bool that shows if the class is busy doing stuff
        public bool HasToken { get; set; }

        // --- Constructors --------------------------------------------------
        public DistributedCalculation(bool HasToken)
        {
            CurrentValue = 0;
            NeedsToAccessCriticalSection = false;
            this.HasToken = HasToken;
        }

        // --- Public Abstract Methods ---------------------------------------
        public abstract void Run(int Value);
        protected abstract void Reset();
        public abstract void Acquire(string ip = null);
        public abstract void Release();
        protected void Done()
        {
            Console.WriteLine("----------------------------------------------------------");
            Console.WriteLine("DONE!!!");
            Console.WriteLine("Final result: " + CurrentValue);
            Console.WriteLine("----------------------------------------------------------");
            Reset();
        }
        protected void SleepCurrentThread(int? val = null)
        {
            try
            {
                int sleepInterval = val != null ? (int)val : 100 + (int)(random.NextDouble() * 200);
                Thread.Sleep(sleepInterval);
            }
            catch (ThreadInterruptedException e)
            {
                Console.WriteLine(e.StackTrace);
            }
        }
        public void Start(int? StartValue = null)
        {
            if (HasStarted && StartValue != null)
                CurrentValue = (int)StartValue;

            if (StartValue == null) StartValue = (int)(random.NextDouble() * 100);
            //if (StartTime != DateTime.MinValue) return;
            StartTime = DateTime.Now;
            Thread thread = new Thread(() => Run((int)StartValue));
            thread.Start();
        }
        public void Update(MathOp op, int arg)
        {
            lock (ThisLock)
            {
                Console.WriteLine("Performing: " + op + "(" + CurrentValue + ", " + arg + ")");
                switch (op)
                {
                    case MathOp.Add:
                        CurrentValue += arg;
                        break;
                    case MathOp.Sub:
                        CurrentValue -= arg;
                        break;
                    case MathOp.Mul:
                        CurrentValue *= arg;
                        break;
                    case MathOp.Div:
                        CurrentValue /= arg;
                        break;
                    default:
                        CurrentValue += arg;
                        break;
                }
            }
        }

        // --- Protected Methods ---------------------------------------------        
        protected void PropagateState(MathOp op, int val)
        {
            Console.WriteLine("Sending:    (" + ((MathOp)Enum.Parse(typeof(MathOp), op.ToString())).ToString() + ", " + val + ")");
            foreach (string ip in Node.Instance.Network)
            {
                if (!ip.Equals(Node.Instance.Address))
                {
                    IRPCOperations API = Node.Instance.ConnectTo(ip);
                    if (API != null)
                        API.propagate_state((int)op, val);
                    else
                        Console.WriteLine("Method: PropagateState(). Problem trying to get the API for the client: " + ip);

                }
            }
        }                
    }

    public enum MathOp
    {
        Add = 0, 
        Sub = 1, 
        Mul = 2, 
        Div = 3
    }
}
