// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading;
using Windows.UI.Xaml.Controls;
using Windows.Devices.Enumeration;
using Windows.Devices.I2c;

namespace TMD2771
{
	struct ProxALS
	{
		public double PROX;
		public double ALS;
	};

	// App that reads data over I2C from an TMD2771 Proximity and Light Sensor
	public sealed partial class MainPage : Page
	{
		private const byte PROXALS_I2C_ADDR = 0x39;		// I2C address of the TMD2771
		private const byte PROXALS_REG_COMMAND = 0xA0;		// Command register
		private const byte PROXALS_REG_ENABLE = 0x00;		// Enables state and interrupt register
		private const byte PROXALS_REG_ATIME = 0x01;		// ALS ADC time register
		private const byte PROXALS_REG_PTIME = 0x02;		// Proximity ADC time register
		private const byte PROXALS_REG_WTIME = 0x03;		// Wait time register
		private const byte PROXALS_REG_PPULSE = 0x0E;		// Proximity pulse count register
		private const byte PROXALS_REG_CONTROL = 0x0F;		// Control register
		private const byte PROXALS_REG_C0DATA = 0x14;		// ALS Ch0 ADC low data register
		private const byte PROXALS_REG_C1DATA = 0x16;		// ALS Ch1 ADC low data register
		private const byte PROXALS_REG_PDATA = 0x18;		// Proximity ADC low data register

		private I2cDevice I2CProxALS;
		private Timer periodicTimer;

		public MainPage()
		{
			this.InitializeComponent();

			// Register for the unloaded event so we can clean up upon exit
			Unloaded += MainPage_Unloaded;

			// Initialize the I2C bus, Proximity and Light Sensor, and timer
			InitI2CProxALS();
		}

		private async void InitI2CProxALS()
		{
			string aqs = I2cDevice.GetDeviceSelector();		// Get a selector string that will return all I2C controllers on the system
			var dis = await DeviceInformation.FindAllAsync(aqs);	// Find the I2C bus controller device with our selector string
			if (dis.Count == 0)
			{
				Text_Status.Text = "No I2C controllers were found on the system";
				return;
			}

			var settings = new I2cConnectionSettings(PROXALS_I2C_ADDR);
			settings.BusSpeed = I2cBusSpeed.FastMode;
			I2CProxALS = await I2cDevice.FromIdAsync(dis[0].Id, settings);	// Create an I2C Device with our selected bus controller and I2C settings
			if (I2CProxALS == null)
			{
				Text_Status.Text = string.Format(
					"Slave address {0} on I2C Controller {1} is currently in use by " +
					"another application. Please ensure that no other applications are using I2C.",
				settings.SlaveAddress,
				dis[0].Id);
				return;
			}

			/*
				Initialize the Proximity and Light Sensor:
				For this device, we create 2-byte write buffers:
				The first byte is the register address we want to write to.
				The second byte is the contents that we want to write to the register.
			*/
			byte[] WriteBuf_Enable = new byte[] { PROXALS_REG_ENABLE | PROXALS_REG_COMMAND, 0x0F };		// 0x03 sets Power ON and Wait, Proximity and ALS features are enabled
			byte[] WriteBuf_Atime = new byte[] { PROXALS_REG_ATIME | PROXALS_REG_COMMAND, 0xDB };		// 0xDB sets ATIME : 101 ms, 37 cycles, 37888 max count
			byte[] WriteBuf_Ptime = new byte[] { PROXALS_REG_PTIME | PROXALS_REG_COMMAND, 0xFF };		// 0x00 sets PTIME : 2.72 ms, 1 cycle, 1023 max count
			byte[] WriteBuf_Wtime = new byte[] { PROXALS_REG_WTIME | PROXALS_REG_COMMAND, 0xFF };		// 0xFF sets WTIME : 2.72 ms (WLONG = 0), 1 wait time
			byte[] WriteBuf_Ppulse = new byte[] { PROXALS_REG_PPULSE | PROXALS_REG_COMMAND, 0x04 };		// 0x04 sets 4 number of proximity pulses to be transmitted
			byte[] WriteBuf_Control = new byte[] { PROXALS_REG_CONTROL | PROXALS_REG_COMMAND, 0x20 };	// 0x20 sets 120 mA LED strength, Proximity uses CH1 diode, Proximity gain 1x, ALS gain 1x

			// Write the register settings
			try
			{
				I2CProxALS.Write(WriteBuf_Enable);
				I2CProxALS.Write(WriteBuf_Atime);
				I2CProxALS.Write(WriteBuf_Ptime);
				I2CProxALS.Write(WriteBuf_Wtime);
				I2CProxALS.Write(WriteBuf_Ppulse);
				I2CProxALS.Write(WriteBuf_Control);
			}
			// If the write fails display the error and stop running
			catch (Exception ex)
			{
				Text_Status.Text = "Failed to communicate with device: " + ex.Message;
				return;
			}

			// Create a timer to read data every 900ms
			periodicTimer = new Timer(this.TimerCallback, null, 0, 900);
		}

		private void MainPage_Unloaded(object sender, object args)
		{
			// Cleanup
			I2CProxALS.Dispose();
		}

		private void TimerCallback(object state)
		{
			string alsText, proxText;
			string addressText, statusText;

			// Read and format Proximity and Light Sensor data
			try
			{
				ProxALS proxals = ReadI2CProxALS();
				addressText = "I2C Address of the Proximity and Light Sensor TMD2771: 0x39";
				alsText = String.Format("Ambient Light Luminance: {0:F2} lux", proxals.ALS);
				proxText = String.Format("Proximity of the Device: {0:F2}", proxals.PROX);
				statusText = "Status: Running";
			}
			catch (Exception ex)
			{
				alsText = "Ambient Light Luminance: Error";
				proxText = "Proximity of the Device: Error";
				statusText = "Failed to read from Proximity and Light Sensor: " + ex.Message;
			}

			// UI updates must be invoked on the UI thread
			var task = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
			{
				Text_Ambient_Light_Luminance.Text = alsText;
				Text_Proximity_of_the_Device.Text = proxText;
				Text_Status.Text = statusText;
			});
		}

		private ProxALS ReadI2CProxALS()
		{
			byte[] RegAddrBuf = new byte[] { PROXALS_REG_C0DATA | PROXALS_REG_COMMAND };	// Read data from the register address
			byte[] ReadBuf = new byte[6];													// We read 6 bytes sequentially to get all 3 two-byte data registers in one read

			/*
				Read from the Proximity and Light Sensor 
				We call WriteRead() so we first write the address of the ALS CH0 data low register, then read all 3 values
			*/
			I2CProxALS.WriteRead(RegAddrBuf, ReadBuf);

			/*
				In order to get the raw 16-bit data values, we need to concatenate two 8-bit bytes from the I2C read.
			*/
			
			ushort c0Data = (ushort)(ReadBuf[0] & 0xFF);
			c0Data |= (ushort)((ReadBuf[1] & 0xFF) * 256);
			ushort c1Data = (ushort)(ReadBuf[2] & 0xFF);
			c1Data |= (ushort)((ReadBuf[3] & 0xFF) * 256);
			ushort proximity = (ushort)(ReadBuf[4] & 0xFF);
			proximity |= (ushort)((ReadBuf[5] & 0xFF) * 256);
			double luminance = 0.0;
			double CPL = 101 / 24;
			double luminance1 = (1.00 *  c0Data - (2 * c1Data)) / CPL;
			double luminance2 = ((0.6 * c0Data) - (1.00 * c1Data)) / CPL;
			if (luminance1 > 0 && luminance2 > 0)
			{
				if (luminance1 > luminance2)
				{
					luminance = luminance1;
				}
				else
				{
					luminance = luminance2;
				}
			}

			ProxALS proxals;
			proxals.ALS = luminance;
			proxals.PROX = proximity;

			return proxals;
		}
	}
}
