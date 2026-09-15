//
//  FlavorProfile.swift
//  CarteBlanche
//
//  Created for Carte Blanche - Offline Luxe Menu & Tasting Guide
//  iOS 17.0+ SwiftUI
//

import Foundation
import SwiftUI

/// 5-axis sensory taste profile (values normalized between 0.0 and 1.0)
public struct FlavorProfile: Codable, Hashable, Sendable {
    public var umami: Double
    public var acidity: Double
    public var sweetness: Double
    public var spiciness: Double
    public var texture: Double

    public init(
        umami: Double,
        acidity: Double,
        sweetness: Double,
        spiciness: Double,
        texture: Double
    ) {
        self.umami = min(max(umami, 0.0), 1.0)
        self.acidity = min(max(acidity, 0.0), 1.0)
        self.sweetness = min(max(sweetness, 0.0), 1.0)
        self.spiciness = min(max(spiciness, 0.0), 1.0)
        self.texture = min(max(texture, 0.0), 1.0)
    }

    // MARK: - Static Presets

    public static let balanced = FlavorProfile(
        umami: 0.50,
        acidity: 0.50,
        sweetness: 0.50,
        spiciness: 0.30,
        texture: 0.50
    )

    public static let rich = FlavorProfile(
        umami: 0.90,
        acidity: 0.30,
        sweetness: 0.35,
        spiciness: 0.20,
        texture: 0.85
    )

    public static let lightFresh = FlavorProfile(
        umami: 0.40,
        acidity: 0.85,
        sweetness: 0.30,
        spiciness: 0.15,
        texture: 0.65
    )

    public static let sweetIntense = FlavorProfile(
        umami: 0.20,
        acidity: 0.45,
        sweetness: 0.92,
        spiciness: 0.10,
        texture: 0.70
    )

    // MARK: - Sensory Axes Helper

    public enum Axis: String, CaseIterable, Identifiable {
        case umami = "Umami"
        case acidity = "Acidity"
        case sweetness = "Sweetness"
        case spiciness = "Spiciness"
        case texture = "Texture"

        public var id: String { rawValue }

        public var titleRu: String {
            switch self {
            case .umami: return "Умами"
            case .acidity: return "Кислотность"
            case .sweetness: return "Сладость"
            case .spiciness: return "Пряность"
            case .texture: return "Текстура"
            }
        }
    }

    public func value(for axis: Axis) -> Double {
        switch axis {
        case .umami: return umami
        case .acidity: return acidity
        case .sweetness: return sweetness
        case .spiciness: return spiciness
        case .texture: return texture
        }
    }
}

// MARK: - Interactive Preview

#Preview("FlavorProfile Presets") {
    ZStack {
        Color(red: 0.055, green: 0.063, blue: 0.075).ignoresSafeArea()

        ScrollView {
            VStack(alignment: .leading, spacing: 16) {
                Text("FLAVOR PROFILE PRESETS (5-AXIS)")
                    .font(.system(size: 11, weight: .bold, design: .monospaced))
                    .foregroundStyle(Color(red: 0.90, green: 0.66, blue: 0.38))

                let presets: [(name: String, profile: FlavorProfile)] = [
                    ("Balanced", .balanced),
                    ("Rich", .rich),
                    ("Light & Fresh", .lightFresh),
                    ("Sweet & Intense", .sweetIntense)
                ]

                ForEach(presets, id: \.name) { preset in
                    VStack(alignment: .leading, spacing: 8) {
                        Text(preset.name)
                            .font(.system(.headline, design: .serif, weight: .bold))
                            .foregroundStyle(Color(red: 0.96, green: 0.96, blue: 0.97))

                        ForEach(FlavorProfile.Axis.allCases) { axis in
                            HStack {
                                Text("\(axis.titleRu) (\(axis.rawValue))")
                                    .font(.system(size: 12))
                                    .foregroundStyle(Color.gray)
                                Spacer()
                                Text(String(format: "%.0f%%", preset.profile.value(for: axis) * 100))
                                    .font(.system(size: 12, weight: .bold, design: .monospaced))
                                    .foregroundStyle(Color(red: 0.90, green: 0.66, blue: 0.38))
                            }
                        }
                    }
                    .padding(14)
                    .background(Color(red: 0.086, green: 0.098, blue: 0.122))
                    .clipShape(RoundedRectangle(cornerRadius: 12))
                }
            }
            .padding()
        }
    }
}
