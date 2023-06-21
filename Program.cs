using System;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;


namespace nb2_unpair_console
{
    /// <summary>
    /// Simple Bluetooth watcher 
    /// </summary>
    public static class Program
    {
        static DeviceWatcher deviceWatcher;
        public static void Main()
        {
            Console.WriteLine($"Detect bluetooth and force unpair NB2 devices.");
            Console.WriteLine($"Press <ENTER> to quit.");

            string[] requestedProperties = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected" };
            deviceWatcher = DeviceInformation.CreateWatcher("(System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\")", requestedProperties, DeviceInformationKind.AssociationEndpoint);
            deviceWatcher.Added += DeviceWatcher_Added;
            deviceWatcher.Removed += DeviceWatcher_Removed;

            deviceWatcher.Start();

            DeviceInformationCollection pairedBluetoothDevices = DeviceInformation.FindAllAsync(BluetoothLEDevice.GetDeviceSelector()).AsTask().Result;
            foreach (DeviceInformation pairedBluetoothDevice in pairedBluetoothDevices)
            {
                Console.WriteLine(pairedBluetoothDevice.Name);
            }

            Console.ReadLine();
        }

        private static void DeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
        {
        }

        private static async void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation args)
        {
            string name = args.Name;
            if(name.Contains("NB2"))
            {
                Console.WriteLine();
                Console.WriteLine($"==========================");
                Console.WriteLine($"NB2 device found: {name}");

                if (args.Pairing.IsPaired)
                {
                    Console.WriteLine($"Paired! Try to unpair.");

                    var result = await args.Pairing.UnpairAsync();
                    Console.WriteLine($"Unpairing result = {result.Status.ToString()}");
                }
            }

        }


    }
}






