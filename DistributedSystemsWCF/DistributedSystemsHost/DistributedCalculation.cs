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
        // --- Private Properties ---------------------------------------------
        private bool HasStarted = false;

        // --- Protected Properties -------------------------------------------
        protected static Random random = new Random();
        protected Object ThisLock = new Object();
        protected DateTime StartTime;
        //{
        //    get { lock (ThisLock) { return this.StartTime; } }
        //    set { lock (ThisLock) { this.StartTime = value; } }
        //}
        protected Semaphore Pool = new Semaphore(0, 1);

        // --- Public Properties ----------------------------------------------
        public int CurrentValue { get; set; }                           // The current value known by this Node that is calculated and passed through the network
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
        public abstract void Done();
        public abstract void Acquire();
        public abstract void Release();
        public void Start(int? StartValue = null)
        {
            if (HasStarted && StartValue != null)
                CurrentValue = (int)StartValue;

            if (StartValue == null) StartValue = (int)(random.NextDouble() * 100);
            if (StartTime != null) return;
            StartTime = DateTime.Now;
            Thread thread = new Thread(() => Run((int)StartValue));
            thread.Start();
        }

        // --- Protected Methods ---------------------------------------------
        protected void Update(MathOp op, int arg)
        {
            Console.WriteLine("Performing Operation: " + op + "(" + CurrentValue + ", " + arg + ")");
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
        protected void PropagateState()
        {
            Console.WriteLine("Sending current value to network");
            foreach (string ip in Node.Instance.Network)
            {
                if (!ip.Equals(Node.Instance.Address))
                {
                    IRPCOperations API = Node.Instance.ConnectTo(ip);
                    if (API != null)
                        API.PropagateState(CurrentValue);
                    else
                        Console.WriteLine("Method: PropagateState(). Problem trying to get the API for the client: " + ip);

                }
            }
        }                
    }

    public enum MathOp
    {
        Add = 1, 
        Sub = 2, 
        Mul = 3, 
        Div = 4
    }
}
