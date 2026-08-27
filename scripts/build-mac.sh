#!/bin/bash
# build-mac.sh - macOS 发布脚本
# 用法: ./build-mac.sh [x64|arm64] (默认 arm64)

set -e

ARCH="${1:-arm64}"
PROJECT="src/Liangwengu/Liangwengu.fsproj"
CONFIG="Release"
OUTPUT_DIR="artifacts/macos-$ARCH"

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

echo -e "\033[36mPublishing for macOS $ARCH...\033[0m"

dotnet publish "$PROJECT" \
    -c "$CONFIG" \
    -f net10.0 \
    -r "osx-$ARCH" \
    --self-contained true \
    -o "$OUTPUT_DIR"

echo -e "\033[32mPublish complete!\033[0m"
echo ""
echo "运行方式:"
echo "  ./$OUTPUT_DIR/liangwengu"
