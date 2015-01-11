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
        private LamportClock LC;
        private Queue<string> WaitingQueue;
        private List<string> ReceivedOKReplies;
        private bool CurrentlyUsingResource = false;

        // --- Constructor -----------------------------------------------
        public RicartAgrawala() : base(false)
        {
            // Initialize the Lamport clock
            this.LC = new LamportClock(Node.Instance.Address);
            
            this.WaitingQueue = new Queue<string>();
            
            ResetReceivedOKReplies();
        }

        // --- Public Methods -------------------------------------------
        /// <summary>
        /// Process received reequest from another node in the network
        /// </summary>
        /// <param name="receivedLC">Received Lamport clock state</param>
        public void ReceiveRequest(Tuple<long, string> receivedLC)
        {
            string requesterIP = receivedLC.Item2;
            if (NeedsToAccessCriticalSection)
            {
                if (CurrentlyUsingResource)
                {
                    WaitingQueue.Enqueue(requesterIP);
                }
                else
                {
                    if (LC.Compare(receivedLC) < 0)
                    {
                        WaitingQueue.Enqueue(requesterIP);
                    }
                    else
                    {
                        LC.EventReceive(receivedLC);
                        SendOKReply(requesterIP);
                    }
                }
            }
            else
            {
                LC.EventReceive(receivedLC);
                SendOKReply(requesterIP);
            }
        }
        // ----------- Inherited Public Methods -------------------------
        /// <summary>
        /// Processes an OK reply from some node in the network
        /// </summary>
        public override void Acquire(Tuple<long, string> receivedLC)
        {
            bool allReplied;
            List<string> network = Node.Instance.Network;

            // Update Lamport clock
            LC.EventReceive(receivedLC);

            // Add the reply to the list of received OK replies
            ReceivedOKReplies.Add(receivedLC.Item2);

            // Check if all nodes in the network have already replied with OK
            // TODO: This can lead to deadlock if a new node joins the network!!!
            allReplied = network.AsParallel().All(x => ReceivedOKReplies.Contains(x));

            // If yes, allow access
            if (allReplied)
            {
                Pool.Release();
            }
        }
        /// <summary>
        /// Sends an OK reply to the all elements in the WaitingQueue
        /// </summary>
        public override void Release()
        {
            // TODO: In case we consider each iteration as an event, we should put the LC.EventSend() in the foreach loop
            Tuple<long, string> lcState = LC.EventSend();
            
            foreach (string ip in WaitingQueue)
            {
                SendOKReply(ip, lcState);
            }

            WaitingQueue.Clear();
        }
        public override void Run(int Value)
        {
            CurrentValue = Value;
            while (!((DateTime.Now - StartTime).TotalSeconds > 3.0)) // predefined period of 3 seconds
            {
                //lock (ThisLock)
                //{
                NeedsToAccessCriticalSection = true;
                // Request access to all nodes in the network
                RequestToAll();
                Pool.WaitOne();
                // We have the token, enter critical section
                CurrentlyUsingResource = true;
                MathOp op = (MathOp)Enum.GetValues(typeof(MathOp)).GetValue(random.Next(Enum.GetValues(typeof(MathOp)).Length));
                int arg = (int)(random.NextDouble() * 100) + 1; // never divide by zero
                Update(op, arg);
                PropagateState();
                CurrentlyUsingResource = false;
                // Update Lamport clock for local event
                LC.EventLocal();
                // Done with critical section, release token
                Release();
                NeedsToAccessCriticalSection = false;
                //}

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
            Console.WriteLine("Final result: " + CurrentValue);
        }

        // --- Private Methods -------------------------------------------------
        /// <summary>
        /// Send request for access to the resource to all nodes in the network
        /// </summary>
        private void RequestToAll()
        {
            Node node = Node.Instance;
            List<string> network = node.Network;

            ResetReceivedOKReplies();

            foreach (var ip in network)
            {
                IRPCOperations API = node.ConnectTo(ip);
                if (API != null)
                {
                    Tuple<long, string> lcState = LC.EventSend();
                    API.raRequest(lcState.Item1, lcState.Item2);
                }
                else
                {
                    Console.WriteLine("Method: RequestToAll(). Problem trying to get the API for the client: " + ip);
                }
            }
        }

        private void SendOKReply(string ip, Tuple<long, string> lcState = null)
        {
            if (ip != Node.Instance.Address)
            {
                IRPCOperations API = Node.Instance.ConnectTo(ip);
                if (API != null)
                {
                    Tuple<long, string> tempLcState = (lcState != null) ? lcState : LC.EventSend();

                    API.raReply(tempLcState.Item1, tempLcState.Item2);
                }
                else
                {
                    Console.WriteLine("Method: SendOKReply(). Problem trying to get the API");
                }
            }
            else
            {
                Tuple<long, string> tempLcState = (lcState != null) ? lcState : LC.EventSend();
                Console.WriteLine("Receiving OK reply from self...");

                Node.Instance.DistrCalc.Acquire(tempLcState);
            }
        }

        private void ResetReceivedOKReplies()
        {
            ReceivedOKReplies = new List<string>();
        }
    }
}
