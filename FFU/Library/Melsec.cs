using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Animation;
using System.Threading;

namespace FFU
{
    public class Melsec
    {
        public enum DeviceType : byte
        {
            M = 0x90,
            L = 0x92,
            D = 0xA8,
            ZR = 0xB0,
        }

        #region Fields
        private TcpClient tcpClient;
        private NetworkStream ns;
        private int port;
        private string ip;

        private bool IsPLCConnected = false;
        private Mutex plcLock = new Mutex();

        #endregion

        #region Methods
        private void PLCOpen()
        {
            if (IsPLCConnected) return;
            try
            {
                tcpClient = new TcpClient();
                tcpClient.Connect(IPAddress.Parse(ip), port);
                if (tcpClient != null && tcpClient.Connected)
                {
                    ns = tcpClient.GetStream();
                    IsPLCConnected = true;
                }
                //log.WriteLog("PLC Connection Opened!", LOG.LogType.PLC);
            }
            catch (Exception ex)
            {
                IsPLCConnected = false;
                //log.WriteLog("PLC Open Fail:" + ex.Message.ToString(), LOG.LogType.PLC);
            }
        }

        private void PLCClose()
        {
            if (IsPLCConnected) return;
            if (ns != null)
            {
                ns.Dispose();
                ns = null;
            }
            if (tcpClient != null)
            {
                tcpClient.Close();
            }

            IsPLCConnected = false;
            //log.WriteLog("PLC Connection Closed!", LOG.LogTyPe.PLC);
        }

        private void PLCReadDeviceBlock(int nStartAddr, int nLength, ref int[] refData)
        {
            plcLock.WaitOne();
            int[] returnValue = new int[nLength];
          
          byte[] sendByte = new byte[21 + nLength * 2];
            byte[] MCLength = BitConverter.GetBytes(nLength * 2 + 12);
            byte[] DataLength = BitConverter.GetBytes(nLength);
            byte[] Byteadd = BitConverter.GetBytes(nStartAddr);

            sendByte[0] = 0x50; //SubHeader
            sendByte[1] = 0x00; //SubHeader
            sendByte[2] = 0x00; //Access Route-StationNo
            sendByte[3] = 0xFF; //AccessRoute-Network No - 00: Network| FF: PC No
            sendByte[4] = 0xFF; //Access Route-StationNo
            sendByte[5] = 0x03; //Access Route-StationNo
            sendByte[6] = 0x00;
            sendByte[7] = 0x0C;
            sendByte[8] = 0X00;
            sendByte[9] = 0x10;
            sendByte[10] = 0x00;
            sendByte[11] = 0x01; //Command //read
            sendByte[12] = 0x04; //Command //read
            sendByte[13] = 0x00; //Subcommand
            sendByte[14] = 0x00; //SubCommand
            sendByte[15] = Byteadd[0];
            sendByte[16] = Byteadd[1];
            sendByte[17] = Byteadd[2];
            sendByte[18] = 0xA8; // device D
            sendByte[19] = DataLength[0];
            sendByte[20] = DataLength[1];
            try
            {
                ns.Write(sendByte, 0, 21);
                ns.Flush();
                ns.ReadTimeout = 1000;

                byte[] readByte = new byte[256];
                int length = ns.Read(readByte, 0, readByte.Length);
                if (length >= 0)
                {
                    int index = 0;
                    int num11 = ((readByte[8] << 8) + readByte[7]) - 2;
                    int[] numArray = new int[num11 / 2];
                    int num12 = 11;
                    while (num12 < (11 + num11))
                    {
                        numArray[index] = readByte[num12 + 1] << 8;
                        numArray[index] += (Int16)readByte[num12];
                num12 += 2;
                        index++;
                    }
                    returnValue = numArray;
                }
                else
                {
                    for (int i = 0; i < nLength; i++)
                    {
                        returnValue[i] = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                //log.WriteLog("Read Block Fail:" + ex.Message.ToString(), LOG.LogType.PLC);
            }
            finally
            {
                refData = returnValue;
                plcLock.ReleaseMutex();
            }
        }

        private void PLCWriteDevice(int nAddress, int nVal)
        {
            int[] val = new int[] { nVal };
            PLCWriteDeviceBlock(nAddress, 1, val);
        }

        private void PLCWriteDeviceBlock(int nStartAddr, int nLength, int[] nVal)
        {
            if (!IsPLCConnected || ns == null) return;

            plcLock.WaitOne();

            byte[] sendByte = new byte[21 + nLength * 2];
            byte[] mcLengthByte = BitConverter.GetBytes(12 + nLength * 2);
            byte[] lengthByte = BitConverter.GetBytes(nLength);
            byte[] addrByte = BitConverter.GetBytes(nStartAddr);

            sendByte[0] = 80;
            sendByte[1] = 0;
            sendByte[2] = 0;
            sendByte[3] = 0xFF;
            sendByte[4] = 0xFF;
            sendByte[5] = 3;
            sendByte[6] = 0;
            sendByte[7] = mcLengthByte[0];
            sendByte[8] = mcLengthByte[1];
            sendByte[9] = 10;
            sendByte[10] = 0;
            sendByte[11] = 1;
            sendByte[12] = 20;
            sendByte[13] = 0;
            sendByte[14] = 0;
            sendByte[15] = addrByte[0];
            sendByte[16] = addrByte[1];
            sendByte[17] = addrByte[2];
            sendByte[18] = 0xA8;
            sendByte[19] = lengthByte[0];
            sendByte[20] = lengthByte[1];

            for (int i = 0; i < nLength; i++)
            {
                byte[] data = BitConverter.GetBytes(nVal[i]);
                sendByte[21 + i * 2] = data[0];
                sendByte[21 + i * 2 + 1] = data[1];
            }

            try
            {
                ns.Write(sendByte, 0, 21 + nLength * 2);
                ns.Flush();
                byte[] readByte = new byte[256];
                int length = ns.Read(readByte, 0, readByte.Length);
            }
            catch (Exception ex)
            {
                //log.WriteLog("Write Device Fail:" + ex.Message.ToString(), LOG.LogType.PLC);
            }
            finally
            {
                plcLock.ReleaseMutex();
            }
        }
        #endregion
    }
}
