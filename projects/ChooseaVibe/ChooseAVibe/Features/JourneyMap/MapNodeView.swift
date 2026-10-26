import SwiftUI

public enum MapNodeState {
    case completed
    case active
    case locked
}

/// Сферический стеклянный узел карты приключений (68x68, Apple HIG)
public struct MapNodeView: View {
    public let questNumber: Int
    public let title: String
    public let vibes: [VibeTag]
    public let state: MapNodeState
    public let action: () -> Void

    @State private var isPulsing: Bool = false

    public init(
        questNumber: Int,
        title: String,
        vibes: [VibeTag] = [],
        state: MapNodeState,
        action: @escaping () -> Void
    ) {
        self.questNumber = questNumber
        self.title = title
        self.vibes = vibes
        self.state = state
        self.action = action
    }

    public var body: some View {
        Button {
            if state != .locked {
                AppTheme.hapticSoft()
                action()
            } else {
                AppTheme.hapticWarning()
            }
        } label: {
            VStack(spacing: 8) {
                ZStack {
                    // Внешний пульсирующий ореол для активного узла
                    if state == .active {
                        Circle()
                            .stroke(AppTheme.primaryRose.opacity(0.4), lineWidth: 8)
                            .frame(width: 86, height: 86)
                            .scaleEffect(isPulsing ? 1.15 : 0.95)
                            .opacity(isPulsing ? 0.3 : 0.8)
                            .animation(.easeInOut(duration: 1.2).repeatForever(autoreverses: true), value: isPulsing)
                    }

                    // Сферический стеклянный узел (68x68)
                    Circle()
                        .fill(sphericalGradient)
                        .frame(width: 68, height: 68)
                        .overlay(
                            // Верхний стеклянный блик
                            Circle()
                                .stroke(
                                    LinearGradient(
                                        colors: [Color.white.opacity(0.6), Color.white.opacity(0.05)],
                                        startPoint: .topLeading,
                                        endPoint: .bottomTrailing
                                    ),
                                    lineWidth: 1.5
                                )
                        )
                        .overlay(
                            Circle()
                                .stroke(nodeBorderColor, lineWidth: state == .completed ? 2.5 : 1.5)
                        )
                        .shadow(color: nodeGlowColor, radius: state == .active ? 14 : 6, y: 2)

                    // Центральная иконка (галочка / сияющая звезда / замок)
                    nodeIcon
                }

                // Чистый заголовок квеста снизу без лишних чипов
                VStack(spacing: 2) {
                    Text("Квест #\(questNumber)")
                        .font(.system(size: 11, weight: .bold, design: .rounded))
                        .foregroundStyle(statusLabelColor)

                    Text(title)
                        .font(.system(size: 13, weight: .semibold, design: .rounded))
                        .foregroundStyle(state == .locked ? AppTheme.softText.opacity(0.6) : .white)
                        .multilineTextAlignment(.center)
                        .lineLimit(2)
                        .frame(maxWidth: 130)
                }
            }
        }
        .buttonStyle(.plain)
        .onAppear {
            if state == .active {
                isPulsing = true
            }
        }
    }

    // MARK: - Сферический градиент (Стеклянная сфера)
    private var sphericalGradient: RadialGradient {
        switch state {
        case .completed:
            return RadialGradient(
                colors: [
                    Color(red: 0.95, green: 0.80, blue: 0.35),
                    Color(red: 0.70, green: 0.50, blue: 0.12),
                    Color(red: 0.32, green: 0.22, blue: 0.05)
                ],
                center: .init(x: 0.35, y: 0.3),
                startRadius: 4,
                endRadius: 42
            )
        case .active:
            return RadialGradient(
                colors: [
                    Color(red: 1.00, green: 0.45, blue: 0.65),
                    Color(red: 0.95, green: 0.20, blue: 0.42),
                    Color(red: 0.45, green: 0.15, blue: 0.55)
                ],
                center: .init(x: 0.35, y: 0.3),
                startRadius: 4,
                endRadius: 42
            )
        case .locked:
            return RadialGradient(
                colors: [
                    Color(red: 0.25, green: 0.25, blue: 0.35),
                    Color(red: 0.14, green: 0.14, blue: 0.22),
                    Color(red: 0.08, green: 0.08, blue: 0.14)
                ],
                center: .init(x: 0.35, y: 0.3),
                startRadius: 4,
                endRadius: 42
            )
        }
    }

    private var nodeBorderColor: Color {
        switch state {
        case .completed:
            return AppTheme.warmGold
        case .active:
            return AppTheme.primaryRose
        case .locked:
            return Color.white.opacity(0.12)
        }
    }

    private var nodeGlowColor: Color {
        switch state {
        case .completed:
            return AppTheme.warmGold.opacity(0.4)
        case .active:
            return AppTheme.primaryRose.opacity(0.65)
        case .locked:
            return Color.clear
        }
    }

    private var statusLabelColor: Color {
        switch state {
        case .completed:
            return AppTheme.warmGold
        case .active:
            return AppTheme.primaryRose
        case .locked:
            return AppTheme.softText.opacity(0.6)
        }
    }

    // MARK: - Центральная иконка (галочка / светящаяся звезда / замок)
    @ViewBuilder
    private var nodeIcon: some View {
        switch state {
        case .completed:
            Image(systemName: "checkmark")
                .font(.system(size: 22, weight: .black))
                .foregroundStyle(.black)
                .shadow(color: Color.white.opacity(0.5), radius: 2)

        case .active:
            Image(systemName: "star.fill")
                .font(.system(size: 24, weight: .black))
                .foregroundStyle(.white)
                .shadow(color: Color.white.opacity(0.8), radius: 6)

        case .locked:
            Image(systemName: "lock.fill")
                .font(.system(size: 20))
                .foregroundStyle(Color.white.opacity(0.4))
        }
    }
}
