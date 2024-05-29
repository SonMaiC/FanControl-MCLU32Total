using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FFU
{
    public class MCLU32
    {
        #region Constructor
        public MCLU32(string portName)
        {
            sp.PortName = portName;
            sp.BaudRate = 9600;
            sp.Parity = Parity.None;
            sp.StopBits = StopBits.One;
            sp.DataBits = 8;
            sp.Encoding = Encoding.GetEncoding(28591);
            sp.ReadTimeout = 1000;
        }
        #endregion

        #region Fields
        private Mutex comLock = new Mutex();
        private SerialPort sp = new SerialPort();
        public bool IsConnected = false;

        /// <summary>
        /// Lower speed limit value 0~1000rpm
        /// </summary>
        private int dataLSV = 0x00;
        public int DataLSV
        {
            get { return dataLSV; }
            set
            {
                if (value < dataHSV) dataLSV = dataHSV;
                if (value < 0) dataLSV = 0;
                dataLSV = value / 10;
            }
        }
        /// <summary>
        /// Upper speed limit value 0~1000rpm
        /// </summary>
        private int dataHSV = 0x64;
        public int DataHSV
        {
            get { return dataHSV; }
            set
            {
                if (value < dataLSV) dataHSV = dataLSV;
                if (value > 1000) dataHSV = 1000 / 10;
                dataHSV = value / 10;
            }
        }
        #endregion

        private enum Mode1
        {
            GroupRead = 0x8E,
            GroupWrite = 0x8D,
            BlockRead = 0x8A,
            BlockWrite = 0x89,
            Reset = 0x99
        }
        private enum Mode2
        {
            GroupRead = 0x83,
            GroupWrite = 0x9F,
            BlockRead = 0x9F,
            BlockWrite = 0xBF,
            Reset = 0xBF
        }

        public void Connect()
        {
            if (sp.IsOpen) return;

            try
            {
                sp.Open();
                if (sp.IsOpen) { IsConnected = true; }
            }
            catch (Exception)
            {
                IsConnected = false;
            }
        }
        public void Disconnect()
        {
            if (!sp.IsOpen) return;
            sp.Close();
            IsConnected = false;
        }
        public bool GroupWrite(int nSpeed)
        {
            return GroupWrite(0, nSpeed);
        }
        public bool GroupWrite(int nMCULID, int nSpeed)
        {
            if (!sp.IsOpen) return false;
            if (nMCULID < 0 || nMCULID >= 32) return false;

            int mculID = nMCULID;
            byte[] packet = new byte[10];
            int dataSV = (nSpeed / 10);
            byte checksum = 0;

            packet[0] = 0x02;
            packet[1] = (byte)Mode1.GroupWrite;
            packet[2] = (byte)Mode2.GroupWrite;
            packet[3] = (byte)(0x81 + mculID);
            packet[4] = 0x9F;
            packet[5] = (byte)dataSV;
            packet[6] = (byte)dataLSV;
            packet[7] = (byte)dataHSV;
            packet[9] = 0x03;
            for (int i = 1; i < 8; i++)
            {
                checksum += packet[i];
            }
            packet[8] = checksum;

            comLock.WaitOne();
            try
            {
                sp.Write(packet, 0, packet.Length);
                byte[] resByte = new byte[256];
                int length = sp.Read(resByte, 0, resByte.Length);
                if (length <= 0) return false;
                if (resByte[1] == (byte)Mode1.GroupWrite &&
                    resByte[2] == (byte)Mode2.GroupWrite &&
                    resByte[3] == (byte)(0x81 + mculID) &&
                    resByte[5] == 0xB9)
                    return true;
                else
                    return false;
            }
            catch { return false; }
            finally
            {
                comLock.ReleaseMutex();
            }
        }
        public bool BlockWrite(int nLCUID, int nSpeed)
        {
            return BlockWrite(0, nLCUID, nSpeed);
        }
        public bool BlockWrite(int nMCULID, int nLCUID, int nSpeed)
        {
            if (!sp.IsOpen) return false;
            if (nMCULID < 0 || nMCULID >= 32 || nLCUID < 0 || nLCUID > 32) return false;

            int mculID = nMCULID;
            int lcuID = nLCUID;
            byte[] packet = new byte[12];
            int dataSV = (nSpeed / 10);
            byte checksum = 0;

            packet[0] = 0x02;
            packet[1] = (byte)Mode1.BlockWrite;
            packet[2] = (byte)Mode2.BlockWrite;
            packet[3] = (byte)(0x81 + mculID);
            packet[4] = 0x9F;
            packet[5] = (byte)(0x81 + lcuID);
            packet[6] = (byte)(0x81 + lcuID);
            packet[7] = (byte)dataSV;
            packet[8] = (byte)dataLSV;
            packet[9] = (byte)dataHSV;
            packet[11] = 0x03;
            for (int i = 1; i < 10; i++)
            {
                checksum += packet[i];
            }
            packet[10] = checksum;

            comLock.WaitOne();
            try
            {
                sp.Write(packet, 0, packet.Length);
                byte[] resByte = new byte[256];
                int length = sp.Read(resByte, 0, resByte.Length);
                if (length <= 0) return false;
                if (resByte[1] == (byte)Mode1.GroupWrite &&
                    resByte[2] == (byte)Mode2.GroupWrite &&
                    resByte[3] == (byte)(0x81 + mculID) &&
                    resByte[5] == 0xB9)
                    return true;
                else
                    return false;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                comLock.ReleaseMutex();
            }
        }
        public bool GroupRead(ref int[] nSpeed, ref int[] nStatus)
        {
            return GroupRead(0, ref nSpeed, ref nStatus);
        }
        public bool GroupRead(int nMCULID, ref int[] nSpeed, ref int[] nStatus)
        {
            if (!sp.IsOpen) return false;
            if (nMCULID < 0 || nMCULID >= 32) return false;

            int[] retSpeed = new int[32];
            int[] retStatus = new int[32];
            int mculID = nMCULID;
            byte[] packet = new byte[7];
            byte checksum = 0;

            packet[0] = 0x02;
            packet[1] = (byte)Mode1.GroupRead;
            packet[2] = (byte)Mode2.GroupRead;
            packet[3] = (byte)(0x081 + mculID);
            packet[4] = 0x9F;
            packet[6] = 0x03;
            for (int i = 1; i < 5; i++)
            {
                checksum += packet[i];
            }
            packet[5] = checksum;

            comLock.WaitOne();
            try
            {
                sp.Write(packet, 0, packet.Length);
                byte[] resByte = new byte[256];
                int length = sp.Read(resByte, 0, resByte.Length);
                if (length <= 0) return false;
                if (resByte[1] == (byte)Mode1.GroupWrite &&
                    resByte[2] == (byte)Mode2.GroupWrite &&
                    resByte[3] == (byte)(0x81 + mculID))
                {
                    for (int j = 0; j < 32; j++)
                    {
                        retSpeed[j] = resByte[6 + j];
                        retStatus[j] = resByte[7 + j];
                    }
                    return true;
                }
                else
                {
                    Array.Fill(retSpeed, 0);
                    Array.Fill(retStatus, 0);
                    return false;
                }
            }
            catch (Exception)
            {
                Array.Fill(retSpeed, 0);
                Array.Fill(retStatus, 0);
                return false;
            }
            finally
            {
                nSpeed = retSpeed;
                nStatus = retStatus;
                comLock.ReleaseMutex();
            }
        }
        public bool BlockRead(int nLCUID, ref int nPV, ref int nSV, ref int nST)
        {
            return BlockRead(0, nLCUID, ref nPV, ref nSV, ref nST);
        }
        public bool BlockRead(int nMCULID, int nLCUID, ref int nPV, ref int nSV, ref int nST)
        {
            if (!sp.IsOpen) return false;
            if (nMCULID < 0 || nMCULID >= 32 || nLCUID < 0 || nLCUID > 32) return false;

            int pv = 0, sv = 0, st = 0;
            int mculID = nMCULID;
            int lcuID = nLCUID;
            byte[] packet = new byte[9];
            byte checksum = 0;

            packet[0] = 0x02;
            packet[1] = (byte)Mode1.BlockRead;
            packet[2] = (byte)Mode2.BlockRead;
            packet[3] = (byte)(0x81 + mculID);
            packet[4] = 0x9F;
            packet[5] = (byte)(0x81 + lcuID);
            packet[6] = (byte)(0x81 + lcuID);
            packet[8] = 0x03;
            for (int i = 1; i < 7; i++)
            {
                checksum += packet[i];
            }
            packet[7] = checksum;

            comLock.WaitOne();
            try
            {
                sp.Write(packet, 0, packet.Length);
                byte[] resByte = new byte[256];
                int length = sp.Read(resByte, 0, resByte.Length);
                if (length <= 0) return false;
                if (resByte[1] == (byte)Mode1.BlockRead &&
                    resByte[2] == (byte)Mode2.BlockRead &&
                    resByte[3] == (byte)(0x81 + mculID) &&
                    resByte[5] == (byte)(0x81 + lcuID))
                {
                    pv = resByte[6];
                    st = resByte[7];
                    sv = resByte[8];
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                nPV = pv;
                nSV = sv;
                nST = st;
                comLock.ReleaseMutex();
            }
        }
        public bool Reset(int nLCUID)
        {
            return Reset(0, nLCUID);
        }
        public bool Reset(int nMCULID, int nLCUID)
        {
            if (!sp.IsOpen) return false;
            if (nMCULID < 0 || nMCULID >= 32 || nLCUID < 0 || nLCUID > 32) return false;

            int mculID = nMCULID;
            int lcuID = nLCUID;
            byte[] packet = new byte[10];
            byte checksum = 0;

            packet[0] = 0x02;
            packet[1] = (byte)Mode1.Reset;
            packet[2] = (byte)Mode2.Reset;
            packet[3] = (byte)(0x81 + mculID);
            packet[4] = 0x9F;
            packet[5] = (byte)(0x81 + lcuID);
            packet[6] = (byte)(0x81 + lcuID);
            packet[7] = 0xF5;
            packet[9] = 0x03;
            for (int i = 1; i < 8; i++)
            {
                checksum += packet[i];
            }
            packet[8] = checksum;

            comLock.WaitOne();
            try
            {
                sp.Write(packet, 0, packet.Length);
                byte[] resByte = new byte[256];
                int length = sp.Read(resByte, 0, resByte.Length);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                comLock.ReleaseMutex();
            }
        }
    }
}
