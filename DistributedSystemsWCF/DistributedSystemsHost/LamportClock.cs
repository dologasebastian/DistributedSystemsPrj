using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DistributedSystems
{
    /**
    * Should increment the counter before processing received messages
    * We break ties by comparing IP address of requesting hosts
    * Reference: http://krzyzanowski.org/rutgers/notes/pdf/06-clocks.pdf
    */
    class LamportClock
    {
        private readonly string Ip;
        private uint Counter = 0;
        private Object ThisLock = new Object();

        public LamportClock(string ip)
        {
            this.Ip = ip;
        }

        public LamportClock(uint counter, string ip)
        {
            this.Counter = counter;
            this.Ip = ip;
        }

        /// <summary>
        /// Update the clock in case of a "Local" event.
        /// </summary>
        /// <returns>Tuple containing the clock value after performing the event and the computer ID</returns>
        public Tuple<uint, string> EventLocal()
        {
            IncCounter();
           
            return this.ToTuple();
        }
        
        /// <summary>
        /// Update the clock in case of a "Send" event.
        /// </summary>
        /// <returns>Tuple containing the clock value after performing the event and the computer ID</returns>
        public Tuple<uint, string> EventSend()
        {
            IncCounter();

            return this.ToTuple();
        }

        /// <summary>
        /// Update the clock in case of a "Receive" event.
        /// </summary>
        /// <param name="receivedLC">Tuple containing the received clock value and the computer ID of the sender</param>
        /// <returns>Tuple containing the clock value after performing the event and the computer ID</returns>
        public Tuple<uint, string> EventReceive(Tuple<uint, string> receivedLC)
        {
            uint rCounter = receivedLC.Item1;

            if (rCounter > Counter)
            {
                // timestamp of the received event and all further timestamps will be greater than
                // that of the timestamp of sending the message as well as all previous messages
                Counter = rCounter;
            }

            IncCounter();

            return this.ToTuple();
        }
        
        private void IncCounter()
        {
            lock (ThisLock)
            {
                ++Counter;
            }
        }

        /// <summary>
        /// Compares the current state of the clock with another state. This would allow us to have a total ordering of events.
        /// </summary>
        /// <param name="receivedLC">Tuple containing the other clock value: (Counter, IP)</param>
        /// <returns>1 if gt, 0 if eq, -1 if lt</returns>
        public int Compare(Tuple<uint, string> receivedLC)
        {

            if (receivedLC.Item1 == this.Counter)
            {
                string rIp = receivedLC.Item2;

                if (Ip == rIp)
                {
                    return 0;
                }
                else
                {
                    List<string> ips = new List<string>() { Ip, rIp };

                    ips = ips.OrderBy(x => int.Parse(x.Split('.').First()))
                        .ThenBy(x => int.Parse(x.Split('.')[1]))
                        .ThenBy(x => int.Parse(x.Split('.')[2]))
                        .ThenBy(x => int.Parse(x.Split('.').Last())).ToList();

                    return ips.IndexOf(Ip).CompareTo(ips.IndexOf(rIp));
                }
            }

            return Counter.CompareTo(receivedLC.Item1);
        }

        public Tuple<uint, string> ToTuple()
        {
            return new Tuple<uint, string>(Counter, Ip);
        }
    }
}
