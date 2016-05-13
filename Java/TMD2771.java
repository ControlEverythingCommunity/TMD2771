// Distributed with a free-will license.
// Use it any way you want, profit or free, provided it fits in the licenses of its associated works.
// TMD2771
// This code is designed to work with the TMD2771_I2CS I2C Mini Module available from ControlEverything.com.
// https://www.controleverything.com/content/Light?sku=TMD2771_I2CS#tabs-0-product_tabset-2

import com.pi4j.io.i2c.I2CBus;
import com.pi4j.io.i2c.I2CDevice;
import com.pi4j.io.i2c.I2CFactory;
import java.io.IOException;

public class TMD2771
{
	public static void main(String args[]) throws Exception
	{
		// Create I2C bus
		I2CBus bus = I2CFactory.getInstance(I2CBus.BUS_1);
		// Get I2C device, TMD2771 I2C address is 0x39(57)
		I2CDevice device = bus.getDevice(0x39);

		// Select enable register OR with command register
		// Set Power ON, Proximity , wait and ALS Enabled
		device.write(0x00 | 0xA0, (byte)0x0F);
		// Select ALS time register OR with command register
		// Atime = 101 ms
		device.write(0x01 | 0xA0, (byte)0xDB);
		// Select proximity time register OR with command register
		// Ptime = 2.72 ms, max count = 1023
		device.write(0x02 | 0xA0, (byte)0xFF);
		// Select wait time register OR with command register
		// Wtime = 2.72 ms
		device.write(0x03 | 0xA0, (byte)0xFF);
		// Select pulse count register
		// Pulse count = 4
		device.write(0x0E | 0xA0, (byte)0x04);
		// Select control register
		// 120 mA LED strength, proximtiy uses CH1 diode, 1x PGAIN, 1x AGAIN
		device.write(0x0F | 0xA0, (byte)0x20);
		Thread.sleep(800);

		// Read 6 Bytes of data from address 0x14(20)
		// c0Data lsb, c0Data msb, c1Data lsb, c1Data msb, proximity lsb, proximity msb
		byte[] data = new byte[6];
		device.read(0x14 | 0xA0, data, 0, 6);

		// Convert the data
		int c0Data = ((data[1] & 0xFF) * 256) + (data[0] & 0xFF);
		int c1Data = ((data[3] & 0xFF) * 256) + (data[2] & 0xFF);
		double proximity = ((data[5] & 0xFF) * 256) + (data[4] & 0xFF);
		double CPL = (101.0) / 24.0;
		double luminance1 = (1.0 * c0Data - 2 * c1Data) / CPL;
		double luminance2 = (0.6 * c0Data - 1.00 * c1Data) / CPL;
		double luminance = 0.0; 
		if((luminance1 > 0) && (luminance1 > luminance2))
		{
			luminance = luminance1;
		}
		else 
		if((luminance2 > 0) && (luminance2 > luminance1))
		{
			luminance = luminance2;
		}

		// Output data to screen
		System.out.printf("Ambient Light Luminance : %.2f lux %n", luminance);
		System.out.printf("Proximity of the Device : %.2f %n", proximity);
	}
}