#import <os/log.h>

void OsLogWithType(void *logPtr, uint8_t type, const char *message) {
    os_log_t log = (__bridge os_log_t)logPtr;
    os_log_with_type(log, type, "%{public}s", message);
}