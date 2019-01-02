using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace testFastDraw
{
    public class Synchr
    {
        static public long rpos;
        public AutoResetEvent rEvent;
        public AutoResetEvent sEvent;
        public long nbuf;
        public long lenPack;
        public long maxPos;
        public byte[] rbuff;
        public byte[] imbuff1;
        public byte[] imbuff2;
        public byte[] bmbuff1;
        public byte[] bmbuff2;
        public int w, h;
        public int indent;
    }

    public class UDP_Receiver
    {
        public long nPac = 0;
        public long nMiss = 0;
        public long nTimeout = 0;
        public int nLastString = 0;
        //static AutoResetEvent rEvent;
        Thread rThread;
        Synchr sync;
        Byte[] rbuff;
        public Socket _udp;
        public volatile bool stopRead = false;
        public volatile bool pauseRead = false;
        int lenPack;
        int rpos = 0;
        //ref long rrpos= ref rpos;
        private int Timestamp = 0;
        public byte CC = 0, CSRS = 0, headerRTP = 12;
        public uint SSRC = 0;
        public int errBind = 0;
        public int Indent = 12;
        public int decimation = 10;
        public long recFrame = 0;
        public long frameNumber=0;
        public bool pause 
        {
            get { return pauseRead;}
            set { pauseRead = value; }
        }

        public UDP_Receiver( string Port, int _lenPack, int rbuffSize, Synchr snc)
        {
            sync = snc;
            IPAddress myIP1 = IPAddress.Parse("127.0.0.1");

            //IPAddress mcIP = IPAddress.Parse(mcIPstr);
            _udp = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            _udp.ReceiveBufferSize = rbuffSize;
            IPEndPoint _endpoint = new IPEndPoint(IPAddress.Any, int.Parse(Port));
            //IPEndPoint _endpoint = new IPEndPoint(myIP1, int.Parse(Port));
            
            //try
            //{
            //    _udp.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(mcIP, myIP1));
            //}
            //catch
            //{
            //}

            _udp.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
            try
            {
                _udp.Bind(_endpoint);
            }
            catch
            {
                errBind++;
            }
            _udp.ReceiveTimeout = 1000;

            rbuff = new Byte[rbuffSize];
            rThread = new Thread(readUDP);
            lenPack = _lenPack;
            sync.rbuff = rbuff;
        }

        private void readUDP()
        {
            int retval;
            int nString;
            int LengthString;
            long recPack = 0;

            //lenPack = (int)Interlocked.Read(ref sync.lenPack);
            Interlocked.Exchange(ref Synchr.rpos, 0);
            //Interlocked.Exchange(ref sync.lenPack, lenPack);
            Interlocked.Exchange(ref sync.maxPos, rbuff.Length - lenPack);

            while (!stopRead)
            {
                if (pauseRead)
                {
                    Thread.Sleep(100);
                    continue;
                }
   

                #region Прием данных
                try
                {
                    retval = _udp.Receive(rbuff, rpos, lenPack, SocketFlags.None);
                    nPac++;
                    

                    frameNumber = (long)((rbuff[rpos + 0] << 24) + (rbuff[rpos + 1] << 16)+ (rbuff[rpos + 2] << 8) + rbuff[rpos + 3]);
                    nString = (int)((rbuff[rpos + 6] << 8) + rbuff[rpos + 7]);
                    rpos += retval;

                    if (nString != nLastString + 1 && nString != nLastString - 1023)
                        //if (nString != 1)
                        nMiss++;
                    nLastString = nString;
                    lenPack = retval;
                    if (rpos + lenPack > rbuff.Length)
                        rpos = 0;
                    Interlocked.Exchange(ref Synchr.rpos, rpos);
                    Interlocked.Exchange(ref sync.lenPack, lenPack);
                    Interlocked.Exchange(ref sync.maxPos, rbuff.Length-lenPack);
                    if (nPac % decimation == 0)
                        sync.rEvent.Set();
                }
                catch
                {
                    nTimeout++;

                    continue;
                }
                #endregion

            }
        }
        public void start()
        {
            if (!pauseRead)
                rThread.Start();
            else
                pauseRead = false;
        }
        public void stop()
        {

            stopRead = true;
        }
    }
}
