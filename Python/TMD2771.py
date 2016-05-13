# Distributed with a free-will license.
# Use it any way you want, profit or free, provided it fits in the licenses of its associated works.
# TMD2771
# This code is designed to work with the TMD2771_I2CS I2C Mini Module available from ControlEverything.com.
# https://www.controleverything.com/content/Light?sku=TMD2771_I2CS#tabs-0-product_tabset-2

import smbus
import time

# Get I2C bus
bus = smbus.SMBus(1)

# TMD2771 address, 0x39(57)
# Select enable register 0x00(00), with command register, 0xA0(160)
#		0x0F(15)	Power ON, Wait enable, Proximity enable, ALS enable
bus.write_byte_data(0x39, 0x00 | 0xA0, 0x0F)
# TMD2771 address, 0x39(57)
# Select ALS time register 0x01(01), with command register, 0xA0(160)
#		0xFF(255)	Time - 101 ms
bus.write_byte_data(0x39, 0x01 | 0xA0, 0xDB)
# TMD2771 address, 0x39(57)
# Select proximity ADC time register 0x02(02), with command register, 0xA0(160)
#		0xFF(255)	Time - 2.72 ms
bus.write_byte_data(0x39, 0x02 | 0xA0, 0xFF)
# TMD2771 address, 0x39(57)
# Select Wait time register 0x03(03), with command register, 0xA0(160)
#		0xFF(255)	Time - 2.72 ms
bus.write_byte_data(0x39, 0x03 | 0xA0, 0xFF)
# TMD2771 address, 0x39(57)
# Select pulse count register 0x0E(14), with command register, 0xA0(160)
#		0x04(04)	Pulse count = 4
bus.write_byte_data(0x39, 0x0E | 0xA0, 0x04)
# TMD2771 address, 0x39(57)
# Select control register 0x0F(15), with command register, 0x80(128)
#		0x20(32)	120 mA LED strength, Proximity uses CH1 diode
#					Proximity gain 1x, ALS gain 1x
bus.write_byte_data(0x39, 0x0F | 0xA0, 0x20)

time.sleep(0.5)

# TMD2771 address, 0x39(57)
# Read data back from 0x14(20), with command register, 0x80(128), 6 bytes
# c0Data LSB, c0Data MSB, c1Data LSB, c1Data MSB, Proximity LSB, Proximity MSB
data = bus.read_i2c_block_data(0x39, 0x14 | 0xA0, 6)

# Convert the data
c0Data = data[1] * 256 + data[0]
c1Data = data[3] * 256 + data[2]
proximity = data[5] * 256 + data[4]
luminance = 0.0
CPL = (101.0) / 24.0
luminance1 = ((1.00 *  c0Data) - (2 * c1Data)) / CPL
luminance2 = ((0.6 * c0Data) - (1.00 * c1Data)) / CPL
if luminance1 > 0 and luminance2 > 0 :
	if luminance1 > luminance2 :
		luminance = luminance1
	else :
		luminance = luminance2

# Output data to screen
print "Ambient Light luminance : %.2f lux" %luminance
print "Proximity of the Device : %.2f" %proximity