using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace DistributedSystems
{
    public class App
    {
        static void Main(string[] args)
        {
            // inform the user if debug/release was built
            System.Console.WriteLine(System.Diagnostics.Debugger.IsAttached ? "--- Debug mode ---" : "--- Release mode ---");
            System.Console.WriteLine();      
      
            // read IP directly whitout user interference 
            System.Console.WriteLine("Use localhost? y/n.");
            bool useLocalHost = false;
            do
            {
                string input = System.Console.ReadLine();
                if (input == "y" || input == "n")
                {
                    useLocalHost = input == "y";
                    break;
                }
                else
                    System.Console.WriteLine("Unkown input. Try again...");
                
            } while (true);

            System.Console.WriteLine("Use Wifi (Ethernet otherwise) adapter? y/n.");
            bool useWifi = true;
            do
            {
                string input = System.Console.ReadLine();
                if (input == "y" || input == "n")
                {
                    useWifi = input == "y";
                    break;
                }
                else
                    System.Console.WriteLine("Unkown input. Try again...");

            } while (true);
            
            // create the node with the desired IP.
            Node node = new Node(LocalIPAddress(useLocalHost, useWifi));
            node.Start();
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
                    case "-quit":
                        run = false;
                        node.Stop();
                        break;
                    case "-join":
                        if (splits.Count() != 2)
                        {
                            System.Console.WriteLine("Please specify IP address to join\n");
                        }
                        else
                        {
                            node.Join(splits[1]);
                        }
                        break;
                    case "-alg":
                        if (splits.Count() != 2)
                        {
                            System.Console.WriteLine("Please specify what algorithms you wish to use\n");
                        }
                        else
                        {
                            node.SelectAlgorithm(splits[1]);
                        }
                        break;
                    case "-start":
                        if (splits.Count() == 2)
                        {
                            int startValue;
                            node.StartCalculation(int.TryParse(splits[1], out startValue) ? startValue :
                                (int)((new Random()).NextDouble() * 100));
                            Console.WriteLine("Started calculation.");
                        }
                        break;
                    case "-show":
                        string output = string.Empty;
                        Console.WriteLine("Current node address: " + node.Address);
                        foreach (string network in node.Network)
                            output += network + "  ";
                        Console.WriteLine("Nodes connected: " + (!string.IsNullOrEmpty(output) ? output : "None"));
                        break;
                    case "-signoff":
                        node.SignOff();
                        Console.WriteLine("Signed off from the network.");
                        break;
                    default:
                        Console.WriteLine("Unknown command\n");
                        break;
                }
            }
        }
        private static string LocalIPAddress(bool UseLocalHost, bool UseWifi)
        {
            if (!UseLocalHost && System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                return NetworkInterface.GetAllNetworkInterfaces()
                    .Where(x => x.OperationalStatus == OperationalStatus.Up)
                    .FirstOrDefault(x => UseWifi ? x.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 : x.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                    .GetIPProperties().UnicastAddresses.FirstOrDefault(x => x.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).Address.ToString();                
            }
            else
                return "127.0.0.1";
        }
        private static void PrintOptions()
        {
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("-join 127.0.0.1       Join a specific Node in the network.");
            Console.WriteLine("-alg -tk / -ra        Select the desired algorithm:");
            Console.WriteLine("                      Token Ring / Ricart & Agrawala.");
            Console.WriteLine("-start (int)          Start a distributed calculation.");
            Console.WriteLine("                      Specify initial value if desired.");
            Console.WriteLine("-signoff              Sign off from the network.");
            Console.WriteLine("-show                 Show information related to this node.");
            Console.WriteLine("-quit                 Close the node and it's connections.");

        }
    }
}
