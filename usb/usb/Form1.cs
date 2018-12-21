using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using System.Threading.Tasks;
using System.Runtime.InteropServices; //声明调用dll需要引用命名空间
//引用空间
using System.Threading;
using System.Collections;
using System.IO;


namespace usb
{
    public partial class Form1 : Form
    {
        /*  ======================声明dll文件中需要调用的函数,以下是调用windows的API的函数======================= */

        //获得USB设备的GUID
        [DllImport("hid.dll")]
        public static extern void HidD_GetHidGuid(ref Guid HidGuid);
        Guid guidHID = Guid.Empty;


        //获得一个包含全部HID信息的结构数组的指针，具体配置看函数说明:https://docs.microsoft.com/zh-cn/windows/desktop/api/setupapi/nf-setupapi-setupdigetclassdevsw
        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern IntPtr SetupDiGetClassDevs(ref Guid ClassGuid, uint Enumerator, IntPtr HwndParent, DIGCF Flags);
        IntPtr hDevInfo;

        public enum DIGCF   
        {
            DIGCF_DEFAULT = 0x1,
            DIGCF_PRESENT = 0x2,
            DIGCF_ALLCLASSES = 0x4,
            DIGCF_PROFILE = 0x8,
            DIGCF_DEVICEINTERFACE = 0x10
        }

        //该结构用于识别一个HID设备接口，获取设备，true获取到
        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern Boolean SetupDiEnumDeviceInterfaces(IntPtr hDevInfo, IntPtr devInfo, ref Guid interfaceClassGuid, UInt32 memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);
        //SetupDiEnumDeviceInterfaces 识别出来的SP_DEVICE_INTERFACE_DATA结构，该结构标识满足搜索参数的接口
        public struct SP_DEVICE_INTERFACE_DATA
        {
            public int cbSize;                     //SP_DEVICE_INTERFACE_DATA结构的大小
            public Guid interfaceClassGuid;        //设备接口所属的类的GUID
            public int flags;                      //接口转态标记
            public int reserved;                   //保留，不做使用
        }

        // 获得一个指向该设备的路径名，接口的详细信息 必须调用两次 第1次返回长度 第2次获取数据 
        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, IntPtr deviceInterfaceDetailData,
                                                                   int deviceInterfaceDetailDataSize, ref int requiredSize, SP_DEVINFO_DATA deviceInfoData);

        [StructLayout(LayoutKind.Sequential)]
        public class SP_DEVINFO_DATA
        {
            public int cbSize = Marshal.SizeOf(typeof(SP_DEVINFO_DATA));
            public Guid classGuid = Guid.Empty; // temp
            public int devInst = 0; // dumy
            public int reserved = 0;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        internal struct SP_DEVICE_INTERFACE_DETAIL_DATA
        {
            internal int cbSize;
            internal short devicePath;
        }
         
        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern Boolean SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        //获取设备文件（获取句柄）
        [DllImport("kernel32.dll", SetLastError = true)]
        //根据要求可在下面设定参数，具体参考参数说明：https://docs.microsoft.com/zh-cn/windows/desktop/api/fileapi/nf-fileapi-createfilea
        private static extern int CreateFile
            (
             string lpFileName,                             // file name 文件名
             uint   dwDesiredAccess,                        // access mode 访问模式
             uint   dwShareMode,                            // share mode 共享模式
             uint   lpSecurityAttributes,                   // SD 安全属性
             uint   dwCreationDisposition,                  // how to create 如何创建
             uint   dwFlagsAndAttributes,                   // file attributes 文件属性
             uint   hTemplateFile                           // handle to template file 模板文件的句柄
            );
      
        [DllImport("Kernel32.dll", SetLastError = true)]
        private static extern bool ReadFile
            (
                IntPtr hFile,
                byte[] lpBuffer,
                uint nNumberOfBytesToRead,
                ref uint lpNumberOfBytesRead,
                IntPtr lpOverlapped
            );

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern Boolean WriteFile
            (
                IntPtr hFile,
                byte[] lpBuffer,
                uint nNumberOfBytesToWrite,
                ref uint nNumberOfBytesWrite,
                IntPtr lpOverlapped
            );
        
        [DllImport("hid.dll")]       
        /*HidDeviceObject:指定顶级集合的打开句柄
          Attributes:指向调用者分配的HIDD_ATTRIBUTES结构的指针，该结构返回由HidDeviceObject指定的集合的属性*/        
        private static extern Boolean HidD_GetAttributes(IntPtr hidDeviceObject, out HIDD_ATTRIBUTES HIDD_ATTRIBUTES);       
        //HidD_GetAttributes的调用者使用此结构来对比查找设备
        public unsafe struct HIDD_ATTRIBUTES
        {
            public int    Size;            //指定HIDD_ATTRIBUTES结构的大小（以字节为单位）
            public ushort VendorID;        //指定HID设备的供应商ID( VID )
            public ushort ProductID;       //指定HID设备的产品ID( PID )
            public ushort VersionNumber;   //指定HIDClass设备的制造商版本号
        }

        //自定义的结构体，用来存放自己要操作的设备信息
        public unsafe struct my_usb_id
        {
            public ushort my_vid;
            public ushort my_Pid;
            public ushort my_number;
        }

        /**/
        [DllImport("hid.dll")]
        /*HidP_GetCaps返回一个顶级集合的 HIDP_CAPS结构，获取设备具体信息，这里暂时用不上
        PreparsedData:指向顶级集合的预分析数据的指针; Capabilities:指向调用程序分配的缓冲区的指针，该缓冲区用于返回集合的HIDP_CAPS结构*/
        private static extern uint HidP_GetCaps(IntPtr PreparsedData, out HIDP_CAPS Capabilities);
        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct HIDP_CAPS
        {
            public ushort UsagePage;                                            //指定顶级集合的使用情况
            public uint   Usage;                                                //指定顶级集合的 使用ID
            public ushort InputReportByteLength;                                //指定所有输入报告的最大大小（以字节为单位）
            public ushort OutputReportByteLength;                               //指定所有输出报告的最大大小（以字节为单位）
            public ushort FeatureReportByteLength;                              //指定所有功能报告的最大长度（以字节为单位）
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]               //保留供内部系统使用数组
            public ushort NumberLinkCollectionNodes;                            //指定的数量HIDP_LINK_COLLECTION_NODE了为这个顶级集合返回的结构HidP_GetLinkCollectionNodes
            public ushort NumberInputButtonCaps;                                //指定HidP_GetButtonCaps返回的输入HIDP_BUTTON_CAPS结构的数量
            public ushort NumberInputValueCaps;                                 //指定HidP_GetValueCaps返回的输入HIDP_VALUE_CAPS结构的数量
            public ushort NumberInputDataIndices;                               //指定分配给所有输入报告中的按钮和值的数据索引数
            public ushort NumberOutputButtonCaps;                               //指定HidP_GetButtonCaps返回的输出HIDP_BUTTON_CAPS结构的数量
            public ushort NumberOutputValueCaps;                                //指定HidP_GetValueCaps返回的输出HIDP_VALUE_CAPS结构的数量
            public ushort NumberOutputDataIndices;                              //指定分配给所有输出报告中的按钮和值的数据索引数
            public ushort NumberFeatureButtonCaps;                              //指定HidP_GetButtonCaps返回的功能HIDP_BUTTONS_CAPS结构的总数
            public ushort NumberFeatureValueCaps;                               //指定HidP_GetValueCaps返回的功能HIDP_VALUE_CAPS结构的总数
            public ushort NumberFeatureDataIndices;                             //指定分配给所有要素报告中的按钮和值的数据索引数
        }

        [DllImport("hid.dll")]
        private static extern Boolean HidD_GetPreparsedData(IntPtr hidDeviceObject, out IntPtr PreparsedData);     

        //释放设备
        [DllImport("hid.dll")]
        static public extern bool HidD_FreePreparsedData(ref IntPtr PreparsedData);

        //关闭访问设备句柄，结束进程的时候把这个加上保险点
        [DllImport("kernel32.dll")]
        static public extern int CloseHandle(int hObject);

        //查看数据传输异常函数
        [DllImport("kernel32.dll", EntryPoint = "GetProcAddress", SetLastError = true)]
        public static extern IntPtr GetProcAddress(int hModule, [MarshalAs(UnmanagedType.LPStr)] string lpProcName);

        //定于句柄序号和一些参数，具体可以去网上找这些API的参数说明
        int HidHandle = -1;
        int sele = 0;
        int usb_flag = 0;
        bool result;
        string devicePathName;

        //CreateFile参数配置
        public const uint GENERIC_READ = 0x80000000;
        public const uint GENERIC_WRITE = 0x40000000;
        public const uint FILE_SHARE_READ = 0x00000001;
        public const uint FILE_SHARE_WRITE = 0x00000002;
        public const int OPEN_EXISTING = 3;

        private void UsBMethod(int index)
        {
            //获取USB设备的GUID
            HidD_GetHidGuid(ref guidHID);

            //Console.WriteLine(" GUID_HID = "+ guidHID);       //输出guid信息调试用

            //获取系统中存在的所有设备的列表，这些设备已从存储卷设备接口类启用了接口
            hDevInfo = SetupDiGetClassDevs(ref guidHID, 0, IntPtr.Zero, DIGCF.DIGCF_PRESENT | DIGCF.DIGCF_DEVICEINTERFACE);

            int bufferSize = 0;
            ArrayList HIDUSBAddress = new ArrayList();

            while (true)
            {

                //获取设备，true获取到
                SP_DEVICE_INTERFACE_DATA DeviceInterfaceData = new SP_DEVICE_INTERFACE_DATA();
                DeviceInterfaceData.cbSize = Marshal.SizeOf(DeviceInterfaceData);

                for (int i = 0; i < 3; i++)
                {
                    //识别HID设备接口，获取设备，返回true成功
                    result = SetupDiEnumDeviceInterfaces(hDevInfo, IntPtr.Zero, ref guidHID, (UInt32)index, ref DeviceInterfaceData);
                }

                //Console.WriteLine(" 识别HID接口\t"+result);       //识别接口打印信息查看

                //第一次调用出错，但可以返回正确的Size 
                SP_DEVINFO_DATA strtInterfaceData = new SP_DEVINFO_DATA();
                //获得一个指向该设备的路径名，接口的详细信息 必须调用两次 第1次返回路径长度 
                result = SetupDiGetDeviceInterfaceDetail(hDevInfo, ref DeviceInterfaceData, IntPtr.Zero, 0, ref bufferSize, strtInterfaceData);

                //第二次调用传递返回值，调用即可成功 , 第2次获取路径数据 
                IntPtr detailDataBuffer = Marshal.AllocHGlobal(bufferSize);
                SP_DEVICE_INTERFACE_DETAIL_DATA detailData = new SP_DEVICE_INTERFACE_DETAIL_DATA();
                detailData.cbSize = Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DETAIL_DATA));

                Marshal.StructureToPtr(detailData, detailDataBuffer, false);
                result = SetupDiGetDeviceInterfaceDetail(hDevInfo, ref DeviceInterfaceData, detailDataBuffer, bufferSize, ref bufferSize, strtInterfaceData);

                if (result == false)
                {
                    break;
                }

                //获取设备路径访
                IntPtr pdevicePathName = (IntPtr)((int)detailDataBuffer + 4);
                devicePathName = Marshal.PtrToStringAuto(pdevicePathName);
                HIDUSBAddress.Add(devicePathName);

                //Console.WriteLine(" Get_DvicePathName = "+ devicePathName);     //打印路径信息，调试用

                //连接设备文件
                int aa = CT_CreateFile(devicePathName);
                usb_flag = aa;
                if (aa == 1)                                    //设备连接成功               
                {
                    //获取设备VID PID 出厂编号信息判断是否跟自定义的USB设备匹配，匹配返回 1
                    usb_flag = HidD_GetAttributes(HidHandle);
                    if (usb_flag == 1) break;
                    else usb_flag = 0;
                }
                else usb_flag = 0;
                index++;
            }

        }

        /*  =================建立和设备的连接==================    */
        public unsafe int CT_CreateFile(string DeviceName)
        {
            HidHandle = CreateFile
            (
                DeviceName,
                //GENERIC_READ |          // | GENERIC_WRITE,//读写，或者一起
                GENERIC_READ | GENERIC_WRITE,
                //FILE_SHARE_READ |       // | FILE_SHARE_WRITE,//共享读写，或者一起
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                0,
                OPEN_EXISTING,
                0,
                0
             );

            //Console.WriteLine(" IN_DeviceName = " + DeviceName);                    //查看参数是否传入           

            if (HidHandle == -1) //INVALID_HANDLE_VALUE实际值等于-1，连接失败
            {

                //Console.WriteLine(" 失败 HidHandle = 0x" + "{0:x}",HidHandle );     //查看状态，打印调试用
                return 0;
            }
            else    //连接成功
            {
                //Console.WriteLine(" 成功 HidHandle = 0x" + "{0:x}",HidHandle);      //查看状态，打印调试用
                return 1;
            }
        }


        /*  ==============获取设备的VID PID 出厂编号等信息，存放到HIDD_ATTRIBUTES==============   */
        public unsafe int HidD_GetAttributes(int handle)
        {
            HIDD_ATTRIBUTES HIDD_ATTRIBUTE = new HIDD_ATTRIBUTES();
            //handle是CreateFile函数返回一个有效的设备操作句柄，HIDD_ATTRIBUTES是函数返回的结构体信息（VID PID 设备号）
            bool sel = HidD_GetAttributes((IntPtr)handle, out HIDD_ATTRIBUTE);

            /*//打印VID、PID信息以16进制显示调试用，打印数据前不能接+号，不然打印不出来，信息为0
            Console.Write("\t" + "VID:{0:x}", HIDD_ATTRIBUTE.VendorID );
            Console.Write("\t" + "PID:{0:x}", HIDD_ATTRIBUTE.ProductID);
            Console.WriteLine("\r\n");   */

            if (sel == true)  //获取设备信息成功
            {
                //对自己定义的my_usb_id结构体赋值，输入自己要操作的设备参数，用来跟读取出来的设备参数比较
                my_usb_id my_usb_id = new my_usb_id();
                my_usb_id.my_vid = 4292;        //自定义的USB设备VID 0x10c4=4292
                my_usb_id.my_Pid = 33485;       //自定义的USB设备PID 0x82cd=33485

                if (my_usb_id.my_vid == HIDD_ATTRIBUTE.VendorID && my_usb_id.my_Pid == HIDD_ATTRIBUTE.ProductID) //判断识别出来的是不是自定义的USB设备
                {
                    //Console.WriteLine("获取VID PID成功"); //打印信息调试用
                    sele = 1;
                }
                else sele = 0;
                return sele;
            }
            else
            {
                //Console.WriteLine("获取VID PID失败");     //打印信息调试用
                return sele = 0;
            }
        }


        /*  释放关闭USB设备   */
        public void Dispost()
        {
            //释放设备资源(hDevInfo是SetupDiGetClassDevs获取的)
            SetupDiDestroyDeviceInfoList(hDevInfo);
            //关闭连接(HidHandle是Create的时候获取的)
            CloseHandle(HidHandle);
        }
        //====================================================

        public Form1()
        {
            InitializeComponent();
        }       

        int link_flag = 0;
        int SH;
        int SW;
        int self_SH;
        int self_SW;
        int star_win_flag = 1;//窗口初始化位置标志位,防止隐藏窗口后定时器重新跑窗口函数再次在初始化位置打开
        private void Form1_Load(object sender, EventArgs e)
        {
            //获取显示器屏幕的大小,不包括任务栏、停靠窗口
            SH = Screen.PrimaryScreen.WorkingArea.Height;
            SW = Screen.PrimaryScreen.WorkingArea.Width;
            //获取当前活动窗口高度跟宽度
            self_SH = this.Size.Height;
            self_SW = this.Size.Width;
            if(star_win_flag==1)
            {
                //设置窗口打开的位置为下方居中
                SetDesktopLocation((SW - self_SW) / 2, SH - self_SH);
                star_win_flag = 0;
            }
            

            UsBMethod(0);                           //连接设备
            if (usb_flag == 0) UsBMethod(0);
            if (usb_flag == 0)
            {
                MessageBox.Show(" Please insertion the USB ！"); //USB 检测失败信息    
                link_flag = 1;
            }

            //============添加窗体所在位置定时检测=================
            TopMost = true;
            System.Windows.Forms.Timer MyTimer = new System.Windows.Forms.Timer();
            MyTimer.Tick += new EventHandler(StopRectTimer_Tick);
            MyTimer.Interval = 100;
            MyTimer.Enabled = true;

        }

        //=================隐藏窗体&显示部分==================
        int check_flag = 0; //窗体隐藏标志位，0为不开启隐藏功能，初始默认0
        int win = 0;
        int frmX;
        int frmY;
        private void StopRectTimer_Tick(object sender, EventArgs e)
        {
            // 获取窗体位置
            frmX = this.Location.X;
            frmY = this.Location.Y;

            if (check_flag == 1)
            {
                //获取窗口的边沿与桌面的间距，判断窗口是否靠近边沿里面-1个位置
                if (this.Left <= 0) //获取控件左边沿与桌面左边沿的间距，窗口靠近左边桌面边沿         
                    win = 1;
                else if (this.Top <= 0 && this.Left > 0 && this.Right < SW - 1)////获取控件上边沿与桌面上边沿的间距，窗口靠近顶端桌面边沿 
                    win = 2;
                else if (this.Right >= SW) ////获取控件右边沿与桌面左边沿的间距，窗口靠近右边桌面边沿  
                    win = 3;
                else //窗体没有靠近边沿
                    win = 0;

                /* Cursor.Position获取当前鼠标的位置
                 * Bounds.Contains(Cursor.Position)获取鼠标位置是否在窗口边界里面，在返回ture
                 *如果鼠标在窗体上，则根据停靠位置显示整个窗体
                 *窗体边沿计算是以左边沿为主*/

                if (Bounds.Contains(Cursor.Position))
                {
                    switch (win)
                    {
                        case 1:
                            this.Opacity = 1.0f;    //窗口恢复不透明状态
                                                    //窗体靠近左沿时，鼠标在窗体显示完整窗体 
                            SetDesktopLocation(0, frmY);
                            break;
                        case 2:
                            this.Opacity = 1.0f;    //窗口恢复不透明状态
                                                    //窗体靠近顶部时，鼠标在窗体显示完整窗体  
                            SetDesktopLocation(frmX, 0);
                            break;
                        case 3:
                            this.Opacity = 1.0f;    //窗口恢复不透明状态
                                                    //窗体靠近右沿时，鼠标在窗体显示完整窗体 
                            SetDesktopLocation(SW - self_SW, frmY);
                            break;
                    }
                }

                //如果鼠标离开窗体，则根据停靠位置隐藏窗体（即把窗体显示出桌面以外），但须留出部分窗体边缘以便鼠标选中窗体，这里留7个位置
                else
                {
                    switch (win)
                    {
                        case 1:
                            this.Opacity = 0.2f; //窗口半透明
                                                 //窗体靠近左沿时，鼠标不在窗体时隐藏 
                            SetDesktopLocation(20 - self_SW, frmY);
                            break;
                        case 2:
                            this.Opacity = 0.2f; //窗口半透明
                                                 //窗体靠近顶部时，鼠标不在窗体时隐藏  
                            SetDesktopLocation(frmX, 20 - self_SH);
                            break;
                        case 3:
                            this.Opacity = 0.2f; //窗口半透明
                                                 //窗体靠近右沿时，鼠标不在窗体时隐藏  
                            SetDesktopLocation(SW - 20, frmY);
                            break; 
                    }
                }
            }
        }

        /*==========窗体边沿隐藏功能开启选择框===========*/
        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.CheckState == CheckState.Checked) //判断复选框选中

            {
                check_flag = 1;
                //if(win==0)//判断框功能选中时，判断窗口不在边沿时自动收到上边沿中间隐藏
                {
                    this.Opacity = 0.2f; //窗口半透明                   
                    SetDesktopLocation((SW - self_SW) / 2, 20-SH );
                }
                
                //MessageBox.Show("checkbox1 is checked\n" + checkBox1.Text);

            }

            else if (checkBox1.CheckState == CheckState.Unchecked) //判断复选框没选中

            {
                check_flag = 0;
                //MessageBox.Show("checkbox1 is Unchecked\n" + checkBox1.Text);

            }
        }
        /*  ======================USB 插拔检测===========================    */

        protected override void WndProc(ref Message m)
        {
            //Console.WriteLine(m.WParam.ToInt32());    //打印程序检测到的变化信息
            try
            {
                //检测到USB口发生了变化,这里USB口变化时触发值是7，并判断是否在程序开关开启的状态下
                if (m.WParam.ToInt32() == 7)
                {
                    UsBMethod(0);       //检测到USB口有变化时重新连接设备，连接不成功则判断自定义的USB设备已断开

                    if (usb_flag == 1)  //没找到设备处理事件
                    {
                        //Console.WriteLine("usb_flag1=" + usb_flag);
                        button2.Enabled = true;
                        button3.Enabled = true;
                        button4.Enabled = true;
                        button1.Enabled = true;
                        if (link_flag == 1)
                        {
                            link_flag = 0;
                            MessageBox.Show(" USB link succeed ！");
                        }
                    }
                    else
                    {
                        //Console.WriteLine("usb_flag0=" + usb_flag);
                        button2.Enabled = false;
                        button3.Enabled = false;
                        button4.Enabled = false;
                        button1.Enabled = false;
                        Dispost();      //关闭设备
                        if (link_flag == 0)
                        {
                            link_flag = 1;
                            MessageBox.Show(" Please insertion the USB ！");
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            base.WndProc(ref m);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (usb_flag == 1)   //USB识别成功后发送数据
            {
                uint read = 0;
                byte[] src = { 1, 2,1 };
                bool isread = WriteFile((IntPtr)HidHandle, src, (uint)9, ref read, IntPtr.Zero);
                if (isread == false)
                {
                    int errCode = Marshal.GetLastWin32Error();
                    // Console.WriteLine("数据发送失败！错误代码：" + errCode);
                }
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (usb_flag == 1)   //USB识别成功后发送数据
            {
                uint read = 0;
                byte[] src = { 1, 3,1 };
                bool isread = WriteFile((IntPtr)HidHandle, src, (uint)9, ref read, IntPtr.Zero);
                if (isread == false)
                {
                    int errCode = Marshal.GetLastWin32Error();
                    // Console.WriteLine("数据发送失败！错误代码：" + errCode);
                }
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (usb_flag == 1)   //USB识别成功后发送数据
            {
                uint read = 0;
                byte[] src = { 1, 4,1 };
                bool isread = WriteFile((IntPtr)HidHandle, src, (uint)9, ref read, IntPtr.Zero);
                if (isread == false)
                {
                    int errCode = Marshal.GetLastWin32Error();
                    // Console.WriteLine("数据发送失败！错误代码：" + errCode);
                }
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (usb_flag == 1)   //USB识别成功后发送数据
            {
                uint read = 0;
                byte[] src = { 1, 5,1 };
                bool isread = WriteFile((IntPtr)HidHandle, src, (uint)9, ref read, IntPtr.Zero);
                if (isread == false)
                {
                    int errCode = Marshal.GetLastWin32Error();
                    // Console.WriteLine("数据发送失败！错误代码：" + errCode);
                }
            }
        }


    }
}
