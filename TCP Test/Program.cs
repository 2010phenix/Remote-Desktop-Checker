using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Diagnostics;
using Utilities;
using System.Threading.Tasks;

namespace TCP_Test
{
    class Program
    {
        class TestResult
        {
            public IPAddress ip;
            public int port;
            public bool isOnline;
            public int goodOnes;
            public int goodUntil;
            public int scoreNoNull;
        }
    public static readonly Byte[] tosend = new byte[] {
            0x03, 0x00, 0x00, 0x13, 0x0e, 0xe0, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x08, 0x00, 0x0b,
            0x00, 0x00, 0x00 };
    public static readonly Byte[] shouldrec = new byte[]  {
            0x03, 0x00, 0x00, 0x13, 0x0e, 0xd0, 0x00, 0x00,
            0x12, 0x34, 0x00, 0x02, 0x09, 0x08, 0x00, 0x02,
            0x00, 0x00, 0x00 };
        static void Main(string[] args)
        {
            List<Task<TestResult>> tlist = new List<Task<TestResult>>();

            Dictionary<int, UInt64> goodOnesStatistic = new Dictionary<int, UInt64>();
            UInt64 GoodRdps =0;
            UInt64 RdpsTested=0;
            string line;
            bool lineend = false;
            System.IO.StreamReader file = new System.IO.StreamReader(@"input.txt");
            Stopwatch clock = new Stopwatch();
            clock.Start();

            while(!lineend || tlist.Count > 0) //only as long as line has not ended and tlist is not 0
            {
                if (!lineend && tlist.Count < 1000) //add 1k new if its under 1k
                {
                    for (int i = 0; i <= 1000; i++)
                    {
                        line = file.ReadLine();
                        if (line == null)
                        {
                            lineend = true;
                            break;
                        }
                        IPAddress prIp;
                        UInt16 prPort = 3389;

                        var tmp = line.Split(new[] { ':' });
                        if (tmp.Length == 1)
                        {
                            if (!IPAddress.TryParse(tmp[0], out prIp))
                                continue;
                        }
                        else if (tmp.Length == 2)
                        {
                            if (!IPAddress.TryParse(tmp[0], out prIp))
                                continue;
                            if (!UInt16.TryParse(tmp[1], out prPort))
                                continue;
                        }
                        else
                        {
                            continue;
                        }
                        tlist.Add(Task.Run(() => checkPort(prIp, prPort)));
                    }
                }
                for(int i = tlist.Count - 1; i >= 0; i--)
                {
                    if(TimeSpan.FromMilliseconds(clock.ElapsedMilliseconds).TotalSeconds > 1)
                    {
                        clock.Reset();
                        clock.Start();
                        UpdateScreen(goodOnesStatistic, GoodRdps, tlist.Count, RdpsTested);
                    }
                    if (tlist[i].Status == TaskStatus.RanToCompletion)
                    {
                        if (tlist[i].Result.isOnline && tlist[i].Result.scoreNoNull > 2 && tlist[i].Result.goodUntil > 1)
                        {
                            GoodRdps++;
                            if (goodOnesStatistic.ContainsKey(tlist[i].Result.goodUntil))
                                goodOnesStatistic[tlist[i].Result.goodUntil]++;
                            else
                                goodOnesStatistic.Add(tlist[i].Result.goodUntil,1);

                            if (!Directory.Exists("results"))
                                Directory.CreateDirectory("results");

                            File.AppendAllText(@"results\" + tlist[i].Result.goodUntil + ".txt", tlist[i].Result.ip.ToString() + ":" + tlist[i].Result.port.ToString() + Environment.NewLine);
                            File.AppendAllText(@"results\All.txt", tlist[i].Result.ip.ToString() + ":" + tlist[i].Result.port.ToString() + Environment.NewLine);

                        }
                    }

                if (tlist[i].Status == TaskStatus.Faulted       ||
                    tlist[i].Status == TaskStatus.Canceled      ||
                    tlist[i].Status == TaskStatus.RanToCompletion)
                    {
                        RdpsTested++;
                        tlist[i].Dispose();
                        tlist[i] = null;
                        tlist.RemoveAt(i);
                    }
                }
            }


           // var kk = checkPort("50.249.211.202");




            file.Close();
            Debugger.Break();


        }
      static  void UpdateScreen(Dictionary<int, UInt64> goodOnesStatistic, UInt64 TotalGood, int tlistCount,UInt64 rdpsTested)
        {
            Console.Clear();

            Console.WriteLine("Running Tasks:");
            Console.WriteLine("   {0}", tlistCount);

            Console.WriteLine("Total tested Rdps:");
            Console.WriteLine("   {0}", rdpsTested);

            Console.WriteLine("Total good Rdps:");
            Console.WriteLine("   {0}", TotalGood);

            Console.WriteLine("Good Until Statistic:");
            foreach (int key in goodOnesStatistic.Keys)
            {
                Console.WriteLine("   {0}\t{1}", key,goodOnesStatistic[key]);
            }
        }
        static TestResult checkPort(IPAddress ip,int port=3389)
        {
            try
            {
                int goodUntil=-50;
                int scoreNoNull=0;
                int score = 0;
                List<byte> received = new List<byte>();

                TcpClient tcpclnt;// = new TcpClient();
                CTcpConnect Ctct = new CTcpConnect(ip, port, TimeSpan.FromSeconds(15));
                tcpclnt = Ctct.Connect();
                //tcpclnt.Connect(ip, port);
                Stream stm = tcpclnt.GetStream();
                stm.WriteTimeout = Convert.ToInt32(TimeSpan.FromSeconds(15).TotalMilliseconds);
                stm.ReadTimeout = Convert.ToInt32(TimeSpan.FromSeconds(15).TotalMilliseconds);
                stm.Write(tosend, 0, tosend.Length);
                for (int i = 0; i < 19; i++)
                {
                    received.Add(Convert.ToByte(stm.ReadByte()));
                }
                tcpclnt.Close();

                for (int i = 0; i < received.Count; i++)
                {
                    if (shouldrec[i] == received[i])
                    {
                        if (shouldrec[i] != 0x00)
                        {
                            scoreNoNull++;
                        }
                        
                        score++;
                       // Console.WriteLine("Byte {0} OK!", i);
                        //if (i == 11)
                        //    break;
                    }
                    else
                    {
                        if(goodUntil==-50)
                            goodUntil = i - 1;
                       // Console.WriteLine("Byte {0} NOT OK!", i);
                       // break;
                    }
                }
                return new TestResult { isOnline = true, goodOnes = score, goodUntil = goodUntil, ip = ip, port = port, scoreNoNull=scoreNoNull };
            }
            catch(Exception ex)
            {
                return new TestResult { isOnline = false, goodOnes = 0, goodUntil = 0, ip = ip, port = port, scoreNoNull =0};
            }

            
        }

    }
}
