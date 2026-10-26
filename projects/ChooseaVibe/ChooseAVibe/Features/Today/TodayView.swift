import SwiftUI
import SwiftData

/// Главный экран вкладки «Сегодня» (Apple HIG iOS 18)
/// Содержит профиль пары, героический виджет старта/продолжения вечера,
/// быстрый выбор настроения, интерактивный гид и вызов виброотклика.
public struct TodayView: View {
    @Environment(\.modelContext) private var modelContext
    @Query private var profiles: [CoupleProfile]
    @Query(sort: \AdventureSessionRecord.startedAt, order: .reverse) private var sessions: [AdventureSessionRecord]

    @State private var showingWalkthrough: Bool = false
    @State private var showingVibePicker: Bool = false
    @State private var preselectedVibe: VibeTag? = nil
    @State private var selectedAdventureForBriefing: AdventureDefinition?
    @State private var activeSessionToResume: AdventureSessionRecord?

    private let catalog = CatalogRepository.shared
    private let engine = ProgressionEngine.shared

    public init() {}

    private var profile: CoupleProfile {
        profiles.first ?? CoupleProfile(
            partnerOneName: "Алишер",
            partnerTwoName: "Дана"
        )
    }

    private var activeSession: AdventureSessionRecord? {
        sessions.first(where: { !$0.isFinished })
    }

    private var completedAdventureIds: Set<String> {
        Set(sessions.filter { $0.isFinished }.map { $0.adventureId })
    }

    public var body: some View {
        NavigationStack {
            ZStack {
                AppTheme.midnightBg
                    .ignoresSafeArea()

                ScrollView(showsIndicators: false) {
                    VStack(spacing: 24) {
                        // 1. Шапка профиля пары с кнопкой справки "?"
                        coupleProfileHeaderView
                            .padding(.horizontal, 20)
                            .padding(.top, 12)

                        // 2. Главный герой-виджет (активная сессия или витрина старта с 3 веерными картами)
                        if let ongoing = activeSession, let adv = catalog.adventure(by: ongoing.adventureId) {
                            activeSessionHeroCard(session: ongoing, adventure: adv)
                                .padding(.horizontal, 20)
                        } else {
                            newEveningHeroCard
                                .padding(.horizontal, 20)
                        }

                        // 3. Быстрый выбор настроения (5 карточек)
                        quickMoodSection
                            .padding(.top, 4)

                        // 4. Баннер интерактивного гида
                        interactiveGuideBanner
                            .padding(.horizontal, 20)
                            .padding(.bottom, 24)
                    }
                }
            }
            .navigationTitle("Сегодня")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button {
                        AppTheme.hapticLight()
                        showingWalkthrough = true
                    } label: {
                        Image(systemName: "questionmark.circle")
                            .font(.system(size: 18, weight: .semibold))
                            .foregroundStyle(AppTheme.softText)
                    }
                }
            }
        }
        // Лист интерактивного гида
        .sheet(isPresented: $showingWalkthrough) {
            InteractiveWalkthroughSheet()
        }
        // Лист подбора вайба
        .sheet(isPresented: $showingVibePicker) {
            VibePickerSheet(
                profile: profile,
                completedAdventureIds: completedAdventureIds,
                initialVibe: preselectedVibe,
                onAdventureSelected: { adv in
                    showingVibePicker = false
                    DispatchQueue.main.asyncAfter(deadline: .now() + 0.3) {
                        selectedAdventureForBriefing = adv
                    }
                }
            )
        }
        // Лист брифинга конверта
        .sheet(item: $selectedAdventureForBriefing) { adv in
            AdventureBriefingView(
                adventure: adv,
                profile: profile,
                isFallbackMatch: false,
                onStart: {
                    startNewSession(for: adv)
                },
                onPickAnother: {
                    selectedAdventureForBriefing = nil
                    showingVibePicker = true
                }
            )
        }
        // Полноэкранная скретч-сессия
        .fullScreenCover(item: $activeSessionToResume) { sess in
            if let adv = catalog.adventure(by: sess.adventureId) {
                ScratchCardContainer(
                    session: sess,
                    adventure: adv,
                    profile: profile,
                    onDismiss: {
                        activeSessionToResume = nil
                    }
                )
            }
        }
    }

    // MARK: - 1. Шапка пары (Имена, ранг, шкала XP и кнопка "?")
    private var coupleProfileHeaderView: some View {
        let levelInfo = engine.level(for: profile.totalXP)
        let progress = levelInfo.progressPercentage(currentXP: profile.totalXP)

        return VStack(spacing: 14) {
            HStack(alignment: .center) {
                VStack(alignment: .leading, spacing: 4) {
                    HStack(spacing: 6) {
                        Image(systemName: "heart.fill")
                            .font(.system(size: 14))
                            .foregroundStyle(AppTheme.primaryRose)

                        Text("\(profile.partnerOneName) & \(profile.partnerTwoName)")
                            .font(.system(size: 20, weight: .bold, design: .rounded))
                            .foregroundStyle(.white)
                    }

                    Text("Уровень \(levelInfo.level) · \(levelInfo.title)")
                        .font(.system(size: 13, weight: .semibold))
                        .foregroundStyle(AppTheme.warmGold)
                }

                Spacer()

                // Кнопка быстрого открытия гида "?"
                Button {
                    AppTheme.hapticLight()
                    showingWalkthrough = true
                } label: {
                    ZStack {
                        Circle()
                            .fill(Color.white.opacity(0.08))
                            .frame(width: 38, height: 38)

                        Image(systemName: "questionmark")
                            .font(.system(size: 15, weight: .bold))
                            .foregroundStyle(AppTheme.softText)
                    }
                }
                .buttonStyle(.plain)

                // Значок уровня с градиентом
                ZStack {
                    Circle()
                        .fill(AppTheme.heroGradient)
                        .frame(width: 42, height: 42)

                    Text("\(levelInfo.level)")
                        .font(.system(size: 17, weight: .heavy, design: .rounded))
                        .foregroundStyle(.white)
                }
            }

            // Шкала прогресса XP
            VStack(spacing: 6) {
                HStack {
                    Text("Опыт приключений")
                        .font(.system(size: 12))
                        .foregroundStyle(AppTheme.softText)

                    Spacer()

                    if let nextXP = levelInfo.nextLevelXP {
                        Text("\(profile.totalXP) / \(nextXP) XP")
                            .font(.system(size: 12, weight: .bold, design: .monospaced))
                            .foregroundStyle(.white)
                    } else {
                        Text("\(profile.totalXP) XP · Максимум")
                            .font(.system(size: 12, weight: .bold, design: .monospaced))
                            .foregroundStyle(AppTheme.warmGold)
                    }
                }

                GeometryReader { geo in
                    ZStack(alignment: .leading) {
                        Capsule()
                            .fill(Color.white.opacity(0.12))
                            .frame(height: 8)

                        Capsule()
                            .fill(AppTheme.heroGradient)
                            .frame(width: geo.size.width * CGFloat(progress), height: 8)
                            .animation(.spring(duration: 0.5), value: progress)
                    }
                }
                .frame(height: 8)
            }
        }
        .padding(18)
        .background(AppTheme.cardSurface)
        .clipShape(RoundedRectangle(cornerRadius: 22))
        .overlay(
            RoundedRectangle(cornerRadius: 22)
                .stroke(Color.white.opacity(0.08), lineWidth: 1)
        )
    }

    // MARK: - 2.1. Герой-виджет: Продолжение активного вечера
    private func activeSessionHeroCard(session: AdventureSessionRecord, adventure: AdventureDefinition) -> some View {
        Button {
            AppTheme.hapticMedium()
            activeSessionToResume = session
        } label: {
            VStack(alignment: .leading, spacing: 16) {
                HStack(spacing: 12) {
                    ZStack {
                        Circle()
                            .fill(AppTheme.primaryRose.opacity(0.25))
                            .frame(width: 48, height: 48)

                        Image(systemName: "flame.fill")
                            .font(.system(size: 22))
                            .foregroundStyle(AppTheme.primaryRose)
                    }

                    VStack(alignment: .leading, spacing: 3) {
                        Text("ВЕЧЕР В РАЗГАРЕ")
                            .font(.system(size: 11, weight: .heavy, design: .rounded))
                            .tracking(1.2)
                            .foregroundStyle(AppTheme.primaryRose)

                        Text("Продолжить ваш вечер")
                            .font(.system(size: 18, weight: .bold, design: .rounded))
                            .foregroundStyle(.white)
                    }

                    Spacer()

                    Image(systemName: "arrow.right.circle.fill")
                        .font(.system(size: 28))
                        .foregroundStyle(AppTheme.warmGold)
                }

                VStack(alignment: .leading, spacing: 6) {
                    Text(adventure.title)
                        .font(.system(size: 16, weight: .semibold))
                        .foregroundStyle(.white)

                    Text("Этап \(session.currentStepIndex) из 4 · \(adventure.subtitle)")
                        .font(.system(size: 13))
                        .foregroundStyle(AppTheme.softText)
                        .lineLimit(2)
                }

                // Индикатор пройденных шагов
                HStack(spacing: 8) {
                    ForEach(1...4, id: \.self) { step in
                        let isDone = session.isStepCompleted(at: step)
                        let isCurrent = session.currentStepIndex == step
                        Capsule()
                            .fill(
                                isDone ? AppTheme.warmGold :
                                isCurrent ? AppTheme.primaryRose :
                                Color.white.opacity(0.15)
                            )
                            .frame(height: 5)
                            .frame(maxWidth: .infinity)
                    }
                }
            }
            .padding(20)
            .background(
                LinearGradient(
                    colors: [Color(red: 0.24, green: 0.12, blue: 0.22), AppTheme.cardSurface],
                    startPoint: .topLeading,
                    endPoint: .bottomTrailing
                )
            )
            .clipShape(RoundedRectangle(cornerRadius: 24))
            .overlay(
                RoundedRectangle(cornerRadius: 24)
                    .stroke(AppTheme.primaryRose.opacity(0.6), lineWidth: 1.5)
            )
            .shadow(color: AppTheme.primaryRose.opacity(0.3), radius: 18, y: 6)
        }
        .buttonStyle(.plain)
    }

    // MARK: - 2.2. Герой-виджет: Старт нового вечера с 3 веерными картами
    private var newEveningHeroCard: some View {
        VStack(spacing: 20) {
            // Верхняя веерная композиция из 3 карточек
            fannedCardsPreview
                .padding(.top, 10)

            // Заголовок и пояснение
            VStack(spacing: 8) {
                Text("Ваш вечер вдвоём")
                    .font(.system(size: 12, weight: .heavy, design: .rounded))
                    .tracking(1.4)
                    .foregroundStyle(AppTheme.warmGold)

                Text("Готовы к маленькому\nприключению?")
                    .font(.system(size: 22, weight: .bold, design: .rounded))
                    .foregroundStyle(.white)
                    .multilineTextAlignment(.center)

                Text("Выберите настроение, а мы соберём секретный маршрут из 4 карточек-сюрпризов.")
                    .font(.system(size: 14))
                    .foregroundStyle(AppTheme.softText)
                    .multilineTextAlignment(.center)
                    .padding(.horizontal, 16)
            }

            // Большая акцентная кнопка
            Button {
                AppTheme.hapticMedium()
                preselectedVibe = nil
                showingVibePicker = true
            } label: {
                HStack(spacing: 10) {
                    Image(systemName: "wand.and.stars")
                        .font(.system(size: 18, weight: .bold))

                    Text("Подобрать вайб на вечер")
                        .font(.system(size: 16, weight: .bold, design: .rounded))
                }
                .foregroundStyle(.white)
                .frame(maxWidth: .infinity)
                .frame(height: 52)
                .background(AppTheme.heroGradient)
                .clipShape(RoundedRectangle(cornerRadius: 16))
                .shadow(color: AppTheme.primaryRose.opacity(0.4), radius: 12, y: 4)
            }
        }
        .padding(22)
        .background(
            LinearGradient(
                colors: [Color(red: 0.16, green: 0.14, blue: 0.24), AppTheme.cardSurface],
                startPoint: .top,
                endPoint: .bottom
            )
        )
        .clipShape(RoundedRectangle(cornerRadius: 24))
        .overlay(
            RoundedRectangle(cornerRadius: 24)
                .stroke(Color.white.opacity(0.10), lineWidth: 1)
        )
        .shadow(color: AppTheme.Shadows.cardShadowColor, radius: 16, y: 8)
    }

    // Веер из 3 карт
    private var fannedCardsPreview: some View {
        ZStack {
            // Левая карта (Уют/Спонтанность)
            miniDeckCard(icon: "bolt.fill", gradient: AppTheme.spontaneous.accentGradient)
                .rotationEffect(.degrees(-14))
                .offset(x: -42, y: 6)

            // Правая карта (Романтика)
            miniDeckCard(icon: "heart.fill", gradient: AppTheme.romantic.accentGradient)
                .rotationEffect(.degrees(14))
                .offset(x: 42, y: 6)

            // Центральная карта (Игривое / Золото)
            miniDeckCard(icon: "sparkles", gradient: AppTheme.Gradients.goldReward)
                .rotationEffect(.degrees(0))
                .offset(y: -4)
                .shadow(color: AppTheme.warmGold.opacity(0.35), radius: 10)
        }
        .frame(height: 96)
    }

    private func miniDeckCard(icon: String, gradient: LinearGradient) -> some View {
        RoundedRectangle(cornerRadius: 12)
            .fill(gradient)
            .frame(width: 66, height: 88)
            .overlay(
                RoundedRectangle(cornerRadius: 12)
                    .stroke(Color.white.opacity(0.3), lineWidth: 1)
            )
            .overlay(
                Image(systemName: icon)
                    .font(.system(size: 22, weight: .bold))
                    .foregroundStyle(.white)
            )
            .shadow(color: Color.black.opacity(0.3), radius: 8, y: 4)
    }

    // MARK: - 3. Быстрый выбор настроения (Quick Mood Row)
    private var quickMoodSection: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack {
                Text("Настроение вечера")
                    .font(.system(size: 18, weight: .bold, design: .rounded))
                    .foregroundStyle(.white)

                Spacer()

                Text("Быстрый старт")
                    .font(.system(size: 12, weight: .semibold))
                    .foregroundStyle(AppTheme.softText)
            }
            .padding(.horizontal, 20)

            ScrollView(.horizontal, showsIndicators: false) {
                HStack(spacing: 12) {
                    ForEach(VibeTag.allCases) { vibe in
                        quickMoodCard(for: vibe)
                    }
                }
                .padding(.horizontal, 20)
                .padding(.vertical, 4)
            }
        }
    }

    private func quickMoodCard(for vibe: VibeTag) -> some View {
        let palette = AppTheme.palette(for: vibe)

        return Button {
            AppTheme.hapticLight()
            preselectedVibe = vibe
            showingVibePicker = true
        } label: {
            VStack(spacing: 10) {
                ZStack {
                    Circle()
                        .fill(palette.accentGradient)
                        .frame(width: 48, height: 48)

                    Image(systemName: vibe.iconName)
                        .font(.system(size: 20, weight: .bold))
                        .foregroundStyle(.white)
                }
                .shadow(color: palette.glowColor, radius: 8)

                Text(vibe.displayName)
                    .font(.system(size: 13, weight: .bold, design: .rounded))
                    .foregroundStyle(.white)
                    .lineLimit(1)
            }
            .frame(width: 98, height: 112)
            .background(AppTheme.cardSurface)
            .clipShape(RoundedRectangle(cornerRadius: 18))
            .overlay(
                RoundedRectangle(cornerRadius: 18)
                    .stroke(palette.primary.opacity(0.35), lineWidth: 1)
            )
            .shadow(color: Color.black.opacity(0.15), radius: 6, y: 3)
        }
        .buttonStyle(.plain)
    }

    // MARK: - 4. Баннер интерактивного гида
    private var interactiveGuideBanner: some View {
        Button {
            AppTheme.hapticLight()
            showingWalkthrough = true
        } label: {
            HStack(spacing: 14) {
                ZStack {
                    Circle()
                        .fill(AppTheme.warmGold.opacity(0.18))
                        .frame(width: 44, height: 44)

                    Image(systemName: "book.fill")
                        .font(.system(size: 18))
                        .foregroundStyle(AppTheme.warmGold)
                }

                VStack(alignment: .leading, spacing: 3) {
                    Text("Как играть · Интерактивный гид")
                        .font(.system(size: 15, weight: .bold, design: .rounded))
                        .foregroundStyle(.white)

                    Text("Правила стирания карт, таймер и подсказки")
                        .font(.system(size: 12))
                        .foregroundStyle(AppTheme.softText)
                }

                Spacer()

                Image(systemName: "chevron.right")
                    .font(.system(size: 14, weight: .bold))
                    .foregroundStyle(AppTheme.warmGold)
            }
            .padding(16)
            .background(AppTheme.cardSurface)
            .clipShape(RoundedRectangle(cornerRadius: 18))
            .overlay(
                RoundedRectangle(cornerRadius: 18)
                    .stroke(Color.white.opacity(0.08), lineWidth: 1)
            )
        }
        .buttonStyle(.plain)
    }

    // MARK: - Хелпер запуска новой сессии
    private func startNewSession(for adventure: AdventureDefinition) {
        selectedAdventureForBriefing = nil
        let newSession = AdventureSessionRecord(adventureId: adventure.id, stepCount: 4)
        modelContext.insert(newSession)
        try? modelContext.save()
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.3) {
            activeSessionToResume = newSession
        }
    }
}
