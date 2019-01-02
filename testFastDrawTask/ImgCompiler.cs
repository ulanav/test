using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace testFastDraw
{
    struct PacketHeader
    {
         //public UInt32  timestamp;
         //public UInt32  prevTimestamp;
         //public UInt32 SSRC;
         //public int     lenRow;
         public int     nRow;
         public int     lastRow;
        public long frameNumber;
        public long prevNumber;
        
        //public int nString;
        
    }
    class ImgCompiler
    {
        bool _pause;
        public bool pause
        {
            get { return _pause;}
            set {_pause = value;}
        }

        const int headerRTP=12;
        int CSRS = 0;
        public int nBuff = 0;
        PacketHeader hdr;
        Thread thread;
        bool stopComp = false;
        //public static AutoResetEvent rEvent;
        Synchr sync;
        int wpos = 0;
        public long imgErr=0;
        public long wpcnt;
        public long nmiss=0;
        public long nframes = 0;
        public long jobfrmCnt = 0;
        public ImgCompiler(ref Synchr  snc)
        {
            sync = snc;
            stopComp = false;
           thread = new Thread(compileImage);

        }
       private void compileImage()
       {
           bool isNeedUpdate = false;
           while (!stopComp)
           {
               if (_pause)
               {
                   Thread.Sleep(100);
                   continue;
               }
               sync.rEvent.WaitOne(10, false);
               //sync.rEvent.WaitOne();
               while (wpos != Interlocked.Read(ref Synchr.rpos))
               {
                   int retval=parseHeader(wpos,sync.rbuff);
                   if (retval>0)
                   {

                       if((retval&0x1)>0) {
                           extractRowPixels(nBuff > 0 ? sync.imbuff1 : sync.imbuff2, sync.rbuff, wpos, sync.indent, hdr.nRow, sync.w);
                           isNeedUpdate = false;
                            }
                       imgErr+= checkImage(nBuff > 0 ? sync.imbuff1 : sync.imbuff2,sync.w,sync.h);

                       if ((retval & 0x1) > 0 || isNeedUpdate)
                       {
                           while (Interlocked.Read(ref jobfrmCnt) >= 1)
                           {

                               //Thread.Sleep(1);
                               sync.rEvent.WaitOne(1000);
                               if (stopComp)
                                   break;
                           }
                           nBuff = 1 - nBuff;
                           Interlocked.Exchange(ref sync.nbuf, nBuff);
                           Interlocked.Increment(ref jobfrmCnt);
                           frameReady(nBuff);
                           nframes++;
                       }
                   }
                   if ((retval & 0x1) == 0)
                   {
                       isNeedUpdate = true;
                       extractRowPixels(nBuff > 0 ? sync.imbuff1 : sync.imbuff2, sync.rbuff, wpos, sync.indent, hdr.nRow, sync.w);
                   }

                   wpcnt++;
                   wpos += (int)Interlocked.Read(ref sync.lenPack);
                   if (wpos > Interlocked.Read(ref sync.maxPos))
                       wpos = 0;
               }

           }

       }
        private int checkImage(byte[] imbuf,int w,int h) 
        {
            int nerr = 0;
            int val=imbuf[100];
            for (int i = 1; i < h; i++)
            {
                if(val!= imbuf[100+i*w]) 
                {
                    nerr++;
                    return 1;
                }
            }

            val = imbuf[0];
            for (int i = 1; i < h; i++)
            {
                if (val != imbuf[0 + i * w])
                {
                    nerr++;
                    return 1;
                }
            }
            val = imbuf[1023];
            for (int i = 1; i < h; i++)
            {
                if (val != imbuf[1023 + i * w])
                {
                    nerr++;
                    return 1;
                }
            }

                return nerr;
        }
        private void  extractRowPixels(byte[] imbuf, byte[] rbuff,int pos, int indent, int nrow,int w) 
        {
            int dst = nrow  * w;
            int ist = pos + indent;
            //int ien = pos + indent+w*10/8;
            int ien = pos + indent + w ;
            for (int igr = ist,j=dst; igr < ien; )
            {
                imbuf[dst++] = rbuff[igr++];
            }
        }
       private bool frameReady(int nbf)
       {
       //    byte[] bt =  {1,2,3,4};
       //    int ind =0;
       //    int sum =bt[ind++] +bt[ind++] + bt[ind];
       //    sum = ind++ + ind++ + ind;
           sync.sEvent.Set();
           return true;
       }
       public bool start()
       {
           stopComp = false;
           if (!_pause)
               thread.Start();
           else
               _pause = false;
           return true;
       }
        public bool stop() 
        {
            stopComp = true;
            return true;
        }
        private int parseHeader(int rpos,byte[] rbuff)
        {
            int retval = 0;
            

            hdr.frameNumber = (long)((rbuff[rpos + 0] << 24) + (rbuff[rpos + 1] << 16) + (rbuff[rpos + 2] << 8) + rbuff[rpos + 3]);
            hdr.nRow = (int)((rbuff[rpos + 6] << 8) + rbuff[rpos + 7]);
            nmiss += checkMiss();
            hdr.lastRow = hdr.nRow;
            if (hdr.nRow == sync.h - 1)
                retval |= 1;
            if (hdr.prevNumber != hdr.frameNumber)
                retval |= 2;
            hdr.prevNumber = hdr.frameNumber;
            return retval;
            
        }

        private int checkMiss()
        {
            if (hdr.nRow != hdr.lastRow + 1 && hdr.nRow != hdr.lastRow - sync.h + 1)
                return 1;
            return 0;
        }   
    }
}
