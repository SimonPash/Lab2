using System;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Text;
using System.Diagnostics;

namespace Lab2
{
    class ICMP
    {
        public byte Type;
        public byte Code;
        public UInt16 Checksum;
        public int MessageSize;
        public byte[] Message = new byte[1024];

        public ICMP()
        {
        }

        public ICMP(byte[] data, int size)
        {
            Type = data[20];
            Code = data[21];
            Checksum = BitConverter.ToUInt16(data, 22);
            MessageSize = size - 24;
            Buffer.BlockCopy(data, 24, Message, 0, MessageSize);
        }

        public byte[] getBytes()
        {
            byte[] data = new byte[MessageSize + 9];
            Buffer.BlockCopy(BitConverter.GetBytes(Type), 0, data, 0, 1);
            Buffer.BlockCopy(BitConverter.GetBytes(Code), 0, data, 1, 1);
            Buffer.BlockCopy(BitConverter.GetBytes(Checksum), 0, data, 2, 2);
            Buffer.BlockCopy(Message, 0, data, 4, MessageSize);
            return data;
        }

        public UInt16 getChecksum()
        {
            UInt32 chcksm = 0;
            byte[] data = getBytes();
            int packetsize = MessageSize + 8;
            int index = 0;

            while (index < packetsize)
            {
                chcksm += Convert.ToUInt32(BitConverter.ToUInt16(data, index));
                index += 2;
            }
            chcksm = (chcksm >> 16) + (chcksm & 0xffff);
            chcksm += (chcksm >> 16);
            return (UInt16)(~chcksm);
        }
    }

    class Program
    {
        public static void Tracert(String remoteHost)
        {
            byte[] data = new byte[1024];
            int recv = 0;
            int maxHops = 30;
            bool stopPing = false;
            Socket host = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp);
            try
            {
                IPHostEntry iphe = Dns.Resolve(remoteHost);
                IPEndPoint iep = new IPEndPoint(iphe.AddressList[0], 0);
                EndPoint ep = (EndPoint)iep;
                ICMP packet = new ICMP();
                Stopwatch stopWatch = new Stopwatch();

                packet.Type = 0x08;
                packet.Code = 0x00;
                packet.Checksum = 0;
                Buffer.BlockCopy(BitConverter.GetBytes(1), 0, packet.Message, 0, 2);
                Buffer.BlockCopy(BitConverter.GetBytes(1), 0, packet.Message, 2, 2);
                data = Encoding.ASCII.GetBytes("test packet");
                Buffer.BlockCopy(data, 0, packet.Message, 4, data.Length);
                packet.MessageSize = data.Length + 4;
                int packetsize = packet.MessageSize + 4;

                UInt16 chcksum = packet.getChecksum();
                packet.Checksum = chcksum;

                host.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 3000);

                Console.WriteLine(string.Format("Tracing route to {0} [{1}] over a maximum of {2} hops:", iphe.HostName, 
                    iep.Address.ToString(), maxHops));
                Console.WriteLine("Trace may take some time...");

                for (int i = 1; i < maxHops + 1; i++)
                {
                    int badcount = 0;
                    host.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive, i);
                    Console.Write("{0}:\t", i);
                    for (int k = 0; k < 3; k++)
                    {
                        DateTime timestart = DateTime.Now;
                        host.SendTo(packet.getBytes(), packetsize, SocketFlags.None, iep);
                        try
                        {
                            data = new byte[1024];
                            recv = host.ReceiveFrom(data, ref ep);
                            TimeSpan timestop = DateTime.Now - timestart;
                            ICMP response = new ICMP(data, recv);

                            if (response.Type == 11)
                            {
                                Console.Write("{0} ms\t", (timestop.Milliseconds.ToString()));
                            }

                            if (response.Type == 0)
                            {
                                Console.Write("{0} ms\t", timestop.Milliseconds.ToString());
                                stopPing = true;
                            }
                            badcount = 0;
                        }
                        catch (SocketException)
                        {
                            Console.Write("*\t");
                            badcount++;
                        }
                    }
                    if (badcount == 3)
                    {
                        Console.Write("Timed out request.\n");
                        continue;
                    }
                    IPEndPoint ipep = (IPEndPoint)ep;
                    try
                    {
                        Console.Write("{0} [{1}]\n",Dns.GetHostEntry(ipep.Address.ToString()).HostName, ipep.Address.ToString());
                    }
                    catch(SocketException)
                    {
                        Console.Write("{0}\n", ipep.Address.ToString());
                    }
                    if (stopPing)
                    {
                        break;
                    }
                }
                if (!stopPing)
                {
                    Console.WriteLine("\nHost unriched.");
                }
                Console.WriteLine("\nTrace complete.\n");
                host.Close();
            }
           catch (SocketException)
            {
                Console.WriteLine("\nUnknown host.");
            }
        }

        static void Main(string[] args)
        {
            string InpIP;
            Console.WriteLine("Enter IP or Host name:");
            InpIP = Console.ReadLine();
            Tracert(InpIP);
            Console.Read();
        }
    }
}
