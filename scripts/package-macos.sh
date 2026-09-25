#!/usr/bin/env bash
# Builds Calypsov into a distributable macOS .app + .dmg.
#
# Usage: scripts/package-macos.sh [osx-arm64|osx-x64]
# Defaults to osx-arm64 (Apple Silicon). Output lands in Deployed/MacOs/.
#
# This does NOT code-sign with a Developer ID or notarize. It ad-hoc-signs
# only so the app runs locally without Gatekeeper calling it "damaged".
# Sharing the .dmg with other people will still show an "unidentified
# developer" warning unless you sign with a paid Apple Developer account
# and notarize via `xcrun notarytool`.

set -euo pipefail

RID="${1:-osx-arm64}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
APP_PROJECT_DIR="$ROOT_DIR/Calypsov"
UI_DIR="$APP_PROJECT_DIR/UserInterface"
OUT_DIR="$ROOT_DIR/Deployed/MacOs"
APP_NAME="Calypsov"
BUNDLE_ID="com.calypsov.desktop"
VERSION="1.0.0"

echo "==> Building frontend"
# Uses build-only rather than build: the full `build` script also runs a
# type-check step that currently fails on a pre-existing TS parsing error
# inside @vitejs/plugin-vue-jsx's own .d.mts files (unrelated to app code,
# and unrelated to packaging) - it doesn't affect the actual Vite output.
(cd "$UI_DIR" && npm run build-only)

echo "==> Syncing frontend build into wwwroot"
rm -f "$APP_PROJECT_DIR/wwwroot/assets/"*.js "$APP_PROJECT_DIR/wwwroot/assets/"*.css
cp -R "$UI_DIR/dist/assets/." "$APP_PROJECT_DIR/wwwroot/assets/"
cp "$UI_DIR/dist/index.html" "$APP_PROJECT_DIR/wwwroot/index.html"
cp "$UI_DIR/dist/favicon.ico" "$APP_PROJECT_DIR/wwwroot/favicon.ico"

echo "==> Publishing self-contained .NET app for $RID"
PUBLISH_DIR="$APP_PROJECT_DIR/bin/Release/net8.0/$RID/publish"
(cd "$APP_PROJECT_DIR" && dotnet publish -c Release -r "$RID" --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true)

echo "==> Assembling .app bundle"
rm -rf "$OUT_DIR/$APP_NAME.app"
CONTENTS_DIR="$OUT_DIR/$APP_NAME.app/Contents"
mkdir -p "$CONTENTS_DIR/MacOS" "$CONTENTS_DIR/Resources"

cp "$PUBLISH_DIR/$APP_NAME" "$CONTENTS_DIR/MacOS/$APP_NAME"
chmod +x "$CONTENTS_DIR/MacOS/$APP_NAME"
# AppIcon/ and wwwroot/ are loose "copy to output" content, not merged into the
# single-file executable - PhotinoWindow.SetIconFile and the static file server
# both resolve them relative to the executable's own directory at runtime, so
# they need to physically sit next to it inside the bundle.
cp -R "$PUBLISH_DIR/AppIcon" "$CONTENTS_DIR/MacOS/AppIcon"
cp -R "$PUBLISH_DIR/wwwroot" "$CONTENTS_DIR/MacOS/wwwroot"
cp "$APP_PROJECT_DIR/AppIcon/app.icns" "$CONTENTS_DIR/Resources/app.icns"

cat > "$CONTENTS_DIR/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>$APP_NAME</string>
    <key>CFBundleDisplayName</key>
    <string>$APP_NAME</string>
    <key>CFBundleIdentifier</key>
    <string>$BUNDLE_ID</string>
    <key>CFBundleVersion</key>
    <string>$VERSION</string>
    <key>CFBundleShortVersionString</key>
    <string>$VERSION</string>
    <key>CFBundleExecutable</key>
    <string>$APP_NAME</string>
    <key>CFBundleIconFile</key>
    <string>app.icns</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>LSMinimumSystemVersion</key>
    <string>11.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
PLIST

echo "==> Ad-hoc signing (local use only, not for distribution)"
codesign --force --deep --sign - "$OUT_DIR/$APP_NAME.app"

echo "==> Creating .dmg with drag-to-Applications layout"
DMG_WORK_DIR="$(mktemp -d)"
STAGING_DIR="$DMG_WORK_DIR/staging"
mkdir -p "$STAGING_DIR"
cp -R "$OUT_DIR/$APP_NAME.app" "$STAGING_DIR/"
ln -s /Applications "$STAGING_DIR/Applications"

RW_DMG="$DMG_WORK_DIR/$APP_NAME-rw.dmg"
hdiutil create -volname "$APP_NAME" -srcfolder "$STAGING_DIR" -fs HFS+ -format UDRW -ov "$RW_DMG" >/dev/null

ATTACH_OUTPUT="$(hdiutil attach "$RW_DMG" -nobrowse -noautoopen)"
MOUNT_DIR="$(echo "$ATTACH_OUTPUT" | grep -Eo '/Volumes/.*' | tail -1)"

# Best-effort Finder window styling (icon layout, window size) so the mounted
# dmg shows the app next to an Applications shortcut for drag-to-install.
# macOS may prompt once for "osascript wants to control Finder" - if that's
# declined or unavailable, the dmg still installs fine, just without the
# custom layout (icons default to Finder's normal auto-arranged grid).
osascript <<APPLESCRIPT || echo "    (Finder window styling skipped - dmg still works, just unstyled)"
tell application "Finder"
    tell disk "$APP_NAME"
        open
        set current view of container window to icon view
        set toolbar visible of container window to false
        set statusbar visible of container window to false
        set bounds of container window to {200, 120, 700, 470}
        set viewOptions to icon view options of container window
        set arrangement of viewOptions to not arranged
        set icon size of viewOptions to 100
        set position of item "$APP_NAME.app" to {130, 150}
        set position of item "Applications" to {370, 150}
        update without registering applications
        delay 1
        close
    end tell
end tell
APPLESCRIPT

hdiutil detach "$MOUNT_DIR" >/dev/null 2>&1 || hdiutil detach "$MOUNT_DIR" -force >/dev/null 2>&1

rm -f "$OUT_DIR/$APP_NAME.dmg"
hdiutil convert "$RW_DMG" -format UDZO -ov -o "$OUT_DIR/$APP_NAME.dmg" >/dev/null

rm -rf "$DMG_WORK_DIR"

echo "==> Done"
echo "App: $OUT_DIR/$APP_NAME.app"
echo "Dmg: $OUT_DIR/$APP_NAME.dmg"
