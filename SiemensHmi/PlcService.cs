using System;
using System.Threading.Tasks;
using S7.Net;

namespace SiemensHmi
{
    /// <summary>
    /// 与西门子 S7 系列 PLC 通信的底层服务（最初版本 / 接手前的样子）。
    /// 这个文件里有 1 个真机隐患（机架槽位写死），后面会带你自己改，现在先别动它。
    /// </summary>
    public class PlcService:IPlcService
    {
        private Plc? _plc;

        public bool IsConnected => _plc?.IsConnected ?? false;

        public async Task ConnectAsync(string ip, string cpuType, short rack = 0, short slot = 1)
        {
            
            var cpu = Enum.TryParse<CpuType>(cpuType, true, out var c) ? c : CpuType.S71200;
            _plc = new Plc(cpu, ip, rack, slot);
            try
            {
                await Task.Run(() => _plc.Open());
            }
            catch
            {
                _plc = null;
            }
        }

        public void Disconnect()
        {
            _plc?.Close();
            _plc = null;
        }
        

        public bool ReadBit(string address)
        {
            var (area, db, start, bit) = ParseBit(address);
            var data = _plc!.ReadBytes(area, db, start, 1);
            return (data[0] & (1 << bit)) != 0;
        }

        public byte[] ReadBytes(string startAddress,int count) 
        {
            var (area, db, start) = ParseWord(startAddress);
            return _plc.ReadBytes(area, db, start,count);
        }

        public int ReadWord(string address)
        {
            var (area, db, start) = ParseWord(address);
            var data = _plc!.ReadBytes(area, db, start, 2);
            return (short)((data[0] << 8) | data[1]);
        }

        public long ReadDWord(string address)
        {
            var (area, db, start) = ParseWord(address);
            var data = _plc!.ReadBytes(area, db, start, 4);
            return (long)data[0] * 16777216L    // 2^24
                 + (long)data[1] * 65536L       // 2^16
                 + (long)data[2] * 256L         // 2^8
                 + (long)data[3];
        }

        public byte[] ReadBlock(int startByte,int count)
        {
            return _plc!.ReadBytes(DataType.Memory, 0,startByte, count);
        }

        

        public void WriteBit(string address, bool value)
        {
            var (area, db, start, bit) = ParseBit(address);
            var data = _plc!.ReadBytes(area, db, start, 1);
            if (value) data[0] = (byte)(data[0] | (1 << bit));
            else data[0] = (byte)(data[0] & ~(1 << bit));
            _plc.WriteBytes(area, db, start, data);
        }
        public void WriteWord(string address, ushort value)
        {
            var (area, db, start) = ParseWord(address);
            var data = new byte[2];
            data[0] = (byte)(value >> 8);   // 高字节
            data[1] = (byte)(value & 0xFF); // 低字节
            _plc!.WriteBytes(area, db, start, data);
        }

        public void WriteDWord(string address, uint value)
        {
            var (area, db, start) = ParseWord(address);
            var data = new byte[4];
            data[0] = (byte)(value >> 24);
            data[1] = (byte)(value >> 16);
            data[2] = (byte)(value >> 8);
            data[3] = (byte)(value & 0xFF);
            _plc!.WriteBytes(area, db, start, data);
        }
        private (DataType area, int db, int start, int bit) ParseBit(string address)
        {
            if (address.StartsWith("M"))
            {
                var p = address.Substring(1).Split('.');
                return (DataType.Memory, 0, int.Parse(p[0]), int.Parse(p[1]));
            }
            var m = System.Text.RegularExpressions.Regex.Match(address, @"DB(\d+)\.DBX(\d+)\.(\d+)");
            if (m.Success)
                return (DataType.DataBlock, int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value));
            throw new ArgumentException($"无法解析位地址: {address}");
        }

        private (DataType area, int db, int start) ParseWord(string address)
        {
            if (address.StartsWith("MW")) return (DataType.Memory, 0, int.Parse(address.Substring(2)));
            if (address.StartsWith("MD")) return (DataType.Memory, 0, int.Parse(address.Substring(2)));
            var m = System.Text.RegularExpressions.Regex.Match(address, @"DB(\d+)\.DBW(\d+)");
            if (m.Success)
                return (DataType.DataBlock, int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value));
            throw new ArgumentException($"无法解析字地址: {address}");
        }

        
    }
}
