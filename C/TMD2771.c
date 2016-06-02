// Distributed with a free-will license.
// Use it any way you want, profit or free, provided it fits in the licenses of its associated works.
// TMD2771
// This code is designed to work with the TMD2771_I2CS I2C Mini Module available from ControlEverything.com.
// https://www.controleverything.com/content/Light?sku=TMD2771_I2CS#tabs-0-product_tabset-2

#include <stdio.h>
#include <stdlib.h>
#include <linux/i2c-dev.h>
#include <sys/ioctl.h>
#include <fcntl.h>

void main() 
{
	// Create I2C bus
	int file;
	char *bus = "/dev/i2c-1";
	if ((file = open(bus, O_RDWR)) < 0) 
	{
		printf("Failed to open the bus. \n");
		exit(1);
	}
	// Get I2C device, TMD2771 I2C address is 0x39(57)
	ioctl(file, I2C_SLAVE, 0x39);

	// Select proximity time register OR with command register(0x00 | 0xA0)
	// Set Power ON, Proximity , wait and ALS Enabled(0x0F)
	char config[2] = {0};
	config[0] = 0x00 | 0xA0;
	config[1] = 0x0F;
	write(file, config, 2);
	// Select ALS time register OR with command register(0x01 | 0xA0)
	// Atime = 101 ms(0xDB)
	config[0] = 0x01 | 0xA0;
	config[1] = 0xDB;
	write(file, config, 2);
	// Select proximity time register OR with command register(0x02 | 0xA0)
	// Ptime = 2.72 ms, max count = 1023(0xFF)
	config[0] = 0x02 | 0xA0;
	config[1] = 0xFF;
	write(file, config, 2);
	// Select wait time register OR with command register(0x03 | 0xA0)
	// Wtime = 2.72 ms(0xFF)
	config[0] = 0x03 | 0xA0;
	config[1] = 0xFF;
	write(file, config, 2);
	// Select pulse count register(0x0E | 0xA0)
	// Pulse count = 4(0x04)
	config[0] = 0x0E | 0xA0;
	config[1] = 0x04;
	write(file, config, 2);
	// Select control register(0x0F | 0xA0)
	// 120 mA LED strength, proximtiy uses CH1 diode, 1x PGAIN, 1x AGAIN(0x20)
	config[0] = 0x0F | 0xA0;
	config[1] = 0x20;
	write(file, config, 2);
	sleep(1);

	// Read 6 Bytes of data from register(0x14)
	// c0Data lsb, c0Data msb, c1Data lsb, c1Data msb, proximity lsb, proximity msb
	char reg[1] = {0x14 | 0xA0} ;
	write(file, reg, 1);
	char data[6] = {0};
	if(read(file, data, 6) != 6)
	{
		printf("Erorr : Input/output Erorr \n");
	}
	else
	{
		// Convert the data
		int c0Data = (data[1] * 256 + data[0]);
		int c1Data = (data[3] * 256 + data[2]);
		float proximity = (data[5] * 256 + data[4]);
		float CPL = (101.0) / 24.0;
		float luminance1 = (1.0 * c0Data - 2 * c1Data) / CPL;
		float luminance2 = (0.6 * c0Data - 1.00 * c1Data) / CPL;
		float luminance = 0.0; 
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
		printf("Ambient Light Luminance : %.2f lux \n", luminance);
		printf("Proximity of the Device : %.2f \n", proximity);
	}
}
