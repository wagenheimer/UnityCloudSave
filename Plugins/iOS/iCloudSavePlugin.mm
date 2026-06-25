// Native bridge for iCloud Key-Value Store (NSUbiquitousKeyValueStore).
// Compiled into the iOS app by Unity — no manual Xcode setup needed beyond
// enabling the iCloud capability (handled by iOSPostBuildProcessor).

#import <Foundation/Foundation.h>
#include <stdlib.h>

extern "C" {

void _iCloudKVSave(const char* key, const char* value) {
    if (!key || !value) return;
    NSUbiquitousKeyValueStore *store = [NSUbiquitousKeyValueStore defaultStore];
    [store setString:[NSString stringWithUTF8String:value]
              forKey:[NSString stringWithUTF8String:key]];
    [store synchronize];
}

const char* _iCloudKVLoad(const char* key) {
    if (!key) return NULL;
    NSUbiquitousKeyValueStore *store = [NSUbiquitousKeyValueStore defaultStore];
    NSString *value = [store stringForKey:[NSString stringWithUTF8String:key]];
    if (!value) return NULL;
    return strdup([value UTF8String]);
}

void _iCloudKVSync() {
    [[NSUbiquitousKeyValueStore defaultStore] synchronize];
}

} // extern "C"
