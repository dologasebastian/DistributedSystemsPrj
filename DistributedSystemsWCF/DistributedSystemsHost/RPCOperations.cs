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
        string[] join(string ip);
        /// <summary>
        /// Starts the calculation in this specific Node with the passed value.
        /// </summary>
        /// <param name="val">The value to start/continue the computation</param>
        /// <returns></returns>
        [OperationContract]
        int start_calculation(int val);
        /// <summary>
        /// Informs the node that it has the token
        /// </summary>
        /// <returns></returns>
        [OperationContract]
        int take_token();
        /// <summary>
        /// Propagate the state
        /// </summary>
        /// <param name="Operation">Paramerter of type MathOp</param>
        /// <param name="Value"></param>
        /// <returns></returns>
        [OperationContract]
        int propagate_state(int val);
        /// <summary>
        /// Called by a node when it wants to leave the network.
        /// </summary>
        /// <param name="ip">Sends its IP so the other nodes know which one left.</param>
        [OperationContract]
        int sign_off(string ip);
        /// <summary>
        /// Sends ok reply
        /// </summary>
        /// <returns></returns>
        [OperationContract]
        int raReply(string ip, long clock);
        /// <summary>
        /// Informs all the other nodes that this nodes wants access to the resource (Used for Recard & Agrawala)
        /// </summary>
        /// <param name="receivedLC">Received Lamport clock state</param>
        /// <returns></returns>
        [OperationContract]
        int raRequest(string ip, long clock);
    }

    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    public class RPCOperations : IRPCOperations
    {
        public string[] join(string ip)
        {
            List<string> Network = new List<string>(Node.Instance.Network);
            Node.Instance.Network.Add(ip); // Add requesting node to this network
            Console.WriteLine(ip + " joined the network.");
            // return an Array of all connected IPs to this Node, except the one that called Join.
            return Network.ToArray();
        }
        public int start_calculation(int val)
        {
            Console.WriteLine("Starting distributed calculation with value: " + val);
            Node.Instance.DistrCalc.Start(val);
            
            return 0;
        }
        public int take_token()
        {
            Console.WriteLine("Acquiring Token...");

            Node.Instance.DistrCalc.Acquire();

            return 0;
        }
        public int propagate_state(int val)
        {
            Console.WriteLine("Receiving update " + val);
            Node.Instance.DistrCalc.CurrentValue = val;

            return 0;
        }
        public int sign_off(string ip)
        {
            if (Node.Instance.Network.Contains(ip))
            {
                Node.Instance.Network.Remove(ip);
                Console.WriteLine("Node " + ip + " has signed off.");
                return 0;
            }
            else
            {
                Console.WriteLine("Node " + ip + " signed off, but it was not registered by this node. This should not have happened.");
                Console.WriteLine("Continuing normal execution...");
                return 1;
            }
        }
        public int raReply(string ip, long clock)
        {
            Tuple<long, string> receivedLC;
            Console.WriteLine("Reciving OK reply...");

            receivedLC = new Tuple<long, string>((long)clock, ip.ToString());

            Node.Instance.DistrCalc.Acquire(receivedLC);

            return 0;
        }
        public int raRequest(string ip, long clock)
        {
            Tuple<long, string> receivedLC = new Tuple<long, string>(clock, ip);
            try
            {
                DistributedCalculation algo = Node.Instance.DistrCalc;
                if (algo.GetType() == typeof(RicartAgrawala))
                {
                    Console.WriteLine("Process received request...");
                    ((RicartAgrawala)algo).ReceiveRequest(receivedLC);
                }
                else
                {
                    throw new Exception("RPCOperations: Method RequestToken is only available for Ricart & Agrawala algorithm.");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return 1;
            }

            return 0;
        }
    }
}
