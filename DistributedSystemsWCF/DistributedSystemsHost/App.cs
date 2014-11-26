using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace DistributedSystems
{
    public class App
    {
        static void Main(string[] args)
        {
            // !!! Warning --------------
            // App should always start with:     myApp.exe -listen myIP    in CMD

            // Test if input arguments were supplied: 
            if (args.Length == 0)
            {
                System.Console.WriteLine("Please enter one of the options: '-listen IP', 'help'");
                Console.ReadLine();
                return;
            }
            else
            {
                if ((string)args[0] == "-help")
                {
                    Console.WriteLine("usage: app -listen 127.0.0.1");
                }
                else
                {
                    // 
                    Node node = new Node((string)args[0] == "-listen" ? (string)args[1] : "127.0.0.1");
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
                            case "-alg ":
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
                                    node.StartCalculation(int.TryParse(splits[1],out startValue) ? startValue : 
                                        (int)((new Random()).NextDouble() * 100)  );
                                }                                
                                break;
                            case "-show":
                                string output = string.Empty;
                                Console.WriteLine("Current node address: " + node.Address);
                                foreach (string network in node.Network)
                                    output += network + "  ";
                                Console.WriteLine("Nodes connected: " + (!string.IsNullOrEmpty(output) ? output : "None"));
                                break;
                            default:
                                Console.WriteLine("Unknown command\n");
                                break;
                        }
                    }

                }
            }

        }
        public static void PrintOptions()
        {
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("-join 127.0.0.1       Join a specific Node in the network.");
            Console.WriteLine("-alg -tk / -ra        Select the desired algorithm:");
            Console.WriteLine("                      Token Ring / Ricart & Agrawala.");
            Console.WriteLine("-start (int)          Start a distributed calculation.");
            Console.WriteLine("                      Specify initial value if desired.");
            Console.WriteLine("-show                 Show information related to this node.");
            Console.WriteLine("-quit                 Close the node and it's connections.");
        }
    }
}
