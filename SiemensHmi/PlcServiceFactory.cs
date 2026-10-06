using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SiemensHmi
{
    /// <summary>
    /// PLC 服务工厂：根据 UseSimulator 决定 new 真机还是模拟器。
    /// 整个项目只改这一个值，就能切换环境。
    /// </summary>
    public static class PlcServiceFactory
    {
        /// <summary>
        /// true  = 用模拟器（默认，方便开发）
        /// false = 用真机（连实验室 PLC 时改成 false）
        /// </summary>
        /// 想去实验室连真 PLC 时，打开 PlcServiceFactory.cs，改一个字：
        /// public static bool UseSimulator { get; set; } = false;   // ← 改成 false
        /// 自己在家开发时，改回：
        /// public static bool UseSimulator { get; set; } = true;    // ← 改回 true
        public static bool UseSimulator { get; set; } = true;

        /// <summary>
        /// 根据 UseSimulator 返回对应的 IPlcService 实例。
        /// </summary>
        public static IPlcService Create()
        {
            return UseSimulator
                ? new SimulatedPlcService()
                : new PlcService();
        }
    }
}