# just 命令运行器: https://just.systems
# 安装: brew install just (macOS) / winget install Casey.Just (Windows)
# 用法: just --list 查看全部命令; 同名 recipe 通过 [macos]/[windows] 注解按当前平台自动选择

main_proj := "src/Liangwengu/Liangwengu.fsproj"
test_proj := "tests/Liangwengu.Tests/Liangwengu.Tests.fsproj"
mac_tfm := "net10.0-macos"
win_tfm := "net10.0-windows10.0.17763.0"
win_rid := "win-x64"
default_version := "0.1.0"

# 按当前机器 CPU 自动推导 mac 参数 (Apple Silicon -> arm64/osx-arm64; Intel -> x64/osx-x64)
mac_arch := if arch() == "aarch64" { "arm64" } else { "x64" }
mac_rid := if arch() == "aarch64" { "osx-arm64" } else { "osx-x64" }

# 列出全部命令
default:
    @just --list

# 启动 app (macOS)
[macos]
run:
    dotnet run --project {{main_proj}} -f {{mac_tfm}}

# 启动 app (Windows)
[windows]
run:
    dotnet run --project {{main_proj}} -f {{win_tfm}}

# 通用测试 (跨平台通用 target)
test:
    dotnet test {{test_proj}} -f net10.0

# 平台相关测试 (macOS, RID 按当前 CPU 自动推导, 与 CI 一致)
[macos]
test-platform:
    dotnet test {{test_proj}} -f {{mac_tfm}} -r {{mac_rid}}

# 平台相关测试 (Windows, 与 CI 一致)
[windows]
test-platform:
    dotnet test {{test_proj}} -f {{win_tfm}} -r {{win_rid}}

# Fantomas 格式化全部 F# 代码
format:
    dotnet tool restore
    dotnet fantomas src tests

# 检查格式 (与 CI 的 fantomas --check 一致)
format-check:
    dotnet tool restore
    dotnet fantomas src tests --check

# 系统通知 smoke test (macOS; 可覆盖 RID: just smoke-test osx-x64)
[macos]
smoke-test rid=mac_rid:
    dotnet run --project {{main_proj}} -f {{mac_tfm}} -r {{rid}} -- --notification-smoke-test

# 系统通知 smoke test (Windows)
[windows]
smoke-test:
    dotnet run --project {{main_proj}} -f {{win_tfm}} -r {{win_rid}} -- --notification-smoke-test

# 打 macOS dmg (arch/version 默认按当前 CPU 和 0.1.0; 示例: just publish-macos x64 1.0.0)
[macos]
publish-macos arch=mac_arch version=default_version:
    #!/bin/bash
    set -e

    ARCH="{{arch}}"
    VERSION="{{version}}"
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

    err() { echo -e "\033[31mError: $1\033[0m" >&2; exit 1; }
    info() { echo -e "\033[36m$1\033[0m"; }

    cleanup() {
        if [ "$DMG_MOUNTED" = true ]; then
            hdiutil detach "$VERIFY_MOUNT_DIR" -quiet || true
        fi
        rm -rf "$STAGING_DIR"
    }
    trap cleanup EXIT

    command -v dotnet >/dev/null 2>&1 || err "dotnet is not installed or not in PATH"
    [ -f "$PROJECT" ] || err "Project file not found: $PROJECT"
    [ "$ARCH" = "x64" ] || [ "$ARCH" = "arm64" ] || err "Invalid architecture '$ARCH'. Supported: x64, arm64"
    [ -f "$ICON_SOURCE" ] || err "App icon not found: $ICON_SOURCE"
    for cmd in sips iconutil hdiutil ditto; do
        command -v "$cmd" >/dev/null 2>&1 || err "$cmd is required on macOS"
    done

    info "Publishing for macOS $ARCH..."

    rm -rf "$OUTPUT_DIR"
    mkdir -p "$OUTPUT_DIR"

    dotnet build "$PROJECT" \
        -c "$CONFIG" \
        -f net10.0-macos \
        -r "osx-$ARCH" \
        --self-contained true \
        -p:CreatePackage=false

    [ -d "$BUILT_APP" ] || err "macOS app bundle not found: $BUILT_APP"

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

    [ -f "$APP_DIR/Contents/Resources/app-icon.icns" ] || err "app icon was not added to the bundle"

    EXPECTED_ARCH="arm64"
    if [ "$ARCH" = "x64" ]; then
        EXPECTED_ARCH="x86_64"
    fi
    lipo -archs "$APP_DIR/Contents/MacOS/liangwengu" | tr ' ' '\n' | grep -Fxq "$EXPECTED_ARCH" \
        || err "app executable does not contain the expected $EXPECTED_ARCH architecture"

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

    [ "$(plutil -extract CFBundleIdentifier raw "$APP_DIR/Contents/Info.plist")" = "com.liangwengu.app" ] \
        || err "unexpected bundle identifier"

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

    [ -d "$VERIFY_MOUNT_DIR/$APP_NAME" ] && [ -L "$VERIFY_MOUNT_DIR/Applications" ] \
        || err "DMG does not contain the expected app and Applications link"

    hdiutil detach "$VERIFY_MOUNT_DIR" -quiet
    DMG_MOUNTED=false

    echo -e "\033[32mPublish complete!\033[0m"
    echo ""
    echo "运行方式:"
    echo "  open $APP_DIR"
    echo "DMG:"
    echo "  $DMG_PATH"

# 打 Windows 单文件 exe (version 默认取 fsproj 的 0.1.0; 示例: just publish-windows 1.2.3)
[windows]
publish-windows version=default_version:
    #!/usr/bin/env pwsh
    $project = "src/Liangwengu/Liangwengu.fsproj"
    $outputDir = "artifacts/windows-x64"
    $version = "{{version}}"

    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        Write-Error "dotnet is not installed or not in PATH"
        exit 1
    }
    if (-not (Test-Path $project)) {
        Write-Error "Project file not found: $project"
        exit 1
    }

    Write-Host "Publishing for Windows x64 (version: $version)..." -ForegroundColor Cyan

    if (Test-Path -LiteralPath $outputDir) {
        Remove-Item -LiteralPath $outputDir -Recurse -Force
    }

    dotnet restore $project -r win-x64 -p:TargetFramework=net10.0-windows10.0.17763.0
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet publish $project -c Release -f net10.0-windows10.0.17763.0 -r win-x64 --self-contained true --no-restore -p:Version=$version -o $outputDir
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $exes = @(Get-ChildItem -LiteralPath $outputDir -Filter *.exe -File)
    if ($exes.Count -ne 1 -or $exes[0].Name -ne "liangwengu.exe") {
        Write-Error "publish directory must contain only liangwengu.exe, found: $($exes.Name -join ', ')"
        exit 1
    }
    $leftover = @(Get-ChildItem -LiteralPath $outputDir -File | Where-Object { $_.Extension -notin @('.exe', '.pdb') })
    if ($leftover.Count -gt 0) {
        Write-Error "publish directory has unexpected files: $($leftover.Name -join ', ')"
        exit 1
    }

    Write-Host "Publish complete!" -ForegroundColor Green
    Write-Host "运行方式: .\$outputDir\liangwengu.exe"
