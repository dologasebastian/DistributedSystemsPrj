using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DistributedSystems
{
    [ServiceContract]
    public interface IRPCOperations
    {
        /// <summary>
        /// A node sends this message to join the current network
        /// Sends its IP address and gets back a List of the IPs
        /// *WITHOUT* its own
        /// </summary>
        /// <param name="ip"></param>
        /// <returns></returns>
        [OperationContract]
        string[] Join(string IP);
        /// <summary>
        /// Starts the calculation in this specific Node with the passed value.
        /// </summary>
        /// <param name="val">The value to start/continue the computation</param>
        /// <returns></returns>
        [OperationContract]
        int StartCalculation(int Value);
        /// <summary>
        /// Informs the node that it has the token
        /// </summary>
        /// <returns></returns>
        [OperationContract]
        int TakeToken(long lcCounter = -1, string lcIP = "");
        /// <summary>
        /// Informs all the other nodes that this nodes wants access to the resource (Used for Recard & Agrawala)
        /// </summary>
        /// <param name="receivedLC">Received Lamport clock state</param>
        /// <returns></returns>
        [OperationContract]
        int RequestToken(long lcCounter, string lcIP);
        /// <summary>
        /// Propagate the state
        /// </summary>
        /// <param name="Operation">Paramerter of type MathOp</param>
        /// <param name="Value"></param>
        /// <returns></returns>
        [OperationContract]
        int PropagateState(int CurrentValue);
        /// <summary>
        /// Called by a node when it wants to leave the network.
        /// </summary>
        /// <param name="IP">Sends its IP so the other nodes know which one left.</param>
        [OperationContract]
        int SignOff(string IP);
    }

    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    public class RPCOperations : IRPCOperations
    {
        public string[] Join(string IP)
        {
            List<string> Network = new List<string>(Node.Instance.Network);
            Node.Instance.Network.Add(IP); // Add requesting node to this network
            Console.WriteLine(IP + " joined the network.");
            // return an Array of all connected IPs to this Node, except the one that called Join.
            return Network.ToArray();
        }
        public int StartCalculation(int Value)
        {
            Console.WriteLine("Starting distributed calculation with value: " + Value);
            Node.Instance.DistrCalc.Start(Value);
            
            return 0;
        }
        public int TakeToken(long lcCounter = -1, string lcIP = "")
        {
            Tuple<long, string> receivedLC = null;
            Console.WriteLine("Acquiring Token...");

            if (lcCounter >= 0 && lcIP != "")
            {
                receivedLC = new Tuple<long, string>((long)lcCounter, lcIP.ToString());
            }
            Node.Instance.DistrCalc.Acquire(receivedLC);
            
            return 0;
        }
        // TODO: RPC shows some strange behavior when passing the arguments
        public int RequestToken(long lcCounter, string lcIP)
        {
            Tuple<long, string> receivedLC = new Tuple<long, string>(lcCounter, lcIP);
            //try
            //{
                DistributedCalculation algo = Node.Instance.DistrCalc;
                //if (algo.GetType() == typeof(RicartAgrawala))
                //{
                    Console.WriteLine("Process received request...");
                    ((RicartAgrawala)algo).ReceiveRequest(receivedLC);
                //}
                //else
                //{
                //    throw new Exception("RPCOperations: Method RequestToken is only available for Ricart & Agrawala algorithm.");
                //}
            //}
            //catch (Exception e)
            //{
            //    Console.WriteLine(e.ToString());
            //}

            return 0;
        }
        public int PropagateState(int CurrentValue)
        {
            Console.WriteLine("Receiving update " + CurrentValue);
            Node.Instance.DistrCalc.CurrentValue = CurrentValue;

            return 0;
        }
        public int SignOff(string IP)
        {
            if (Node.Instance.Network.Contains(IP))
            {
                Node.Instance.Network.Remove(IP);
                Console.WriteLine("Node " + IP + " has signed off.");
                return 0;
            }
            else
            {
                Console.WriteLine("Node " + IP + " signed off, but it was not registered by this node. This should not have happened.");
                Console.WriteLine("Continuing normal execution...");
                return 1;
            }
        }
    }
}
