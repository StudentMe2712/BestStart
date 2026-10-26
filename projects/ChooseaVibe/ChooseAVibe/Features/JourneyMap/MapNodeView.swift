import SwiftUI

public enum MapNodeState {
    case completed
    case active
    case locked
}

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
                            .frame(width: 82, height: 82)
                            .scaleEffect(isPulsing ? 1.15 : 0.95)
                            .opacity(isPulsing ? 0.3 : 0.8)
                            .animation(.easeInOut(duration: 1.2).repeatForever(autoreverses: true), value: isPulsing)
                    }

                    // Фоновый круг узла
                    Circle()
                        .fill(nodeBackground)
                        .frame(width: 66, height: 66)
                        .overlay(
                            Circle()
                                .stroke(nodeBorderColor, lineWidth: state == .completed ? 3 : 2)
                        )
                        .shadow(color: nodeGlowColor, radius: state == .active ? 12 : 4)

                    // Центральная иконка
                    nodeIcon
                }

                // Индикатор и подпись
                VStack(spacing: 2) {
                    Text("Квест #\(questNumber)")
                        .font(.system(size: 11, weight: .bold, design: .rounded))
                        .foregroundStyle(statusLabelColor)

                    Text(title)
                        .font(.system(size: 13, weight: .semibold))
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

    // MARK: - Стили узла
    private var nodeBackground: Color {
        switch state {
        case .completed:
            return Color(red: 0.22, green: 0.18, blue: 0.10)
        case .active:
            return AppTheme.cardSurfaceLight
        case .locked:
            return AppTheme.cardSurface.opacity(0.6)
        }
    }

    private var nodeBorderColor: Color {
        switch state {
        case .completed:
            return AppTheme.warmGold
        case .active:
            return AppTheme.primaryRose
        case .locked:
            return Color.white.opacity(0.15)
        }
    }

    private var nodeGlowColor: Color {
        switch state {
        case .completed:
            return AppTheme.warmGold.opacity(0.3)
        case .active:
            return AppTheme.primaryRose.opacity(0.6)
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

    @ViewBuilder
    private var nodeIcon: some View {
        switch state {
        case .completed:
            ZStack {
                Circle()
                    .fill(AppTheme.goldGradient)
                    .frame(width: 32, height: 32)

                Image(systemName: "checkmark")
                    .font(.system(size: 16, weight: .heavy))
                    .foregroundStyle(.black)
            }
        case .active:
            Image(systemName: "sparkles")
                .font(.system(size: 24, weight: .bold))
                .foregroundStyle(AppTheme.heroGradient)
        case .locked:
            Image(systemName: "lock.fill")
                .font(.system(size: 20))
                .foregroundStyle(Color.white.opacity(0.35))
        }
    }
}
