﻿using System;
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
            Reset();
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
                        //LC.EventReceive(receivedLC);
                        SendOKReply(requesterIP);
                    }
                }
            }
            else
            {
                //LC.EventReceive(receivedLC);
                SendOKReply(requesterIP);
            }
        }
        // ----------- Inherited Public Methods -------------------------
        /// <summary>
        /// Processes an OK reply from some node in the network
        /// </summary>
        public override void Acquire(string ip = null)
        {
            bool allReplied;
            List<string> network = Node.Instance.Network;

            // Add the reply to the list of received OK replies
            ReceivedOKReplies.Add(ip);

            // Check if all nodes in the network have already replied with OK
            // TODO: This can lead to deadlock if a new node joins the network!!!
            allReplied = network.All(x => ReceivedOKReplies.Contains(x));

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
            foreach (string ip in WaitingQueue)
            {
                SendOKReply(ip);
            }

            WaitingQueue.Clear();
        }
        public override void Run(int Value)
        {
            CurrentValue = Value;
            while (!((DateTime.Now - StartTime).TotalSeconds > 20.0)) // predefined period of 3 seconds
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
                // Update Lamport clock for local event
                LC.EventLocal();
                PropagateState(op, arg);
                CurrentlyUsingResource = false;
                NeedsToAccessCriticalSection = false;
                // Done with critical section, release token
                Release();
                //}

                try
                {
                    int sleepInterval = 500 + (int)(random.NextDouble() * 500);
                    //Console.WriteLine("Sleeping for " + sleepInterval);
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

            Reset();
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

            Tuple<long, string> lcState = LC.EventSend();
            
            foreach (var ip in network)
            {
                IRPCOperations API = node.ConnectTo(ip);
                if (API != null)
                {
                    API.raRequest(lcState.Item2, lcState.Item1);
                }
                else
                {
                    Console.WriteLine("Method: RequestToAll(). Problem trying to get the API for the client: " + ip);
                }
            }
        }

        private void SendOKReply(string ip)
        {
            if (ip != Node.Instance.Address)
            {
                IRPCOperations API = Node.Instance.ConnectTo(ip);
                if (API != null)
                {
                    API.raReply(LC.EventSend().Item2);
                }
                else
                {
                    Console.WriteLine("Method: SendOKReply(). Problem trying to get the API");
                }
            }
            else
            {
                Console.WriteLine("Receiving OK reply from self...");

                Node.Instance.DistrCalc.Acquire(LC.EventSend().Item2);
            }
        }

        private void ResetReceivedOKReplies()
        {
            ReceivedOKReplies = new List<string>();
        }

        protected override void Reset()
        {
            CurrentValue = 0;
            NeedsToAccessCriticalSection = false;
            this.HasToken = false;
            Pool = new Semaphore(0, 1);
            StartTime = DateTime.Now;
            HasStarted = false;

            // Initialize the Lamport clock
            this.LC = new LamportClock(Node.Instance.Address);

            this.WaitingQueue = new Queue<string>();

            ResetReceivedOKReplies();
        }
    }
}
