//
//  CarteBlancheApp.swift
//  CarteBlanche
//
//  Created for Carte Blanche - Offline Luxe Menu & Tasting Guide
//  iOS 17.0+ SwiftUI
//

import SwiftUI

@main
struct CarteBlancheApp: App {
    var body: some Scene {
        WindowGroup {
            MenuHomeView()
                .preferredColorScheme(.dark)
        }
    }
}

// MARK: - Interactive Preview

#Preview("Carte Blanche App Root") {
    MenuHomeView()
        .preferredColorScheme(.dark)
}
