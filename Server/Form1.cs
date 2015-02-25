using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

using System.Net;
using System.Threading;
using System.Net.Sockets;               
using System.Collections;               

namespace Server
{
    public partial class Form1 : Form
    {
        private Socket clientSocket;                        
        private Thread clientService;                        
        String SendData_8 = "";
        String SendData_9 = "";
        private const int Port = 5800;    //Port
        string[] stringArray = new string[5] { "C0F0000000074198", "C0F0000000074193", "C0F0000000123456", "aaacccddd111222", "cccddd111222" };   // TagIDs for Downloading to Devices  
      
        public static ArrayList HandleArray = new ArrayList();                    
        public static ArrayList clienthandle = new ArrayList(); 
        private delegate void Add_Invoke(ListViewItem lst);            
        private delegate void Del_Invoke(string handle);      
        private delegate void UpdateStatusDelegate(string status);
        private delegate void Add_Combox_Delegate(string Readerid);
        Dictionary<string, Socket> dictSocket = new Dictionary<string, Socket>();
        Dictionary<Socket,string> dictFirmware = new Dictionary<Socket,string>();

      public  Socket stServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
  
        public Form1()
        {
            InitializeComponent();
                
            for (int i=0; i < stringArray.Length; i++)
            {
                stringArray[i] = stringArray[i].PadLeft(24, ' ');
            }

            Array.Sort(stringArray);

            int iCount=0;
            string PlateNumber = "GOODPLATE";
          
            foreach (string str in stringArray)
            {
                iCount++;
                SendData_9 = SendData_9 + String.Format("{0:D4}", iCount) + str + "05" + "20100101" + "21100101"+ PlateNumber.PadLeft(14,' ');
                
            }
            iCount = 0;
            foreach (string str in stringArray)
            {
                iCount++;
                SendData_8 = SendData_8 + String.Format("{0:D4}", iCount) + str + "05" ;

            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            lstViewServer.View = View.Details;
            int lngWidth = lstViewServer.Width;
            lstViewServer.Columns.Add("NO.", lngWidth * 1 / 15, HorizontalAlignment.Center);
            lstViewServer.Columns.Add("READER ID", lngWidth * 3 / 15, HorizontalAlignment.Center);
            lstViewServer.Columns.Add("READER IP", lngWidth * 3 / 15, HorizontalAlignment.Center);
            lstViewServer.Columns.Add("SOCKET HANDLE", lngWidth * 2 / 15, HorizontalAlignment.Center);
            lstViewServer.Columns.Add("PORT", lngWidth * 2 / 15, HorizontalAlignment.Center);
            lstViewServer.Columns.Add("CONNECT TIME", lngWidth * 3 / 15, HorizontalAlignment.Center);
            lstViewServer.FullRowSelect = true;
            
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Thread ThrWinsockListen = new Thread(new ThreadStart(WinsockListen));        // Winsock Listen Thread
            ThrWinsockListen.IsBackground = true;
            ThrWinsockListen.Start();

            button1.Text = "Listening";
            button1.Enabled = false;
        }


        public void WinsockListen()
        {
                IPHostEntry ipHN = new IPHostEntry();
            ipHN = Dns.GetHostEntry(Dns.GetHostName());

            string ServerIP = "";
            for (int i = 0; i < ipHN.AddressList.Length; i++)
            {
                ServerIP = ipHN.AddressList[i].ToString();//get local IP
                if (ServerIP.IndexOf(".") >= 0)
                    break;
            }
                    
            if (ServerIP == "")
                return;

            IPAddress ip = IPAddress.Parse(ServerIP);
            IPEndPoint iep = new IPEndPoint(ip, Port);
    
            byte[] byteMessage = new byte[1024];
            stServer.Bind(iep);             
            stServer.Listen(0);

            while (true)
            {
                try
                {
                    clientSocket = stServer.Accept();                                       //A new Client
                                  clientService = new Thread(new ThreadStart(ServiceClient));             // New thread for Client Socket
                    clientService.IsBackground = true;                 
                    clientService.Start();
                }
                catch
                {
                    continue;
                }
            }
        }


        private void ServiceClient()
        {
            bool keepalive = true;                          //Client Sockets activing flag    
            string socketFlag = "";                         //Client Socket flag
            EndPoint endPoint = null;
            string strip="";                                // Device IP
            string strReaderID = "";                        //Device NO.
            string strPort = "";                            //Port 
            Socket childClient = clientSocket;              //New Client Socket
            
            while (keepalive)
            {
                try
                {
                    Byte[] byBuff = new Byte[256];                                     
                    childClient.Receive(byBuff, byBuff.Length, 0);
                    string strBuff = Encoding.Default.GetString(byBuff).TrimEnd('\0');  //Data from Client
                    string mystr;
                    string strFindKey="";

              
                    socketFlag = childClient.Handle.ToString();         // Socket handle
                    endPoint = childClient.RemoteEndPoint;              //client IP and Port
                    strip = ((IPEndPoint)endPoint).Address.ToString();
                    strPort = ((IPEndPoint)endPoint).Port.ToString();
                    foreach (var kv in dictSocket)
                    {
                        if (kv.Value == childClient)
                        {
                            strFindKey = kv.Key;
                            break;
                        }
                    }

                   if (strBuff == "")                                     
                   {                                                    
                        childClient.Disconnect(false);
                        keepalive = false;
                       Socket_Connection(1,strReaderID, strip,strPort, socketFlag);
                    }

                 
                    if (strBuff.Length >= 10)
                    {
                        string sFg = strBuff.Substring(0, strBuff.Length);
                        

                        if (sFg.IndexOf("READERID")>-1)      //Receive Device NO.
                        {
                           
                            mystr = sFg.Substring(0, 19) + "\n";
                            strReaderID = mystr.Substring(9, 10);
                            if (dictSocket.ContainsKey(strReaderID) == false)
                            {
                                dictSocket.Add(strReaderID, childClient);
                                this.BeginInvoke(new Add_Combox_Delegate(Add_Combox), new object[] { strReaderID });

                                byte[] buffer;
                                buffer = System.Text.Encoding.UTF8.GetBytes("FIRMWARE" + "\r\n");
                                childClient.Send(buffer);//to get firmware
                            }
                            this.BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] {mystr });
                            if (HandleArray.IndexOf(socketFlag) == -1)
                            {
                                HandleArray.Add(socketFlag);                       //Add Socket handle to array
                                clienthandle.Add(childClient.Handle);
                                Socket_Connection(0,strReaderID, strip,strPort, socketFlag);
                            }
                        }
                        else if (sFg.IndexOf("PARKMODE UPLOAD OK") > -1)
                        {
                            
                            byte[] Mybuffer;              
                            int nDownSize;

                            if (dictFirmware.ContainsKey(childClient))
                            {
                                String fmstr = "";
                                if(!dictFirmware.TryGetValue(childClient,out fmstr))
                                {
                            this.BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { "No found firmware about the Reader, Not continue Download TagIDs" });
                             continue;
                                }

                                float fValue = 0;
                                fValue=Convert.ToSingle(fmstr);

                                if (fValue > 8.0)
                                    Mybuffer = System.Text.Encoding.UTF8.GetBytes(SendData_9);
                                else
                                    Mybuffer = System.Text.Encoding.UTF8.GetBytes(SendData_8);

                                    nDownSize = Mybuffer.Length;
                                    Mybuffer = System.Text.Encoding.UTF8.GetBytes("PARKMODE DOWNLOAD " + nDownSize.ToString() + "\r\n");
                                    childClient.Send(Mybuffer);                                       
                            }   
                        }
                        else if ((sFg.IndexOf("PARKMODE UPLOAD FAIL") > -1) || (sFg.IndexOf("PARKMODE DOWNLOAD FAIL") > -1) || ((sFg.IndexOf("PARKMODE CRC FAIL") > -1)))
                        {

                            if (strFindKey != "")
                            {
                                mystr = strFindKey + ":Download Fail\n";
                                this.BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { mystr });
                            }
                        

                        }
                        else if (sFg.IndexOf("PARKMODE CRC OK") > -1)
                        {
                            if (strFindKey != "")
                            {
                                mystr = strFindKey + ":Download Success\n";
                                this.BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { mystr });
                            }                                           
                        }

                        else if (sFg.IndexOf("DOWNLOAD OK") > -1)
                        {
                            byte[] Mybuffer;
                            byte[] mySendBuffer;
                            string strmydata;
                            ushort mycrc = 0;

                            String fmstr = "";
                            String senddata = SendData_8;


                            if (dictFirmware.TryGetValue(childClient, out fmstr))
                            {
                                float fValue = 0;
                                fValue = Convert.ToSingle(fmstr);

                                if (fValue > 8.0)
                                senddata=SendData_9;


                                Mybuffer = System.Text.Encoding.UTF8.GetBytes(senddata);
                                Monitoring_On_line.ClassInfo.CRC16 aa = new Monitoring_On_line.ClassInfo.CRC16();
                                Mybuffer = System.Text.Encoding.UTF8.GetBytes(senddata);
                                mycrc = aa.Crc16ValueMakeTa(Mybuffer, Mybuffer.Length);
                                strmydata = senddata + " " + Convert.ToString(mycrc, 16).PadLeft(4, '0') + "\r\n";
                                mySendBuffer = System.Text.Encoding.UTF8.GetBytes(strmydata);
                                childClient.Send(mySendBuffer);
                            }
                        }
                        
                        else if (sFg.IndexOf("BUMAC=") > -1)
                        {
                            mystr = sFg.Substring(sFg.IndexOf("BUMAC="), 16) + "\n";
                            this.BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { mystr });


                        }
                        else if (sFg.IndexOf("PARKMODE RECORD ") > -1) //Real-Time Read TagID Record
                        {
                            string[] split = sFg.Split(new Char[] { ' ' });
                            mystr = "Get a tagID:" + split[2] + "\n";
                            if (split[2].IndexOf("H") > -1)
                                mystr = "Get a tagID:" + split[2].Substring(split[2].IndexOf("H") + 1, (split[2].Length - 1)) + "\n";
                            this.BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { sFg });
                            this.BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { mystr });

                        }

                        else if (sFg.IndexOf("FIRMWARE") > -1) //Real-Time Read TagID Record
                        {
                            this.BeginInvoke(new UpdateStatusDelegate(UpdateStatus), new object[] { sFg });


                            int iIndex=sFg.IndexOf("-");
                            int iEqu = sFg.IndexOf("=");
                            mystr = sFg.Substring(iEqu+1, iIndex-iEqu-1);// get firmware string

                            if (dictFirmware.ContainsKey(childClient) == false)
                                dictFirmware.Add(childClient, mystr);   
                         }
                                          
                    }
                }
                catch (SocketException se)
                {
                    if (se.ErrorCode == 10054)              
                    {
                        HandleArray.Remove(socketFlag);                
                        childClient.Disconnect(false);
                        keepalive = false;

                        Socket_Connection(1,strReaderID, strip,strPort, socketFlag);
                    }
                    else
                    {
                        continue;
                    }
                }

            }

        }


        //Update Listview
        //flag-0:Online;1-Offline
        private void Socket_Connection(int flag,string strReader, string devip,string strPort, string hd)
        {
            string hostName = strReader;
            string hostPort =strPort;
            string hostIP = devip;   
            int index = lstViewServer.Items.Count + 1;
            ListViewItem lstItem = new ListViewItem(index.ToString());

            if (flag == 0)          //OnLine
            {
                lstItem.SubItems.Add(hostName);
                lstItem.SubItems.Add(hostIP);
                lstItem.SubItems.Add(hd);
                lstItem.SubItems.Add(hostPort);
                lstItem.SubItems.Add(DateTime.Now.ToString());
                lstAdd(lstItem);
            }
            else
            {
                lstDel(hd);
                //when disconnect,then clear all about the socket
                Socket mytempSock;

                if (!string.IsNullOrEmpty(strReader))
                   if (dictSocket.TryGetValue(strReader,out mytempSock))
                   {
                       dictSocket.Remove(strReader);
                       if (dictFirmware.ContainsKey(mytempSock))
                           dictFirmware.Remove(mytempSock);                   
                   }
            }
        }



        public void lstAdd(ListViewItem lstItem)
        {
            Add_Invoke meInvoke = new Add_Invoke(AddListItem);
            this.Invoke(meInvoke, new object[] { lstItem });
        }

        public void Add_Combox(string Readerid)
        {
            if (cboClient.Items.IndexOf(Readerid) == -1)
                cboClient.Items.Add(Readerid);
        }

        public void UpdateStatus(string status)
        {
            this.textBox1.AppendText(status);
        }

   
        private void AddListItem(ListViewItem lst)
        {
            lstViewServer.Items.Add(lst);
        }


        public void lstDel(string handle)
        {
            Del_Invoke meInvoke = new Del_Invoke(DelListItem);
            this.Invoke(meInvoke, new object[] { handle });
        }

        private void DelListItem(string handle)
        {
            ListViewItem lstItem = lstViewServer.FindItemWithText(handle);
            lstViewServer.Items.Remove(lstItem);

        }

       private void timer1_Tick(object sender, EventArgs e)
        {
            UdpClient udpClient = new UdpClient();
            string IP = "255.255.255.255";
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(IP), 5810);
          //  IPHostEntry IpEntry = Dns.GetHostEntry(Dns.GetHostName());
            IPHostEntry IpEntry = Dns.GetHostEntry(Dns.GetHostName());
            string LocalIP="";
            for (int i = 0; i < IpEntry.AddressList.Length; i++)
            {
                LocalIP= IpEntry.AddressList[i].ToString();
                if (LocalIP.IndexOf(".") >= 0)
                    break;
            }
            if (LocalIP != "")
            {
                Byte[] sendBytes = Encoding.ASCII.GetBytes("SERVERIP " + LocalIP + " 5800\r\n");
                udpClient.Send(sendBytes, sendBytes.Length, ep);
            }
        }


        private void button2_Click(object sender, EventArgs e)
        {
            byte[] buffer;
            String aa=this.textBox2.Text.Trim();
            if (aa.Length == 0) return;
            buffer = System.Text.Encoding.UTF8.GetBytes(aa + "\r\n");
            string connectionSokKey = cboClient.Text;

            if (!string.IsNullOrEmpty(connectionSokKey))
                dictSocket[connectionSokKey].Send(buffer);  
        }

        private void button3_Click(object sender, EventArgs e)
        {
            DateTime currentTime;
            string strdatetime;
            byte[] buffer;
          currentTime=new System.DateTime(); 
          currentTime=System.DateTime.Now;
          strdatetime = String.Format("{0:yyyy-MM-dd HH:mm:ss}", currentTime);
          buffer = System.Text.Encoding.UTF8.GetBytes("DATETIME "+strdatetime + "\r\n");
          string connectionSokKey = cboClient.Text;

          if (!string.IsNullOrEmpty(connectionSokKey))
              dictSocket[connectionSokKey].Send(buffer);
        }

        private void button4_Click(object sender, EventArgs e)
        {

            byte[] buffer;
            buffer = System.Text.Encoding.UTF8.GetBytes("BUMAC" + "\r\n");
            string connectionSokKey = cboClient.Text;
            if (!string.IsNullOrEmpty(connectionSokKey))
                dictSocket[connectionSokKey].Send(buffer);
        }

        private void button5_Click(object sender, EventArgs e)
        {
            byte[] buffer;
            buffer = System.Text.Encoding.UTF8.GetBytes("PARKMODE UPLOADLIST " + "\r\n");
            string connectionSokKey = cboClient.Text;
          if  (dictSocket.ContainsKey(connectionSokKey))
              dictSocket[connectionSokKey].Send(buffer);       
        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            byte[] buffer;
            buffer = System.Text.Encoding.UTF8.GetBytes("watchdog" + "\r\n");
            foreach (var kv in dictSocket)
            {
                try
                {
                    kv.Value.Send(buffer);
                }
                catch (SocketException se)
                {
                    
                }

            }
        }

        private void button6_Click(object sender, EventArgs e)
        {
            byte[] buffer;
            buffer = System.Text.Encoding.UTF8.GetBytes("FIRMWARE" + "\r\n");
            string connectionSokKey = cboClient.Text;
            if (!string.IsNullOrEmpty(connectionSokKey))
                dictSocket[connectionSokKey].Send(buffer);
        }

    }
} 

//Hint: The two orders sent to a device should have a interval of 300ms.

   

