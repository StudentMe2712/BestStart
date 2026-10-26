#!/usr/bin/env bash
# ==============================================================================
# Choose a Vibe — Build & CI Validation Script (macOS / CI)
# ==============================================================================
# Выполняет генерацию Xcode-проекта через xcodegen, валидацию сборки
# и запуск набора Unit-тестов на симуляторе iPhone 16 Pro.

set -euo pipefail

# Цветовая палитра для терминального вывода
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
PURPLE='\033[0;35m'
CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m' # No Color

PROJECT_NAME="ChooseAVibe"
SCHEME_NAME="ChooseAVibe"
DESTINATION="platform=iOS Simulator,name=iPhone 16 Pro"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

START_TIME=$(date +%s)

print_header() {
    echo -e "${CYAN}========================================================================${NC}"
    echo -e "${BOLD}${PURPLE} ✨ Choose a Vibe: Скрипт сборки и валидации проекта${NC}"
    echo -e "${CYAN}========================================================================${NC}"
    echo -e " Рабочая директория: ${BLUE}${SCRIPT_DIR}${NC}"
    echo -e " Целевая схема:     ${BLUE}${SCHEME_NAME}${NC}"
    echo -e " Симулятор:         ${BLUE}${DESTINATION}${NC}"
    echo -e "${CYAN}------------------------------------------------------------------------${NC}"
}

print_step() {
    local step_num="$1"
    local step_title="$2"
    echo -e "\n${BOLD}${BLUE}[Шаг ${step_num}]${NC} ${YELLOW}${step_title}...${NC}"
}

print_success() {
    local message="$1"
    echo -e "${GREEN}  ✓ ${message}${NC}"
}

print_error() {
    local message="$1"
    echo -e "${RED}  ✗ ОШИБКА: ${message}${NC}" >&2
}

# Обработка аварийного завершения
cleanup_on_error() {
    local exit_code=$?
    if [ $exit_code -ne 0 ]; then
        echo -e "\n${RED}========================================================================${NC}"
        echo -e "${BOLD}${RED} ❌ Сборка завершилась с ошибкой (код возврата: ${exit_code})${NC}"
        echo -e "${RED}========================================================================${NC}"
    fi
}
trap cleanup_on_error EXIT

cd "${SCRIPT_DIR}"
print_header

# ------------------------------------------------------------------------------
# 1. Проверка окружения и утилит
# ------------------------------------------------------------------------------
print_step "1/4" "Проверка установленных инструментов разработки"

if ! command -v xcodegen &> /dev/null; then
    print_error "Утилита 'xcodegen' не найдена в PATH!"
    echo -e "  Установите через Homebrew: ${CYAN}brew install xcodegen${NC}"
    exit 1
fi
XCODEGEN_VER=$(xcodegen --version 2>&1 || echo "unknown")
print_success "xcodegen доступен (версия: ${XCODEGEN_VER})"

if ! command -v xcodebuild &> /dev/null; then
    print_error "Утилита 'xcodebuild' не найдена! Убедитесь, что Xcode установлен и выбран через xcode-select."
    exit 1
fi
XCODE_VER=$(xcodebuild -version | tr '\n' ' ' || echo "unknown")
print_success "xcodebuild доступен (${XCODE_VER})"

# ------------------------------------------------------------------------------
# 2. Генерация Xcode-проекта (xcodegen generate)
# ------------------------------------------------------------------------------
print_step "2/4" "Генерация проекта из project.yml (xcodegen generate)"

if [ ! -f "project.yml" ]; then
    print_error "Файл project.yml не найден в ${SCRIPT_DIR}"
    exit 1
fi

xcodegen generate
print_success "Проект ${PROJECT_NAME}.xcodeproj успешно сгенерирован"

# ------------------------------------------------------------------------------
# 3. Валидация сборки проекта (xcodebuild build)
# ------------------------------------------------------------------------------
print_step "3/4" "Сборка проекта ChooseAVibe для симулятора iPhone 16 Pro"

xcodebuild \
    -project "${PROJECT_NAME}.xcodeproj" \
    -scheme "${SCHEME_NAME}" \
    -destination "${DESTINATION}" \
    clean build \
    CODE_SIGNING_ALLOWED=NO \
    | xcpretty 2>/dev/null || xcodebuild \
        -project "${PROJECT_NAME}.xcodeproj" \
        -scheme "${SCHEME_NAME}" \
        -destination "${DESTINATION}" \
        clean build \
        CODE_SIGNING_ALLOWED=NO

print_success "Сборка приложения успешно завершена без ошибок"

# ------------------------------------------------------------------------------
# 4. Запуск набора тестов (xcodebuild test)
# ------------------------------------------------------------------------------
print_step "4/4" "Запуск Unit-тестов таргета ChooseAVibeTests"

xcodebuild test \
    -project "${PROJECT_NAME}.xcodeproj" \
    -scheme "${SCHEME_NAME}" \
    -destination "${DESTINATION}" \
    CODE_SIGNING_ALLOWED=NO \
    | xcpretty 2>/dev/null || xcodebuild test \
        -project "${PROJECT_NAME}.xcodeproj" \
        -scheme "${SCHEME_NAME}" \
        -destination "${DESTINATION}" \
        CODE_SIGNING_ALLOWED=NO

print_success "Все Unit-тесты успешно пройдены"

# ------------------------------------------------------------------------------
# Итоговый отчёт
# ------------------------------------------------------------------------------
END_TIME=$(date +%s)
DURATION=$((END_TIME - START_TIME))

echo -e "\n${GREEN}========================================================================${NC}"
echo -e "${BOLD}${GREEN} 🎉 ВСЕ ПРОВЕРКИ УСПЕШНО ПРОЙДЕНЫ ЗА ${DURATION} сек!${NC}"
echo -e "${GREEN}========================================================================${NC}"
echo -e " ✓ Генерация xcodegen: Успешно"
echo -e " ✓ Компиляция проекта: Успешно"
echo -e " ✓ Набор unit-тестов:  Успешно (ChooseAVibeTests)"
echo -e " Проект готов к созданию релиза и отправке в TestFlight / App Store."
echo -e "${GREEN}========================================================================${NC}"

exit 0
