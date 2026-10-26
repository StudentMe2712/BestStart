import SwiftUI

public struct AdventureBriefingView: View {
    @Environment(\.dismiss) private var dismiss

    public let adventure: AdventureDefinition
    public let profile: CoupleProfile
    public let isFallbackMatch: Bool
    public let onStart: () -> Void
    public let onPickAnother: () -> Void

    public init(
        adventure: AdventureDefinition,
        profile: CoupleProfile,
        isFallbackMatch: Bool = false,
        onStart: @escaping () -> Void,
        onPickAnother: @escaping () -> Void
    ) {
        self.adventure = adventure
        self.profile = profile
        self.isFallbackMatch = isFallbackMatch
        self.onStart = onStart
        self.onPickAnother = onPickAnother
    }

    public var body: some View {
        NavigationStack {
            ZStack {
                AppTheme.midnightBg
                    .ignoresSafeArea()

                ScrollView(showsIndicators: false) {
                    VStack(spacing: 24) {
                        // Конверт-карточка
                        envelopeCard
                            .padding(.top, 12)

                        // Мягкая плашка фолбэка (если применимо)
                        if isFallbackMatch {
                            fallbackNoticeView
                        }

                        // Блок подготовки ("Что понадобится")
                        preparationSection

                        // Блок "Как это работает"
                        rulesNoticeSection

                        // Кнопки действий
                        actionButtons
                            .padding(.bottom, 24)
                    }
                    .padding(.horizontal, 20)
                }
            }
            .navigationTitle("Секретный конверт")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button("Отмена") {
                        dismiss()
                    }
                    .foregroundStyle(AppTheme.softText)
                }
            }
        }
    }

    // MARK: - Карточка конверта
    private var envelopeCard: some View {
        VStack(alignment: .leading, spacing: 18) {
            // Верхняя плашка с номером
            HStack {
                HStack(spacing: 6) {
                    Image(systemName: "envelope.fill")
                        .font(.system(size: 14))
                        .foregroundStyle(AppTheme.warmGold)

                    Text("Приключение #\(adventure.orderInChapter)")
                        .font(.system(size: 13, weight: .bold, design: .rounded))
                        .foregroundStyle(AppTheme.warmGold)
                }

                Spacer()

                HStack(spacing: 4) {
                    Image(systemName: "sparkles")
                        .font(.system(size: 12))
                    Text("4 этапа")
                        .font(.system(size: 12, weight: .semibold))
                }
                .foregroundStyle(AppTheme.softText)
                .padding(.horizontal, 8)
                .padding(.vertical, 4)
                .background(Color.white.opacity(0.06))
                .clipShape(Capsule())
            }

            // Название и тизер
            VStack(alignment: .leading, spacing: 8) {
                Text(adventure.title)
                    .font(.system(size: 26, weight: .bold, design: .rounded))
                    .foregroundStyle(.white)

                Text(adventure.subtitle)
                    .font(.system(size: 15))
                    .foregroundStyle(AppTheme.softText)
                    .lineSpacing(3)
            }

            Divider()
                .background(Color.white.opacity(0.1))

            // Чипы параметров
            FlowBadgesLayout(spacing: 8) {
                ForEach(adventure.vibes) { vibe in
                    badgeChip(title: vibe.rawValue, icon: vibe.systemIcon, color: AppTheme.primaryRose)
                }

                badgeChip(title: adventure.duration.rawValue, icon: adventure.duration.systemIcon, color: .orange)
                badgeChip(title: adventure.budget.displayText, icon: "banknote", color: AppTheme.warmGold)
                badgeChip(title: adventure.location.rawValue, icon: adventure.location.systemIcon, color: .purple)
            }
        }
        .padding(22)
        .background(
            LinearGradient(
                colors: [AppTheme.cardSurfaceLight, AppTheme.cardSurface],
                startPoint: .topLeading,
                endPoint: .bottomTrailing
            )
        )
        .clipShape(RoundedRectangle(cornerRadius: 22))
        .overlay(
            RoundedRectangle(cornerRadius: 22)
                .stroke(AppTheme.warmGold.opacity(0.4), lineWidth: 1.5)
        )
        .shadow(color: AppTheme.warmGold.opacity(0.12), radius: 14, y: 6)
    }

    private func badgeChip(title: String, icon: String, color: Color) -> some View {
        HStack(spacing: 6) {
            Image(systemName: icon)
                .font(.system(size: 12))
            Text(title)
                .font(.system(size: 12, weight: .semibold))
        }
        .foregroundStyle(color)
        .padding(.horizontal, 10)
        .padding(.vertical, 6)
        .background(color.opacity(0.12))
        .clipShape(Capsule())
    }

    // MARK: - Плашка фолбэка
    private var fallbackNoticeView: some View {
        HStack(spacing: 12) {
            Image(systemName: "info.circle.fill")
                .font(.system(size: 18))
                .foregroundStyle(AppTheme.warmGold)

            Text("Подобрали ближайшее приключение под ваш вечер с учётом доступных карточек.")
                .font(.system(size: 13, weight: .medium))
                .foregroundStyle(AppTheme.softText)
                .lineSpacing(2)
        }
        .padding(14)
        .background(AppTheme.warmGold.opacity(0.12))
        .clipShape(RoundedRectangle(cornerRadius: 14))
        .overlay(
            RoundedRectangle(cornerRadius: 14)
                .stroke(AppTheme.warmGold.opacity(0.3), lineWidth: 1)
        )
    }

    // MARK: - Подготовка
    private var preparationSection: some View {
        VStack(alignment: .leading, spacing: 10) {
            Label("Что понадобится", systemImage: "checklist")
                .font(.system(size: 16, weight: .bold))
                .foregroundStyle(.white)

            Text(adventure.preparationNote)
                .font(.system(size: 14))
                .foregroundStyle(AppTheme.softText)
                .lineSpacing(3)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(18)
        .background(AppTheme.cardSurface)
        .clipShape(RoundedRectangle(cornerRadius: 18))
    }

    // MARK: - Правила сессии
    private var rulesNoticeSection: some View {
        VStack(alignment: .leading, spacing: 10) {
            Label("Очередь ходов", systemImage: "arrow.triangle.2.circlepath")
                .font(.system(size: 15, weight: .bold))
                .foregroundStyle(.white)

            Text("• Нечётные этапы (1 и 3) стирает \(profile.partnerOneName).\n• Чётные этапы (2 и 4) стирает \(profile.partnerTwoName).\n• Если задание покажется некомфортным, можно бесплатно перевернуть карточку на «Другой вариант».")
                .font(.system(size: 13))
                .foregroundStyle(AppTheme.softText)
                .lineSpacing(3)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(18)
        .background(AppTheme.cardSurface.opacity(0.6))
        .clipShape(RoundedRectangle(cornerRadius: 18))
    }

    // MARK: - Кнопки действий
    private var actionButtons: some View {
        VStack(spacing: 12) {
            Button {
                AppTheme.hapticSuccess()
                onStart()
            } label: {
                HStack(spacing: 10) {
                    Image(systemName: "play.fill")
                        .font(.system(size: 16))

                    Text("Начать приключение")
                        .font(.system(size: 17, weight: .bold))
                }
                .foregroundStyle(.white)
                .frame(maxWidth: .infinity)
                .frame(height: 56)
                .background(AppTheme.heroGradient)
                .clipShape(RoundedRectangle(cornerRadius: 16))
                .shadow(color: AppTheme.primaryRose.opacity(0.4), radius: 12, y: 5)
            }

            Button {
                AppTheme.hapticSoft()
                onPickAnother()
            } label: {
                Text("Подобрать другое")
                    .font(.system(size: 15, weight: .semibold))
                    .foregroundStyle(AppTheme.softText)
                    .frame(maxWidth: .infinity)
                    .frame(height: 48)
                    .background(AppTheme.cardSurface)
                    .clipShape(RoundedRectangle(cornerRadius: 14))
            }
        }
    }
}

// Вспомогательный простой FlowLayout для бейджей
private struct FlowBadgesLayout: Layout {
    var spacing: CGFloat = 8

    func sizeThatFits(proposal: ProposedViewSize, subviews: Subviews, cache: inout ()) -> CGSize {
        let width = proposal.width ?? 320
        var height: CGFloat = 0
        var currentX: CGFloat = 0
        var rowHeight: CGFloat = 0

        for subview in subviews {
            let size = subview.sizeThatFits(.unspecified)
            if currentX + size.width > width {
                currentX = 0
                height += rowHeight + spacing
                rowHeight = 0
            }
            currentX += size.width + spacing
            rowHeight = max(rowHeight, size.height)
        }
        height += rowHeight
        return CGSize(width: width, height: height)
    }

    func placeSubviews(in bounds: CGRect, proposal: ProposedViewSize, subviews: Subviews, cache: inout ()) {
        var currentX = bounds.minX
        var currentY = bounds.minY
        var rowHeight: CGFloat = 0

        for subview in subviews {
            let size = subview.sizeThatFits(.unspecified)
            if currentX + size.width > bounds.maxX {
                currentX = bounds.minX
                currentY += rowHeight + spacing
                rowHeight = 0
            }
            subview.place(at: CGPoint(x: currentX, y: currentY), proposal: .unspecified)
            currentX += size.width + spacing
            rowHeight = max(rowHeight, size.height)
        }
    }
}
