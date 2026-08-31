#!/bin/bash
# build-mac.sh - macOS 发布脚本
# 用法: ./build-mac.sh [x64|arm64] [version] (默认 arm64/0.1.0)

set -e

ARCH="${1:-arm64}"
VERSION="${2:-0.1.0}"
BUILD_VERSION="${VERSION%%-*}"
PROJECT="src/Liangwengu/Liangwengu.fsproj"
CONFIG="Release"
OUTPUT_DIR="artifacts/macos-$ARCH"
APP_NAME="Liangwengu.app"
APP_DIR="$OUTPUT_DIR/$APP_NAME"
BUILT_APP="src/Liangwengu/bin/$CONFIG/net10.0-macos/osx-$ARCH/liangwengu.app"
ICON_SOURCE="src/Liangwengu/Assets/app-icon.png"
STAGING_DIR="$(mktemp -d)"
VERIFY_MOUNT_DIR="$STAGING_DIR/mounted-dmg"
DMG_MOUNTED=false

cleanup() {
    if [ "$DMG_MOUNTED" = true ]; then
        hdiutil detach "$VERIFY_MOUNT_DIR" -quiet || true
    fi
    rm -rf "$STAGING_DIR"
}
trap cleanup EXIT

# 验证 dotnet 是否可用
if ! command -v dotnet &> /dev/null; then
    echo -e "\033[31mError: dotnet is not installed or not in PATH\033[0m"
    exit 1
fi

# 验证项目文件是否存在
if [ ! -f "$PROJECT" ]; then
    echo -e "\033[31mError: Project file not found: $PROJECT\033[0m"
    exit 1
fi

# 验证架构参数
if [ "$ARCH" != "x64" ] && [ "$ARCH" != "arm64" ]; then
    echo -e "\033[31mError: Invalid architecture '$ARCH'. Supported: x64, arm64\033[0m"
    exit 1
fi

if [ ! -f "$ICON_SOURCE" ]; then
    echo -e "\033[31mError: App icon not found: $ICON_SOURCE\033[0m"
    exit 1
fi

for command_name in sips iconutil hdiutil ditto; do
    if ! command -v "$command_name" &> /dev/null; then
        echo -e "\033[31mError: $command_name is required on macOS\033[0m"
        exit 1
    fi
done

echo -e "\033[36mPublishing for macOS $ARCH...\033[0m"

rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

dotnet build "$PROJECT" \
    -c "$CONFIG" \
    -f net10.0-macos \
    -r "osx-$ARCH" \
    --self-contained true \
    -p:CreatePackage=false

if [ ! -d "$BUILT_APP" ]; then
    echo -e "\033[31mError: macOS app bundle not found: $BUILT_APP\033[0m"
    exit 1
fi

cp -R "$BUILT_APP" "$APP_DIR"
cp "$ICON_SOURCE" "$APP_DIR/Contents/Resources/app-icon.png"

ICONSET_DIR="$STAGING_DIR/Liangwengu.iconset"
mkdir -p "$ICONSET_DIR"
for size in 16 32 128 256 512; do
    sips -z "$size" "$size" "$ICON_SOURCE" --out "$ICONSET_DIR/icon_${size}x${size}.png" >/dev/null
    retina_size=$((size * 2))
    sips -z "$retina_size" "$retina_size" "$ICON_SOURCE" --out "$ICONSET_DIR/icon_${size}x${size}@2x.png" >/dev/null
done
iconutil -c icns "$ICONSET_DIR" -o "$APP_DIR/Contents/Resources/app-icon.icns"

# 验证 app bundle 的关键发布内容
if [ ! -f "$APP_DIR/Contents/Resources/app-icon.icns" ]; then
    echo -e "\033[31mError: app icon was not added to the bundle\033[0m"
    exit 1
fi

EXPECTED_ARCH="arm64"
if [ "$ARCH" = "x64" ]; then
    EXPECTED_ARCH="x86_64"
fi

if ! lipo -archs "$APP_DIR/Contents/MacOS/liangwengu" | tr ' ' '\n' | grep -Fxq "$EXPECTED_ARCH"; then
    echo -e "\033[31mError: app executable does not contain the expected $EXPECTED_ARCH architecture\033[0m"
    exit 1
fi

cat > "$APP_DIR/Contents/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDisplayName</key>
    <string>Liangwengu</string>
    <key>CFBundleExecutable</key>
    <string>liangwengu</string>
    <key>CFBundleIconFile</key>
    <string>app-icon</string>
    <key>CFBundleIdentifier</key>
    <string>com.liangwengu.app</string>
    <key>CFBundleName</key>
    <string>Liangwengu</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>$VERSION</string>
    <key>CFBundleVersion</key>
    <string>$BUILD_VERSION</string>
    <key>LSMinimumSystemVersion</key>
    <string>13.0</string>
    <key>LSUIElement</key>
    <true/>
</dict>
</plist>
EOF

codesign --deep --force --sign - "$APP_DIR" >/dev/null
codesign --verify --deep --strict --verbose=2 "$APP_DIR"

if [ "$(plutil -extract CFBundleIdentifier raw "$APP_DIR/Contents/Info.plist")" != "com.liangwengu.app" ]; then
    echo -e "\033[31mError: unexpected bundle identifier\033[0m"
    exit 1
fi

DMG_ROOT="$STAGING_DIR/dmg"
mkdir -p "$DMG_ROOT"
cp -R "$APP_DIR" "$DMG_ROOT/$APP_NAME"
ln -s /Applications "$DMG_ROOT/Applications"

DMG_PATH="$OUTPUT_DIR/liangwengu-$VERSION-macos-$ARCH.dmg"
hdiutil create \
    -size 256m \
    -volname "Liangwengu" \
    -srcfolder "$DMG_ROOT" \
    -ov \
    -format UDZO \
    "$DMG_PATH" >/dev/null

hdiutil verify "$DMG_PATH"
mkdir -p "$VERIFY_MOUNT_DIR"
hdiutil attach "$DMG_PATH" -readonly -nobrowse -mountpoint "$VERIFY_MOUNT_DIR" >/dev/null
DMG_MOUNTED=true

if [ ! -d "$VERIFY_MOUNT_DIR/$APP_NAME" ] || [ ! -L "$VERIFY_MOUNT_DIR/Applications" ]; then
    echo -e "\033[31mError: DMG does not contain the expected app and Applications link\033[0m"
    exit 1
fi

hdiutil detach "$VERIFY_MOUNT_DIR" -quiet
DMG_MOUNTED=false

echo -e "\033[32mPublish complete!\033[0m"
echo ""
echo "运行方式:"
echo "  open $APP_DIR"
echo "DMG:"
echo "  $DMG_PATH"
