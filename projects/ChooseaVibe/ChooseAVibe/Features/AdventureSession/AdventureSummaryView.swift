import SwiftUI

public struct AdventureSummaryView: View {
    public let session: AdventureSessionRecord
    public let adventure: AdventureDefinition
    public let profile: CoupleProfile
    public let unlockedAchievements: [AchievementDefinition]
    public let onReturnToMap: () -> Void

    @State private var appearAnimation: Bool = false

    private let engine = ProgressionEngine.shared

    public init(
        session: AdventureSessionRecord,
        adventure: AdventureDefinition,
        profile: CoupleProfile,
        unlockedAchievements: [AchievementDefinition] = [],
        onReturnToMap: @escaping () -> Void
    ) {
        self.session = session
        self.adventure = adventure
        self.profile = profile
        self.unlockedAchievements = unlockedAchievements
        self.onReturnToMap = onReturnToMap
    }

    public var body: some View {
        ZStack {
            AppTheme.midnightBg
                .ignoresSafeArea()

            // Праздничные световые эффекты
            GeometryReader { geo in
                Circle()
                    .fill(AppTheme.warmGold.opacity(0.18))
                    .frame(width: 300, height: 300)
                    .blur(radius: 70)
                    .offset(x: -60, y: -40)

                Circle()
                    .fill(AppTheme.primaryRose.opacity(0.2))
                    .frame(width: 320, height: 320)
                    .blur(radius: 80)
                    .offset(x: geo.size.width - 200, y: geo.size.height - 300)
            }

            ScrollView(showsIndicators: false) {
                VStack(spacing: 24) {
                    // Иконка кубка / праздника
                    trophyAnimationHeader
                        .padding(.top, 28)

                    // Заголовки финала
                    VStack(spacing: 8) {
                        Text("Приключение завершено!")
                            .font(.system(size: 28, weight: .bold, design: .rounded))
                            .foregroundStyle(.white)
                            .multilineTextAlignment(.center)

                        Text("«\(adventure.title)»")
                            .font(.system(size: 18, weight: .semibold))
                            .foregroundStyle(AppTheme.warmGold)

                        Text("Вы провели этот вечер вместе и создали новое общее воспоминание.")
                            .font(.system(size: 14))
                            .foregroundStyle(AppTheme.softText)
                            .multilineTextAlignment(.center)
                            .padding(.horizontal, 24)
                    }

                    // Карточка начисленного XP
                    earnedXPBreakdownCard

                    // Шкала прогресса и уровень
                    levelProgressCard

                    // Новые достижения (если есть)
                    if !unlockedAchievements.isEmpty {
                        newAchievementsSection
                    }

                    // Кнопка возврата
                    Button {
                        AppTheme.hapticSuccess()
                        onReturnToMap()
                    } label: {
                        HStack(spacing: 8) {
                            Image(systemName: "map.fill")
                                .font(.system(size: 16))

                            Text("Вернуться на карту")
                                .font(.system(size: 17, weight: .bold))
                        }
                        .foregroundStyle(.white)
                        .frame(maxWidth: .infinity)
                        .frame(height: 56)
                        .background(AppTheme.heroGradient)
                        .clipShape(RoundedRectangle(cornerRadius: 16))
                        .shadow(color: AppTheme.primaryRose.opacity(0.4), radius: 12, y: 5)
                    }
                    .padding(.top, 12)
                    .padding(.bottom, 28)
                }
                .padding(.horizontal, 20)
            }
        }
        .onAppear {
            withAnimation(.spring(duration: 0.7)) {
                appearAnimation = true
            }
        }
    }

    // MARK: - Праздничный заголовок с кубком
    private var trophyAnimationHeader: some View {
        ZStack {
            Circle()
                .fill(AppTheme.goldGradient.opacity(0.25))
                .frame(width: 130, height: 130)
                .scaleEffect(appearAnimation ? 1.05 : 0.8)
                .animation(.easeInOut(duration: 1.2).repeatForever(autoreverses: true), value: appearAnimation)

            Image(systemName: "trophy.fill")
                .font(.system(size: 60))
                .foregroundStyle(AppTheme.goldGradient)
                .shadow(color: AppTheme.warmGold.opacity(0.5), radius: 14)
        }
    }

    // MARK: - Карточка опыта XP
    private var earnedXPBreakdownCard: some View {
        VStack(spacing: 14) {
            HStack {
                Text("Заработано за вечер")
                    .font(.system(size: 15, weight: .bold))
                    .foregroundStyle(.white)

                Spacer()

                Text("+\(session.earnedXP) XP")
                    .font(.system(size: 22, weight: .heavy, design: .rounded))
                    .foregroundStyle(AppTheme.warmGold)
            }

            Divider()
                .background(Color.white.opacity(0.1))

            VStack(spacing: 8) {
                let completedCardsCount = session.stepStatuses.filter { $0 == "completed" }.count
                HStack {
                    Text("Выполнено заданий: \(completedCardsCount) из 4")
                        .font(.system(size: 13))
                        .foregroundStyle(AppTheme.softText)

                    Spacer()

                    Text("+\(completedCardsCount * 10) XP")
                        .font(.system(size: 13, weight: .bold))
                        .foregroundStyle(.white)
                }

                HStack {
                    Text("Бонус за успешный финал приключения")
                        .font(.system(size: 13))
                        .foregroundStyle(AppTheme.softText)

                    Spacer()

                    Text("+25 XP")
                        .font(.system(size: 13, weight: .bold))
                        .foregroundStyle(AppTheme.warmGold)
                }
            }
        }
        .padding(18)
        .background(AppTheme.cardSurface)
        .clipShape(RoundedRectangle(cornerRadius: 20))
        .overlay(
            RoundedRectangle(cornerRadius: 20)
                .stroke(AppTheme.warmGold.opacity(0.35), lineWidth: 1)
        )
    }

    // MARK: - Шкала уровня
    private var levelProgressCard: some View {
        let levelInfo = engine.level(for: profile.totalXP)
        let progress = levelInfo.progressPercentage(currentXP: profile.totalXP)

        return VStack(spacing: 12) {
            HStack {
                VStack(alignment: .leading, spacing: 3) {
                    Text("Уровень пары")
                        .font(.system(size: 12))
                        .foregroundStyle(AppTheme.softText)

                    Text("Уровень \(levelInfo.level) · \(levelInfo.title)")
                        .font(.system(size: 16, weight: .bold))
                        .foregroundStyle(.white)
                }

                Spacer()

                if let nextXP = levelInfo.nextLevelXP {
                    Text("\(profile.totalXP) / \(nextXP) XP")
                        .font(.system(size: 13, weight: .bold, design: .monospaced))
                        .foregroundStyle(AppTheme.warmGold)
                } else {
                    Text("Макс. уровень")
                        .font(.system(size: 13, weight: .bold))
                        .foregroundStyle(AppTheme.warmGold)
                }
            }

            GeometryReader { geo in
                ZStack(alignment: .leading) {
                    Capsule()
                        .fill(Color.white.opacity(0.12))
                        .frame(height: 10)

                    Capsule()
                        .fill(AppTheme.heroGradient)
                        .frame(width: geo.size.width * CGFloat(progress), height: 10)
                        .animation(.spring(duration: 0.6), value: progress)
                }
            }
            .frame(height: 10)
        }
        .padding(18)
        .background(AppTheme.cardSurface)
        .clipShape(RoundedRectangle(cornerRadius: 20))
    }

    // MARK: - Новые достижения
    private var newAchievementsSection: some View {
        VStack(alignment: .leading, spacing: 12) {
            Label("Открыты новые значки!", systemImage: "sparkles")
                .font(.system(size: 16, weight: .bold))
                .foregroundStyle(AppTheme.warmGold)

            VStack(spacing: 10) {
                ForEach(unlockedAchievements) { ach in
                    HStack(spacing: 14) {
                        ZStack {
                            Circle()
                                .fill(AppTheme.goldGradient)
                                .frame(width: 42, height: 42)

                            Image(systemName: ach.iconName)
                                .font(.system(size: 20))
                                .foregroundStyle(.black)
                        }

                        VStack(alignment: .leading, spacing: 2) {
                            Text(ach.title)
                                .font(.system(size: 15, weight: .bold))
                                .foregroundStyle(.white)

                            Text(ach.description)
                                .font(.system(size: 12))
                                .foregroundStyle(AppTheme.softText)
                        }

                        Spacer()

                        Text("+\(ach.xpReward) XP")
                            .font(.system(size: 12, weight: .bold))
                            .foregroundStyle(AppTheme.warmGold)
                    }
                    .padding(12)
                    .background(Color(red: 0.22, green: 0.18, blue: 0.12))
                    .clipShape(RoundedRectangle(cornerRadius: 14))
                }
            }
        }
    }
}
