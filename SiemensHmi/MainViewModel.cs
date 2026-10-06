using S7.Net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace SiemensHmi
{
    public class MainViewModel : INotifyPropertyChanged
    {
        //private readonly IPlcService _plcService = new PlcService();
        //private readonly IPlcService _plcService = new SimulatedPlcService();
        private readonly IPlcService _plcService = PlcServiceFactory.Create();

        private readonly DispatcherTimer _pollTimer;

        private readonly StringBuilder _logBuilder = new StringBuilder();
        private readonly DatabaseService _dbService = new DatabaseService();
        private int _dbCounter = 0;
        private DateTime _lastReconnectTime = DateTime.MinValue;
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnUpdate(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public MainViewModel()
        {
            try { _dbService.Initialize(); }
            catch (Exception ex) { System.Windows.MessageBox.Show($"数据库初始化失败：\n{ex.Message}"); }

            ConnectCmd = new RelayCommand(ExecuteConnect, CanConnect);
            DisconnectCmd = new RelayCommand(ExecuteDisconnect, CanDisconnect);
            StartCmd = new RelayCommand(ExecuteStart, CanStartStop);
            StopCmd = new RelayCommand(ExecuteStop, CanStartStop);
            ResetCmd = new RelayCommand(ExecuteReset, CanStartStop);
            ClearLogCmd = new RelayCommand(ExecuteClearLog);
            QueryHistoryCmd = new RelayCommand(QueryHistory);
            ApplyParamsCmd = new RelayCommand(ExecuteApplyParams, CanStartStop);
            SimDisconnectCmd = new RelayCommand(SimulateDisconnect);
            SimTimeoutCmd = new RelayCommand(SimulateTimeout);
            BiasDataCmd = new RelayCommand(BiasData);

            CpuList = new List<string> { "S7-1200", "S7-1500", "S7-300", "S7-400" };
            SelectedCpu = "S7-1200";
            PlcIp = "127.0.0.1";

            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _pollTimer.Tick += PollTimer_Tick;

            ConnStatus = "未连接";
            ConnColor = Brushes.Red;
            ResetDisplay();
            LogText = "";
            InitPlot();
        }

        

        private string _plcIp = "";
        public string PlcIp { get => _plcIp; set { _plcIp = value; OnUpdate("PlcIp"); } }

        private List<string> _cpuList = new();
        public List<string> CpuList { get => _cpuList; set { _cpuList = value; OnUpdate("CpuList"); } }

        private string _selectedCpu = "";
        public string SelectedCpu { get => _selectedCpu; set { _selectedCpu = value; OnUpdate("SelectedCpu"); } }

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set { _isConnected = value; OnUpdate("IsConnected"); System.Windows.Input.CommandManager.InvalidateRequerySuggested(); }
        }

        private string _connStatus = "";
        public string ConnStatus { get => _connStatus; set { _connStatus = value; OnUpdate("ConnStatus"); } }

        private Brush _connColor = Brushes.Red;
        public Brush ConnColor { get => _connColor; set { _connColor = value; OnUpdate("ConnColor"); } }

        private string _motorRunning = "";
        public string MotorRunning { get => _motorRunning; set { _motorRunning = value; OnUpdate("MotorRunning"); } }
        private Brush _motorRunColor = Brushes.Gray;
        public Brush MotorRunColor { get => _motorRunColor; set { _motorRunColor = value; OnUpdate("MotorRunColor"); } }

        private string _motorFault = "";
        public string MotorFault { get => _motorFault; set { _motorFault = value; OnUpdate("MotorFault"); } }
        private Brush _motorFaultColor = Brushes.Gray;
        public Brush MotorFaultColor { get => _motorFaultColor; set { _motorFaultColor = value; OnUpdate("MotorFaultColor"); } }

        private string _temperature = "--";
        public string Temperature { get => _temperature; set { _temperature = value; OnUpdate("Temperature"); } }

        private string _pressure = "--";
        public string Pressure { get => _pressure; set { _pressure = value; OnUpdate("Pressure"); } }

        private string _productionCount = "--";
        public string ProductionCount { get => _productionCount; set { _productionCount = value; OnUpdate("ProductionCount"); } }

        private string _logText = "";
        public string LogText { get => _logText; set { _logText = value; OnUpdate("LogText"); } }

        private List<HistoryRecord> _historyList = new();
        public List<HistoryRecord> HistoryList { get => _historyList; set { _historyList = value; OnUpdate("HistoryList"); } }

        private string _targetTemperature = "";
        public string TargetTemperature
        {
            get => _targetTemperature;
            set
            {
                _targetTemperature = value;
                OnUpdate("TargetTemperature");
            }
        }

        private string _targetPressure = "";
        public string TargetPressure
        {
            get => _targetPressure;
            set { _targetPressure = value; OnUpdate("TargetPressure"); }
        }
        private bool _isPolling = false;

        private PlotModel _plotModel;
        public PlotModel PlotModel
        {
            get=> _plotModel;
            set
            {
                _plotModel = value;
                OnUpdate("PlotModel");
            }
        }

        private LineSeries _tempSeries;
        private LineSeries _pressureSeries;

        public ICommand ConnectCmd { get; }

        public ICommand DisconnectCmd { get; }
        public ICommand StartCmd { get; }
        public ICommand StopCmd { get; }
        public ICommand ResetCmd { get; }
        public ICommand ClearLogCmd { get; }
        public ICommand QueryHistoryCmd { get; }
        public ICommand ApplyParamsCmd { get; }
        public ICommand SimDisconnectCmd { get; }
        public ICommand SimTimeoutCmd { get; }
        public ICommand BiasDataCmd { get; }
        private async void ExecuteConnect(object? p)
        {
            AddLog($"正在连接 {PlcIp}...");
            ConnStatus = "连接中...";
            ConnColor = Brushes.Orange;
            short slot = (SelectedCpu == "S7-300" || SelectedCpu == "S7-400") ? (short)2 : (short)1;
            await _plcService.ConnectAsync(PlcIp, SelectedCpu, 0, slot);
            if (_plcService.IsConnected)
            {
                IsConnected = true;
                ConnStatus = "已连接";
                ConnColor = Brushes.Green;
                AddLog("连接成功");
                _pollTimer.Start();
            }
            else
            {
                IsConnected = false;
                ConnStatus = "未连接";
                ConnColor = Brushes.Red;
                AddLog("连接失败");
            }

        }

        private void ExecuteDisconnect(object? p)
        {
            _pollTimer.Stop();
            _plcService.Disconnect();
            IsConnected = false;
            ConnStatus = "未连接";
            ConnColor = Brushes.Red;
            AddLog("已断开");
            ResetDisplay();
        }

        private async void ExecuteStart(object? p) => await WritePulseAsync("M100.0");
        private async void ExecuteStop(object? p) => await WritePulseAsync("M100.1");
        private async void ExecuteReset(object? p) => await WritePulseAsync("M100.2");

        private bool CanConnect(object? p) => !IsConnected;
        private bool CanDisconnect(object? p) => IsConnected;
        private bool CanStartStop(object? p) => IsConnected;


        private async Task WritePulseAsync(string address)
        {
            if (!_plcService.IsConnected)
            {
                AddLog("未连接，无法写入");
                return;
            }
            try
            {
                _plcService.WriteBit(address, true);
                AddLog($"脉冲写入 {address} = True");

                await Task.Delay(150);

                _plcService.WriteBit(address, false);
                AddLog($"脉冲写入 {address} = False");
            }
            catch (Exception ex)
            {
                AddLog($"脉冲写入错误: {ex.Message}");
                _plcService.Disconnect();
            }
        }

        private async void PollTimer_Tick(object? sender, EventArgs e)
        {
            if (_isPolling) return;
            _isPolling = true;

            try
            {
                if (!_plcService.IsConnected)
                {
                    if ((DateTime.Now - _lastReconnectTime).TotalSeconds < 5)
                    {
                        return;
                    }
                    _lastReconnectTime = DateTime.Now;

                    await TryReconnectAsync();
                    return;
                }
                await Task.Run(() => ReadPlcData());
            }
            catch(Exception ex)
            {
                AddLog($"读取失败: {ex.Message}");

                _plcService.Disconnect();
                IsConnected = false;
                ConnStatus = "未连接";
                ConnColor = Brushes.Red;

                await TryReconnectAsync();
            }
            finally
            {
                _isPolling = false;
            }    
        }
        private async Task TryReconnectAsync()
        {
            AddLog("检测到断线，正在尝试重连...");
            short slot = (SelectedCpu == "S7-300" || SelectedCpu == "S7-400") ? (short)2 : (short)1;
            await _plcService.ConnectAsync(PlcIp, SelectedCpu, 0, slot);
            if (_plcService.IsConnected)
            {
                IsConnected = true;
                ConnStatus = "已连接";
                ConnColor = Brushes.Green;
                AddLog("重连成功！");
            }
            else
            {
                AddLog("重连失败，稍后重试");
            }
        }
        private void ExecuteApplyParams(object? p)
        {
            if (!_plcService.IsConnected)
            {
                AddLog("未连接，无法下发参数");
                return;
            }
            if (!double.TryParse(TargetTemperature, out double tempC))
            {
                AddLog("目标温度格式错误，请输入数字");
                return;
            }
            if (!double.TryParse(TargetPressure, out double pressKpa))
            {
                AddLog("目标压力格式错误，请输入数字");
                return;
            }
            if (tempC < 0 || tempC > 200)
            {
                AddLog("目标温度超范围（0~200℃）");
                return;
            }
            if (pressKpa < 0 || pressKpa > 10)
            {
                AddLog("目标压力超范围（0~10kPa）");
                return;
            }
            try
            {
                ushort tempScaled = (ushort)(tempC * 10);
                ushort pressScaled = (ushort)(pressKpa * 100);

                _plcService.WriteWord("MW400", tempScaled);
                _plcService.WriteWord("MW402", pressScaled);

                AddLog($"参数下发成功：目标温度 {tempC:F1}℃，目标压力 {pressKpa:F2}kPa");
            }
            catch (Exception ex)
            {
                AddLog($"参数下发失败: {ex.Message}");
            }
        }

        private void QueryHistory(object? p)
        {
            HistoryList = _dbService.GetLatestRecords(100);
            AddLog($"查询到 {HistoryList.Count} 条历史记录");
        }

        private void ExecuteClearLog(object? p)
        {
            _logBuilder.Clear();
            LogText = "";
        }

        private void ResetDisplay()
        {
            MotorRunning = "否";
            MotorRunColor = Brushes.Gray;
            MotorFault = "无";
            MotorFaultColor = Brushes.Gray;
            Temperature = "--";
            Pressure = "--";
            ProductionCount = "--";
        }

        private void AddLog(string message)
        {
            _logBuilder.AppendLine($"[{DateTime.Now:HH:mm:ss}] {message}");
            LogText = _logBuilder.ToString();
        }

        private void InitPlot()
        {
            PlotModel = new PlotModel{ Title = "实时趋势" };
            PlotModel.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = "HH:mm:ss",
                Title = "时间"
            });
            PlotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Minimum = 0,
                Title = "数值"
            });
            _tempSeries = new LineSeries
            {
                Title = "温度",
                Color = OxyColors.Red,
                StrokeThickness = 2
            };

            _pressureSeries = new LineSeries
            {
                Title = "压力",
                Color = OxyColors.Blue,
                StrokeThickness = 2
            };
            PlotModel.Series.Add(_tempSeries);
            PlotModel.Series.Add(_pressureSeries);
        }
        public void ReadPlcData()
        {
            byte[] buffer = _plcService.ReadBlock(200, 10);
            bool running = (buffer[0] & (1 << 0)) != 0;
            bool fault = (buffer[0] & (1 << 1)) != 0;
            short tempRaw = (short)(buffer[2] * 256 + buffer[3]);
            short presRaw = (short)(buffer[4] * 256 + buffer[5]);

            long count = (long)buffer[6] * 16777216
              + (long)buffer[7] * 65536
              + (long)buffer[8] * 256
              + (long)buffer[9];

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                MotorRunning = running ? "是" : "否";
                MotorRunColor = running ? Brushes.Green : Brushes.Gray;
                MotorFault = fault ? "有故障" : "无";
                MotorFaultColor = fault ? Brushes.Red : Brushes.Gray;
                Temperature = (tempRaw / 10.0).ToString("F1");
                Pressure = (presRaw / 100.0).ToString("F2");
                ProductionCount = count.ToString();
                var now = DateTime.Now;
                _tempSeries.Points.Add(new OxyPlot.DataPoint(DateTimeAxis.ToDouble(now),tempRaw/10.0));
                _pressureSeries.Points.Add(new OxyPlot.DataPoint(DateTimeAxis.ToDouble(now), presRaw / 100.0));
                if (_tempSeries.Points.Count > 100) _tempSeries.Points.RemoveAt(0);
                if (_pressureSeries.Points.Count > 100) _pressureSeries.Points.RemoveAt(0);
                PlotModel.InvalidatePlot(true);
                

                if (++_dbCounter >= 20)
                {
                    _dbCounter = 0;
                    _dbService.InsertRecord(double.Parse(Temperature),double.Parse(Pressure),count);
                }
            });
        }

        private void SimulateDisconnect(object? p)
        {
            if (_plcService is SimulatedPlcService sim) 
            {
                sim.SimulateDisconnect();
                AddLog("【故障注入】已强制断线，观察重连逻辑");
            }
        }
        private void SimulateTimeout(object? p)
        {
            if (_plcService is SimulatedPlcService sim)
            {
                sim.SimulateTimeout();
                AddLog("【故障注入】已模拟超时，观察异常处理");
            }
        }

        private void BiasData(object? p)
        {
            if (_plcService is SimulatedPlcService sim)
            {
                sim.BiasData();
                AddLog("【故障注入】温度已跳到 999，观察报警");
            }
        }
    }
}
