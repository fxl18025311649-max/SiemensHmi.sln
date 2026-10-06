using System;
using System.Threading.Tasks;

namespace SiemensHmi
{
    public class SimulatedPlcService : IPlcService
    {
        // ===== M 区内存映像 =====
        private readonly byte[] _mArea = new byte[1024];
        private readonly object _lock = new object();

        // ===== 后台扫描线程 =====
        private readonly System.Timers.Timer _scanTimer;

        // ===== 状态 =====
        private bool _connected;
        private bool _motorRunning = false;
        private bool _motorFault = false;

        // ===== 物理量 =====
        private double _temperature = 25.0;
        private double _pressure = 1.00;
        private long _count = 0;

        // ===== 命令上升沿检测 =====
        private bool _prevStart = false;
        private bool _prevStop = false;
        private bool _prevReset = false;

        // ===== 物理演化参数 =====
        private const double TempMin = 25.0, TempMax = 60.0;
        private const double PressMin = 1.00, PressMax = 2.50;
        private const double TempRise = 0.05;
        private const double TempFall = 0.03;
        private const double PressRise = 0.005;
        private const double PressFall = 0.003;
        private int _countTick = 0;

        // ===== 故障注入开关 =====
        private bool _forceDisconnect = false;
        private DateTime _timeoutUntil = DateTime.MinValue;
        private DateTime _badTempUntil = DateTime.MinValue;

        public bool IsConnected => _connected && !_forceDisconnect;

        public SimulatedPlcService()
        {
            // 初始化 M 区
            _mArea[200] = 0x00;
            WriteAnalogToMemory();

            // 启动扫描线程
            _scanTimer = new System.Timers.Timer(30);
            _scanTimer.AutoReset = true;
            _scanTimer.Elapsed += (s, e) => Scan();
            _scanTimer.Start();
        }

        // ===== 扫描线程 =====
        private void Scan()
        {
            lock (_lock)
            {
                bool startCmd = (_mArea[100] & 0x01) != 0;
                bool stopCmd = (_mArea[100] & 0x02) != 0;
                bool resetCmd = (_mArea[100] & 0x04) != 0;

                if (startCmd && !_prevStart) _motorRunning = true;
                if (stopCmd && !_prevStop) _motorRunning = false;
                if (resetCmd && !_prevReset) { _motorRunning = false; _motorFault = false; }

                _prevStart = startCmd;
                _prevStop = stopCmd;
                _prevReset = resetCmd;

                if (_motorRunning)
                {
                    _temperature = Math.Min(TempMax, _temperature + TempRise);
                    _pressure = Math.Min(PressMax, _pressure + PressRise);

                    _countTick++;
                    if (_countTick >= 10) { _count++; _countTick = 0; }
                }
                else
                {
                    _temperature = Math.Max(TempMin, _temperature - TempFall);
                    _pressure = Math.Max(PressMin, _pressure - PressFall);
                }

                WriteAnalogToMemory();
            }
        }

        private void WriteAnalogToMemory()
        {
            byte status = 0x00;
            if (_motorRunning) status |= 0x01;
            if (_motorFault) status |= 0x02;
            _mArea[200] = status;

            short tempScaled = (short)(_temperature * 10);
            _mArea[202] = (byte)(tempScaled >> 8);
            _mArea[203] = (byte)(tempScaled & 0xFF);

            ushort pressScaled = (ushort)(_pressure * 100);
            _mArea[204] = (byte)(pressScaled >> 8);
            _mArea[205] = (byte)(pressScaled & 0xFF);

            _mArea[206] = (byte)(_count >> 24);
            _mArea[207] = (byte)(_count >> 16);
            _mArea[208] = (byte)(_count >> 8);
            _mArea[209] = (byte)(_count & 0xFF);
        }

        // ===== 连接管理 =====
        public Task ConnectAsync(string ip, string cpuType, short rack = 0, short slot = 1)
        {
            _connected = true;
            _forceDisconnect = false;
            return Task.CompletedTask;
        }

        public void Disconnect() => _connected = false;

        // ===== 读 =====
        public byte[] ReadBlock(int startByte, int count)
        {
            if (DateTime.Now< _timeoutUntil)
                throw new TimeoutException("模拟读取超时");

            lock (_lock)
            {
                if (DateTime.Now < _badTempUntil)
                {
                    _mArea[202] = 0x27;
                    _mArea[203] = 0x0F;
                }

                var result = new byte[count];
                Array.Copy(_mArea, startByte, result, 0, count);
                return result;
            }
        }

        public bool ReadBit(string address)
        {
            var (start, bit) = ParseBit(address);
            lock (_lock) return (_mArea[start] & (1 << bit)) != 0;
        }

        public int ReadWord(string address)
        {
            int s = ParseByteAddress(address);
            lock (_lock) return (_mArea[s] << 8) | _mArea[s + 1];
        }

        public long ReadDWord(string address)
        {
            int s = ParseByteAddress(address);
            lock (_lock)
            {
                return (long)_mArea[s] * 16777216L
                     + (long)_mArea[s + 1] * 65536L
                     + (long)_mArea[s + 2] * 256L
                     + _mArea[s + 3];
            }
        }

        // ===== 写 =====
        public void WriteBit(string address, bool value)
        {
            var (start, bit) = ParseBit(address);
            lock (_lock)
            {
                if (value) _mArea[start] |= (byte)(1 << bit);
                else _mArea[start] &= (byte)~(1 << bit);
            }
        }

        public void WriteWord(string address, ushort value)
        {
            int s = ParseByteAddress(address);
            lock (_lock)
            {
                _mArea[s] = (byte)(value >> 8);
                _mArea[s + 1] = (byte)(value & 0xFF);
            }
        }

        public void WriteDWord(string address, uint value)
        {
            int s = ParseByteAddress(address);
            lock (_lock)
            {
                _mArea[s] = (byte)(value >> 24);
                _mArea[s + 1] = (byte)(value >> 16);
                _mArea[s + 2] = (byte)(value >> 8);
                _mArea[s + 3] = (byte)(value & 0xFF);
            }
        }

        // ===== 故障注入 =====
        public void SimulateDisconnect()
        {
            _forceDisconnect = true;
            _connected = false;
        }

        public void SimulateTimeout()
        {
            _timeoutUntil = DateTime.Now.AddSeconds(3);
        }

        public void BiasData()
        {
            _badTempUntil = DateTime.Now.AddSeconds(3);
        }

        public void ClearFaults()
        {
            _forceDisconnect = false;
            _timeoutUntil = DateTime.MinValue;
            _badTempUntil = DateTime.MinValue;
            _connected = true;
        }

        // ===== 地址解析 =====
        private (int start, int bit) ParseBit(string address)
        {
            var p = address.Substring(1).Split('.');
            return (int.Parse(p[0]), int.Parse(p[1]));
        }

        private int ParseByteAddress(string address)
        {
            return int.Parse(address.Substring(2));
        }
    }
}