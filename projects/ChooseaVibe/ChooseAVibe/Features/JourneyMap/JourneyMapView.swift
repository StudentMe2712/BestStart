import SwiftUI
import SwiftData

/// Экран «Путь» (Вкладка 2) с Apple HIG iOS 18 архитектурой:
/// - Segmented Picker глав вверху ("Глава 1" | "Глава 2" | "Глава 3")
/// - Шапка главы: Название, подзаголовок и статус-пилюля ("X из 4 открыто")
/// - S-образная извилистая тропа с 4 сферическими стеклянными узлами (68x68)
/// - Открытие AdventureBriefingView при тапе на доступный узел
public struct JourneyMapView: View {
    @Environment(\.modelContext) private var modelContext
    @Query private var profiles: [CoupleProfile]
    @Query(sort: \AdventureSessionRecord.startedAt, order: .reverse) private var sessions: [AdventureSessionRecord]

    @State private var selectedChapterIndex: Int = 0
    @State private var selectedAdventureForBriefing: AdventureDefinition?
    @State private var activeSessionToResume: AdventureSessionRecord?

    private let catalog = CatalogRepository.shared

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

    private var currentChapter: ChapterDefinition {
        let chapters = catalog.chapters
        if selectedChapterIndex >= 0 && selectedChapterIndex < chapters.count {
            return chapters[selectedChapterIndex]
        }
        return chapters.first ?? ChapterDefinition(
            id: "ch_01",
            order: 1,
            title: "Первые искры",
            subtitle: "Лёгкие сценарии для быстрого погружения",
            requiredCompletedCount: 0,
            adventureIds: []
        )
    }

    public var body: some View {
        NavigationStack {
            ZStack {
                AppTheme.midnightBg
                    .ignoresSafeArea()

                VStack(spacing: 0) {
                    // 1. Верхний переключатель глав (Segmented Picker)
                    chapterSegmentedPicker
                        .padding(.horizontal, 20)
                        .padding(.top, 12)
                        .padding(.bottom, 16)

                    // 2. Скроллируемая область карты с извилистой тропой S-curve
                    ScrollView(showsIndicators: false) {
                        VStack(spacing: 24) {
                            // Информационная карточка выбранной главы
                            chapterHeaderView(for: currentChapter)
                                .padding(.horizontal, 20)

                            // Извилистая тропа с 4 сферическими стеклянными узлами
                            chapterTrailView(for: currentChapter)
                                .padding(.horizontal, 20)
                                .padding(.bottom, 36)
                        }
                    }
                }
            }
            .navigationTitle("Путь")
            .navigationBarTitleDisplayMode(.inline)
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

    // MARK: - 1. Верхний Segmented Picker ("Глава 1" | "Глава 2" | "Глава 3")
    private var chapterSegmentedPicker: some View {
        Picker("Глава", selection: $selectedChapterIndex) {
            ForEach(Array(catalog.chapters.enumerated()), id: \.element.id) { index, chapter in
                Text("Глава \(chapter.chapterNumber)")
                    .tag(index)
            }
        }
        .pickerStyle(.segmented)
        .onChange(of: selectedChapterIndex) { _, _ in
            AppTheme.hapticLight()
        }
    }

    // MARK: - 2. Шапка главы (Название, подзаголовок и пилюля прогресса)
    private func chapterHeaderView(for chapter: ChapterDefinition) -> some View {
        let isUnlocked = isChapterUnlocked(chapter)
        let chapterAdvs = catalog.adventures(for: chapter.id)
        let completedInChapter = chapterAdvs.filter { completedAdventureIds.contains($0.id) }.count
        let totalCount = max(chapterAdvs.count, 4)

        return VStack(spacing: 12) {
            HStack {
                // Пилюля статуса прогресса главы
                HStack(spacing: 6) {
                    if isUnlocked {
                        Image(systemName: completedInChapter == totalCount ? "checkmark.circle.fill" : "sparkle")
                            .font(.system(size: 11, weight: .bold))
                            .foregroundStyle(completedInChapter == totalCount ? AppTheme.warmGold : AppTheme.primaryRose)

                        Text("\(completedInChapter) из \(totalCount) открыто")
                            .font(.system(size: 12, weight: .bold, design: .rounded))
                            .foregroundStyle(.white)
                    } else {
                        Image(systemName: "lock.fill")
                            .font(.system(size: 11))
                            .foregroundStyle(AppTheme.softText)

                        Text("Глава заблокирована")
                            .font(.system(size: 12, weight: .semibold, design: .rounded))
                            .foregroundStyle(AppTheme.softText)
                    }
                }
                .padding(.horizontal, 12)
                .padding(.vertical, 6)
                .background(isUnlocked ? AppTheme.cardSurfaceLight : Color.white.opacity(0.06))
                .clipShape(Capsule())

                Spacer()

                if !isUnlocked {
                    Text("Нужно 3 квеста из пред. главы")
                        .font(.system(size: 11))
                        .foregroundStyle(AppTheme.softText.opacity(0.8))
                }
            }

            VStack(alignment: .leading, spacing: 4) {
                HStack {
                    Text(chapter.title)
                        .font(.system(size: 22, weight: .bold, design: .rounded))
                        .foregroundStyle(isUnlocked ? .white : AppTheme.softText)

                    Spacer()
                }

                Text(chapter.subtitle)
                    .font(.system(size: 14))
                    .foregroundStyle(AppTheme.softText)
                    .lineLimit(3)
            }
        }
        .padding(18)
        .background(AppTheme.cardSurface)
        .clipShape(RoundedRectangle(cornerRadius: 22))
        .overlay(
            RoundedRectangle(cornerRadius: 22)
                .stroke(isUnlocked ? AppTheme.primaryRose.opacity(0.3) : Color.white.opacity(0.06), lineWidth: 1)
        )
    }

    // MARK: - 3. Извилистая тропа S-curve с 4 узлами
    private func chapterTrailView(for chapter: ChapterDefinition) -> some View {
        let isUnlocked = isChapterUnlocked(chapter)
        let chapterAdvs = catalog.adventures(for: chapter.id)

        return ZStack {
            // Фоновая соединительная линия извилистой S-образной дороги
            windingTrailPath
                .stroke(
                    LinearGradient(
                        colors: [
                            isUnlocked ? AppTheme.primaryRose.opacity(0.6) : Color.white.opacity(0.1),
                            isUnlocked ? AppTheme.warmGold.opacity(0.6) : Color.white.opacity(0.06)
                        ],
                        startPoint: .top,
                        endPoint: .bottom
                    ),
                    style: StrokeStyle(lineWidth: 3, lineCap: .round, lineJoin: .round, dash: [8, 6])
                )
                .frame(height: 480)
                .padding(.vertical, 40)

            // 4 узла, расставленные по S-образной дуге
            VStack(spacing: 36) {
                ForEach(Array(chapterAdvs.enumerated()), id: \.element.id) { index, adv in
                    let questNumber = (chapter.chapterNumber - 1) * 4 + (index + 1)
                    let nodeState = determineNodeState(chapterAdvs: chapterAdvs, index: index, isChapterUnlocked: isUnlocked)
                    // Чередование смещения влево / вправо для создания S-кривой
                    let horizontalOffset: CGFloat = (index % 2 == 0) ? -55 : 55

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
            .padding(.vertical, 24)
        }
        .padding(20)
        .background(
            LinearGradient(
                colors: [Color(red: 0.12, green: 0.12, blue: 0.20).opacity(0.6), AppTheme.cardSurface.opacity(0.4)],
                startPoint: .top,
                endPoint: .bottom
            )
        )
        .clipShape(RoundedRectangle(cornerRadius: 26))
        .overlay(
            RoundedRectangle(cornerRadius: 26)
                .stroke(Color.white.opacity(0.06), lineWidth: 1)
        )
    }

    // Соединительная S-образная линия между 4 точками
    private var windingTrailPath: some View {
        GeometryReader { geo in
            let w = geo.size.width
            let h = geo.size.height

            // Центры 4 узлов со смещениями:
            // Node 0 (слева): x = w * 0.5 - 55, y = h * 0.12
            // Node 1 (справа): x = w * 0.5 + 55, y = h * 0.37
            // Node 2 (слева): x = w * 0.5 - 55, y = h * 0.62
            // Node 3 (справа): x = w * 0.5 + 55, y = h * 0.88
            let p0 = CGPoint(x: w * 0.5 - 55, y: h * 0.12)
            let p1 = CGPoint(x: w * 0.5 + 55, y: h * 0.37)
            let p2 = CGPoint(x: w * 0.5 - 55, y: h * 0.62)
            let p3 = CGPoint(x: w * 0.5 + 55, y: h * 0.88)

            Path { path in
                path.move(to: p0)
                path.addCurve(
                    to: p1,
                    control1: CGPoint(x: w * 0.5 - 20, y: h * 0.24),
                    control2: CGPoint(x: w * 0.5 + 70, y: h * 0.25)
                )
                path.addCurve(
                    to: p2,
                    control1: CGPoint(x: w * 0.5 + 40, y: h * 0.49),
                    control2: CGPoint(x: w * 0.5 - 70, y: h * 0.50)
                )
                path.addCurve(
                    to: p3,
                    control1: CGPoint(x: w * 0.5 - 40, y: h * 0.75),
                    control2: CGPoint(x: w * 0.5 + 70, y: h * 0.76)
                )
            }
        }
    }

    // MARK: - Логика доступности и состояния узлов
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

    private func determineNodeState(chapterAdvs: [AdventureDefinition], index: Int, isChapterUnlocked: Bool) -> MapNodeState {
        guard isChapterUnlocked else { return .locked }
        let adv = chapterAdvs[index]

        if completedAdventureIds.contains(adv.id) {
            return .completed
        }
        if activeSession?.adventureId == adv.id {
            return .active
        }
        // В открытой главе первый незавершённый квест или если пройден предыдущий
        if index == 0 {
            return .active
        }
        let prevAdv = chapterAdvs[index - 1]
        if completedAdventureIds.contains(prevAdv.id) {
            return .active
        }
        // Если уже начата сессия для этого квеста
        if sessions.contains(where: { $0.adventureId == adv.id }) {
            return .active
        }
        return .locked
    }

    private func handleNodeTap(adv: AdventureDefinition, state: MapNodeState) {
        if let ongoing = activeSession, ongoing.adventureId == adv.id {
            activeSessionToResume = ongoing
        } else {
            selectedAdventureForBriefing = adv
        }
    }

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
