using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.Windows.Forms;
using System.IO;
using SAS.ClassSet.MemberInfo;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace SAS.ClassSet.FunctionTools
{
    class Client
    {

        private readonly ManualResetEvent TimeoutObject = new ManualResetEvent(false); //连接超时对象 
        private readonly Dictionary<string, ManualResetEvent> DictTimeoutObject = new Dictionary<string, ManualResetEvent>();//连接超时对象集合
        private readonly Dictionary<string, ManualResetEvent> DictSendoutObject = new Dictionary<string, ManualResetEvent>();//发送超时集合 
        private readonly Dictionary<string, ManualResetEvent> DictRecoutObject = new Dictionary<string, ManualResetEvent>();//接收超时集合 
        private readonly ManualResetEvent SendTimeout = new ManualResetEvent(false); //发送超时对象 
        private readonly ManualResetEvent RecTimeout = new ManualResetEvent(false); //接受超时对象 
        private Dictionary<string, bool> IsApplyRetry = new Dictionary<string, bool>();
        public static Dictionary<string, SocketInfo> dict = new Dictionary<string, SocketInfo>();//当前在线的服务器字典<服务器Ip,对象的socket对象>
        private Dictionary<string,Thread> ThreadPool = new Dictionary<string, Thread>();//线程池
        private UIShow Show = new UIShow();//事务处理对象
       // int timeoutSent = 5000;//发送超时参数
        //int timeoutRec = 10000;//接收超时参数
        int timeoutCon = 5000;//连接超时参数
        private HandleCommand handle = new HandleCommand();//字符处理对象
        private const string OnLine = "在线";
        private const string DisConnection = "断线";
        public string recflag = "";//发送消息等待一段时间后接收的信息
        public string Buf1 = "";//发送消息等待一段时间后接收的信息
        private Insert2DataBase Database = new Insert2DataBase();//写入数据库
        private string PinxBof = "";//记录断网原因
        byte[] buffer = new byte[1024];
        private Queue<SocketInfo> Qmessage = new Queue<SocketInfo>();
        private int Smy = 0;
        private bool fing = false;  //用于发送是否结束
        /// <summary>
        /// 连接服务器方法，循环创建线程用于连接每台服务器
        /// </summary>
        /// <param name="ObjIp">传入的Ip对象集合</param>
        public void connect(object ObjIp)
        {
            DateTime dt = DateTime.Now;
            string tm1 = string.Format("{0:T}", dt);

            frmMain.fm.textBox8.Text = "[1]录入数据[" + tm1 + "]\r\n";
            
            ParameterizedThreadStart handlemessage = new ParameterizedThreadStart(HandleMessage);//处理数据显示线程
            Thread handle = new Thread(handlemessage);
            handle.IsBackground = true;
            handle.Start();
            List<ClientInfo> IpAndport = (List<ClientInfo>)ObjIp;
            frmMain.fm.textBox1.Text = "共有设备[" + IpAndport.Count + "]台\r\n";
            int i = 0;
            for (i = 0; i < IpAndport.Count; i++)
            {
                IsApplyRetry.Add(IpAndport[i].Ip,false);
                ManualResetEvent ConnectTimeout = new ManualResetEvent(false);
                DictTimeoutObject.Add(IpAndport[i].Ip, TimeoutObject);
                ManualResetEvent SendTimeout = new ManualResetEvent(false);
                DictSendoutObject.Add(IpAndport[i].Ip, SendTimeout);
                ManualResetEvent RecTimeout = new ManualResetEvent(false);
                DictRecoutObject.Add(IpAndport[i].Ip, RecTimeout);
                fing = true;
                ParameterizedThreadStart pts = new ParameterizedThreadStart(AloneConnect);
                Thread thradRecMsg = new Thread(pts);
                thradRecMsg.IsBackground = true;
                thradRecMsg.Start(IpAndport[i]);
                //Thread.Sleep(1000);
                int t = 0;
                while(fing)//
                {
                    t++;
                  Thread.Sleep(1000);
                  frmMain.fm.textBox8.Text = "[3]IP["+IpAndport[i].Ip+"]TM[" + t + "]\r\n";
                  

                }
                 
               // MessageBox.Show("IP1-" + IpAndport[i].Ip, "系统[" + i + "]");
                
            }
           // MessageBox.Show("开始[" + i +"]");

            //-------------拼IP线程------
            
            ParameterizedThreadStart p = new ParameterizedThreadStart(Testonline);
            Thread testonline = new Thread(p);
            testonline.IsBackground = true;
            testonline.Start(IpAndport);

           // ParameterizedThreadStart handlemessage = new ParameterizedThreadStart(HandleMessage);
            //Thread handle = new Thread(handlemessage);
           // handle.IsBackground = true;
           // handle.Start();
        }
        private int Findipname(ListView listView1, string ip)
        {

            try
            {
                for (int x = 0; x < listView1.Items.Count; x++)
                {
                    if (ip == listView1.Items[x].SubItems[1].Text)
                    {
                        // MessageBox.Show("ID"+x);

                        return x;
                    }
                }
                return -1;
            }
            catch (Exception)
            {
                //MessageBox.Show("ID[" + i + "]名称-" + mc + "-[" + ex + "]"); 
                return -1;
            }

        }
        //---------向显示屏发送 （在线/断线）---------
        public void FaiSong_Led(int Id, string txt1, int ye)
        {
            string command;
            string mc = txt1;
            if (Id >= 2)
            {
                int id = Id-2;
                if (mc.Length > 4)
                {
                    mc = mc.Substring(0, mc.Length - 4);
                }
                command = "+ZHA-" + ye + "ID-" + id + "-MC-" + mc + "\r\n";
                byte[] bytearray = Encoding.GetEncoding("GBK").GetBytes(command.Trim());//转码为Byte数组（GBK）
                                                                                        //-------------------------------------------------------------------------
                                                                                        // MessageBox.Show("ID[" + i + "]名称-" + mc + "-[" + command+"]");

                if (frmMain.FormList.Items[0].SubItems[2].Text == "在线")
                {
                    string Ip = frmMain.FormList.Items[0].SubItems[1].Text;
                    //sdt.BHUF = "";
                    send(Ip,bytearray);



                }
            }
        }
        //-------------------------------------------
        /// <summary>
        /// 连接一个服务器的方法
        /// </summary>
        /// <param name="objinfo">服务器信息对象</param>
        private void AloneConnect(object objinfo)
        {
            ClientInfo info = (ClientInfo)objinfo;
            
            SocketInfo socketinfo = new SocketInfo();
           
           if (PingTest_1(info.Ip,600))
           {
                try
                {
                    DictTimeoutObject[info.Ip].Reset();
                    Socket socketClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Parse(info.Ip), int.Parse(info.Port));
                    socketinfo.Ip = info.Ip;
                    socketinfo.Port = info.Port;
                    socketinfo.Socket = socketClient;
                    socketClient.BeginConnect(remoteEndPoint, ConnectCallBackMethod, socketinfo);
                    //socketClient.Connect(remoteEndPoint);
                   
                    //remoteEndPoint=ConnectCallBackMethod(socketinfo);

                }
                catch (Exception)
                {
                   // MessageBox.Show("DT-" + info.Ip, "系统有异常[1]");

                    fing = false;
                
                }
           }
            else
           {
                  /*  int len1 = Findipname(frmMain.FormList, info.Ip);
                    frmMain.FormList.Items[len1].SubItems[17].Text = Convert.ToString("3");

                    IsApplyRetry[info.Ip] = true;
                    SocketInfo socketinfo = new SocketInfo();
                    DictTimeoutObject[info.Ip].Reset();
                    ParameterizedThreadStart pts = new ParameterizedThreadStart(Retry);
                    Thread thradRecMsg = new Thread(pts);
                    thradRecMsg.IsBackground = true;
                    thradRecMsg.Start(info);*/

               fing = false;
           }



        }
        /// <summary>
        /// 连接时的回调方法
        /// </summary>
        /// <param name="asyncresult"></param>
        private void ConnectCallBackMethod(IAsyncResult asyncresult)
        {     //使阻塞的线程继续
            SocketInfo socket = asyncresult.AsyncState as SocketInfo;
            try
            {


                if (socket != null)
                {
                    socket.Socket.EndConnect(asyncresult);
                    socket.Isconnect = true;
                    ShowIpStauts(socket.Ip, OnLine);
                    dict.Add(socket.Ip, socket);                   
                    //开通讯线程
                    ApplyThread(OnLine, socket);
                    FaSongThread(socket);//委托发送指令
                    //MsgToServer(socket);
                    
                }
                else
                {
                   // MessageBox.Show("IP-" + socket.Ip, "系统[ON]"); 
                    fing = false;
                }
                //MessageBox.Show("IP-" + socket.Ip, "系统[OK]");
            }
            catch (Exception d)
            {
               // MessageBox.Show("DT-" + d.ToString(), "系统有异常[2]");
                fing = false;
               // return;
            }
            finally
            {

                DictTimeoutObject[socket.Ip].Set();
                //MessageBox.Show("IP-" + socket.Ip, "系统[ON]-1"); 
            }



        }
        /// <summary>
        /// 发送查询命令
        /// </summary>
        /// <param name="sender"></param>
      // public void MsgToServer(SocketInfo sender)
        //public void MsgToServer(Socket sender)
      private void MsgToServer(SocketInfo sender)
        {
           // ClientInfo info = (ClientInfo)Info;
           // SocketInfo socketinfo = new SocketInfo();
             
           
            SocketInfo sk = (SocketInfo)sender;
            string strMsg = "+查询主机\r\n";//指令
            byte[] data = Encoding.GetEncoding("GBK").GetBytes(strMsg.Trim());//转码为Byte数组（GBK） 
           //dict[sender.Ip].Socket.Send(data);
            int len = Findipname(frmMain.FormList, sender.Ip);
            FaiSong_Led(len + 1, frmMain.FormList.Items[len].SubItems[3].Text, 1);//
               // send(sender.Ip, data);
               
                Thread.Sleep(1000);
                int i = 3;
           //----------------------------------
            while(i>0)
           // for (int i = 0; i < 3; i++)
            {
                string GJ = "CHK-OK";
                Buf1 = "";
                if (SendAcy(sender.Ip, data, GJ))
                {
                    //MessageBox.Show("转发成功");


                    fing = false;
                    break;
                }
                else
                {
                    Thread.Sleep(2000);
                    i--;
                    //continue;
                }
                
            }
            fing = false;
            //----------------------------------
          // sender.Send(data);
           // MessageBox.Show("[" + i + "]-" + sender.Ip); 


        }
        //-------发送线程-------------
        //------发送查询命令-----
        private void FaSongThread(object obj)
        {
            ParameterizedThreadStart thradstart1;
            Thread thrad1;
            thradstart1 = new ParameterizedThreadStart(FaSongsx);
            thrad1 = new Thread(thradstart1);
            thrad1.IsBackground = true;
            thrad1.Start((SocketInfo)obj);
            SocketInfo info = (SocketInfo)obj;
          
        
        
        }
        /// <summary>
        /// 申请线程
        /// </summary>
        /// <param name="flag">申请线程类型</param>
        /// <param name="obj">委托对象</param>
        private void ApplyThread(string flag, object obj)
        {
            ParameterizedThreadStart thradstart;
            Thread thrad;
          
            switch (flag)
            {
                case OnLine:
                    thradstart = new ParameterizedThreadStart(RecMsg);
                    thrad = new Thread(thradstart);
                    thrad.IsBackground = true;
                    thrad.Start((SocketInfo)obj);
                    SocketInfo info = (SocketInfo)obj;
                    ThreadPool.Add(info.Ip, thrad);
                    break;
                case DisConnection:
                    thradstart = new ParameterizedThreadStart(Retry);
                    thrad = new Thread(thradstart);
                    thrad.IsBackground = true;
                    thrad.Start((ClientInfo)obj);
                    ClientInfo clientinfo = (ClientInfo)obj;
                    ThreadPool.Add(clientinfo.Ip, thrad);
                    //MessageBox.Show("2KKK");
                    break;

            }
        }
        /// <summary>
        /// 关闭所有线程
        /// </summary>
        public void CloseThread()
        {
             
            foreach (SocketInfo s in dict.Values)
            {
                s.Socket.Close();
               
            }
            foreach (Thread t in ThreadPool.Values)
            {
                t.Abort();
                

            }

            //dict.Clear();
           // ThreadPool.Clear();
        }


        //--异步发送回调方法         
        private void SendCallBackMethod(IAsyncResult asyncresult)
        {
            //使阻塞的线程继续  
            SocketInfo socket = asyncresult.AsyncState as SocketInfo;
            DictTimeoutObject[socket.Ip].Set();
        }
    
      
        /// <summary>
        /// 服务器无法连接时，不断尝试连接服务器
        /// </summary>
        /// <param name="ip">无法连接的Ip</param>
        /// <param name="Port">无法连接的无法端口</param>
        private void Retry(object Info)
        {
            ClientInfo info = (ClientInfo)Info;
            SocketInfo socketinfo = new SocketInfo();
            //Smy++;
            while (true)
            {
                
               // I++;
             //   frmMain.fm.textBox1.Text = "[" + Smy + "]IP[" + info.Ip + "]\r\n";
                try
                {
                    if (PingTest_1(info.Ip,3000))
                    {
                        DictTimeoutObject[info.Ip].Reset();
                        Socket socketClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Parse(info.Ip), int.Parse(info.Port));
                        socketinfo.Ip = info.Ip;
                        socketinfo.Port = info.Port;
                        socketinfo.Socket = socketClient;
                        //阻塞当前线程
                        socketClient.BeginConnect(remoteEndPoint, ConnectCallBackMethod, socketinfo);//开通一接收新线程
                       if (DictTimeoutObject[info.Ip].WaitOne(timeoutCon, false))
                        {
                           if (dict[info.Ip].Isconnect)
                            {
                                IsApplyRetry[info.Ip] = false;
                               // int Len = Findipname(frmMain.FormList, info.Ip);
                               // frmMain.FormList.Items[Len].SubItems[17].Text = Convert.ToString("4");
                               // Smy--;
                                
                               /*
                                 DictTimeoutObject[info.Ip].Set();
                                  ParameterizedThreadStart thradstart;
                                 Thread thrad;
                                thradstart = new ParameterizedThreadStart(RecMsg);
                                 thrad = new Thread(thradstart);
                                 thrad.Start();*/
                                break;

                            }
                            else
                           {
                               //MessageBox.Show("K-1");
                                 continue;
                            }
                          }
                        //Thread.Sleep(100);
                        

                       else
                        {
                            //MessageBox.Show("K-2");
                            IsApplyRetry[info.Ip] = false;
                            Smy--;
                            break;
                           //continue;
                        }
                      
                    }
                  Thread.Sleep(2000);
                }
                catch (Exception )
                {
                    //MessageBox.Show(X.ToString() + "\r\nIP-" + info.Ip,"系统异常警告[1]");
                   // return;
                }
                
            }
           
        }
       
      
        /// <summary>
        /// 向服务器发送数据
        /// </summary>
        /// <param name="Ip">服务器Ip</param>
        /// <param name="arrMsg">发送的信息</param>
        public void send(string Ip, byte[] arrMsg)
        {
            //Socket ef = new Socket();
           
            try
            {
                if (Ip == "")
                {
                    foreach (SocketInfo s in dict.Values)
                    {
                        s.Socket.Send(arrMsg);

                    }
                }
                else
                {
                   
                 dict[Ip].Socket.Send(arrMsg);
                 //dict[Ip].Socket.Disconnect(true);
                 //dict[Ip].Socket.Shutdown(SocketShutdown.Both);// 关闭套接字
                 //2.发送数据
               //  byte[] sendData = AddEndMark(data);
                   // dict[Ip].Socket.BeginSend(arrMsg, 0, arrMsg.Length, SocketFlags.None, EndSend, dict[Ip].Socket);
                   // dict[Ip].Socket.BeginSend(arrMsg, arrMsg.Length, dict[Ip].Socket);
                 

                }

            }
            catch (System.Exception )
            {
               // MessageBox.Show(X.ToString());
               // return;
            }

        }

       
        /// <summary>
        /// 用于在主界面显示Ip及其在线状态
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="stauts"></param>
        private void ShowIpStauts(string ip, string stauts)
        {
            string[] str = new string[]{
                       stauts ,
                       ip
                    };
            Show.ShwMsgforView(frmMain.FormList, str);

        }
        /// <summary>
        /// 用于判别返回结果
        /// </summary>
        /// <param name="command">返回结果的字符串</param>
        private void ContorlCommand(object obj)
        {
           
            SocketInfo socketinfo=(SocketInfo)obj;
            string command = socketinfo.Command;
            string ip = socketinfo.Ip;
            Show.SetIpAndRec(command, ip);
            if (command.IndexOf("SHOW-2") != -1)
            {

               
                Show.ShwStuatsforView(frmMain.FormList, handle.QueryHandle(command), ip);
                //MessageInfo info = new MessageInfo(command, ip, "查询返回", DateTime.Now.ToString());
                //Database.insert(info);
                // Database.insert(info);//写入数据库
                recflag = command;
                DictSendoutObject[socketinfo.Ip].Set();
                return;

            }
            else if (command.IndexOf("SHOW-6") != -1)
            {

                Show.ShwStuatsforView(frmMain.FormList, handle.QueryHandle(command), ip);
                recflag = command;
               // MessageBox.Show(command);
                DictSendoutObject[socketinfo.Ip].Set();
                
                return;
            }
            //--------------------------------------------------------------
            else if (command.IndexOf("SHOW-9[") != -1)
            {
                DateTime dt = DateTime.Now;
                command = "+" + command;
                string[] str = command.Split('[', ']');
                MessageInfo info = new MessageInfo(frmMain.Decode(str[1]), ip, "警报记录", DateTime.Now.ToString());//将转发记录转码后保存
                Database.insert(info);
                recflag = command;
                DictSendoutObject[socketinfo.Ip].Set();
                
                return;
            }
            //--------------------------------------------------------------------------------
            else if (command.IndexOf("SHOW-8[") != -1)
            {
               
                DateTime dt = DateTime.Now;
                command = "+" + command;
                string[] str = command.Split('[', ']');
                MessageInfo info = new MessageInfo(frmMain.Decode(str[1]), ip, "警报记录", DateTime.Now.ToString());//将转发记录转码后保存
                Database.insert(info);
                //-------------------------------------------------------------向主机发数据--------------------------
                byte[] bytearray = Encoding.Unicode.GetBytes(command);
                
                foreach (string host in frmMain.HostIp)
                {   //--------------------------------
                    int len1 = Findipname(frmMain.FormList, host);//找出主机在列表位置
                    string Luxi = frmMain.FormList.Items[len1].SubItems[2].Text;
                    if (Luxi=="在线")                            //判别主机是否在线
                    {
                    //------------------------------------------------------------在（STE）
                            for (int i = 0; i < 3; i++)
                                 {
                                                string GJ = "-OK";
                                                Buf1 = "";
                                                if (SendAcy(host, bytearray, GJ))
                                                {
                                                    //MessageBox.Show("转发成功");
                                                    MessageInfo info1 = new MessageInfo("成功转发信息" + frmMain.Decode(str[1]), host, "转发记录", DateTime.Now.ToString());//将转发记录转码后保存
                                                    Database.insert(info1);
                                                    //MessageBox.Show("OK-" + host);
                                                    break;
                                                }
                                            else
                                                {
                                                    recflag = command;
                                                    DictSendoutObject[socketinfo.Ip].Set();
                                                    Thread.Sleep(30000);
                                                    continue;
                                                }

                                }
                     //----------------------------------------------------------------在（OK）
                    }
                   // else{MessageBox.Show("[" + host+"]不在线");}
                    
                       
              }
               recflag = command;
               DictSendoutObject[socketinfo.Ip].Set();
                
                return;
             }
            else if (command.IndexOf("SHOW-1") != -1)
            { frmMain.fm.textBox8.Text ="收到回复["+ command+"]\r\n";
              recflag = command;
              DictRecoutObject[socketinfo.Ip].Set();
              return;
            }
            else if (command.IndexOf("SWT-OK") != -1)
            {
                   recflag = command;
                   DictRecoutObject[socketinfo.Ip].Set();
                   return;

            }
            else if (command.IndexOf("REC-OK") != -1)
            {

                recflag = command;
                DictRecoutObject[socketinfo.Ip].Set();
                return;
            }
            else if (command.IndexOf("LedGengXin") != -1)
            {   
                recflag = command;
                DictRecoutObject[socketinfo.Ip].Set();
                Thread.Sleep(1000);
                frmMain.fm.GengXin_OK();
                return;
                
               // 

            }
            else if (command.IndexOf("CHK-OK") != -1)
            {
                recflag = command;

                DictRecoutObject[socketinfo.Ip].Set();
               // return;
            }
            else if (command.IndexOf("OAL-OK") != -1)
            {
                recflag = command;
                DictRecoutObject[socketinfo.Ip].Set();
               // return;
            }
            else if (command.IndexOf("CAL-OK") != -1)
            {
                recflag = command;
                DictRecoutObject[socketinfo.Ip].Set();
                //return;
            }
            else if (command.IndexOf("LED-OK") != -1)
            {
                recflag = command;
                DictRecoutObject[socketinfo.Ip].Set();
                //return;
            }
            else if (command.IndexOf("-OK") != -1)
            {
                recflag = command;
                DictRecoutObject[socketinfo.Ip].Set();
                //return;
            }
            Buf1 = recflag;
        }

        public bool PingTest_1(string Ip1, int tim1)
        {
            Ping pingSender = new Ping();
            PingReply reply = pingSender.Send(Ip1,tim1);//第一个参数为ip地址，第二个参数为ping的时间 

            if (reply.Status == IPStatus.Success)
            {
                return true;
               
            }
            else
            {
              
                
                return false;
            }
        
        }
     public bool PingTest(string strIp,int tim)
        {
           // DateTime dt = DateTime.Now;
           // string tm1 = string.Format("{0:T}", dt);
            //bool Suy = false;
            bool YS = false;
            PinxBof = "";
            Process p = new Process();
            p.StartInfo.FileName = "cmd.exe";//设定程序名   
            p.StartInfo.UseShellExecute = false; //关闭Shell的使用   
            p.StartInfo.RedirectStandardInput = true;//重定向标准输入   
            p.StartInfo.RedirectStandardOutput = true;//重定向标准输出   
            p.StartInfo.RedirectStandardError = true;//重定向错误输出   
            p.StartInfo.CreateNoWindow = true;//设置不显示窗口   
            //string pingrst; 
            p.Start(); 
            p.StandardInput.WriteLine("ping " + strIp);   
            p.StandardInput.WriteLine("exit");   
            string strRst = p.StandardOutput.ReadToEnd();
            //MessageBox.Show(strRst); 
            if (strRst.IndexOf("(0% loss)") != -1)
            {  // pingrst = "连接";
                YS =true;
            }   
           // else if (strRst.IndexOf("Destination host unreachable.") != -1)
            //{ PinxBof = "无法到达目的主机"; }   
             else if (strRst.IndexOf("Request timed out.") != -1)
            { PinxBof = "超时"; }   
             //else if (strRst.IndexOf("Unknown host") != -1)
            //{ PinxBof = "无法解析主机"; }
            //---------------------------------------WIN7
            else if (strRst.IndexOf("请求超时") != -1)
            { PinxBof = "请求超时"; }
            else if (strRst.IndexOf("无法访问目标主机") != -1)
            { PinxBof = "无法访问目标主机"; }
             else if (strRst.IndexOf("平均") != -1)
                 { YS = true;  }
            //---------------------------------------
             else { PinxBof = strRst;}   
                p.Close();
                
               // dt = DateTime.Now;
                //string tm2 = string.Format("{0:T}", dt);
                //frmMain.fm.textBox8.Text = strRst + "\r\n";
                return YS;
         
          //---------------------------
        
      /*     Ping pingSender = new Ping();
            PingReply reply = pingSender.Send(strIp, tim);//第一个参数为ip地址，第二个参数为ping的时间 
                                
               if (reply.Status == IPStatus.Success)
                {   
                    YS= true;
                }
                else
                {
                   
                    YS= false;
               }
              // string tm2 = string.Format("{0:T}", dt);
               //frmMain.fm.textBox8.Text = frmMain.fm.textBox8.Text + strIp + "-[" + reply.RoundtripTime + "]-" + tm1 + "-" + tm2 + "\r\n";

              return YS;*/
          
        }



        public void Testonline(object ListIp)
        {  
            
                List<ClientInfo> list = (List<ClientInfo>)ListIp;
                DateTime dt = DateTime.Now;
                string tm1 = string.Format("{0:T}", dt);
                bool Szing = false;
                bool UINTD = false;
                frmMain.fm.textBox8.Text = frmMain.fm.textBox8.Text+"[2]进入网络监测["+ tm1 +"]\r\n";
        try
            {
                while (true)
                {
                    //int X = 0;
                    //Thread.Sleep(3000);         
                    foreach (ClientInfo info in list)
                    {   
                        int len1 = Findipname(frmMain.FormList, info.Ip);
                        string SU = frmMain.FormList.Items[len1].SubItems[17].Text;
                        string Luxi = frmMain.FormList.Items[len1].SubItems[2].Text;
                        //X++;
                       // MessageBox.Show("[" + X + "]" + info.Ip);
                        if (SU != "3"|| Luxi == "在线")
                        {

                            if (Szing == false)
                            {
                                dt = DateTime.Now;
                                tm1 = string.Format("{0:T}", dt);
                                frmMain.fm.textBox8.Text = frmMain.fm.textBox8.Text + "[" + info.Ip + "]-[" + tm1 + "]\r\n";
                                UINTD = PingTest_1(info.Ip, 1000);
                            }
                            else { UINTD = PingTest(info.Ip, 3000); }
                            //if (PingTest(info.Ip,3000))
                            if (UINTD==true)
                            {
                                //-----------------------------------------------------------

                                if (!FindIsExist(info.Ip) && !IsApplyRetry[info.Ip])
                                {
                                    //frmMain.FormList.Items[len1].SubItems[17].Text = Convert.ToString("3");
                                    
                                    IsApplyRetry[info.Ip] = true;
                                    SocketInfo socketinfo = new SocketInfo();
                                    DictTimeoutObject[info.Ip].Reset();
                                   // ParameterizedThreadStart pts = new ParameterizedThreadStart(Retry);
                                   // Thread thradRecMsg = new Thread(pts);
                                    //thradRecMsg.IsBackground = true;
                                   // thradRecMsg.Start(info);
                                    //--------------------------------------
                                   // MessageBox.Show("KOF"+info.Ip);
                                    // socketClient.BeginConnect(remoteEndPoint, ConnectCallBackMethod, socketinfo);
                                    //--------------------------------------
                                }
                                else
                                {
                                    //Socket socketClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                                    // IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Parse(info.Ip), int.Parse(info.Port));
                                    //socketinfo.Ip = info.Ip;
                                    // socketinfo.Port = info.Port;
                                    // socketinfo.Socket = socketClient;
                                    // socketClient.Connect(info.Ip, int.Parse(info.Port));
                                    // MessageBox.Show("IP[" + info.Ip + "]PROT-" + int.Parse(info.Port));
                                   
                                }

                                //-------------------------------------------------------------            
                            }
                            else
                            {

                                //  if (PingTest(info.Ip, 300))
                                // {
                                if (FindIsExist(info.Ip) && !IsApplyRetry[info.Ip])
                                {
                                    frmMain.FormList.Items[len1].SubItems[17].Text = Convert.ToString("3");
                                    int len = Findipname(frmMain.FormList, info.Ip);
                                    FaiSong_Led(len + 1, frmMain.FormList.Items[len].SubItems[3].Text, 0);
                                    MessageInfo messageinfo = new MessageInfo("服务器强行断线[" + PinxBof+"]", info.Ip, "断网记录", DateTime.Now.ToString());//将转发记录转码后保存
                                    Database.insert(messageinfo);
                                    IsApplyRetry[info.Ip] = true;
                                    ThreadPool[info.Ip].Abort();
                                    ThreadPool.Remove(info.Ip);
                                    // try
                                    // {
                                    dict[info.Ip].Socket.Shutdown(SocketShutdown.Both);            // 关闭套接字
                                    // }
                                    // catch (Exception)
                                    //{
                                    //  continue;
                                    //}
                                    dict[info.Ip].Socket.Close();
                                    dict[info.Ip].Socket.Dispose();
                                    dict.Remove(info.Ip);
                                    ShowIpStauts(info.Ip, DisConnection);
                                    //AloneConnect(object objinfo)
                                    ParameterizedThreadStart pts = new ParameterizedThreadStart(Retry);
                                    // ParameterizedThreadStart pts = new ParameterizedThreadStart(AloneConnect);
                                    Thread thradRecMsg = new Thread(pts);
                                    thradRecMsg.IsBackground = true;
                                    thradRecMsg.Start(info);
                                }
                                else 
                                {
                                    frmMain.FormList.Items[len1].SubItems[17].Text = Convert.ToString("3");
                                    //IsApplyRetry[info.Ip] = true;
                                    //SocketInfo socketinfo = new SocketInfo();
                                    //DictTimeoutObject[info.Ip].Reset();
                                    ParameterizedThreadStart pts = new ParameterizedThreadStart(Retry);
                                    Thread thradRecMsg = new Thread(pts);
                                    thradRecMsg.IsBackground = true;
                                    thradRecMsg.Start(info);
                                
                                }
                            }
                        }
                     
                    }
                    if (Szing == false)
                    {
                        dt = DateTime.Now;
                        tm1 = string.Format("{0:T}", dt);  
                        frmMain.fm.textBox8.Text = frmMain.fm.textBox8.Text + "[结束]-[" + tm1 + "]\r\n";
                    }
                    Szing = true;
                  // Thread.Sleep(1000); 
                }
            }
            catch (Exception)
            {  }
        }

        /// <summary>
        /// 查找线程字典是否存在
        /// </summary>
        private bool FindIsExist(string Ip)
        {
            bool IsExist = false;
            foreach (string ip in dict.Keys)
            {
                if (Ip.Equals(ip))
                {
                    IsExist = true;
                }
            }
            return IsExist;
        }
        private void HandleMessage(object obj)
        {
            
            while (true)
            {   
                if (Qmessage.Count>0)
                {
                    SocketInfo info = Qmessage.Dequeue();
                    ContorlCommand(info);
                }
              Thread.Sleep(300); 
            }
        }
        /// <summary>
        /// 异步转发
        /// </summary>
        /// <param name="Ip"></param>
        /// <param name="arrMsg"></param>
        /// <param name="GanJin"></param>
        /// <returns></returns>
        public bool SendAcy(string Ip, byte[] arrMsg, string GanJin)
        {
            bool IsSendSuccess = false;
            Buf1 = "";
            
            //
            try
            {

               // 
                DictSendoutObject[Ip].Reset();
                
                dict[Ip].Socket.Send(arrMsg);
                //DictRecoutObject[Ip].Set();
                //Thread.Sleep(timeoutRec);
                //for (int y = 0; y < 20; y++)true
                 int y=4;
                Thread.Sleep(500);
                
                 while(y>0)
                {
                    
                    if (Buf1.IndexOf(GanJin) != -1)
                    {

                        IsSendSuccess = true;
                       // recflag = "";
                        //return IsSendSuccess;
                        break;
                    }
                    y--;
                   Thread.Sleep(500);
  
                }
                // MessageBox.Show("TU-" + Buf1);
                DictRecoutObject[Ip].Set();
                // MessageBox.Show("T-[" + y + "]-" + Buf1);
                
                return IsSendSuccess;
                   




            }
            catch (System.Exception)
            {
                //MessageBox.Show("TUK-" + Buf1);
                return IsSendSuccess;
            }
           
        }
        //------------------------------------------------------ 
        private void FaSongsx(object Socketclient1)
        {  
            SocketInfo scok = (SocketInfo)Socketclient1;
            string strMsg = "+查询主机\r\n";//指令
            byte[] data = Encoding.GetEncoding("GBK").GetBytes(strMsg.Trim());//转码为Byte数组（GBK） 
            //dict[sender.Ip].Socket.Send(data);
            int len = Findipname(frmMain.FormList, scok.Ip);
            FaiSong_Led(len + 1, frmMain.FormList.Items[len].SubItems[3].Text, 1);//
            // send(sender.Ip, data);

            Thread.Sleep(1000);
            int i = 3;
            //----------------------------------
            while (i > 0)
            // for (int i = 0; i < 3; i++)
            {
                string GJ = "CHK-OK";
                Buf1 = "";
                if (SendAcy(scok.Ip, data, GJ))
                {
                    


                    fing = false;
                    break;
                }
                else
                {
                    Thread.Sleep(2000);
                    // continue;
                }
                i--;
            }
            fing = false;
            //----------------------------------
           // MessageBox.Show("IP-" + scok.Ip + "-TU-" + i);
            // sender.Send(data);
            
        }
        /// <summary>
        /// 接受来自服务器的信息
        /// </summary>
        /// <param name="Socketclient">服务器的socket对象</param>
        private void RecMsg(object Socketclient)
        {
            SocketInfo s = (SocketInfo)Socketclient;
            
            try
            {
               
             // DateTime dt = DateTime.Now;
              //frmMain.fm.textBox8.Text = string.Format("{0:T}", dt) + "\r\n";//14:23:23 ;
              while (s.Socket.Receive(s.Bufffer, 0, s.Bufffer.Length, SocketFlags.None) > 0)
               //while (s.Socket.Receive(s.Bufffer) > 0)
              {
                  // dt = DateTime.Now;
                    string command = Encoding.GetEncoding("GBK").GetString(s.Bufffer);
                    s.Command = command;//.Trim();      // 去字符串首尾空格的函数
                    Buf1 = command;//.Trim(); 
                    Qmessage.Enqueue(s);
                    s.Bufffer = new byte[1024]; 
                   // frmMain.fm.textBox8.Text = "[" + Buf1 + "]\r\n";//14:23:23 ;
                   // Thread.Sleep(50);
                }
            }
            catch (Exception)
            {
               if (FindIsExist(s.Ip) && !IsApplyRetry[s.Ip])
               {
                  
                   IsApplyRetry[s.Ip] = true;
                    ThreadPool[s.Ip].Abort();
                    ThreadPool.Remove(s.Ip);
                  
                 
                   //  dict[s.Ip].Socket.Shutdown(SocketShutdown.Both);            // 关闭套接字                 
                    dict[s.Ip].Socket.Close();
                   dict[s.Ip].Socket.Dispose();
                   dict.Remove(s.Ip);
                    ShowIpStauts(s.Ip, DisConnection);
                    int len = Findipname(frmMain.FormList, s.Ip);
                    FaiSong_Led(len+1, frmMain.FormList.Items[len].SubItems[3].Text, 0);
                    MessageInfo messageinfo = new MessageInfo("服务器软关机断线?!!", s.Ip, "断网记录", DateTime.Now.ToString());//将转发记录转码后保存
                    Database.insert(messageinfo);
                    
                    ParameterizedThreadStart pts = new ParameterizedThreadStart(Retry);
                    Thread thradRecMsg = new Thread(pts);
                    thradRecMsg.IsBackground = true;
                    ClientInfo info = new ClientInfo(s.Ip,s.Port);
                    thradRecMsg.Start(info);
                }

                s.Socket.Close();
                s.Socket.Dispose();
                
               // MessageBox.Show("ON");
                //return;
                
            }    

        }
    }
}
