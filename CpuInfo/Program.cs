using LibreHardwareMonitor.Hardware;

namespace Application
{
    class CpuInfo
    {
        public static bool loop = true;
        public static Mutex loopLock = new Mutex();
        public static void CpuInfoLoop()
        {
            Computer computer = new Computer();
            computer.IsCpuEnabled = true;
            computer.Open();
            String outString = "";
            while (true)
            {
                loopLock.WaitOne();
                if (!loop)
                {
                    break;
                }
                loopLock.ReleaseMutex();
                foreach (var hardware in computer.Hardware)
                {

                    foreach (var sensor in hardware.Sensors)
                    {

                        if ((sensor.SensorType == SensorType.Temperature || sensor.SensorType == SensorType.Clock) && sensor.Value.HasValue)
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
                Thread.Sleep(1000);
                outString = "";
            }
            try
            {
                computer.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

        }
        static void Main(string[] args)
        {
            Thread t = new Thread(CpuInfoLoop);
            t.Start();
            string line = "";
            while (line != "exit")
            {
                line = Console.ReadLine();
                if (line == "exit")
                {
                    loopLock.WaitOne();
                    loop = false;
                    loopLock.ReleaseMutex();
                }
            }
        }
    }
}

