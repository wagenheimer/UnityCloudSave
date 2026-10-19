// Game Center identity verification bridge for Unity Gaming Services' Cloud Save linking
// (AuthenticationService.LinkWithAppleGameCenterAsync).
//
// Unity's Social API (GameCenterPlatform) does not expose the identity verification signature, so
// this plugin calls GKLocalPlayer.generateIdentityVerificationSignature and replies through
// UnitySendMessage with a JSON payload:
//   { "publicKeyUrl": "...", "signature": "<base64>", "salt": "<base64>",
//     "timestamp": "1234567890", "teamPlayerId": "...", "error": null }
// On failure it replies { "error": "..." } and the C# side falls back to an anonymous account.

#import <Foundation/Foundation.h>
#import <GameKit/GameKit.h>

extern "C" void UnitySendMessage(const char* obj, const char* method, const char* msg);

static void UCS_SendResult(NSString* go, NSString* method, NSDictionary* payload)
{
    NSError* jsonError = nil;
    NSData* json = [NSJSONSerialization dataWithJSONObject:payload options:0 error:&jsonError];
    NSString* text = nil;

    if (json != nil)
        text = [[NSString alloc] initWithData:json encoding:NSUTF8StringEncoding];
    else
        text = [NSString stringWithFormat:@"{\"error\":\"json:%@\"}", jsonError.localizedDescription ?: @"unknown"];

    const char* goUtf8 = [go UTF8String];
    const char* methodUtf8 = [method UTF8String];
    const char* textUtf8 = [text UTF8String];

    UnitySendMessage(goUtf8, methodUtf8, textUtf8);
}

extern "C" void UCS_GameCenterRequestIdentitySignature(const char* callbackObject, const char* callbackMethod)
{
    NSString* go = callbackObject != NULL ? [NSString stringWithUTF8String:callbackObject] : @"CloudSaveSignatureReceiver";
    NSString* method = callbackMethod != NULL ? [NSString stringWithUTF8String:callbackMethod] : @"OnSignature";

    GKLocalPlayer* player = [GKLocalPlayer localPlayer];
    if (player == nil || !player.isAuthenticated)
    {
        UCS_SendResult(go, method, @{ @"error": @"not_authenticated" });
        return;
    }

    [player generateIdentityVerificationSignatureWithCompletionHandler:^(NSURL* publicKeyUrl, NSData* signature, NSData* salt, uint64_t timestamp, NSError* error) {
        if (error != nil || signature == nil || salt == nil || publicKeyUrl == nil)
        {
            UCS_SendResult(go, method, @{ @"error": error.localizedDescription ?: @"no_signature" });
            return;
        }

        NSDictionary* payload = @{
            @"publicKeyUrl": publicKeyUrl.absoluteString ?: @"",
            @"signature": [signature base64EncodedStringWithOptions:0],
            @"salt": [salt base64EncodedStringWithOptions:0],
            @"timestamp": [NSString stringWithFormat:@"%llu", (unsigned long long)timestamp],
            @"teamPlayerId": player.teamPlayerID ?: @""
        };

        // GameKit calls the completion handler off the main thread; UnitySendMessage needs main.
        dispatch_async(dispatch_get_main_queue(), ^{
            UCS_SendResult(go, method, payload);
        });
    }];
}
