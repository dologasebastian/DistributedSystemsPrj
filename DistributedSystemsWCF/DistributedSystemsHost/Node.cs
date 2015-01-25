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
        private ServiceHost RPCServiceHost = null;                    // The host class
        private Uri ListenUri = null;                                 // The URI where the server listens to incoming connetions
        private ChannelFactory<IRPCOperations> ChannelFactory = null; // The API/Connection to another Node. Needed in order to open/close connections
        private IRPCOperations ChannelAPI = null;                     // The API for the client

        // --- Public Properties -----------------------------------------
        public static Node Instance = null;                           // Singleton Class
        public static int Port = 3105;                                // All nodes use the same port.
        public List<string> Network { get; set; }                     // List of addresses of all connected Nodes (including this one)
        public string Address { get; set; }                           // The address of the current Node.
        public DistributedCalculation DistrCalc { get; set; }         // Class that does the distributed calculation

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
                Uri baseAddress = new UriBuilder(Uri.UriSchemeHttp, System.Net.Dns.GetHostName(), Port, "/").Uri;
                //Uri baseAddress = new Uri(string.Format("net.tcp://{0}:{1}/", IP == "127.0.0.1" ? "127.0.0.1" : System.Net.Dns.GetHostName(), Port));

                // create a new ServiceHost. Pass the interface / class that will be used to communicate with other nodes.
                RPCServiceHost = new ServiceHost(typeof(RPCOperations));
                /* All communication with a Windows Communication Foundation (WCF) service occurs through the endpoints 
                   of the service. Endpoints provide clients access to the functionality offered by a WCF service.
                   Each endpoint consists of four properties:
                    - An address that indicates where the endpoint can be found.
                    - A binding that specifies how a client can communicate with the endpoint. This includes the transport protocol (TCP, HTTP)
                    - A contract that identifies the operations available.
                    - A set of behaviors that specify local implementation details of the endpoint
                */
                
                //var epXmlRpc = RPCServiceHost.AddServiceEndpoint(typeof(IRPCOperations), new NetTcpBinding(SecurityMode.None), baseAddress);
                var epXmlRpc = RPCServiceHost.AddServiceEndpoint(typeof(IRPCOperations), new WebHttpBinding(WebHttpSecurityMode.None), baseAddress);
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
            Console.WriteLine("Listening at {0}", ListenUri);
            Console.WriteLine("Address is " + Address);
            Console.WriteLine("----------------------------------------------------------");
        }
        /// <summary>
        /// Stop this host. Send your IP to all other nodes so that they can remove you from the list.
        /// </summary>
        public void Stop()
        {
            SignOff();
            if (ChannelAPI != null && ChannelFactory != null && ChannelFactory.State == CommunicationState.Opened)
            {
                ChannelFactory.Close();
            }
            RPCServiceHost.Close();
            Console.WriteLine("Node listening at {0} closed succesfully", ListenUri);
            Console.WriteLine("Press any key to close...");
        }
        /// <summary>
        /// Return True if Join was succeesfull.
        /// </summary>
        /// <param name="IP">the IP used to find the Node. Port is assumed identical</param>
        /// <returns></returns>
        public bool Join(string IP)
        {
            try
            {
                if (Network.Count > 1)
                {
                    Console.WriteLine("This node already joined a network.");
                    Console.WriteLine("Only a single node can join another node/network.");
                    Console.WriteLine("Sign off first, then join.");
                    return false;
                }

                IRPCOperations API = ConnectTo(IP);
                if (API != null && !Network.Contains(IP)) // if succesful connection Join the entire network
                {
                    string n = API.join(Address);
                    if (!string.IsNullOrEmpty(n))// a node that is already in the network tried to join. Skip it
                    {
                        List<string> nodes = n.Split(',').Where(x => !string.IsNullOrEmpty(x)).ToList();
                        if (nodes != null && nodes.Count() > 0)
                        {
                            foreach (string ipReceived in nodes.Where(x => x != IP))
                            {
                                if (!Network.Contains(ipReceived))
                                {
                                    API = ConnectTo(ipReceived);
                                    if (API != null) // if succesful connection Join the entire network
                                        API.join(Address);
                                }
                            }
                            Network.AddRange(nodes);
                        }

                        //Node.Instance.Network = new HashSet<string>(Node.Instance.Network).ToList();
                        // Nodes should have an ordering in the ring
                        // They are ordered by their IP addresses
                        // TODO: Shouldn't it be "Network = Network.Order....ToList();"?
                        Network = Network
                            .OrderBy(x => int.Parse(x.Split('.')[0]))
                            .ThenBy(x => int.Parse(x.Split('.')[1]))
                            .ThenBy(x => int.Parse(x.Split('.')[2]))
                            .ThenBy(x => int.Parse(x.Split('.')[3])).ToList();
                        if (System.Diagnostics.Debugger.IsAttached) Console.WriteLine("Joined network successfully.");
                    }
                    return true;
                }
                if (System.Diagnostics.Debugger.IsAttached) Console.WriteLine("A non-fatal problem occured while trying to join.");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine("A fatal problem occured while trying to join.");
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    Console.WriteLine("-------------------------------------------------");
                    Console.WriteLine(ex.StackTrace);
                }
                return false;
            }
        }
        /// <summary>
        /// Send your IP to all other nodes so that they can remove you from the list.
        /// </summary>
        public void SignOff()
        {
            try
            {
                foreach (string ip in Network.Where(x => x != Address))
                {
                    IRPCOperations API = ConnectTo(ip);
                    if (API != null)
                        API.sign_off(Address);
                }

                Network.Clear();
                Network.Add(Address);
            }
            catch (Exception ex)
            {
                Console.WriteLine("A fatal problem occured while trying to signoff.");
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    Console.WriteLine("-------------------------------------------------");
                    Console.WriteLine(ex.StackTrace);
                }
            }
        }
        /// <summary>
        /// Connects to a Node using a specific IP.
        /// The new API to the new Node can be found in the list APIs if connection succesful.
        /// </summary>
        /// <param name="IP">The IP to which you want to connect to</param>
        /// <returns>True if connection could be made, False if not.</returns>
        public IRPCOperations ConnectTo(string IP)
        {
            try
            {
                Uri NodeAddress = new UriBuilder(Uri.UriSchemeHttp, IP, Port, "/").Uri;

                // close the last connection before creating a new one
                if (ChannelAPI != null && ChannelFactory != null && ChannelFactory.State == CommunicationState.Opened)
                {
                    // if connecting to same Node, skip the next steps
                    if (ChannelFactory.Endpoint.Address.Uri.AbsoluteUri == NodeAddress.AbsoluteUri)
                        return ChannelAPI;  // send the last API
                    ChannelFactory.Close();
                    ChannelAPI = null;
                }

                ChannelFactory = new ChannelFactory<IRPCOperations>(
                    new WebHttpBinding(WebHttpSecurityMode.None), new EndpointAddress(NodeAddress));
                ChannelFactory.Endpoint.Behaviors.Add(new XmlRpcEndpointBehavior());
                // check if channel was created succesfully
                if ((ChannelFactory != null) || (ChannelFactory.State != CommunicationState.Faulted))
                {
                    ChannelAPI = ChannelFactory.CreateChannel();    // create new API
                    //ChannelFactory.Open();
                    return ChannelAPI;
                }
                Console.WriteLine("Could not ConnectTo: " + IP + " Might crash.");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("A fatal problem occured while trying to connect to Node: " + IP);
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    Console.WriteLine("-------------------------------------------------");
                    Console.WriteLine(ex.StackTrace);
                }
                return null;
            }
        }
        /// <summary>
        /// Select the desired Algorithm.
        /// </summary>
        /// <param name="Alg"></param>
        public void SelectAlgorithm(string Alg, bool isLocked = false)
        {
            if (Alg == "tr")
            {
                DistrCalc = new TokenRing();
                Console.WriteLine("Using TokenRing.");
            }
            else if (Alg == "ra")
            {
                DistrCalc = new RicartAgrawala(isLocked);
                Console.WriteLine("Using RicartAgrawala.");
            }
            else
            {
                Console.WriteLine("Unkown algorithm. Using TokenRing by default");
            }
        }
        /// <summary>
        /// Method that starts one of the implemented Algorithms
        /// </summary>
        /// <param name="Algorithm"></param>
        public void StartCalculation(int StartingValue, string alg)
        {
            // selecting the desired algorithm
            SelectAlgorithm(alg, true);

            // starting calculation from this Node. We know this for sure, so we give this Node the Token.
            DistrCalc.HasToken = true;

            // Inform other nodes that we are about to start the calculation
            Console.WriteLine("Telling other nodes to prepare for a distributed calculation.");
            // StartCalculation for each connected Node.
            foreach (string ip in Network.Where(x => x != Address))
            {
                // On receiving this message each node will initialize a DC object with initial value
                // And they will all block until they receive the token to perform an operation
                IRPCOperations API = ConnectTo(ip);
                if (API != null)
                    API.start_calculation(StartingValue, alg);
                else
                    Console.WriteLine("Method: StartCalculation(). Problem trying to get the API for the client: " + ip);
            }
            // This is the only node with the token
            Console.WriteLine("Starting calculation...");
            DistrCalc.Start(StartingValue);
        }

        public void Test(int val)
        {
            IRPCOperations API = ConnectTo("172.16.1.104");
            if (API != null)
                API.test(val);
        }
    }
}
