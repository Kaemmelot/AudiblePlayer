#ifndef _WRITER_SETUP_H
#define _WRITER_SETUP_H

#define VERSION "1.0"

#include <arduino.h>

// command length + bytes per block + additional command param length
#define MAX_CMD_LEN 1 + 16 + 2

// default: 9600
// limit for Arduino IDE: 115200
// more: https://arduino.stackexchange.com/a/299
// https://www.arduino.cc/reference/en/language/functions/communication/serial/begin/
#define SERIAL_BAUDRATE 115200
// 8 bit, no parity, one stop
#define SERIAL_MODE SERIAL_8N1

//Commands (received):
// Read card (1 byte: sector, 1 byte: block, 1 byte: number of blocks)
#define READ_CARD 'R'
// Write card (1 byte: sector, 1 byte: block, 16 byte: content) [no trailers!]
#define WRITE_CARD 'W'
// Switch trailers/keys to use (6 byte: keyA, 3 byte: access bits, 1 byte unused/custom, 6 byte: keyB, 1 byte: 'A' or 'B' for current key selection)
#define SET_TRAILERS 'T'
// Change card trailers to new trailer [keys and access bits] (same params as K)
#define CHANGE_TRAILERS 't'
// Check trailers for problems
#define CHECK_TRAILERS 'C'
// Perform self-test of card
#define SELF_TEST 'S'
// Reset and reboot this program
#define RESET 'X'
// Toggle byte_mode on or off (on by default; off = text mode = every byte after the command must be 2 digit hex)
#define TOGGLE_BYTE_MODE 'b'
// ACK and NACK for error check

#define RECEIVE_CMDS "RWTtCSXb"

//Commands (send):
// Comment line (verbose info, only for direct user communication)
#define COMMENT '#'
// Initialization of this program complete
#define INIT_COMPLETE 'I'
// Card detected/removed (4/7/10 byte uid or 0 bytes on removal)
#define CARD_CHANGED 'C'
// Read/trailer/self-test partial result [before (n)ack or data]
#define PARTIAL_RESULT 'P'
// Acknowledgement of command required (error prevention)
#define ERROR_CHECK 'E'
// Authentication failed
#define AUTH_FAILED 'x'
// Invalid command/response;
#define INVALID_COMMAND 'X'
// ACK and NACK if command successful

// Acknowledgement of command
#define ACK 'A'
// Not acknowledged command
#define NACK 'N'

#endif
