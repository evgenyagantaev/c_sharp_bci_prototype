﻿using System;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using System.Collections;
using System.Threading.Tasks;
using System.Threading;
using System.ComponentModel.Design;
using Windows.Storage.Streams;
using System.Collections.Generic;
using static LSL.liblsl;

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using Windows.Media.Protection.PlayReady;

using System.Linq;
using Newtonsoft.Json;
using Windows.Data.Json;
using Newtonsoft.Json.Linq;

namespace c_sharp_bci_prototype
{
    /// <summary>
    /// Simple Bluetooth watcher 
    /// </summary>
    public static class Program
    {
        static Thread update;
        static Thread updateLSL;
        static StreamInfo infoEEG;
        static StreamInfo infoIMP;
        static StreamOutlet outletEEG;
        static StreamOutlet outletIMP;
        static Dictionary<string, List<float>> lslData = new Dictionary<string, List<float>>();



        static List<BluetoothLEDevice> foundDevices = new List<BluetoothLEDevice>();
        static TcpListener listener;

        //public static void Main(string[] args) 
        //{
        //    _ = async_operation_test();

        //    int i = 0;
        //    while (true) 
        //    {
        //        Console.WriteLine("Main " + i.ToString());
        //        Thread.Sleep(1000); // Wait for 1 second
        //        i++;
        //    }
        //}

        //public static async Task async_operation_test() // асинхронный метод
        //{
        //    BeforeCall();
        //    Task task = OperationAsync(); //асинхронная операция
        //    //Task task = Task.Delay(7000); //асинхронная операция
        //    AfterCall();
        //    await task;
        //    AfterAwait();
        //}

        //private static void BeforeCall()
        //{
        //    for(int i=0; i<3; i++)
        //    {
        //        Console.WriteLine("BeforeCall " + i.ToString());
        //        Thread.Sleep(1000); // Wait for 1 second
        //    }
        //}

        //private static void AfterCall()
        //{
        //    for (int i = 0; i < 3; i++)
        //    {
        //        Console.WriteLine("AfterCall " + i.ToString());
        //        Thread.Sleep(1000); // Wait for 1 second
        //    }
        //}

        //private static void AfterAwait()
        //{
        //    for (int i = 0; i < 3; i++)
        //    {
        //        Console.WriteLine("AfterAwait " + i.ToString());
        //        Thread.Sleep(1000); // Wait for 1 second
        //    }
        //}

        //public static async Task OperationAsync()
        //{
        //    for (int i = 0; i < 7; i++)
        //    {
        //        Console.WriteLine("OperationAsync " + i.ToString());
        //        await Task.Delay(1000);
        //    }
        //}

        public static void Main()
        {
            Console.WriteLine($"BCI C# NB2 prototype.");

            listener = new TcpListener(IPAddress.Any, 7523);
            listener.Start();
            Console.WriteLine("Server started...");
            _ = accept_connection_async();

            while (true)
            {

            }

            infoEEG = new StreamInfo("EegEmu", "EEG", 5, 250.0, channel_format_t.cf_float32, "EEGStreamEmulator");
            outletEEG = new StreamOutlet(infoEEG);

            String[] channels_of_interests = { "C3", "C4", "CZ", "FZ", "PZ" };
            XMLElement chns = infoEEG.desc().append_child("channels");
            foreach (var ch in channels_of_interests)
            {
                chns.append_child("channel")
                    .append_child_value("label", ch)
                    .append_child_value("unit", "microvolts")
                    .append_child_value("type", "EEG");
            }

            infoIMP = new StreamInfo("ImpEmu", "IMP", 9, 250.0, channel_format_t.cf_float32, "IMPStreamEmulator");
            outletIMP = new StreamOutlet(infoIMP);

            // Create a watcher
            BluetoothLEAdvertisementWatcher watcher = new BluetoothLEAdvertisementWatcher();
            watcher.ScanningMode = BluetoothLEScanningMode.Active;
            watcher.Received += Watcher_ReceivedAsync;
            Console.WriteLine("Starting BluetoothLEAdvertisementWatcher");
            Console.WriteLine(">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>");
            watcher.Start();

            while (foundDevices.Count == 0)
            {
                Thread.Sleep(100);
            }

            Console.WriteLine("<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<");
            Console.WriteLine("Stopping BluetoothLEAdvertisementWatcher");
            // We can't connect if watch running so stop it.
            watcher.Stop();

            Console.WriteLine();
            Console.WriteLine($"Devices found = {foundDevices.Count}");
            Console.WriteLine();
            Console.WriteLine($"---------------------------------------");

            _ = ConnectAndReceiveSomeAsync(foundDevices[0]);
            Console.WriteLine($"---------------------------------------");

            while (true)
            {

            }

        }

        public static async Task accept_connection_async()
        {
            TcpClient client = await listener.AcceptTcpClientAsync();
            Console.WriteLine("Client connected...");

            await read_message_loop_async(client);

            client.Close();
            Console.WriteLine("Client disconnected...");
        }

        public static async Task read_message_loop_async(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[2048];
            int bytesRead;
            bool stop_command_received = false;

            while (!stop_command_received)
            {
                // Receive the length of the command
                byte[] commandLengthBytes = new byte[4];
                bytesRead = await stream.ReadAsync(commandLengthBytes, 0, 4);
                if (bytesRead == 4)
                {
                    int commandLength = BitConverter.ToInt32(commandLengthBytes, 0);
                    // Receive the answer itself
                    byte[] commandBytes = new byte[commandLength];
                    bytesRead = await stream.ReadAsync(commandBytes, 0, commandLength);
                    if (bytesRead == commandLength)
                    {
                        string command = Encoding.ASCII.GetString(commandBytes, 0, commandLength);
                        Console.WriteLine($"Received message: {command}");

                        stop_command_received = await process_message_async(command, stream);
                    }
                }

            }
        }

        static async Task<bool> process_message_async(string message, NetworkStream stream)
        {
            JObject json_obj = JObject.Parse(message);

            // Creating a JSON object
            var json_command1_responce = new
            {
                Cmd = 1,
                Data = new
                {
                    Devices = new[] { "testnb2" },
                    ErrorCode = 0
                }
            };
            var json_command2_responce = new
            {
                Cmd = 2,
                Data = new
                {
                    ErrorCode = 0,
                    IsMultySignal = false,
                    StreamNames = new
                    {
                        EEG = "EEGStreamEmulator",
                        IMP = "IMPStreamEmulator"
                    },
                    Electrodes = new
                    {
                        EEG = new[] { "FZ", "C3", "CZ", "C4", "PZ" },
                        IMP = new[] { "FZ", "C3", "CZ", "C4", "PZ", "GND", "REF" }
                    },
                    DeviceInfo = new
                    {
                        DeviceName = "testnb2",
                        Health = 73,
                        DeviceType = 0
                    }
                }
            };

            string string_command_responce;
            if (json_obj.SelectToken("Cmd").Value<int>() == 2)
            {
                // Serializing the JSON object to a string
                string_command_responce = JsonConvert.SerializeObject(json_command2_responce);
            }
            else
            {
                // Serializing the JSON object to a string
                string_command_responce = JsonConvert.SerializeObject(json_command1_responce);
            }

            Console.WriteLine("Length of json command string: " + string_command_responce.Length);

            byte[] bytes_response = Encoding.ASCII.GetBytes(string_command_responce);
            // Convert the length to a sequence of bytes
            byte[] lengthBytes = BitConverter.GetBytes((Int32)(bytes_response.Length));

            // Send the responce length bytes
            await stream.WriteAsync(lengthBytes, 0, lengthBytes.Length);
            // Send the responce itself
            await stream.WriteAsync(bytes_response, 0, bytes_response.Length);

            return false;
        }

        private static void Watcher_ReceivedAsync(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            _ = ProcessWatcherReceivedAsync(sender, args);
        }

        private static async Task ProcessWatcherReceivedAsync(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            Console.WriteLine("Found " + args.Advertisement.LocalName + " " + $"address :{args.BluetoothAddress:X}");

            if (IsValidDevice(args))
            {
                Console.WriteLine($"Found an NB2 eeg amplifyer :{args.BluetoothAddress:X}");

                // Add it to list as a BluetoothLEDevice
                BluetoothLEDevice dev = await BluetoothLEDevice.FromBluetoothAddressAsync(args.BluetoothAddress);
                foundDevices.Add(dev);
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

        [Obsolete]
        private static async Task ConnectAndReceiveSomeAsync(BluetoothLEDevice device)
        {
            var services_result = await device.GetGattServicesAsync(BluetoothCacheMode.Uncached);

            if (services_result.Status == GattCommunicationStatus.Success)
            {
                Console.WriteLine($"===========================");
                Console.WriteLine($"^^^^^^^^^^^^^^^^^^^^^^^^^^^");

                var sr = await device.GetGattServicesForUuidAsync(GenericAccess);
                var cr = await sr.Services[0].GetCharacteristicsForUuidAsync(DeviceNameString);
                var gc = cr.Characteristics[0];
                var rr = await gc.ReadValueAsync();
                var rdr = DataReader.FromBuffer(rr.Value);
                Console.WriteLine($"Name: {rdr.ReadString(14)}");

                sr = await device.GetGattServicesForUuidAsync(DeviceInformation);
                cr = await sr.Services[0].GetCharacteristicsForUuidAsync(SerialNumberString);
                gc = cr.Characteristics[0];
                rr = await gc.ReadValueAsync();
                rdr = DataReader.FromBuffer(rr.Value);
                Console.WriteLine($"Serial: {rdr.ReadString(4)}");

                sr = await device.GetGattServicesForUuidAsync(BatteryService);
                cr = await sr.Services[0].GetCharacteristicsForUuidAsync(BatteryPropertiesUuid);
                gc = cr.Characteristics[0];
                rr = await gc.ReadValueAsync();
                rdr = DataReader.FromBuffer(rr.Value);

                rdr.ReadBytes(value: PropertyBytes);
                BatteryProperties batteryProperties;
                batteryProperties.Capacity = PropertyBytes[1];
                batteryProperties.Capacity = (UInt16)(batteryProperties.Capacity << 8);
                batteryProperties.Capacity += PropertyBytes[0];
                batteryProperties.Level = PropertyBytes[3];
                batteryProperties.Level = (UInt16)(batteryProperties.Level << 8);
                batteryProperties.Level += PropertyBytes[2];
                batteryProperties.Voltage = PropertyBytes[5];
                batteryProperties.Voltage = (UInt16)(batteryProperties.Voltage << 8);
                batteryProperties.Voltage += PropertyBytes[4];
                batteryProperties.Current = PropertyBytes[7];
                batteryProperties.Current = (Int16)(batteryProperties.Current << 8);
                batteryProperties.Current += PropertyBytes[6];
                batteryProperties.Temperature = PropertyBytes[9];
                batteryProperties.Temperature = (Int16)(batteryProperties.Temperature << 8);
                batteryProperties.Temperature += PropertyBytes[8];
                Console.WriteLine($"capacity: {batteryProperties.Capacity}");
                Console.WriteLine($"level: {batteryProperties.Level}");
                Console.WriteLine($"voltage: {batteryProperties.Voltage}");
                Console.WriteLine($"current: {batteryProperties.Current}");
                Console.WriteLine($"temperature: {batteryProperties.Temperature}");

                GattCharacteristic command_characteristic = null;
                GattCharacteristic calibration_characteristic = null;
                sr = await device.GetGattServicesForUuidAsync(ControlServiceUuid);
                var crsr = await sr.Services[0].GetCharacteristicsAsync(0);
                Console.WriteLine($"get all characteristics of control service result status: {cr.Status}");
                var crs = crsr.Characteristics;
                foreach ( var c in crs )
                {
                    if(c.Uuid == CommandUuid)
                    {
                        command_characteristic = c;
                    }
                    else if (c.Uuid == CalibrationUuid)
                    {
                        calibration_characteristic = c;
                    }
                }

                sr = await device.GetGattServicesForUuidAsync(AcquisitionServiceUuid);
                cr = await sr.Services[0].GetCharacteristicsForUuidAsync(DataUuid);
                Console.WriteLine($"get data characteristic result status: {cr.Status}");
                var data_characteristic = cr.Characteristics[0];
                // Enable notifications for the characteristic
                GattClientCharacteristicConfigurationDescriptorValue configValue = GattClientCharacteristicConfigurationDescriptorValue.Notify;
                GattCommunicationStatus status = await data_characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(configValue);
                // subscribe on notification
                data_characteristic.ValueChanged += ReceiveData;

                DataWriter dw;
                GattWriteResult wr;

                // start data acquisition
                dw = new DataWriter();
                dw.WriteBytes(hexvals_to_bytes(start_acquisition_data_command));
                wr = await command_characteristic.WriteValueWithResultAsync(dw.DetachBuffer(), GattWriteOption.WriteWithResponse);
                Console.WriteLine($"Write result status: {wr.Status}");

                //// stop data acquisition
                //dw = new DataWriter();
                //dw.WriteBytes(hexvals_to_bytes(stop_acquisition_data_command));
                //wr = await gc.WriteValueWithResultAsync(dw.DetachBuffer(), GattWriteOption.WriteWithResponse);
                //Console.WriteLine($"Write result status: {wr.Status}");

                //// turn off command
                //dw = new DataWriter();
                //dw.WriteBytes(hexvals_to_bytes(turn_off_command));
                //wr = await gc.WriteValueWithResultAsync(dw.DetachBuffer(), GattWriteOption.WriteWithResponse);
                //Console.WriteLine($"Write result status: {wr.Status}");

                //// connection close command
                //dw = new DataWriter();
                //dw.WriteBytes(hexvals_to_bytes(connection_close_command));
                //wr = await gc.WriteValueWithResultAsync(dw.DetachBuffer(), GattWriteOption.WriteWithResponse);
                //Console.WriteLine($"Write result status: {wr.Status}");

            }

        }

        private static byte[] hexvals_to_bytes(string hex_values)
        {
            var hexValues = hex_values.Split(' ');
            var byteArray = new byte[hexValues.Length];
            for (int i = 0; i < hexValues.Length; i++)
            {
                byteArray[i] = Convert.ToByte(hexValues[i], 16);
            }

            return byteArray;
        }


        // Counters
        static private UInt32 pktCounter = 0;
        static private UInt32 errorsCount = 0;

        // All channels enabled
        static private UInt16 EnabledChannels = UInt16.MaxValue;

        // Array of previous values
        static Int32[] prev = new Int32[Pkt.ChannelsCount];


        //static private void ReceiveData(GattCharacteristic sender, GattValueChangedEventArgs args)
        //{
        //    Console.WriteLine($"value changed: {counter}");
        //    counter++;
        //}

        static int counter = 0;
        static private void ReceiveData(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            //Console.WriteLine($"value changed: {Program.counter}");
            //Program.counter++;

            int val;
            int offset = 0;
            UInt32 code = 0;

            // Array of decoded values
            int[] array = new int[Pkt.ChannelsCount * Pkt.SamplesCount];
            int pos = 0;

            // Flag indicates that the channel is disabled
            bool skip;

            // An Indicate or Notify reported that the value has changed
            var reader = DataReader.FromBuffer(args.CharacteristicValue);

            // Parse the data however required
            byte[] input = new byte[reader.UnconsumedBufferLength];

            // Read input
            reader.ReadBytes(input);

            UInt16 counter = BitConverter.ToUInt16(input, input.Length - 2);

            pktCounter++;

            if (pktCounter != counter)
            {
                errorsCount++;
            }

            for (int i = 0; i < Pkt.SamplesCount; i++)
            {
                if (i != 0)
                {
                    code = BitConverter.ToUInt32(input, offset);
                    offset += 4;
                }

                for (int ch = 0; ch < Pkt.ChannelsCount; ch++)
                {
                    skip = ((EnabledChannels >> ch) & 1) == 0;

                    if (i == 0)
                    {
                        if (!skip)
                        {
                            val = (input[offset + 2] << 8) | (input[offset + 1] << 16) | (input[offset] << 24);
                            val /= 256;
                            offset += 3;
                        }
                        else
                        {
                            val = Int32.MaxValue;
                        }
                    }
                    else
                    {
                        if (!skip)
                        {
                            switch ((code >> ch * 2) & 3)
                            {
                                case 0:
                                    val = prev[ch] + (sbyte)input[offset];
                                    offset += 1;
                                    break;

                                case 1:
                                    val = prev[ch] + BitConverter.ToInt16(input, offset);
                                    offset += 2;
                                    break;

                                case 2:
                                    val = (input[offset + 2] << 8) | (input[offset + 1] << 16) | (input[offset] << 24);
                                    val /= 256;
                                    offset += 3;
                                    break;

                                default:
                                    val = Int32.MaxValue;
                                    break;
                            }
                        }
                        else
                        {
                            val = Int32.MaxValue;
                        }
                    }

                    array[pos] = val;
                    prev[ch] = val;
                    pos++;
                }
            }
            Console.WriteLine($" {array[12]}   {array[4]}   {array[13]}   {array[5]}   {array[14]}");
            float[] dataEEG = new float[5] {(float)array[12], (float)array[4], (float)array[13], (float)array[5], (float)array[14]};
            outletEEG.push_sample(dataEEG);
        }

        public struct BatteryProperties
        {
            public UInt16 Capacity;     // mAh
            public UInt16 Level;    // % * 10
            public UInt16 Voltage;  // mV
            public Int16 Current;   // mA
            public Int16 Temperature; // °C * 10
        }

        public struct Pkt
        {
            public const int SamplesCount = 4;
            public const int ChannelsCount = 16;
        }

        static Guid GenericAccess = new Guid("00001800-0000-1000-8000-00805f9b34fb");
        static Guid DeviceNameString = new Guid("00002a00-0000-1000-8000-00805f9b34fb");

        static Guid DeviceInformation = new Guid("0000180a-0000-1000-8000-00805f9b34fb");
        static Guid SerialNumberString = new Guid("00002a25-0000-1000-8000-00805f9b34fb");

        static Guid BatteryService = new Guid("0000180f-0000-1000-8000-00805f9b34fb");
        static Guid BatteryPropertiesUuid = new Guid("5c979c9f-a1ac-5715-9a1b-f81a581179d9");
        const int BatteryPropertiesSize = 10;
        static byte[] PropertyBytes = new byte[BatteryPropertiesSize];

        static Guid CurrentDateServUuid = new Guid("00001805-0000-1000-8000-00805f9b34fb");
        static Guid DateUuid = new Guid("a9b157a1-5827-5553-9ba5-5f5ff8f8e173");
        const int DateSize = 4;
        static byte[] DateBytes = new byte[DateSize];

        static Guid ControlServiceUuid = new Guid("a183c5a7-1e93-8deb-a113-e8d5bb5581db");
        static Guid CommandUuid = new Guid("7395ca15-5997-5a1b-a138-75a7a573b8e5");
        static Guid CalibrationUuid = new Guid("13553757-7d95-5fd9-91b3-87cf759abc79");

        static Guid AcquisitionServiceUuid = new Guid("5775ab91-ee53-57e1-a77b-1c87183bd78c");
        static Guid DataUuid = new Guid("75851135-953a-7739-c781-5a935531397a");

        static readonly string start_acquisition_data_command = "01 00 00 01 00 FF FF 00 02 00 00 00 00 00 00 00 02 00 59 6D 3D B3 DC 5C 63 B8 1B 91 D2 76 2D E7 C5 28 DA A8 95 57 18 53 32 55 8B 7E 97 3C A6 A6 0D 99";
        static readonly string start_acquisition_impedance_command = "01 01 00 01 00 FF FF 00 02 00 00 00 00 00 00 00 02 00 3E 73 58 12 CE 8C 90 B1 B0 AB 03 20 35 C2 06 5A 0F C3 AE 3F BB C8 43 E6 24 FC 0F 9C 23 5A 49 70";
        static readonly string stop_acquisition_data_command = "02 8A DC 88 98 EC A3 3B 08 CB BD 40 12 50 FC 6C EA 4E FF 7D 01 C7 87 DC 69 9A 76 52 18 7F FF D5 21";
        static readonly string connection_close_command = "03 67 ED AD EE C5 5D AF 5D A2 FB DC C5 8C 49 62 22 4E 63 64 ED A9 50 AC 9E 58 DF 70 77 CC 08 E2 BC";
        static readonly string turn_off_command = "05 0D DE C2 81 BC 8B 45 00 68 68 47 03 C7 6C B7 DA C0 49 C8 C1 C0 40 82 60 D7 D7 5B EE D9 4B A8 F8 0E";


    }
}






