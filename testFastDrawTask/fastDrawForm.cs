using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Net;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;
using Multimedia;
using Aspose.Imaging;
using System.Runtime.InteropServices;


namespace testFastDraw
{

    // test receive and fast draw using udp packets
    public partial class fastDrawForm : Form
    {
        Image updateImg;
        Image baseImg;
        Graphics gr;
        int counter, cnt,tmcnt;
        long pcnt = 0;
        AutoResetEvent arEvent;
        AutoResetEvent frEvent;
        System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
        Thread th;
        //volatile bool bNeedStop = false;
        CancellationTokenSource cancelTokenSource;
        CancellationToken token;
        byte[] imgbuf;
        int imWidth,imHeight;
        int kSpeed = 4;
        Multimedia.Timer tmr;
        Multimedia.Timer tmr2;
        UDP_Receiver udp_rec;
        ImgCompiler img_cmp;
        Synchr sync;
        int nDrawBuff = 0;
        long tmr2_tick = 0;
         long cnt_lock = 0;
         long counter_lock = 0;
         long tmcnt_lock = 0;
         bool formClose=false;
         Task updateImgTask; 
        bool selfTest = true;
        
        public fastDrawForm()
        {
            InitializeComponent();
            //updateImgTask =new Task(() => UpdateImage());
            sync=new Synchr();
            sync.w = 1280; sync.h = 1024; sync.indent = 8; sync.lenPack = 1288; // Debug ????
            arEvent = new AutoResetEvent(false);
            frEvent = new AutoResetEvent(false);
            sync.rEvent = frEvent;
            sync.sEvent = arEvent;
            udp_rec = new UDP_Receiver("51031",1288,1288*1024*4 ,sync);
            
            //cancelTokenSource = new CancellationTokenSource();
            //token = cancelTokenSource.Token;
            imWidth = 1280;
            imHeight = 1024;
            Width = imWidth; Height = imHeight;
            imgbuf=new byte[imWidth*imHeight];
            sync.imbuff1 = new byte[imWidth * imHeight];
            sync.imbuff2 = new byte[imWidth * imHeight];
            img_cmp = new ImgCompiler(ref sync);
            baseImg = new Bitmap(imWidth, imHeight);
            gr = Graphics.FromImage(baseImg);
            
            //updateImg = Image.FromStream(new MemoryStream(data));
            //updateImg = new Bitmap(imWidth, imHeight);
            //ThreadPool.QueueUserWorkItem((o) => UpdateImage());
            //th = new Thread(new ThreadStart(UpdateImage));
            //th.Start();
            updateImg = new Bitmap(imWidth, imHeight, PixelFormat.Format8bppIndexed);
            ColorPalette pal = updateImg.Palette;
            for (int i = 0; i < 256; i++)
            {
                pal.Entries[i] = Color.FromArgb((byte)i, (byte)i, (byte)i);
            }
            updateImg.Palette = pal;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            tmr = new Multimedia.Timer();
            tmr.Period = 10;
            tmr.Resolution = 1;
            tmr.Tick += (s, e) => 
            {
                tmcnt++;
                if(selfTest)
                    arEvent.Set(); 
            };
            t.Interval = 1000;
            t.Enabled = true;
            tmr2 = new Multimedia.Timer();
            tmr2 = new Multimedia.Timer();
            tmr2.Period = 500;
            tmr2.Resolution = 1;
            tmr2.Tick += (s, e) =>
            {
                Invoke((MethodInvoker)delegate
                //BeginInvoke((MethodInvoker)delegate
                {
                    Text = string.Format("FPS: {0} {1} {2} Кадр : {3} {4} Ош : {5}", cnt_lock, counter_lock, tmcnt_lock, nDrawBuff, img_cmp.nBuff,img_cmp.imgErr);
                    
                    if (tmr2_tick++ % 2 == 1)
                    {
                        cnt_lock = cnt; ;
                        counter_lock = counter;
                        tmcnt_lock =tmcnt;

                        cnt = 0;
                        counter = 0;
                        tmcnt = 0;
                    }
                    
                });
                //this.Update();
            };
            tmr2.Start();

        }

        //private timerTick()
        private void updateDrawBuff(byte[] buf, int _shift)
        {
            //lock (sync)
            {
                for (int j = 0; j < 1024; j++)
                {
                    for (int i = 0; i < 1280; i++)
                    {
                        buf[j * 1280 + i] = (byte)(i + _shift);
                    }

                }
                
            }

        }
        void copyDrawBuff(byte[] srcBuff,byte[] dstBuff) 
        {
            Array.Copy(srcBuff, dstBuff, srcBuff.Length);
        }
        void UpdateImage()
        {
            Random rnd = new Random();
            //int nDrawBuff = 0;
            //while (!bNeedStop)
            while (!token.IsCancellationRequested)
            {

                if (!arEvent.WaitOne(200))
                {
                    if (Interlocked.Read(ref img_cmp.jobfrmCnt) == 0)
                        continue;
                }

                {
                    if (token.IsCancellationRequested)
                        break;
                    //sync.sEvent.WaitOne(10000, false);
                    if (selfTest)
                    {
                        updateDrawBuff(imgbuf, (byte)(kSpeed * pcnt));
                        counter++;
                        pcnt++;
                        Invalidate();

                    }
                    else
                    {
                        if (Interlocked.Read(ref img_cmp.jobfrmCnt) > 0)
                        {
                            if (token.IsCancellationRequested)
                                break;
                            Invoke((MethodInvoker)delegate
                            {
                                //nDrawBuff = 1 - nDrawBuff;             
                                copyDrawBuff(nDrawBuff > 0 ? sync.imbuff1 : sync.imbuff2, imgbuf);
                            });

                            nDrawBuff = 1 - nDrawBuff;
                            Interlocked.Decrement(ref img_cmp.jobfrmCnt);
                            counter++;
                            pcnt++;
                            Invalidate();

                        }
                    }
                }
  
            }
        }

        void updateBitmap()
        {
            BitmapData bmpData = ((Bitmap)updateImg).LockBits(new Rectangle(Point.Empty, new Size(imWidth, imHeight)), ImageLockMode.ReadWrite, updateImg.PixelFormat);
            IntPtr bmpPtr = bmpData.Scan0;
            Marshal.Copy(imgbuf, 0, bmpPtr, imWidth * imHeight);

            ((Bitmap)updateImg).UnlockBits(bmpData);
            //gr.DrawImageUnscaled(updateImg,new Point(0,0));
        }

        private void Form1_Click(object sender, EventArgs e)
        {
            img_cmp.imgErr = 0;
            //MessageBox.Show("Нажата клавиша", "Сообщение");
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            updateBitmap();
            //gr.DrawImageUnscaled(updateImg, new Point(0, 0));
            //e.Graphics.DrawImageUnscaled(baseImg, 0, 0);
            BMP.DrawGrayBMP(e.Graphics, (Bitmap)updateImg);
            //Text = "Updates: " + counter;
            cnt++;
        }

    


        private async void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = !formClose;
            if (formClose)
                return;
            t.Enabled = false;
            tmr.Stop();
            tmr2.Stop();
            udp_rec.stop();
            img_cmp.stop();
            if (cancelTokenSource != null)
            {
              //updateImgTask.Wait(5000, cancelTokenSource.Token);
              //cancelTokenSource.Cancel();
              //await Task.WhenAll(updateImgTask);
                await Task.WhenAny(updateImgTask,Task.Delay(300));

              //await Task.WaitAll(updateImgTask);
                //updateImgTask.
            }
            //Task.Delay(200);
                //cancelTokenSource.Cancel();
            //updateImgTask.
            //    Application.DoEvents();

           // await Task.Delay(200);
            formClose = true;
            Close();
        }
    



        private void startToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //bNeedStop = false;
            //ThreadPool.QueueUserWorkItem((o) => UpdateImage());
            //Task.Factory.StartNew(() => UpdateImage());
            cancelTokenSource = new CancellationTokenSource();
            token = cancelTokenSource.Token;
            //Task.Run (() => UpdateImage());
            //updateImgTask = new Task(() => UpdateImage());
            updateImgTask = Task.Run(() => UpdateImage());
            //updateImgTask.Start();
            tmr.Start();
            startToolStripMenuItem.Enabled = false;
            udp_rec.start();
            img_cmp.start();
            //Task task = new Task(() => UpdateImage());
            //Task task = Task.Delay(100);
            //task.Start();
            //Task.St  (() => UpdateImage());
           
            
        }

        private void stopToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //bNeedStop = true;
            cancelTokenSource.Cancel();
            startToolStripMenuItem.Enabled = true;
            udp_rec.pause=true;
            img_cmp.pause=true;


        }

        private void toolStripMenuItem8_Click(object sender, EventArgs e)
        {
            tmr.Period = 1;
            radioCheckMenuItems(sender);
        }

        private void toolStripMenuItem7_Click(object sender, EventArgs e)
        {
            tmr.Period = 2;
            radioCheckMenuItems(sender);
        }

        private void toolStripMenuItem6_Click(object sender, EventArgs e)
        {
            tmr.Period = 5;
            radioCheckMenuItems(sender);
        }

        private void toolStripMenuItem5_Click(object sender, EventArgs e)
        {
            tmr.Period = 10;
            radioCheckMenuItems(sender);
        }

        private void toolStripMenuItem4_Click(object sender, EventArgs e)
        {
            tmr.Period = 16;
            radioCheckMenuItems(sender);
        }

        private void toolStripMenuItem3_Click(object sender, EventArgs e)
        {
            tmr.Period = 20;
            radioCheckMenuItems(sender);
        }

        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            tmr.Period = 33;
            radioCheckMenuItems(sender);
        }

        private void radioCheckMenuItems(object sender)
        {
            ToolStripMenuItem chkTmsitem =(ToolStripMenuItem) sender;
            foreach (ToolStripMenuItem tsmitem in chkTmsitem.GetCurrentParent().Items)
            {
                if (tsmitem != chkTmsitem)
                    tsmitem.Checked = false;
            }
            chkTmsitem.Checked = true;

        }
        private void toolStripMenuItem9_Click(object sender, EventArgs e)
        {
            kSpeed = 16;
            radioCheckMenuItems(sender);

        }

        private void toolStripMenuItem10_Click(object sender, EventArgs e)
        {
            kSpeed = 8;
            radioCheckMenuItems(sender);
        }

        private void toolStripMenuItem11_Click(object sender, EventArgs e)
        {
            kSpeed = 4;
            radioCheckMenuItems(sender);
        }

        private void toolStripMenuItem12_Click(object sender, EventArgs e)
        {
            kSpeed = 2;
            radioCheckMenuItems(sender);
        }

        private void toolStripMenuItem13_Click(object sender, EventArgs e)
        {
            kSpeed = 1;
            radioCheckMenuItems(sender);
        }

        private void selfTestToolStripMenuItem_Click(object sender, EventArgs e)
        {
            selfTest = true;
            selfTestToolStripMenuItem.Checked = true;
            ethernetToolStripMenuItem.Checked = false;
            frequencyToolStripMenuItem.Enabled = true;
            speedToolStripMenuItem.Enabled = true;
        }

        private void ethernetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            selfTest = false;
            selfTestToolStripMenuItem.Checked = false;
            ethernetToolStripMenuItem.Checked = true;
            frequencyToolStripMenuItem.Enabled = false;
            speedToolStripMenuItem.Enabled = false;
        }


     
    
    }
}
