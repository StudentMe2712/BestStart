//
//  WaitingTurnOverlayView.swift
//  ChooseAVibe
//
//  Экран ожидания хода для второго устройства в режиме «Два iPhone» (v2.0).
//  Отображает живую пульсирующую ауру, фоновые мерцающие искры,
//  кольцо прогресса стирания карточки партнёром в реальном времени,
//  виброотклик при открытии и секретную подсказку без спойлеров к тексту задания.
//

import SwiftUI

/// Экран ожидания хода партнёра для режима «Два iPhone»
public struct WaitingTurnOverlayView: View {

    // MARK: - Внедрённые зависимости

    /// Сервис P2P-синхронизации сессии
    @ObservedObject public var syncService: PeerSyncService

    /// Имя ведущего партнёра, который сейчас совершает ход (если не задано, берётся из сервиса)
    public let customPartnerName: String?

    /// Номер текущего этапа квеста (1...4)
    public let stepIndex: Int?

    /// Секретный совет / подсказка для подготовки (без раскрытия сути задания)
    public let secretAdvice: String?

    /// Настроение приключения для адаптации градиентов
    public let vibeTag: VibeTag?

    /// Обратный вызов закрытия или выхода из режима ожидания
    public let onDismiss: (() -> Void)?

    // MARK: - Локальное состояние анимаций

    /// Флаг раскрытия секретной подсказки
    @State private var showSecretHint: Bool = false

    /// Состояние пульсации фоновой ауры
    @State private var isPulsingAura: Bool = false

    /// Фаза мерцания фоновых искр
    @State private var sparklesPhase: Bool = false

    /// Предыдущее значение открытия для вызова виброотклика
    @State private var previousCardRevealed: Bool = false

    // MARK: - Инициализатор

    public init(
        syncService: PeerSyncService = .shared,
        customPartnerName: String? = nil,
        stepIndex: Int? = nil,
        secretAdvice: String? = nil,
        vibeTag: VibeTag? = nil,
        onDismiss: (() -> Void)? = nil
    ) {
        self.syncService = syncService
        self.customPartnerName = customPartnerName
        self.stepIndex = stepIndex
        self.secretAdvice = secretAdvice
        self.vibeTag = vibeTag
        self.onDismiss = onDismiss
    }

    // MARK: - Вычисляемые свойства

    /// Имя партнёра, выполняющего текущий ход
    private var activePartner: String {
        if let custom = customPartnerName, !custom.isEmpty {
            return custom
        }
        return syncService.activePartnerName
    }

    /// Текущий отображаемый шаг
    private var currentStep: Int {
        stepIndex ?? syncService.currentStepIndex
    }

    /// Процент стирания защитного слоя (0.0 ... 1.0)
    private var currentProgress: Double {
        syncService.scratchProgress
    }

    /// Флаг полного снятия защитного слоя партнёром
    private var isRevealed: Bool {
        syncService.isPartnerCardRevealed || currentProgress >= 0.45
    }

    /// Текст подготовленной секретной подсказки по умолчанию
    private var effectiveSecretAdvice: String {
        if let advice = secretAdvice, !advice.isEmpty {
            return advice
        }
        return "Пока партнёр стирает карточку, подготовьте атмосферу: включите тёплый свет, подготовьте напитки и сохраняйте предвкушение. Сама суть задания останется тайной до окончания стирания!"
    }

    /// Акцентный градиент для элементов интерфейса
    private var primaryGradient: LinearGradient {
        if let vibe = vibeTag {
            return AppTheme.palette(for: vibe).accentGradient
        }
        return AppTheme.Gradients.brandPrimary
    }

    // MARK: - Основное тело экрана

    public var body: some View {
        ZStack {
            // 1. Тёмный полуночный фон
            AppTheme.midnightBg
                .ignoresSafeArea()

            // 2. Атмосферная пульсирующая аура
            pulsingAuraBackground
                .ignoresSafeArea()

            // 3. Фоновые мерцающие искры
            ambientSparklesField
                .ignoresSafeArea()

            // 4. Основное содержимое экрана
            VStack(spacing: 24) {
                // Верхний информационный бейдж
                headerStatusBar
                    .padding(.top, 16)
                    .padding(.horizontal, 24)

                Spacer(minLength: 10)

                // Заголовок текущего хода
                turnHeaderSection
                    .padding(.horizontal, 24)

                // Центральное кольцо живого прогресса стирания
                scratchProgressRingSection
                    .padding(.vertical, 8)

                // Статус стирания в реальном времени
                progressStatusBadge
                    .padding(.horizontal, 24)

                Spacer(minLength: 10)

                // Карточка секретной подсказки (без спойлеров к тексту задания)
                secretHintCard
                    .padding(.horizontal, 24)

                // Нижняя панель сетевого статуса
                bottomConnectionBar
                    .padding(.horizontal, 24)
                    .padding(.bottom, 20)
            }
        }
        .onAppear {
            startAmbientAnimations()
            previousCardRevealed = isRevealed
        }
        .onChange(of: syncService.isPartnerCardRevealed) { _, revealed in
            if revealed && !previousCardRevealed {
                previousCardRevealed = true
                HapticFeedback.success()
            }
        }
        .onChange(of: syncService.scratchProgress) { _, newProgress in
            if newProgress >= 0.45 && !previousCardRevealed {
                previousCardRevealed = true
                HapticFeedback.success()
            }
        }
    }

    // MARK: - Фоновая пульсирующая аура

    private var pulsingAuraBackground: some View {
        ZStack {
            Circle()
                .fill(
                    RadialGradient(
                        colors: [
                            AppTheme.primaryRose.opacity(0.28),
                            AppTheme.spontaneous.primary.opacity(0.12),
                            Color.clear
                        ],
                        center: .center,
                        startRadius: 10,
                        endRadius: 220
                    )
                )
                .frame(width: 380, height: 380)
                .blur(radius: 65)
                .scaleEffect(isPulsingAura ? 1.22 : 0.88)
                .offset(y: -40)

            Circle()
                .fill(
                    RadialGradient(
                        colors: [
                            AppTheme.warmGold.opacity(0.18),
                            Color.clear
                        ],
                        center: .center,
                        startRadius: 5,
                        endRadius: 180
                    )
                )
                .frame(width: 320, height: 320)
                .blur(radius: 50)
                .scaleEffect(isPulsingAura ? 0.92 : 1.18)
                .offset(y: 80)
        }
    }

    // MARK: - Фоновые мерцающие искры

    private var ambientSparklesField: some View {
        ZStack {
            ForEach(0..<12, id: \.self) { index in
                sparkleParticle(index: index)
            }
        }
    }

    @ViewBuilder
    private func sparkleParticle(index: Int) -> some View {
        let xOffset = CGFloat((index * 67 + 31) % 320) - 160
        let yOffset = CGFloat((index * 89 + 17) % 560) - 280
        let size = CGFloat(10 + (index % 4) * 4)
        let isGold = index % 3 == 0

        Image(systemName: "sparkle")
            .font(.system(size: size))
            .foregroundColor(isGold ? AppTheme.warmGold : AppTheme.primaryRose)
            .opacity(sparklesPhase ? (index.isMultiple(of: 2) ? 0.75 : 0.20) : (index.isMultiple(of: 2) ? 0.20 : 0.85))
            .scaleEffect(sparklesPhase ? 1.15 : 0.85)
            .offset(x: xOffset, y: yOffset)
            .animation(
                .easeInOut(duration: Double(2.0 + Double(index % 3) * 0.7))
                .repeatForever(autoreverses: true),
                value: sparklesPhase
            )
    }

    // MARK: - Верхний информационный бейдж

    private var headerStatusBar: some View {
        HStack {
            HStack(spacing: 8) {
                Circle()
                    .fill(Color.green)
                    .frame(width: 8, height: 8)
                    .vibeGlow(color: Color.green, radius: 6)

                Text("РЕЖИМ ДВА IPHONE")
                    .font(.system(size: 11, weight: .bold, design: .rounded))
                    .foregroundColor(.white.opacity(0.85))
                    .tracking(1.2)
            }
            .padding(.horizontal, 12)
            .padding(.vertical, 6)
            .background(Color.white.opacity(0.08))
            .clipShape(Capsule())

            Spacer()

            Text("Этап \(currentStep) из 4")
                .font(.system(size: 13, weight: .semibold, design: .rounded))
                .foregroundColor(AppTheme.softText)
                .padding(.horizontal, 12)
                .padding(.vertical, 6)
                .background(Color.white.opacity(0.05))
                .clipShape(Capsule())

            if let onDismiss = onDismiss {
                Button(action: onDismiss) {
                    Image(systemName: "xmark.circle.fill")
                        .font(.system(size: 20))
                        .foregroundColor(AppTheme.softText)
                }
            }
        }
    }

    // MARK: - Заголовок текущего хода

    private var turnHeaderSection: some View {
        VStack(spacing: 8) {
            HStack(spacing: 8) {
                Image(systemName: isRevealed ? "sparkles" : "hand.draw.fill")
                    .font(.system(size: 18))
                    .foregroundColor(isRevealed ? AppTheme.warmGold : AppTheme.primaryRose)

                Text("Ход \(activePartner)")
                    .font(.system(size: 24, weight: .bold, design: .rounded))
                    .foregroundColor(.white)
            }

            Text(isRevealed ? "Партнёр стёр карточку! Слушайте задание..." : "Партнёр стирает карточку...")
                .font(.system(size: 15, weight: .medium, design: .rounded))
                .foregroundColor(AppTheme.softText)
                .multilineTextAlignment(.center)
        }
    }

    // MARK: - Центральное кольцо живого прогресса стирания

    private var scratchProgressRingSection: some View {
        ZStack {
            // Фоновая подложка кольца
            Circle()
                .stroke(Color.white.opacity(0.08), lineWidth: 14)
                .frame(width: 190, height: 190)

            // Активная линия прогресса стирания с градиентом
            Circle()
                .trim(from: 0.0, to: CGFloat(min(1.0, currentProgress)))
                .stroke(
                    primaryGradient,
                    style: StrokeStyle(lineWidth: 14, lineCap: .round)
                )
                .frame(width: 190, height: 190)
                .rotationEffect(.degrees(-90))
                .animation(.spring(response: 0.35, dampingFraction: 0.75), value: currentProgress)
                .shadow(
                    color: AppTheme.primaryRose.opacity(currentProgress > 0.1 ? 0.45 : 0.0),
                    radius: 12
                )

            // Центральный блок с процентами и иконкой
            VStack(spacing: 4) {
                Image(systemName: isRevealed ? "checkmark.circle.fill" : "wand.and.stars")
                    .font(.system(size: 28))
                    .foregroundColor(isRevealed ? AppTheme.warmGold : AppTheme.primaryRose)
                    .scaleEffect(isRevealed ? 1.15 : 1.0)
                    .animation(.spring(response: 0.4, dampingFraction: 0.6), value: isRevealed)

                Text("\(Int(currentProgress * 100))%")
                    .font(.system(size: 40, weight: .heavy, design: .rounded))
                    .foregroundColor(.white)

                Text(isRevealed ? "Открыто!" : "Стирание")
                    .font(.system(size: 12, weight: .semibold, design: .rounded))
                    .foregroundColor(AppTheme.softText)
                    .textCase(.uppercase)
                    .tracking(0.8)
            }
        }
    }

    // MARK: - Статус стирания в реальном времени

    private var progressStatusBadge: some View {
        HStack(spacing: 8) {
            Image(systemName: isRevealed ? "lock.open.fill" : "hourglass")
                .font(.system(size: 13, weight: .semibold))
                .foregroundColor(isRevealed ? AppTheme.warmGold : AppTheme.softText)

            Text(statusMessage)
                .font(.system(size: 13, weight: .medium, design: .rounded))
                .foregroundColor(.white.opacity(0.90))
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 8)
        .background(
            RoundedRectangle(cornerRadius: AppTheme.CornerRadius.chip, style: .continuous)
                .fill(isRevealed ? AppTheme.warmGold.opacity(0.18) : Color.white.opacity(0.06))
        )
    }

    private var statusMessage: String {
        if isRevealed {
            return "Защитный слой снят! Карточка готова к чтению."
        } else if currentProgress >= 0.20 {
            return "Стирает защитный слой... Ещё немного!"
        } else if currentProgress > 0.02 {
            return "Партнёр только прикоснулся к карточке..."
        } else {
            return "Ожидание начала стирания..."
        }
    }

    // MARK: - Карточка секретной подсказки («Секретная подсказка»)

    private var secretHintCard: some View {
        VStack(alignment: .leading, spacing: 12) {
            // Переключатель раскрытия подсказки
            Toggle(isOn: $showSecretHint.animation(.spring(response: 0.35, dampingFraction: 0.8))) {
                HStack(spacing: 10) {
                    ZStack {
                        Circle()
                            .fill(AppTheme.warmGold.opacity(0.20))
                            .frame(width: 32, height: 32)

                        Image(systemName: "lightbulb.fill")
                            .font(.system(size: 16))
                            .foregroundColor(AppTheme.warmGold)
                    }

                    VStack(alignment: .leading, spacing: 2) {
                        Text("Секретная подсказка")
                            .font(.system(size: 15, weight: .bold, design: .rounded))
                            .foregroundColor(.white)

                        Text("Совет для подготовки без спойлеров")
                            .font(.system(size: 12, weight: .regular, design: .rounded))
                            .foregroundColor(AppTheme.softText)
                    }
                }
            }
            .tint(AppTheme.primaryRose)

            // Раскрывающийся контент подсказки
            if showSecretHint {
                VStack(alignment: .leading, spacing: 10) {
                    Divider()
                        .background(Color.white.opacity(0.12))

                    HStack(alignment: .top, spacing: 8) {
                        Image(systemName: "sparkle")
                            .font(.system(size: 12))
                            .foregroundColor(AppTheme.warmGold)
                            .padding(.top, 2)

                        Text(effectiveSecretAdvice)
                            .font(.system(size: 14, weight: .medium, design: .rounded))
                            .foregroundColor(.white.opacity(0.92))
                            .lineSpacing(3)
                    }

                    HStack(spacing: 6) {
                        Image(systemName: "lock.shield.fill")
                            .font(.system(size: 11))
                            .foregroundColor(AppTheme.softText)

                        Text("Текст задания скрыт, чтобы сохранить эффект сюрприза")
                            .font(.system(size: 11, weight: .medium, design: .rounded))
                            .foregroundColor(AppTheme.softText)
                    }
                    .padding(.top, 4)
                }
                .transition(.opacity.combined(with: .move(edge: .top)))
            }
        }
        .padding(16)
        .background(
            RoundedRectangle(cornerRadius: AppTheme.CornerRadius.card, style: .continuous)
                .fill(Color(red: 0.12, green: 0.12, blue: 0.20))
                .overlay(
                    RoundedRectangle(cornerRadius: AppTheme.CornerRadius.card, style: .continuous)
                        .stroke(Color.white.opacity(0.09), lineWidth: 1)
                )
        )
    }

    // MARK: - Нижняя панель сетевого статуса

    private var bottomConnectionBar: some View {
        HStack(spacing: 8) {
            Image(systemName: "antenna.radiowaves.left.and.right")
                .font(.system(size: 12))
                .foregroundColor(AppTheme.softText)

            Text("Wi-Fi & Bluetooth P2P связь активна")
                .font(.system(size: 12, weight: .medium, design: .rounded))
                .foregroundColor(AppTheme.softText)

            Spacer()

            if let peer = syncService.connectedPeerName {
                Text(peer)
                    .font(.system(size: 12, weight: .bold, design: .rounded))
                    .foregroundColor(.white.opacity(0.85))
            }
        }
    }

    // MARK: - Запуск фоновых анимаций

    private func startAmbientAnimations() {
        withAnimation(
            .easeInOut(duration: 2.6)
            .repeatForever(autoreverses: true)
        ) {
            isPulsingAura = true
        }

        withAnimation(
            .easeInOut(duration: 2.2)
            .repeatForever(autoreverses: true)
        ) {
            sparklesPhase = true
        }
    }
}

// MARK: - Предпросмотр SwiftUI

#Preview("Экран ожидания хода") {
    WaitingTurnOverlayView(
        syncService: {
            let service = PeerSyncService(partnerOneName: "Алиса", partnerTwoName: "Максим", localRole: .partner2)
            service.simulateIncomingPacketForTesting(.scratchProgress(percent: 0.28))
            return service
        }(),
        customPartnerName: "Алиса",
        stepIndex: 1,
        secretAdvice: "Зажгите любимую свечу с ароматом ванили и наполните бокалы. Не подглядывайте в экран партнёра!",
        vibeTag: .romantic
    )
}

