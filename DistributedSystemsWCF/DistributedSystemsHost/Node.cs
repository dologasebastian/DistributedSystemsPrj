using Microsoft.Samples.XmlRpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

/*
     * Other useful information regarding Proxies and ChannelFactory
     * http://www.codeproject.com/Tips/558163/Difference-between-Proxy-and-Channel-Factory-in-WC
     

      CMD: use this to generate Proxy using CMD and svcutil.
      svcutil /language:cs /t:code http://<service_url> /out:<file_name>.cs /config:<file_name>.config    
      "C:\Program Files (x86)\Microsoft SDKs\Windows\v7.0A\Bin\svcutil.exe" /language:cs /t:code http://localhost:4321/DistributedSystemsService /out:DistributedSystemsProxy.cs /config:DistributedSystemsProxy.config     
    */

namespace DistributedSystems
{
    public class Node
    {
        // --- Private Properties ----------------------------------------
        private ServiceHost RPCServiceHost = null;                  // The host class
        private Uri ListenUri = null;                               // The URI where the server listens to incoming connetions

        // --- Public Properties -----------------------------------------
        public static Node Instance = null;                         // Singleton Class
        public static int Port = 3105;                              // All nodes use the same port.
        public List<string> Network { get; set; }                   // List of addresses of all connected Nodes (including this one)
        public string Address { get; set; }                         // The address of the current Node.
        public DistributedCalculation DistrCalc { get; set; }       // Class that does the distributed calculation

        // --- Constructor -----------------------------------------------
        public Node(string IP)
        {
            try
            {
                // initialize local variables
                Address = IP;
                Network = new List<string>() { Address };
                DistrCalc = new TokenRing();

                // Create the server with the desired IP and Port.
                //Uri baseAddress = new UriBuilder(Uri.UriSchemeHttp, Environment.MachineName, Port, "/").Uri;
                Uri baseAddress = new UriBuilder(Uri.UriSchemeHttp, IP, Port, "/").Uri;
                RPCServiceHost = new ServiceHost(typeof(RPCOperations));
                var epXmlRpc = RPCServiceHost.AddServiceEndpoint(typeof(IRPCOperations), new WebHttpBinding(WebHttpSecurityMode.None), new Uri(baseAddress.AbsoluteUri));
                epXmlRpc.Behaviors.Add(new XmlRpcEndpointBehavior());
                ListenUri = epXmlRpc.ListenUri;
                Console.WriteLine("Initialized Node succesfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                RPCServiceHost.Close();
            }
            Instance = this;
        }

        // --- Public Generic Methods ------------------------------------
        /// <summary>
        /// Start the host
        /// </summary>
        public void Start()
        {
            RPCServiceHost.Open();
            Console.WriteLine("IRPCOperations API endpoint listening at {0}", ListenUri);
        }
        /// <summary>
        /// Stop this host
        /// </summary>
        public void Stop()
        {
            RPCServiceHost.Close();
            Console.WriteLine("Node listening at {0} closed succesfully", ListenUri);
        }
        /// <summary>
        /// Return True if Join was succeesfull.
        /// </summary>
        /// <param name="IP">the IP used to find the Node. Port is assumed identical</param>
        /// <returns></returns>
        public bool Join(string IP)
        {            
            IRPCOperations API = ConnectTo(IP);
            if (API != null) // if succesful connection Join the entire network
            {
                string[] nodes = API.Join(Address);
                if (nodes != null && nodes.Count() > 0)
                    Network.AddRange(nodes);
                // Nodes should have an ordering in the ring
                // They are ordered by their IP addresses
                Network.OrderBy(x => int.Parse(x.Split('.').First()))
                    .ThenBy(x => int.Parse(x.Split('.')[1]))
                    .ThenBy(x => int.Parse(x.Split('.')[2]))
                    .ThenBy(x => int.Parse(x.Split('.').Last())).ToList();
                return true;
            }            
            return false;
        }
        /// <summary>
        /// Connects to a Node using a specific IP.
        /// The new API to the new Node can be found in the list APIs if connection succesful.
        /// </summary>
        /// <param name="IP">The IP to which you want to connect to</param>
        /// <returns>True if connection could be made, False if not.</returns>
        public IRPCOperations ConnectTo(string IP)
        {
            Uri NodeAddress = new UriBuilder(Uri.UriSchemeHttp, IP, Port, "/").Uri;

            ChannelFactory<IRPCOperations> ChannelFactory = new ChannelFactory<IRPCOperations>(
                new WebHttpBinding(WebHttpSecurityMode.None), new EndpointAddress(NodeAddress));
            ChannelFactory.Endpoint.Behaviors.Add(new XmlRpcEndpointBehavior());
            // check if channel was created succesfully
            if ((ChannelFactory != null) || (ChannelFactory.State != CommunicationState.Faulted))
            {
                return ChannelFactory.CreateChannel();
            }
            Console.WriteLine("Could not ConnectTo: " + IP + " Might crash.");
            return null;
        }
        /// <summary>
        /// Select the desired Algorithm.
        /// </summary>
        /// <param name="Alg"></param>
        public void SelectAlgorithm(string Alg)
        {
            if (Alg == "-tk")
                DistrCalc = new TokenRing();
            else if (Alg == "-ra")
                DistrCalc = new RicartAgrawala();
            else
            {
                Console.WriteLine("Unkown algorithm. Using TokenRing by default");
            }
        }
        /// <summary>
        /// Method that starts one of the implemented Algorithms
        /// </summary>
        /// <param name="Algorithm"></param>
        public void StartCalculation(int StartingValue = 0)
        {
            // starting calculation from this Node. We know this for sure, so we give this Node the Token.
            DistrCalc.HasToken = true;

            // Inform other nodes that we are about to start the calculation
            Console.WriteLine("Telling other nodes to prepare for a distributed calculation.");
            // StartCalculation for each connected Node. 
            foreach (string ip in Network)
            {
                // On receiving this message each node will initialize a DC object with initial value
                // And they will all block until they receive the token to perform an operation
                IRPCOperations API = ConnectTo(ip);
                if (API != null)
                    API.StartCalculation(StartingValue);
                else
                    Console.WriteLine("Method: StartCalculation(). Problem trying to get the API for the client: " + ip);
            }
            // This is the only node with the token
            DistrCalc.Start();
        }
    }
}
