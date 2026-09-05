# ADR 0005: Deterministic Score-Based Matching Engine

- **Status:** Accepted
- **Date:** 2026-09-05
- **Deciders:** UniversalMediaPlayer Architecture Team

---

## 1. Context

When a video file is opened, related external audio files, subtitle files, and fonts must be discovered in the file system and accurately paired. 

Relying on naive string equality (e.g. `video.mkv` == `video.srt`) fails completely for real-world releases where file names include complex release tags, language codes, season/episode numbering variants, and dub group credits:
- Video: `[SubsPlease] Sousou no Frieren - 03 (1080p) [9A1B2C3D].mkv`
- Audio: `Frieren_E03_RU_AniLibria.mka`
- Subtitles: `[SubsPlease] Sousou no Frieren - 03 [RU].ass`

Conversely, aggressive fuzzy matching can accidentally attach audio or subtitles from an entirely different movie or episode in the same folder.

---

## 2. Decision

We implement a **Deterministic Score-Based Matching Engine (`MatchEngine`)** with conservative threshold gating.

### Matching Algorithm Pipeline:

1. **Tokenization & Release Tag Stripping:**
   - Extract title, season, and episode using regex patterns (`S\d+E\d+`, `\d+x\d+`, `Episode \d+`, `E\d+`, `\d{2}`).
   - Strip recognized release metadata tokens without destroying the root title:
     - Resolutions: `1080p`, `720p`, `2160p`, `4K`, `UHD`.
     - Sources: `WEB-DL`, `WEBRip`, `BluRay`, `BDRip`, `HDTV`, `DVDRip`.
     - Codecs: `x264`, `x265`, `HEVC`, `AV1`, `AVC`, `10bit`.
     - Audio tags: `AAC`, `AC3`, `FLAC`, `DTS`, `Opus`, `DDP5.1`.
     - Hashes: `[8-char hex CRC32]`.

2. **Language Normalization (`LanguageDetector`):**
   - Identify language markers from file suffixes and token boundaries:
     - Russian: `ru`, `rus`, `russian`, `рус`, `русский` -> canonical `ru` (ISO 639-1).
     - English: `en`, `eng`, `english` -> canonical `en`.
     - Japanese: `ja`, `jp`, `jpn`, `japanese` -> canonical `ja`.
     - Additional major languages normalized via standard lookup table.

3. **Composite Scoring (0 to 100 points):**
   - **Episode Number Match (Crucial Gate):**
     - If both video and candidate have episode numbers: Exact match = `+40` pts. Mismatch = `FATAL (-100 pts)`.
   - **Season Match:**
     - Exact match = `+15` pts. Mismatch = `FATAL (-100 pts)`.
   - **Title Stem Similarity (Normalized Levenshtein / Jaccard):**
     - 0 to `+30` pts proportional to token overlap.
   - **Directory Proximity:**
     - Sibling in same directory = `+10` pts.
     - Sibling in dedicated subfolder (e.g. `Subs/`, `Subtitles/`, `Audio/`) = `+8` pts.
   - **Language Specification:**
     - Valid recognized language token = `+5` pts.

4. **Attachment Decision Policy:**
   - **Score >= 95:** **High Confidence Auto-Attach.** Automatically attached to the active playback session.
   - **Score 80 - 94:** **Likely Match.** Auto-attached, marked as secondary candidate.
   - **Score 50 - 79:** **Possible Match.** Listed in track selector with visual indicator; not activated by default.
   - **Score < 50:** **Rejected.** Ignored to prevent pollution.

---

## 3. Consequences

### Positive:
- Zero false-positive episode attachments (an episode 04 subtitle will NEVER attach to episode 03).
- Handles anime releases, multi-episode torrent folders, and distinct dub group namings effortlessly.
- 100% deterministic and unit-testable.

### Negative:
- Files with severely corrupted names or no discernible episode/title markers may require manual user attachment via drag-and-drop.
