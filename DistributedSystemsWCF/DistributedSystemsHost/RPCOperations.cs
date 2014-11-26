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
        void StartCalculation(int Value);
        /// <summary>
        /// Informs the node that it has the token
        /// </summary>
        /// <returns></returns>
        [OperationContract]
        void TakeToken();
        /// <summary>
        /// Propagate the state
        /// </summary>
        /// <param name="Operation">Paramerter of type MathOp</param>
        /// <param name="Value"></param>
        /// <returns></returns>
        [OperationContract]
        void PropagateState(int CurrentValue);
    }

    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    public class RPCOperations : IRPCOperations
    {
        public string[] Join(string IP)
        {
            List<string> Network = new List<string>(Node.Instance.Network);
            Node.Instance.Network.Add(IP); // Add requesting node to this network
            // return an Array of all connected IPs to this Node, except the one that called Join.
            return Network.ToArray();
        }
        public void StartCalculation(int Value)
        {
            Console.WriteLine("Starting distributed calculation with value: " + Value);
            Node.Instance.DistrCalc.Start(Value);
        }
        public void TakeToken()
        {
            Console.WriteLine("Acquiring Token...");
            Node.Instance.DistrCalc.Acquire();
        }
        public void PropagateState(int CurrentValue)
        {
            Console.WriteLine("Receiving update " + CurrentValue);
            Node.Instance.DistrCalc.CurrentValue = CurrentValue;
        }
    }
}
