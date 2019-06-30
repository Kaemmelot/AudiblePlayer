#ifndef _CMD_H
#define _CMD_H

#include "writer_setup.h"
#include "helper.h"

extern bool cmdRcvd;
extern bool cmdAcked;
extern byte cmd[MAX_CMD_LEN + 1]; // \r will be last byte; \n not recorded
extern byte cmdLen;
extern bool byteMode;

void invalidCommand(const bool skip);
void resetCommand();
void readSerial();

#endif
