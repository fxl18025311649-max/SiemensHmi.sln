using SQLitePCL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SiemensHmi
{
    public interface IPlcService
    {
        bool IsConnected { get; }
        Task ConnectAsync(string ip, string cpuType, short rack = 0, short slot = 1);
        void Disconnect();
        byte[] ReadBlock(int startByte, int count);
        void WriteBit(string address, bool value);
        void WriteWord(string address, ushort value);
        void WriteDWord(string address, uint value);
        
    }
}
