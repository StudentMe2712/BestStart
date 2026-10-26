import SwiftUI
import SwiftData

public struct JourneyMapView: View {
    @Environment(\.modelContext) private var modelContext
    @Query private var profiles: [CoupleProfile]
    @Query(sort: \AdventureSessionRecord.startedAt, order: .reverse) private var sessions: [AdventureSessionRecord]

    @State private var showingVibePicker: Bool = false
    @State private var selectedAdventureForBriefing: AdventureDefinition?
    @State private var activeSessionToResume: AdventureSessionRecord?

    private let catalog = CatalogRepository.shared
    private let engine = ProgressionEngine.shared

    public init() {}

    private var profile: CoupleProfile {
        profiles.first ?? CoupleProfile()
    }

    private var activeSession: AdventureSessionRecord? {
        sessions.first(where: { !$0.isFinished })
    }

    private var completedAdventureIds: Set<String> {
        Set(sessions.filter { $0.isFinished }.map { $0.adventureId })
    }

    public var body: some View {
        ZStack(alignment: .bottom) {
            AppTheme.midnightBg
                .ignoresSafeArea()

            ScrollView(showsIndicators: false) {
                VStack(spacing: 24) {
                    // 1. Шапка профиля пары
                    profileHeaderView
                        .padding(.horizontal, 20)
                        .padding(.top, 16)

                    // 2. Баннер активной сессии (если есть)
                    if let ongoing = activeSession, let adv = catalog.adventure(by: ongoing.adventureId) {
                        activeSessionBanner(session: ongoing, adventure: adv)
                            .padding(.horizontal, 20)
                    }

                    // 3. Карта глав (Главы 1, 2, 3)
                    chaptersMapView
                        .padding(.horizontal, 20)

                    // Отступ под плавающую кнопку (FAB)
                    Color.clear
                        .frame(height: 90)
                }
            }

            // 4. Плавающая кнопка (FAB) внизу
            floatingActionButton
                .padding(.horizontal, 24)
                .padding(.bottom, 20)
        }
        .sheet(isPresented: $showingVibePicker) {
            VibePickerSheet(
                profile: profile,
                completedAdventureIds: completedAdventureIds,
                onAdventureSelected: { adv in
                    showingVibePicker = false
                    DispatchQueue.main.asyncAfter(deadline: .now() + 0.3) {
                        selectedAdventureForBriefing = adv
                    }
                }
            )
        }
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

    // MARK: - Шапка профиля пары
    private var profileHeaderView: some View {
        let levelInfo = engine.level(for: profile.totalXP)
        let progress = levelInfo.progressPercentage(currentXP: profile.totalXP)

        return VStack(spacing: 14) {
            HStack {
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
                        .font(.system(size: 14, weight: .semibold))
                        .foregroundStyle(AppTheme.warmGold)
                }

                Spacer()

                // Значок уровня
                ZStack {
                    Circle()
                        .fill(AppTheme.heroGradient)
                        .frame(width: 44, height: 44)

                    Text("\(levelInfo.level)")
                        .font(.system(size: 18, weight: .heavy, design: .rounded))
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
        .clipShape(RoundedRectangle(cornerRadius: 20))
        .overlay(
            RoundedRectangle(cornerRadius: 20)
                .stroke(Color.white.opacity(0.08), lineWidth: 1)
        )
    }

    // MARK: - Баннер активной сессии
    private func activeSessionBanner(session: AdventureSessionRecord, adventure: AdventureDefinition) -> some View {
        Button {
            AppTheme.hapticMedium()
            activeSessionToResume = session
        } label: {
            HStack(spacing: 14) {
                ZStack {
                    Circle()
                        .fill(AppTheme.primaryRose.opacity(0.2))
                        .frame(width: 44, height: 44)

                    Image(systemName: "flame.fill")
                        .font(.system(size: 20))
                        .foregroundStyle(AppTheme.primaryRose)
                }

                VStack(alignment: .leading, spacing: 3) {
                    Text("Вечер в разгаре!")
                        .font(.system(size: 12, weight: .bold))
                        .foregroundStyle(AppTheme.primaryRose)

                    Text("\(adventure.title) (Этап \(session.currentStepIndex) из 4)")
                        .font(.system(size: 15, weight: .semibold))
                        .foregroundStyle(.white)
                        .lineLimit(1)
                }

                Spacer()

                HStack(spacing: 4) {
                    Text("Продолжить")
                        .font(.system(size: 13, weight: .bold))
                        .foregroundStyle(AppTheme.warmGold)

                    Image(systemName: "chevron.right")
                        .font(.system(size: 12, weight: .bold))
                        .foregroundStyle(AppTheme.warmGold)
                }
            }
            .padding(14)
            .background(Color(red: 0.22, green: 0.12, blue: 0.20))
            .clipShape(RoundedRectangle(cornerRadius: 18))
            .overlay(
                RoundedRectangle(cornerRadius: 18)
                    .stroke(AppTheme.primaryRose.opacity(0.5), lineWidth: 1.5)
            )
        }
        .buttonStyle(.plain)
    }

    // MARK: - Извилистая карта глав
    private var chaptersMapView: some View {
        VStack(spacing: 36) {
            ForEach(catalog.chapters) { chapter in
                chapterSectionView(chapter: chapter)
            }
        }
    }

    private func chapterSectionView(chapter: ChapterDefinition) -> some View {
        let isUnlocked = isChapterUnlocked(chapter)
        let chapterAdvs = catalog.adventures(for: chapter.id)

        return VStack(spacing: 18) {
            // Заголовок главы
            VStack(spacing: 4) {
                HStack(spacing: 8) {
                    Text("ГЛАВА \(chapter.chapterNumber)")
                        .font(.system(size: 12, weight: .heavy, design: .rounded))
                        .textCase(.uppercase)
                        .tracking(1.5)
                        .foregroundStyle(isUnlocked ? AppTheme.warmGold : AppTheme.softText.opacity(0.5))

                    if !isUnlocked {
                        Image(systemName: "lock.fill")
                            .font(.system(size: 12))
                            .foregroundStyle(AppTheme.softText.opacity(0.5))
                    }
                }

                Text(chapter.title)
                    .font(.system(size: 20, weight: .bold, design: .rounded))
                    .foregroundStyle(isUnlocked ? .white : AppTheme.softText.opacity(0.6))

                Text(chapter.subtitle)
                    .font(.system(size: 13))
                    .foregroundStyle(AppTheme.softText.opacity(0.7))
                    .multilineTextAlignment(.center)
                    .padding(.horizontal, 16)
            }

            // Извилистый путь из 4 узлов
            VStack(spacing: 24) {
                ForEach(Array(chapterAdvs.enumerated()), id: \.element.id) { index, adv in
                    let questNumber = (chapter.chapterNumber - 1) * 4 + (index + 1)
                    let nodeState = determineNodeState(adventure: adv, isChapterUnlocked: isUnlocked)
                    let horizontalOffset: CGFloat = (index % 2 == 0) ? -45 : 45

                    HStack {
                        if index % 2 != 0 { Spacer() }

                        MapNodeView(
                            questNumber: questNumber,
                            title: adv.title,
                            vibes: adv.vibes,
                            state: nodeState,
                            action: {
                                handleNodeTap(adv: adv, state: nodeState)
                            }
                        )
                        .offset(x: horizontalOffset)

                        if index % 2 == 0 { Spacer() }
                    }
                }
            }
            .padding(.vertical, 8)
        }
        .padding(.vertical, 20)
        .background(AppTheme.cardSurface.opacity(0.4))
        .clipShape(RoundedRectangle(cornerRadius: 24))
    }

    private func isChapterUnlocked(_ chapter: ChapterDefinition) -> Bool {
        if chapter.chapterNumber == 1 { return true }
        if chapter.chapterNumber == 2 {
            let ch1Advs = catalog.adventures(for: "ch_01").map { $0.id }
            let completedCh1 = ch1Advs.filter { completedAdventureIds.contains($0) }.count
            return completedCh1 >= 3
        }
        if chapter.chapterNumber == 3 {
            let ch2Advs = catalog.adventures(for: "ch_02").map { $0.id }
            let completedCh2 = ch2Advs.filter { completedAdventureIds.contains($0) }.count
            return completedCh2 >= 3
        }
        return false
    }

    private func determineNodeState(adventure: AdventureDefinition, isChapterUnlocked: Bool) -> MapNodeState {
        if !isChapterUnlocked { return .locked }
        if completedAdventureIds.contains(adventure.id) { return .completed }
        if activeSession?.adventureId == adventure.id { return .active }
        return .active
    }

    private func handleNodeTap(adv: AdventureDefinition, state: MapNodeState) {
        if let ongoing = activeSession, ongoing.adventureId == adv.id {
            activeSessionToResume = ongoing
        } else {
            selectedAdventureForBriefing = adv
        }
    }

    // MARK: - Плавающая кнопка (FAB)
    private var floatingActionButton: some View {
        Button {
            AppTheme.hapticMedium()
            showingVibePicker = true
        } label: {
            HStack(spacing: 10) {
                Image(systemName: "wand.and.stars")
                    .font(.system(size: 20, weight: .bold))

                Text("Подобрать вайб на вечер")
                    .font(.system(size: 16, weight: .bold))
            }
            .foregroundStyle(.white)
            .frame(maxWidth: .infinity)
            .frame(height: 56)
            .background(AppTheme.heroGradient)
            .clipShape(Capsule())
            .shadow(color: AppTheme.primaryRose.opacity(0.45), radius: 14, y: 6)
        }
    }

    // MARK: - Запуск новой сессии
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
