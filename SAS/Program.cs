using System;
using System.Collections.Generic;
using System.Windows.Forms;
//using System.Management;
 //using System.Diagnostics;
//using System.Runtime.InteropServices;
namespace SAS
{               
     
    static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        /*public static void CongQiXiTong()
        {
                  Boolean createdNew; //返回是否赋予了使用线程的互斥体初始所属权
                  System.Threading.Mutex instance = new System.Threading.Mutex(true, "MutexName", out createdNew); //同步基元变量
                  if (createdNew) //赋予了线程初始所属权，也就是首次使用互斥体
                  {
                      //Application.Run(new frmMain()); //s/这句是系统自动写的
                      //Application.Run(new frmMain.ShowDialog()); //s/这句是系统自动写的Form.ShowDialog。
                      
                      instance.ReleaseMutex();
                  }
                  else
                  {
                      MessageBox.Show("[1]已经启动了一个程序，请先退出！", "系统提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                      Application.Exit();
                  }
        }
         */
    //-------------------------------------------------------
       
     /*  static void Main()
        {
            bool createNew;
            using (System.Threading.Mutex m = new System.Threading.Mutex(true, Application.ProductName, out createNew))
            {
                if (createNew)
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new frmMain());
                   // CongQiXiTong();
                    //-----------------------
                    // Application.EnableVisualStyles();
                    //Application.SetCompatibleTextRenderingDefault(false);
                    //Application.Run(new frmMain());
                }
                else
                {
                    MessageBox.Show("[2]已经启动了一个程序，请先退出！", "系统提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
     */
       
    //----------------------------------------------------
     /*   public delegate int callback(ref long a);

        private static callback mycall = new callback(newexceptionfilter);

        public static int newexceptionfilter(ref long a)
        {
            System.Threading.Thread.Sleep(1000);
            Process proexplorer = new Process();

       
            proexplorer.StartInfo.FileName = System.Windows.Forms.Application.ExecutablePath;
            proexplorer.Start();
            //return 0; //如果return 0 的话，接着还会弹出windows 底层的异常框
            return 1; //return 1 可以屏蔽掉windows 底层的异常框
        }

        [DllImport("kernel32")]
        private static extern int setunhandledexceptionfilter(callback cb);
        */
    //-------------------------------------------------------
   

        static void Main()
        {
          //  System.Diagnostics.Process.Start("msiexe","/X{460247B2-5FF0-4AF7-B4BC-B8BA3025231B}");
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new frmMain());
        }
    //-----------------------------------------
    }
}
