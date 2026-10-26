import SwiftUI
import SwiftData

public struct ScratchCardContainer: View {
    @Environment(\.modelContext) private var modelContext
    @Bindable var session: AdventureSessionRecord
    public let adventure: AdventureDefinition
    public let profile: CoupleProfile
    public let onDismiss: () -> Void

    @State private var showingHint: Bool = false
    @State private var showingSummary: Bool = false
    @State private var newlyUnlockedAchievements: [AchievementDefinition] = []

    public init(
        session: AdventureSessionRecord,
        adventure: AdventureDefinition,
        profile: CoupleProfile,
        onDismiss: @escaping () -> Void
    ) {
        self.session = session
        self.adventure = adventure
        self.profile = profile
        self.onDismiss = onDismiss
    }

    private var currentCardIndex: Int {
        max(0, min(session.currentStepIndex - 1, adventure.cards.count - 1))
    }

    private var currentCard: ChallengeCardDefinition {
        adventure.cards[currentCardIndex]
    }

    private var activeVariant: ChallengeCardVariant {
        session.isUsingFallbackOnCurrentStep ? currentCard.fallback : currentCard.primary
    }

    private var currentPartnerTurn: String {
        session.currentStepIndex % 2 != 0 ? profile.partnerOneName : profile.partnerTwoName
    }

    public var body: some View {
        ZStack {
            AppTheme.midnightBg
                .ignoresSafeArea()

            VStack(spacing: 20) {
                // 1. Верхняя панель: Шаги и кнопка "Отложить"
                topHeaderBar
                    .padding(.horizontal, 20)
                    .padding(.top, 14)

                // 2. Баннер очереди хода
                turnBannerView
                    .padding(.horizontal, 20)

                // 3. Центральная карточка (Скретч-слой или открытое задание)
                cardContentView
                    .padding(.horizontal, 20)

                Spacer(minLength: 12)

                // 4. Панель действий внизу
                bottomActionButtons
                    .padding(.horizontal, 20)
                    .padding(.bottom, 20)
            }
        }
        .fullScreenCover(isPresented: $showingSummary) {
            AdventureSummaryView(
                session: session,
                adventure: adventure,
                profile: profile,
                unlockedAchievements: newlyUnlockedAchievements,
                onReturnToMap: {
                    showingSummary = false
                    onDismiss()
                }
            )
        }
    }

    // MARK: - 1. Верхняя панель
    private var topHeaderBar: some View {
        HStack {
            // Индикатор этапов
            HStack(spacing: 6) {
                ForEach(1...4, id: \.self) { step in
                    Circle()
                        .fill(stepIndicatorColor(step: step))
                        .frame(width: 10, height: 10)
                }

                Text("Этап \(session.currentStepIndex) из 4")
                    .font(.system(size: 13, weight: .bold))
                    .foregroundStyle(AppTheme.warmGold)
                    .padding(.leading, 4)
            }

            Spacer()

            // Кнопка "Отложить на потом"
            Button {
                AppTheme.hapticSoft()
                try? modelContext.save()
                onDismiss()
            } label: {
                HStack(spacing: 4) {
                    Image(systemName: "pause.fill")
                        .font(.system(size: 11))
                    Text("Отложить")
                        .font(.system(size: 13, weight: .semibold))
                }
                .foregroundStyle(AppTheme.softText)
                .padding(.horizontal, 12)
                .padding(.vertical, 7)
                .background(AppTheme.cardSurface)
                .clipShape(Capsule())
            }
        }
    }

    private func stepIndicatorColor(step: Int) -> Color {
        if step < session.currentStepIndex {
            return AppTheme.warmGold
        } else if step == session.currentStepIndex {
            return AppTheme.primaryRose
        } else {
            return Color.white.opacity(0.2)
        }
    }

    // MARK: - 2. Баннер очереди хода (Pass & Play)
    private var turnBannerView: some View {
        HStack(spacing: 12) {
            ZStack {
                Circle()
                    .fill(session.currentStepIndex % 2 != 0 ? AppTheme.warmGold.opacity(0.2) : AppTheme.primaryRose.opacity(0.2))
                    .frame(width: 38, height: 38)

                Image(systemName: "hand.tap.fill")
                    .font(.system(size: 16))
                    .foregroundStyle(session.currentStepIndex % 2 != 0 ? AppTheme.warmGold : AppTheme.primaryRose)
            }

            VStack(alignment: .leading, spacing: 2) {
                Text("Очередь: \(currentPartnerTurn)")
                    .font(.system(size: 14, weight: .bold))
                    .foregroundStyle(.white)

                Text(session.isCurrentCardScratched ? "Задание открыто — выполняйте вместе!" : "Стирай защитный слой пальцем!")
                    .font(.system(size: 12))
                    .foregroundStyle(AppTheme.softText)
            }

            Spacer()
        }
        .padding(.horizontal, 14)
        .padding(.vertical, 10)
        .background(AppTheme.cardSurface)
        .clipShape(RoundedRectangle(cornerRadius: 16))
    }

    // MARK: - 3. Центральная карточка
    private var cardContentView: some View {
        ZStack {
            // Открытое задание (нижний слой)
            revealedTaskView

            // Интерактивное защитное покрытие (если ещё не стёрто)
            if !session.isCurrentCardScratched {
                ScratchCanvasOverlay(
                    themeId: profile.selectedScratchThemeId,
                    stepIndex: session.currentStepIndex,
                    turnPartnerName: currentPartnerTurn,
                    onScratchedComplete: {
                        session.isCurrentCardScratched = true
                        try? modelContext.save()
                    }
                )
            }
        }
        .frame(maxHeight: 460)
    }

    // MARK: - Открытое задание
    private var revealedTaskView: some View {
        VStack(alignment: .leading, spacing: 16) {
            // Заголовок и бейдж времени
            HStack {
                VStack(alignment: .leading, spacing: 4) {
                    if session.isUsingFallbackOnCurrentStep {
                        Text("ДРУГОЙ ВАРИАНТ")
                            .font(.system(size: 11, weight: .bold))
                            .foregroundStyle(AppTheme.warmGold)
                    }

                    Text(activeVariant.title)
                        .font(.system(size: 22, weight: .bold, design: .rounded))
                        .foregroundStyle(.white)
                }

                Spacer()

                HStack(spacing: 4) {
                    Image(systemName: "clock")
                        .font(.system(size: 12))
                    Text("~\(activeVariant.estimatedMinutes) мин")
                        .font(.system(size: 12, weight: .bold))
                }
                .foregroundStyle(AppTheme.warmGold)
                .padding(.horizontal, 10)
                .padding(.vertical, 6)
                .background(AppTheme.warmGold.opacity(0.15))
                .clipShape(Capsule())
            }

            Divider()
                .background(Color.white.opacity(0.12))

            // Текст инструкции
            ScrollView(showsIndicators: false) {
                VStack(alignment: .leading, spacing: 14) {
                    Text(activeVariant.instruction)
                        .font(.system(size: 17))
                        .foregroundStyle(.white)
                        .lineSpacing(5)

                    // Раскрывающийся блок "Подсказка / Идея"
                    if let hint = activeVariant.hint {
                        DisclosureGroup(
                            isExpanded: $showingHint,
                            content: {
                                Text(hint)
                                    .font(.system(size: 14))
                                    .foregroundStyle(AppTheme.softText)
                                    .lineSpacing(3)
                                    .padding(.top, 8)
                            },
                            label: {
                                Label("Подсказка / Идея", systemImage: "lightbulb.fill")
                                    .font(.system(size: 14, weight: .semibold))
                                    .foregroundStyle(AppTheme.warmGold)
                            }
                        )
                        .padding(12)
                        .background(Color.white.opacity(0.05))
                        .clipShape(RoundedRectangle(cornerRadius: 12))
                    }
                }
            }

            Spacer()
        }
        .padding(22)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
        .background(AppTheme.cardSurface)
        .clipShape(RoundedRectangle(cornerRadius: 24))
        .overlay(
            RoundedRectangle(cornerRadius: 24)
                .stroke(Color.white.opacity(0.12), lineWidth: 1.5)
        )
    }

    // MARK: - 4. Кнопки действий
    private var bottomActionButtons: some View {
        VStack(spacing: 12) {
            // Кнопка: "Мы сделали это! (+10 XP)"
            Button {
                completeCurrentStep()
            } label: {
                HStack(spacing: 8) {
                    Image(systemName: "checkmark.circle.fill")
                        .font(.system(size: 18))

                    Text("Мы сделали это! (+10 XP)")
                        .font(.system(size: 16, weight: .bold))
                }
                .foregroundStyle(.white)
                .frame(maxWidth: .infinity)
                .frame(height: 54)
                .background(session.isCurrentCardScratched ? AppTheme.heroGradient : LinearGradient(colors: [Color.gray.opacity(0.5), Color.gray.opacity(0.5)], startPoint: .leading, endPoint: .trailing))
                .clipShape(RoundedRectangle(cornerRadius: 16))
                .shadow(color: session.isCurrentCardScratched ? AppTheme.primaryRose.opacity(0.4) : Color.clear, radius: 10, y: 5)
            }
            .disabled(!session.isCurrentCardScratched)

            // Вторичные кнопки: "Другой вариант" и "Пропустить этап"
            HStack(spacing: 12) {
                Button {
                    AppTheme.hapticSoft()
                    withAnimation {
                        session.isUsingFallbackOnCurrentStep.toggle()
                        try? modelContext.save()
                    }
                } label: {
                    HStack(spacing: 6) {
                        Image(systemName: "arrow.2.squarepath")
                            .font(.system(size: 13))
                        Text(session.isUsingFallbackOnCurrentStep ? "Основной вариант" : "Другой вариант")
                            .font(.system(size: 13, weight: .semibold))
                    }
                    .foregroundStyle(AppTheme.warmGold)
                    .frame(maxWidth: .infinity)
                    .frame(height: 44)
                    .background(AppTheme.cardSurface)
                    .clipShape(RoundedRectangle(cornerRadius: 12))
                }
                .disabled(!session.isCurrentCardScratched)

                Button {
                    skipCurrentStep()
                } label: {
                    Text("Пропустить этап")
                        .font(.system(size: 13, weight: .semibold))
                        .foregroundStyle(AppTheme.softText)
                        .frame(maxWidth: .infinity)
                        .frame(height: 44)
                        .background(AppTheme.cardSurface)
                        .clipShape(RoundedRectangle(cornerRadius: 12))
                }
                .disabled(!session.isCurrentCardScratched)
            }
        }
    }

    // MARK: - Завершение и пропуск этапа
    private func completeCurrentStep() {
        AppTheme.hapticSuccess()
        session.markCurrentStep(completed: true, xp: 10)
        profile.totalXP += 10
        profile.updatedAt = .now
        advanceOrFinish()
    }

    private func skipCurrentStep() {
        AppTheme.hapticMedium()
        session.markCurrentStep(completed: false, xp: 0)
        advanceOrFinish()
    }

    private func advanceOrFinish() {
        if session.currentStepIndex < 4 {
            withAnimation {
                session.currentStepIndex += 1
                session.isCurrentCardScratched = false
                session.isUsingFallbackOnCurrentStep = false
                showingHint = false
            }
            try? modelContext.save()
        } else {
            finishSession()
        }
    }

    private func finishSession() {
        // Награда за финал +25 XP
        session.earnedXP += 25
        session.isFinished = true
        session.completedAt = .now
        profile.totalXP += 25
        profile.updatedAt = .now

        // Проверка достижений
        let allCompleted = (try? modelContext.fetch(FetchDescriptor<AdventureSessionRecord>())) ?? []
        let newAchievements = ProgressionEngine.evaluateAndAwardAchievements(
            profile: profile,
            currentSession: session,
            currentAdventure: adventure,
            allAdventures: CatalogRepository.shared.adventures,
            pastFinishedSessions: allCompleted
        )

        newlyUnlockedAchievements = newAchievements
        try? modelContext.save()

        DispatchQueue.main.asyncAfter(deadline: .now() + 0.3) {
            showingSummary = true
        }
    }
}
