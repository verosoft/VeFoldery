#!/usr/bin/env bash
# Bundle the self-contained TimeFold binary as a macOS .app
# Usage: ./package-macos.sh <path-to-published-dir> [output-dir]
set -euo pipefail

PUBLISH_DIR="${1:?usage: package-macos.sh <publish-dir> [output-dir]}"
OUT_DIR="${2:-./dist}"
APP_NAME="TimeFold"
BUNDLE_ID="com.verosoft.timefold"
VERSION="$(dotnet read app/TimeFold.Avalonia.csproj -get PropertyGroup:Version 2>/dev/null | tr -d ' ' || echo 1.0.4)"

APP="$OUT_DIR/$APP_NAME.app"
CONTENTS="$APP/Contents"
MACOS="$CONTENTS/MacOS"
RESOURCES="$CONTENTS/Resources"

mkdir -p "$MACOS" "$RESOURCES"

# Binary
cp "$PUBLISH_DIR/TimeFold" "$MACOS/$APP_NAME"
chmod +x "$MACOS/$APP_NAME"

# Icon (icns): build from app.png if sips is available (macOS only)
if [ -f "app/Assets/app.png" ] && command -v sips >/dev/null 2>&1; then
    ICONSET="$OUT_DIR/$APP_NAME.iconset"
    mkdir -p "$ICONSET"
    for size in 16 32 64 128 256 512; do
        sips -z $size $size "app/Assets/app.png" --out "$ICONSET/icon_${size}x${size}.png" >/dev/null
        double=$((size * 2))
        sips -z $double $double "app/Assets/app.png" --out "$ICONSET/icon_${size}x${size}@2x.png" >/dev/null
    done
    iconutil -c icns "$ICONSET" -o "$RESOURCES/$APP_NAME.icns"
    rm -rf "$ICONSET"
    ICON_REF="<string>$APP_NAME.icns</string>"
else
    ICON_REF="<string></string>"
fi

# Info.plist
cat > "$CONTENTS/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>                <string>$APP_NAME</string>
    <key>CFBundleDisplayName</key>          <string>TimeFold</string>
    <key>CFBundleIdentifier</key>           <string>$BUNDLE_ID</string>
    <key>CFBundleVersion</key>              <string>$VERSION</string>
    <key>CFBundleShortVersionString</key>   <string>$VERSION</string>
    <key>CFBundleExecutable</key>           <string>$APP_NAME</string>
    <key>CFBundleIconFile</key>             $ICON_REF
    <key>CFBundlePackageType</key>          <string>APPL</string>
    <key>CFBundleInfoDictionaryVersion</key><string>6.0</string>
    <key>LSMinimumSystemVersion</key>       <string>11.0</string>
    <key>NSHighResolutionCapable</key>      <true/>
    <key>LSApplicationCategoryType</key>    <string>public.app-category.utilities</string>
</dict>
</plist>
PLIST

echo "Bundle creado: $APP"