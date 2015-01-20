using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DistributedSystems
{
    public class App
    {
        // --- Variables -------------------------------------------------
        private static List<Ping> pingers = new List<Ping>();
        private static int instances = 0;
        private static int result = 0;
        private static object @lock = new object();
        private static Node node = null;

        // --- Main -----------------------------------------------------
        static void Main(string[] args)
        {
            // inform the user if debug/release was built
            Console.WriteLine(System.Diagnostics.Debugger.IsAttached ? "----- Debug mode -----" : "----- Release mode -----");
            Console.WriteLine();

            Console.WriteLine("AutoConfigure?  y/n");
            string ans = System.Console.ReadLine();
            if (ans == "y")
            {
                AutoConfigure();
            }
            else
            {
                string IP = string.Empty;
                NetworkInterfaceType InterfaceType = NetworkInterfaceType.Wireless80211;
                Console.WriteLine("Wifi/Ethernet/Localhost/SpecificIP    w/e/l/s");
                do
                {
                    string input = System.Console.ReadLine();

                    if (input == "w")
                    {
                        InterfaceType = NetworkInterfaceType.Wireless80211;
                    }
                    else
                        if (input == "e")
                        {
                            InterfaceType = NetworkInterfaceType.Ethernet;
                        }
                        else
                            if (input == "l")
                            {
                                InterfaceType = NetworkInterfaceType.Unknown;
                            }
                            else if (input == "s")
                            {
                                Console.WriteLine("Enter IP: ");
                                IP = Console.ReadLine();
                                break;
                            } else
                            {
                                System.Console.WriteLine("Unkown input. Try again...");
                                continue;
                            }
                    LocalIPAddress(InterfaceType, out IP);
                    break;
                } while (true);

                // create the node with the desired IP.
                node = new Node(IP);
                node.Start();
            }            
           
            string command;
            bool run = true;
            PrintOptions();
            while (run)
            {
                command = System.Console.ReadLine();
                if (string.IsNullOrEmpty(command)) continue;
                string[] splits = command.Split(' ');
                switch (splits[0])
                {
                    case "quit":
                        run = false;
                        node.Stop();
                        break;
                    case "join":
                        if (splits.Count() != 2)
                        {
                            System.Console.WriteLine("Please specify IP address to join\n");
                        }
                        else
                        {
                            node.Join(splits[1]);
                        }
                        break;
                    case "start":
                        int startValue = 1;
                        if (splits.Count() != 3)
                        {
                            Console.WriteLine("Insufficient arguments.");
                        }
                        else
                        {
                            int.TryParse(splits[1], out startValue);
                            node.StartCalculation(startValue, splits[2]);
                            Console.WriteLine("Started calculation.");
                        }                        
                        break;
                    case "show":
                        string output = string.Empty;
                        Console.WriteLine("Current node address: " + node.Address);
                        foreach (string network in node.Network)
                            output += network + "  ";
                        Console.WriteLine("Nodes connected: " + (!string.IsNullOrEmpty(output) ? output : "None"));
                        break;
                    case "signoff":
                        node.SignOff();
                        Console.WriteLine("Signed off from the network.");
                        break;
                    default:
                        Console.WriteLine("Unknown command\n");
                        break;
                }
                Console.WriteLine("----------------------------------------------------------");
            }
        }

        // --- Methods --------------------------------------------------
        private static bool LocalIPAddress(NetworkInterfaceType InterfaceType, out string IP)
        {
            IP = default(string);
            if (InterfaceType == NetworkInterfaceType.Unknown)
            {
                IP = "127.0.0.1";
                return true;
            }

            if (System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                if (NetworkInterface.GetAllNetworkInterfaces()
                    .Where(x => x.OperationalStatus == OperationalStatus.Up).Count() == 0)
                {
                    Console.WriteLine("There are no operational Network interfaces. Using localhost.");
                    IP = "127.0.0.1";
                    return false;
                }

                foreach (NetworkInterface netInter in NetworkInterface.GetAllNetworkInterfaces()
                    .Where(x => x.OperationalStatus == OperationalStatus.Up))
                {
                    NetworkInterface interf = NetworkInterface.GetAllNetworkInterfaces()
                        .Where(x => x.OperationalStatus == OperationalStatus.Up)
                        .FirstOrDefault(x => InterfaceType == x.NetworkInterfaceType);
                    if (interf == null)
                    {
                        Console.WriteLine((InterfaceType == NetworkInterfaceType.Wireless80211 ? "Wireless " : "Ethernet ") + "is not available. Trying " + (InterfaceType != NetworkInterfaceType.Wireless80211 ? "Wireless..." : "Ethernet..."));
                        interf = NetworkInterface.GetAllNetworkInterfaces()
                        .Where(x => x.OperationalStatus == OperationalStatus.Up)
                        .FirstOrDefault(x => InterfaceType != NetworkInterfaceType.Wireless80211 ? x.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 : x.NetworkInterfaceType == NetworkInterfaceType.Ethernet);
                        if (interf == null)
                        {
                            Console.WriteLine("Problem in trying to retrieve a network interface. Using localhost.");
                            IP = "127.0.0.1";
                            return false;
                        }
                        else
                        {
                            IP = interf.GetIPProperties().UnicastAddresses.FirstOrDefault(x => x.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).Address.ToString();
                            return true;
                        }
                    }
                    else
                    {
                        IP = interf.GetIPProperties().UnicastAddresses.FirstOrDefault(x => x.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).Address.ToString();
                        return true;
                    }
                }
            }
            IP = "127.0.0.1";
            return false;
        }
        private static void AutoConfigure()
        {            
            System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
            byte[] data = enc.GetBytes("test");
            PingOptions po = new PingOptions(5, true);

            string BaseIP, IP;
            LocalIPAddress(NetworkInterfaceType.Wireless80211, out IP);
            node = new Node(IP);
            node.Start();
            if (IP == "127.0.0.1")
                return;
            BaseIP = IP;
            do
            {
                BaseIP = BaseIP.Remove(BaseIP.Count() - 1);
            } while (BaseIP[BaseIP.Count() - 1] != '.');


            SpinWait wait = new SpinWait();
            int cnt = 1;

            Stopwatch watch = Stopwatch.StartNew();

            foreach (Ping p in pingers)
            {
                lock (@lock)
                {
                    instances += 1;
                }

                p.SendAsync(string.Concat(BaseIP, cnt.ToString()), 100, data, po);
                cnt += 1;
            }

            while (instances > 0)
            {
                wait.SpinOnce();
            }
        }
        private static void Ping_completed(object s, PingCompletedEventArgs e)
        {
            lock (@lock)
            {
                instances -= 1;
            }

            if (e.Reply.Status == IPStatus.Success)
            {
                Console.WriteLine(string.Concat("Active IP: ", e.Reply.Address.ToString()));
                result += 1;
                node.Join(e.Reply.Address.ToString());
            }
            else
            {
                //Console.WriteLine(String.Concat("Non-active IP: ", e.Reply.Address.ToString()))
            }
        }
        private static void CreatePingers(int cnt)
        {
            for (int i = 1; i <= cnt; i++)
            {
                Ping p = new Ping();
                p.PingCompleted += Ping_completed;
                pingers.Add(p);
            }
        }
        private static void DestroyPingers()
        {
            foreach (Ping p in pingers)
            {
                p.PingCompleted -= Ping_completed;
                p.Dispose();
            }

            pingers.Clear();

        }
        private static void PrintOptions()
        {
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("join 127.0.0.1        Join a specific Node in the network.");
            Console.WriteLine("start (int) (alg)     Start a distributed calculation.");
            Console.WriteLine("         (int)        Specify initial value if desired.");
            Console.WriteLine("         tr/ra        Token Ring / Ricart & Agrawala.");
            Console.WriteLine("signoff               Sign off from the network.");
            Console.WriteLine("show                  Show information related to this node.");
            Console.WriteLine("quit                  Close the node and it's connections.");
            Console.WriteLine("----------------------------------------------------------");
        }
    }
}
