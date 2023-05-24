using LibreHardwareMonitor.Hardware;
using CommandLine;

namespace Application
{
    class CpuInfo
    {
        public static bool loop = true;
        public static Mutex loopLock = new Mutex(false);
        public static Mutex exitLock = new Mutex(false);
        public class Options
        {
            [Option('f', "flush", Required = false, HelpText = "Flush console each output.", Default = false)]
            public bool Flush { get; set; }

            [Option('i', "info-list", Required = true, HelpText = "All cpu info that you want to output.")]
            public IEnumerable<string>? InfoList { get; set; }

            [Option('t', "time", Required = false, HelpText = "Set millisecond internal time of update, cannot less than 1000.", Default = 1000)]
            public int Time { get; set; }

            [Option('e', "exit-sign", Required = false, HelpText = "Set exit input sign, exp: -e exit means" +
                " when you input 'exit' in console, the application will exit.", Default = "")]
            public string? ExitSign { get; set; }
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
                }
            }
            return sensors;
        }

        public static void CpuInfoLoop(Options options)
        {
            if (options.InfoList == null ||
                options.InfoList.Count() == 0)
            { return; }
            ValueTuple<Int32, Int32> beginConsolePos = (0, 0);
            ValueTuple<Int32, Int32> lastConsolePos = (0, 0);

            if (options.Flush)
            {
                beginConsolePos = Console.GetCursorPosition();
                lastConsolePos = beginConsolePos;
            }

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
                foreach (var hardware in computer.Hardware)
                {
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
                            outString += String.Format("{4}\"type\": \"{6}\", \"name\": \"{0}\", \"value\": {1}, \"minValue\": {2}, \"maxValue\": {3}{5}\n", sensor.Name, sensor.Value.Value, sensor.Min.Value, sensor.Max.Value, "{", "}", typeName);
                        }
                    }
                    hardware.Update();
                }
                Console.Write(outString + "END\n");
                Thread.Sleep(sleepTime);
                if (options.Flush)
                {
                    lastConsolePos = Console.GetCursorPosition();
                    Console.SetCursorPosition(beginConsolePos.Item1, beginConsolePos.Item2);
                }

                outString = "";
            }

            if (options.Flush)
            {
                Console.SetCursorPosition(0, lastConsolePos.Item2 + 1);
            }

            try
            {
                computer.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            exitLock.ReleaseMutex();            
        }

        public static Options? getOptions(string[] args)
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

        private static void processExit()
        {
            AppDomain appd = AppDomain.CurrentDomain;

            appd.ProcessExit += (s, e) =>
            {
                loopLock.WaitOne();
                loop = false;
                loopLock.ReleaseMutex();

                exitLock.WaitOne();
                exitLock.ReleaseMutex();
            };
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
            processExit();

            Options? options = getOptions(args);
            if (options == null)
            {
                return;
            }

            Thread t = new Thread(() => CpuInfoLoop(options));
            t.Start();

            string? es = "";
            string? line = "";
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

