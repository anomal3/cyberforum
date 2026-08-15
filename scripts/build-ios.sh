#!/usr/bin/env bash
set -euo pipefail

# Сборка ipa для iOS (App Store / TestFlight).
#   ./scripts/build-ios.sh
# Подпись и профиль можно переопределить переменными окружения:
#   CODESIGN_KEY="Apple Distribution: ..." CODESIGN_PROVISION="..." ./scripts/build-ios.sh

cd "$(dirname "$0")/.."

CODESIGN_KEY="${CODESIGN_KEY:-Apple Distribution: Roman Koscheev (W9Z673792L)}"
CODESIGN_PROVISION="${CODESIGN_PROVISION:-CyberForum AppStore}"
CONFIG="${CONFIG:-Release}"
PROJECT="src/CyberForum.App/CyberForum.App.csproj"

PROFILES_DIR="$HOME/Library/MobileDevice/Provisioning Profiles"
if ! grep -rls "<string>$CODESIGN_PROVISION</string>" "$PROFILES_DIR" >/dev/null 2>&1; then
    echo "Ошибка: provisioning profile «$CODESIGN_PROVISION» не установлен." >&2
    echo "Скачайте его с developer.apple.com → Profiles и положите в:" >&2
    echo "  $PROFILES_DIR" >&2
    exit 1
fi

dotnet publish "$PROJECT" \
    -f net10.0-ios \
    -c "$CONFIG" \
    -p:RuntimeIdentifier=ios-arm64 \
    -p:ArchiveOnBuild=true \
    -p:CodesignKey="$CODESIGN_KEY" \
    -p:CodesignProvision="$CODESIGN_PROVISION"

IPA=$(ls -t "src/CyberForum.App/bin/$CONFIG/net10.0-ios/ios-arm64/publish/"*.ipa | head -1)
echo
echo "Готово: $IPA"
