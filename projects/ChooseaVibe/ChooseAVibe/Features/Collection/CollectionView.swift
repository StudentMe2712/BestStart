import SwiftUI
import SwiftData

public struct CollectionView: View {
    @Environment(\.modelContext) private var modelContext
    @Query private var profiles: [CoupleProfile]
    @Query(sort: \AdventureSessionRecord.startedAt, order: .reverse) private var sessions: [AdventureSessionRecord]

    @State private var selectedTab: Int = 0
    @State private var selectedAdventureForReplay: AdventureDefinition?

    private let catalog = CatalogRepository.shared
    private let engine = ProgressionEngine.shared

    public init() {}

    private var profile: CoupleProfile {
        profiles.first ?? CoupleProfile()
    }

    private var completedSessions: [AdventureSessionRecord] {
        sessions.filter { $0.isFinished }
    }

    public var body: some View {
        NavigationStack {
            ZStack {
                AppTheme.midnightBg
                    .ignoresSafeArea()

                VStack(spacing: 18) {
                    // Переключатель вкладок
                    Picker("Категория", selection: $selectedTab) {
                        Text("Значки").tag(0)
                        Text("Пройденные").tag(1)
                        Text("Рубашки").tag(2)
                    }
                    .pickerStyle(.segmented)
                    .padding(.horizontal, 20)
                    .padding(.top, 8)

                    // Контент выбранной вкладки
                    ScrollView(showsIndicators: false) {
                        VStack(spacing: 20) {
                            switch selectedTab {
                            case 0:
                                achievementsGridView
                            case 1:
                                completedEveningsListView
                            case 2:
                                scratchThemesListView
                            default:
                                EmptyView()
                            }
                        }
                        .padding(.horizontal, 20)
                        .padding(.bottom, 30)
                    }
                }
            }
            .navigationTitle("Музей пары")
            .navigationBarTitleDisplayMode(.inline)
            .sheet(item: $selectedAdventureForReplay) { adv in
                AdventureBriefingView(
                    adventure: adv,
                    profile: profile,
                    isFallbackMatch: false,
                    onStart: {
                        startReplaySession(for: adv)
                    },
                    onPickAnother: {
                        selectedAdventureForReplay = nil
                    }
                )
            }
        }
    }

    // MARK: - Вкладка 1: Значки (Сетка 8 достижений)
    private var achievementsGridView: some View {
        let unlockedIds = Set(profile.unlockedAchievementIds)
        let allAchievements = catalog.achievements.isEmpty ? AchievementDefinition.allPredefined : catalog.achievements

        return VStack(alignment: .leading, spacing: 14) {
            HStack {
                Text("Достижения (\(unlockedIds.count) из \(allAchievements.count))")
                    .font(.system(size: 15, weight: .bold))
                    .foregroundStyle(.white)

                Spacer()

                Text("+\(unlockedIds.count * 20) XP")
                    .font(.system(size: 13, weight: .bold))
                    .foregroundStyle(AppTheme.warmGold)
            }

            LazyVGrid(columns: [GridItem(.flexible()), GridItem(.flexible())], spacing: 14) {
                ForEach(allAchievements) { ach in
                    let isUnlocked = unlockedIds.contains(ach.id)
                    VStack(spacing: 10) {
                        ZStack {
                            Circle()
                                .fill(isUnlocked ? AppTheme.goldGradient : LinearGradient(colors: [Color.white.opacity(0.08), Color.white.opacity(0.04)], startPoint: .top, endPoint: .bottom))
                                .frame(width: 54, height: 54)

                            Image(systemName: ach.iconName)
                                .font(.system(size: 24))
                                .foregroundStyle(isUnlocked ? .black : Color.white.opacity(0.3))
                        }

                        VStack(spacing: 4) {
                            Text(ach.title)
                                .font(.system(size: 14, weight: .bold))
                                .foregroundStyle(isUnlocked ? .white : AppTheme.softText.opacity(0.6))
                                .multilineTextAlignment(.center)

                            Text(ach.description)
                                .font(.system(size: 11))
                                .foregroundStyle(AppTheme.softText.opacity(isUnlocked ? 0.9 : 0.4))
                                .multilineTextAlignment(.center)
                                .lineLimit(3)
                        }

                        Text(isUnlocked ? "Открыто (+20 XP)" : "Заблокировано")
                            .font(.system(size: 10, weight: .bold))
                            .foregroundStyle(isUnlocked ? AppTheme.warmGold : Color.white.opacity(0.3))
                            .padding(.top, 4)
                    }
                    .padding(14)
                    .frame(maxWidth: .infinity, minHeight: 160)
                    .background(isUnlocked ? AppTheme.cardSurface : AppTheme.cardSurface.opacity(0.5))
                    .clipShape(RoundedRectangle(cornerRadius: 18))
                    .overlay(
                        RoundedRectangle(cornerRadius: 18)
                            .stroke(isUnlocked ? AppTheme.warmGold.opacity(0.35) : Color.white.opacity(0.06), lineWidth: 1)
                    )
                }
            }
        }
    }

    // MARK: - Вкладка 2: Пройденные вечера
    private var completedEveningsListView: some View {
        VStack(alignment: .leading, spacing: 14) {
            Text("Завершённые вечера (\(completedSessions.count))")
                .font(.system(size: 15, weight: .bold))
                .foregroundStyle(.white)

            if completedSessions.isEmpty {
                VStack(spacing: 12) {
                    Image(systemName: "calendar.badge.clock")
                        .font(.system(size: 44))
                        .foregroundStyle(AppTheme.softText.opacity(0.4))
                        .padding(.top, 30)

                    Text("Вы ещё не завершили ни одного приключения")
                        .font(.system(size: 15, weight: .semibold))
                        .foregroundStyle(.white)

                    Text("Подберите вайб на главном экране или начните с первой главы.")
                        .font(.system(size: 13))
                        .foregroundStyle(AppTheme.softText)
                        .multilineTextAlignment(.center)
                }
                .frame(maxWidth: .infinity)
                .padding(24)
            } else {
                ForEach(completedSessions, id: \.sessionId) { sess in
                    if let adv = catalog.adventure(by: sess.adventureId) {
                        completedEveningRow(session: sess, adventure: adv)
                    }
                }
            }
        }
    }

    private func completedEveningRow(session: AdventureSessionRecord, adventure: AdventureDefinition) -> some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack {
                VStack(alignment: .leading, spacing: 4) {
                    Text(adventure.title)
                        .font(.system(size: 17, weight: .bold))
                        .foregroundStyle(.white)

                    if let date = session.completedAt {
                        Text(date.formatted(date: .long, time: .shortened))
                            .font(.system(size: 12))
                            .foregroundStyle(AppTheme.softText)
                    }
                }

                Spacer()

                Text("+\(session.earnedXP) XP")
                    .font(.system(size: 14, weight: .heavy, design: .rounded))
                    .foregroundStyle(AppTheme.warmGold)
                    .padding(.horizontal, 10)
                    .padding(.vertical, 5)
                    .background(AppTheme.warmGold.opacity(0.15))
                    .clipShape(Capsule())
            }

            Text(adventure.subtitle)
                .font(.system(size: 13))
                .foregroundStyle(AppTheme.softText)
                .lineLimit(2)

            Divider()
                .background(Color.white.opacity(0.08))

            HStack {
                Text("\(adventure.duration.rawValue) • \(adventure.budget.displayText)")
                    .font(.system(size: 12))
                    .foregroundStyle(AppTheme.softText)

                Spacer()

                Button {
                    AppTheme.hapticSoft()
                    selectedAdventureForReplay = adventure
                } label: {
                    HStack(spacing: 4) {
                        Image(systemName: "arrow.clockwise")
                            .font(.system(size: 12, weight: .bold))
                        Text("Повторить")
                            .font(.system(size: 13, weight: .bold))
                    }
                    .foregroundStyle(AppTheme.warmGold)
                }
            }
        }
        .padding(16)
        .background(AppTheme.cardSurface)
        .clipShape(RoundedRectangle(cornerRadius: 18))
        .overlay(
            RoundedRectangle(cornerRadius: 18)
                .stroke(Color.white.opacity(0.08), lineWidth: 1)
        )
    }

    // MARK: - Вкладка 3: Рубашки карт
    private var scratchThemesListView: some View {
        VStack(alignment: .leading, spacing: 14) {
            Text("Стили защитного слоя (5 тем)")
                .font(.system(size: 15, weight: .bold))
                .foregroundStyle(.white)

            ForEach(ScratchCardTheme.allThemes) { theme in
                let isUnlocked = profile.isThemeUnlocked(theme)
                let isSelected = profile.selectedScratchThemeId == theme.id

                HStack(spacing: 16) {
                    // Превью градиента
                    RoundedRectangle(cornerRadius: 12)
                        .fill(AppTheme.scratchThemeGradient(for: theme.id))
                        .frame(width: 72, height: 72)
                        .overlay(
                            RoundedRectangle(cornerRadius: 12)
                                .stroke(Color.white.opacity(0.3), lineWidth: 1)
                        )
                        .overlay(
                            Image(systemName: isUnlocked ? "sparkles" : "lock.fill")
                                .font(.system(size: 18))
                                .foregroundStyle(isUnlocked ? .white : Color.white.opacity(0.6))
                        )

                    VStack(alignment: .leading, spacing: 4) {
                        Text(theme.name)
                            .font(.system(size: 16, weight: .bold))
                            .foregroundStyle(.white)

                        Text(theme.description)
                            .font(.system(size: 12))
                            .foregroundStyle(AppTheme.softText)
                            .lineLimit(2)

                        Text(theme.requiredXP == 0 ? "Доступно сразу" : "Требуется \(theme.requiredXP) XP")
                            .font(.system(size: 11, weight: .semibold))
                            .foregroundStyle(isUnlocked ? AppTheme.warmGold : AppTheme.softText.opacity(0.5))
                    }

                    Spacer()

                    if isUnlocked {
                        Button {
                            AppTheme.hapticSuccess()
                            profile.selectedScratchThemeId = theme.id
                            profile.updatedAt = .now
                            try? modelContext.save()
                        } label: {
                            Text(isSelected ? "Выбрано" : "Выбрать")
                                .font(.system(size: 13, weight: .bold))
                                .foregroundStyle(isSelected ? .white : AppTheme.warmGold)
                                .padding(.horizontal, 12)
                                .padding(.vertical, 8)
                                .background(isSelected ? AppTheme.heroGradient : Color.white.opacity(0.08))
                                .clipShape(Capsule())
                        }
                    } else {
                        Image(systemName: "lock.fill")
                            .font(.system(size: 16))
                            .foregroundStyle(Color.white.opacity(0.25))
                            .padding(.trailing, 8)
                    }
                }
                .padding(14)
                .background(AppTheme.cardSurface)
                .clipShape(RoundedRectangle(cornerRadius: 18))
                .overlay(
                    RoundedRectangle(cornerRadius: 18)
                        .stroke(isSelected ? AppTheme.primaryRose.opacity(0.6) : Color.white.opacity(0.08), lineWidth: 1.5)
                )
            }
        }
    }

    private func startReplaySession(for adventure: AdventureDefinition) {
        selectedAdventureForReplay = nil
        let newSession = AdventureSessionRecord(adventureId: adventure.id, stepCount: 4)
        modelContext.insert(newSession)
        try? modelContext.save()
    }
}
