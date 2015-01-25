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
        [OperationContract(Action="pdc.join")]
        string join(string ip);
        /// <summary>
        /// Starts the calculation in this specific Node with the passed value.
        /// </summary>
        /// <param name="val">The value to start/continue the computation</param>
        /// <returns></returns>
        [OperationContract(Action = "pdc.start_calculation")]
        int start_calculation(int val, string alg);
        /// <summary>
        /// Informs the node that it has the token
        /// </summary>
        /// <returns></returns>
        [OperationContract(Action = "pdc.take_token")]
        int take_token();
        /// <summary>
        /// Propagate the state
        /// </summary>
        /// <param name="Operation">Paramerter of type MathOp</param>
        /// <param name="Value"></param>
        /// <returns></returns>
        [OperationContract(Action = "pdc.propagate_state")]
        int propagate_state(int op, int val);
        /// <summary>
        /// Called by a node when it wants to leave the network.
        /// </summary>
        /// <param name="ip">Sends its IP so the other nodes know which one left.</param>
        [OperationContract(Action = "pdc.sign_off")]
        int sign_off(string ip);
        /// <summary>
        /// Sends ok reply
        /// </summary>
        /// <returns></returns>
        [OperationContract(Action = "pdc.ra_reply")]
        int raReply(string ip);
        /// <summary>
        /// Informs all the other nodes that this nodes wants access to the resource (Used for Recard & Agrawala)
        /// </summary>
        /// <param name="receivedLC">Received Lamport clock state</param>
        /// <returns></returns>
        [OperationContract(Action = "pdc.ra_request")]
        int raRequest(string ip, long clock);
        [OperationContract(Action = "pdc.test")]
        int test(int val);
    }

    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    public class RPCOperations : IRPCOperations
    {
        public string join(string ip)
        {
            string Network = "";
            if (!Node.Instance.Network.Contains(ip))
            {
                Node.Instance.Network.Add(ip); // Add requesting node to this network
                //Node.Instance.Network = new HashSet<string>(Node.Instance.Network).ToList();
                Console.WriteLine(ip + " joined.");

                foreach (string i in Node.Instance.Network.Where(x => x != ip))
                {
                    Network += i + ",";
                }
                Node.Instance.Network = Node.Instance.Network
                            .OrderBy(x => int.Parse(x.Split('.')[0]))
                            .ThenBy(x => int.Parse(x.Split('.')[1]))
                            .ThenBy(x => int.Parse(x.Split('.')[2]))
                            .ThenBy(x => int.Parse(x.Split('.')[3])).ToList();
            }            
            // return an Array of all connected IPs to this Node, except the one that called Join.
            return Network;
        }
        public int start_calculation(int val, string alg)
        {            
            Node.Instance.SelectAlgorithm(alg);
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
        public int propagate_state(int op, int val)
        {
            Console.WriteLine("Receiving: Operator(" + ((MathOp)Enum.Parse(typeof(MathOp), op.ToString())).ToString() + "), Argument(" + val + ")");
            Node.Instance.DistrCalc.Update((MathOp)Enum.Parse(typeof(MathOp), op.ToString()), val);

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
        public int raReply(string ip)
        {
            Console.WriteLine("Reciving OK reply from " + ip + "...");

            Node.Instance.DistrCalc.Acquire(ip);

            return 0;
        }
        public int raRequest(string ip, long clock)
        {
            try
            {
                DistributedCalculation algo = Node.Instance.DistrCalc;
                if (algo.GetType() == typeof(RicartAgrawala))
                {
                    Console.WriteLine("Process received request from " + ip + "...");
                    ((RicartAgrawala)algo).MessageReceived(ip, clock);
                }
                else
                {
                    throw new Exception("RPCOperations: Method raRequest is only available for Ricart & Agrawala algorithm.");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return 1;
            }

            return 0;
        }
        public int test(int val)
        {
            Console.WriteLine("Received: " + val);
            return 1;
        }
    }
}
