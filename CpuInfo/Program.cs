using LibreHardwareMonitor.Hardware;
using CommandLine;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System;
using System.Linq;
using System.Diagnostics;

namespace Application
{
    class CpuInfo
    {
        public delegate bool ConsoleCtrlDelegate(int sig);

        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate handler, bool add);

        public static bool loop = true;
        public static Mutex loopLock = new Mutex(false);
        public static Mutex exitLock = new Mutex(false);
        public static List<String> otherSensors = new List<String>();
        public static CounterSample lastCpuSample = new CounterSample();
        public static bool firstInUsage = true;
        public static int osMajorVersion = 0;
        public static PerformanceCounter cpuCounter = null;
        public static ConsoleCtrlDelegate consoleCtrl = new ConsoleCtrlDelegate(OnConsoleClose);

        public class Options
        {
            [Option('f', "flush", Required = false, HelpText = "Flush console each output.", Default = false)]
            public bool Flush { get; set; }

            [Option('i', "info-list", Required = true, HelpText = "All cpu info that you want to output.")]
            public IEnumerable<string> InfoList { get; set; }

            [Option('t', "time", Required = false, HelpText = "Set millisecond internal time of update, cannot less than 1000.", Default = 1000)]
            public int Time { get; set; }

            [Option('e', "exit-sign", Required = false, HelpText = "Set exit input sign, exp: -e exit means" +
                " when you input 'exit' in console, the application will exit.", Default = "")]
            public string ExitSign { get; set; }
        }

        public static List<SensorType> getRequestType(IEnumerable<string> infoList)
        {
            List<SensorType> sensors = new List<SensorType>();
            foreach (var info in infoList)
            {
                switch (info.ToUpper())
                {
                    case "TEMP":
                        {
                            sensors.Add(SensorType.Temperature);
                            break;
                        }
                    case "CLOCK":
                        {
                            sensors.Add(SensorType.Clock);
                            break;
                        }
                    case "USAGE":
                        {
                            otherSensors.Add(info.ToUpper());
                            break;
                        }
                }
            }
            return sensors;
        }

        public static float getCpuUsage()
        {
            // refer to https://github.com/zhongyang219/TrafficMonitor/blob/master/TrafficMonitor/CPUUsage.cpp#L61-#L64
            if (cpuCounter == null)
            {
                if (osMajorVersion >= 10)
                {
                    cpuCounter = new PerformanceCounter("Processor Information", "% Processor Utility", "_Total");
                }
                else
                {
                    cpuCounter = new PerformanceCounter("Processor Information", "% Processor Time", "_Total");
                }
                lastCpuSample = cpuCounter.NextSample();
                return 0.0f;
            }

            // refer to https://stackoverflow.com/a/36572724/14419237            
            CounterSample currentValue = cpuCounter.NextSample();
            float ret = CounterSample.Calculate(lastCpuSample, currentValue);
            lastCpuSample = currentValue;
            return ret;
        }

        public static void CpuInfoLoop(Options options)
        {
            if (options.InfoList == null)
            { return; }
            if (options.InfoList.Count() == 0)
            { return; }

            int sleepTime = options.Time;
            if (sleepTime < 1000) { sleepTime = 1000; }

            Computer computer = new Computer();
            computer.IsCpuEnabled = true;
            computer.Open();

            String outString = "";
            List<SensorType> sensors = getRequestType(options.InfoList);
            exitLock.WaitOne();
            while (true)
            {
                loopLock.WaitOne();
                if (!loop)
                {
                    loopLock.ReleaseMutex();
                    break;
                }
                loopLock.ReleaseMutex();
                int cpuID = -1;
                foreach (var hardware in computer.Hardware)
                {
                    cpuID++;
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensors.IndexOf(sensor.SensorType) != -1 && sensor.Value.HasValue)
                        {
                            String typeName = "";
                            switch (sensor.SensorType)
                            {
                                case SensorType.Temperature:
                                    typeName = "Temperature";
                                    break;
                                case SensorType.Clock:
                                    typeName = "Clock";
                                    break;
                            }
                            float minValue = 0, maxValue = 0;
                            if (sensor.Min.HasValue)
                            {
                                minValue = sensor.Min.Value;
                            }
                            if (sensor.Max.HasValue)
                            {
                                maxValue = sensor.Max.Value;
                            }
                            outString += String.Format("{0}\"cpuid\": {1}, \"type\": \"{2}\", \"name\": \"{3}\", \"value\": {4}, \"minValue\": {5}, \"maxValue\": {6}{7}\n", "{", cpuID, typeName, sensor.Name, sensor.Value.Value, minValue, maxValue, "}");
                        }
                    }
                    hardware.Update();
                }
                foreach (var sensor in otherSensors)
                {
                    switch (sensor)
                    {
                        case "USAGE":
                            {
                                outString += String.Format("{0}\"type\": \"{1}\", \"value\": {2}{3}\n", "{", "Usage", getCpuUsage(), "}");
                                break;
                            }

                    }
                }
                if (options.Flush)
                {
                    Console.Clear();
                }
                Console.Write(outString + "END\n");
                Thread.Sleep(sleepTime);

                outString = "";
            }

            try
            {                
                computer.Close();
                if (cpuCounter != null)
                {
                    cpuCounter.Close();
                    cpuCounter.Dispose();
                }                
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            exitLock.ReleaseMutex();
        }

        public static Options getOptions(string[] args)
        {
            Options options;
            try
            {
                options = Parser.Default.ParseArguments<Options>(args).Value;
                var list = options.InfoList; // try to get
                return options;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return null;
            }
        }

        private static bool OnConsoleClose(int sig)
        {                                    
            loopLock.WaitOne();
            loop = false;
            loopLock.ReleaseMutex();

            exitLock.WaitOne();
            exitLock.ReleaseMutex();
            return false;
        }

        private static void processExit()
        {
            SetConsoleCtrlHandler(consoleCtrl, true);
            AppDomain appd = AppDomain.CurrentDomain;

            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = false;
                loopLock.WaitOne();
                loop = false;
                loopLock.ReleaseMutex();

                exitLock.WaitOne();
                exitLock.ReleaseMutex();
            };
        }

        static void Main(string[] args)
        {
            OperatingSystem os = Environment.OSVersion;
            Version ver = os.Version;
            osMajorVersion = os.Version.Major;

            processExit();
            Options options = getOptions(args);
            if (options == null)
            {
                return;
            }

            Thread t = new Thread(() => CpuInfoLoop(options));
            t.Start();

            string es = "";
            string line = "";
            try
            {
                es = options.ExitSign;
            }
            catch (Exception e)
            {
                es = "";
            }

            if (es != null && es != "")
            {
                while (line != es)
                {
                    line = Console.ReadLine();
                    if (line != null && line == es)
                    {
                        loopLock.WaitOne();
                        loop = false;
                        loopLock.ReleaseMutex();
                    }
                }
            }
        }
    }
}