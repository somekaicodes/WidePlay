#!/bin/bash
# Builds and deploys Wide Play to the connected iPhone.
# Workaround: the MAUI build tooling adds a com.apple.FinderInfo xattr to the .app
# directory during codesign preparation, which /usr/bin/codesign rejects. We let the
# build fail at that step (the .app is already assembled), then copy to /tmp (outside
# Finder's watch), strip xattrs, sign, and install from there.

set -e

APP="bin/Debug/net10.0-ios/ios-arm64/WidePlay.app"
TMP_APP="/tmp/WidePlay_sign.app"
IDENTITY="Apple Development: kkmin123446@icloud.com (96CSNF5L6K)"
PROFILE="$HOME/Library/Developer/Xcode/UserData/Provisioning Profiles/98cfbf14-4f31-428f-9763-22015704582e.mobileprovision"
DEVICE="00008120-000C14602188C01E"
ENTITLEMENTS="/tmp/wideplay_entitlements.plist"

echo "→ Building (codesign step will fail at the end — that's expected)..."
dotnet build WidePlay.csproj -f net10.0-ios -c Debug -p:RuntimeIdentifier=ios-arm64 2>/dev/null; true

echo "→ Copying to /tmp to avoid Finder re-adding xattrs during signing..."
rm -rf "$TMP_APP" && cp -rp "$APP" "$TMP_APP"

echo "→ Stripping xattrs..."
xattr -rc "$TMP_APP"

echo "→ Embedding provisioning profile..."
cp "$PROFILE" "$TMP_APP/embedded.mobileprovision"

echo "→ Extracting entitlements..."
security cms -D -i "$PROFILE" > /tmp/wideplay_profile.plist 2>/dev/null
/usr/libexec/PlistBuddy -x -c "Print :Entitlements" /tmp/wideplay_profile.plist > "$ENTITLEMENTS"

echo "→ Signing..."
/usr/bin/codesign --sign "$IDENTITY" --entitlements "$ENTITLEMENTS" "$TMP_APP"

echo "→ Installing on device..."
xcrun devicectl device install app --device "$DEVICE" "$TMP_APP"

echo "✓ Done — tap Wide Play on the iPhone to launch."
