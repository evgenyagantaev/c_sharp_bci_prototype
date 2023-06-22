using System;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using System.Collections;
using System.Threading.Tasks;
using System.Threading;
using System.ComponentModel.Design;
using Windows.Storage.Streams;

namespace c_sharp_bci_prototype
{
    /// <summary>
    /// Simple Bluetooth watcher 
    /// </summary>
    public static class Program
    {
        // Devices found by watcher
        private readonly static Hashtable s_foundDevices = new Hashtable();
        public static void Main()
        {
            Console.WriteLine($"BCI C# NB2 prototype.");

            // Create a watcher
            BluetoothLEAdvertisementWatcher watcher = new BluetoothLEAdvertisementWatcher();
            watcher.ScanningMode = BluetoothLEScanningMode.Active;
            watcher.Received += Watcher_ReceivedAsync;

            while (true)
            {
                Console.WriteLine("Starting BluetoothLEAdvertisementWatcher");
                Console.WriteLine(">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>");
                watcher.Start();

                // Run until we have found some devices to connect to
                while (s_foundDevices.Count == 0)
                {
                    Thread.Sleep(5000);
                }

                Console.WriteLine("<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<");
                Console.WriteLine("Stopping BluetoothLEAdvertisementWatcher");

                // We can't connect if watch running so stop it.
                watcher.Stop();

                Console.WriteLine();
                Console.WriteLine($"Devices found = {s_foundDevices.Count}");
                Console.WriteLine();
                Console.WriteLine($"---------------------------------------");
                //Console.WriteLine("Connecting and Reading data");

                foreach (DictionaryEntry entry in s_foundDevices)
                {
                    BluetoothLEDevice device = entry.Value as BluetoothLEDevice;

                    _ = ConnectAndReceiveSomeAsync(device);
                }
                Console.WriteLine($"---------------------------------------");

                s_foundDevices.Clear();
            }

        }

        private static void Watcher_ReceivedAsync(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            _ = ProcessWatcherReceivedAsync(sender, args);
        }

        private static async Task ProcessWatcherReceivedAsync(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            if (IsValidDevice(args))
            {
                //Console.WriteLine($"Found an NB2 eeg amplifyer :{args.BluetoothAddress:X}");

                // Add it to list as a BluetoothLEDevice
                //BluetoothLEDevice dev = BluetoothLEDevice.FromBluetoothAddress(args.BluetoothAddress, args.BluetoothAddressType);
                BluetoothLEDevice dev = await BluetoothLEDevice.FromBluetoothAddressAsync(args.BluetoothAddress);
                s_foundDevices.Add(args.BluetoothAddress, dev);
            }
        }

        private static bool IsValidDevice(BluetoothLEAdvertisementReceivedEventArgs args)
        {
            if (args.Advertisement.LocalName.Contains($"NB2"))
            {
                return true;
            }

            return false;
        }

        private static async Task ConnectAndReceiveSomeAsync(BluetoothLEDevice device)
        {
            var services_result = await device.GetGattServicesAsync(BluetoothCacheMode.Uncached);

            if (services_result.Status == GattCommunicationStatus.Success)
            {
                var list_of_services = services_result.Services;
                foreach (GattDeviceService service in list_of_services)
                {
                    Console.WriteLine($"{service.Uuid}");
                    Console.WriteLine($"===========================");
                    
                    var characteristics_result = await service.GetCharacteristicsAsync();
                    if (characteristics_result.Status == GattCommunicationStatus.Success)
                    {
                        var list_of_characteristics = characteristics_result.Characteristics;
                        foreach (GattCharacteristic gatt_char in list_of_characteristics)
                        {
                            Console.WriteLine($"{gatt_char.Uuid}");
                        }
                    }
                    Console.WriteLine($"===========================");
                }
            }

        }



    }
}






