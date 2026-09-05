# UniversalMediaPlayer: Media Format Compatibility & Demuxing/Decoding Analysis

**Document Version:** 1.0.0  
**Status:** Authoritative Engineering Reference  
**Author:** Senior Media Technology Researcher & Systems Engineer  
**Target Platform:** Windows 10/11 (x64, ARM64)  
**Primary Engines:** `libmpv` (FFmpeg / libplacebo / libass) vs. DirectShow / Media Foundation (LAV Filters, MPC-BE custom splitters/renderers)

---

## Executive Summary

The UniversalMediaPlayer project requires universal playback resilience across four decades of digital media formats: from legacy multimedia container standards of the 1990s (AVI, Indeo, Cinepak, MPEG-1) to cutting-edge broadcast and streaming formats (AV1, HEVC, Dolby Vision Profile 5/7/8, VP9, Opus, E-AC-3 JOC/Atmos).

This reference provides an exhaustive, low-level technical analysis of:
1. **Container Architectures:** Byte-level framing, atom/box hierarchies, EBML trees, transport packet PES syntax, chunk-based layouts, variable frame rates (VFR), and header recovery mechanisms.
2. **Video Compression Standards:** Bitstream syntax (NAL units, OBUs, macroblocks), transforms, profile limits, hardware acceleration decoders (DXVA2, D3D11VA, D3D12 Video Decode), high bit-depth rasterization, and dynamic HDR metadata pipelines.
3. **Audio Architectures:** Lossless and lossy bitstreams, frame-synchronization patterns, packet payload parsing, spatial object reconstruction, downmixing matrices, and WASAPI hardware bitstreaming.
4. **Subtitle Systems:** Vector-drawn script-driven ASS/SSA rendering engines (libass styling, glyph caching, font memory loading), HTML/XML-like timed text (SRT, WebVTT, SAMI), and hardware-accelerated bitmap run-length decoders (PGS, VobSub).
5. **Engine Comparative Matrix:** Direct format-by-format capability evaluation between `libmpv` and the DirectShow ecosystem (LAV Filters / MPC-BE).
6. **Error Concealment & Repair:** Algorithms for repairing truncated payloads, reconstructing corrupted indexes, handling asynchronous clock drift, audio packet interleaving skew, and resolving SAR/DAR display aspect ratio conflicts.

---

## 1. Container Formats Analysis & Demuxing Engineering

Container formats encapsulate multiplexed elementary streams (video, audio, subtitles, chapters, telemetry) along with timing and metadata. They fall into five fundamental structural taxonomies:

```
                               CONTAINER TAXONOMY
                                       │
     ┌─────────────────┬───────────────┴───────────────┬─────────────────┐
     │                 │                               │                 │
Box/Atom-Based    EBML-Based                    Packetized Stream   Chunk/Tag-Based
(ISOBMFF/MOV)    (Matroska/WebM)                   (MPEG-TS/PS)     (RIFF/Ogg/ASF/FLV)
  - MP4, M4A       - MKV, MKA                      - TS, M2TS        - AVI, WAV
  - MOV, 3GP       - WebM                          - VOB, MPG        - FLV, RM/RMVB
                                                   - ISO, BDMV       - OGG, OGV, Opus
```

### 1.1 Modern Containers

#### 1.1.1 Matroska (MKV) & WebM
* **Structural Paradigm:** Extensible Binary Meta Language (EBML), a binary equivalent of XML using Variable Size Integer (VINT) descriptors for element IDs and sizes.
* **Header & Box Layout:**
  * `EBML Header` (ID `0x1A45DFA3`): Identifies EBMLVersion, DocType (`matroska` or `webm`), DocTypeVersion.
  * `Segment` (ID `0x18538067`): Root container spanning the payload.
    * `SeekHead` (`0x114D9B74`): Array of `Seek` pointers indexing child elements (`Info`, `Tracks`, `Cues`, `Chapters`, `Attachments`).
    * `Info` (`0x1549A966`): Contains `TimestampScale` (default 1,000,000 ns = 1 ms precision), `Duration`, `MuxingApp`.
    * `Tracks` (`0x1654AE6B`): Stream definitions. Each `TrackEntry` defines `TrackNumber`, `TrackUID`, `TrackType` (1=video, 2=audio, 17=subtitle), `CodecID` (e.g., `V_MPEGH/ISO/HEVC`, `A_OPUS`, `S_TEXT/ASS`), and `CodecPrivate` (extradata).
    * `Cluster` (`0x1F43B675`): Grouping of frames for continuous playback and seeking.
      * `Timestamp` (`0xE7`): Absolute cluster timecode offset relative to segment timecode.
      * `SimpleBlock` (`0xA3`) or `BlockGroup` (`0xA0`): Carries individual packet bitstreams.
    * `Cues` (`0x1C53BB6B`): Keyframe index mapping absolute timestamps to cluster byte offsets (`CueTrackPositions`).
* **Demuxing Complexity:**
  * **VINT Decoding:** High-order zero bits determine byte width. `1xxxxxxx` = 1 byte (value $0 \dots 127$), `01xxxxxx` = 2 bytes, down to `00000001` = 8 bytes. All-1s payload indicates undefined size (streaming/live capture).
  * **Block Lacing:** Multiple compressed frames can be laced in a single `Block`:
    1. *Xiph lacing:* Bytes coded in 255-byte runs ($255 + 255 + \dots + n$).
    2. *EBML lacing:* First frame size coded as VINT; subsequent frames coded as signed VINT deltas.
    3. *Fixed-size lacing:* Header specifies total count; all frames have identical byte lengths.
  * **WebM Restrictions:** Restricted subset of Matroska. EBML DocType = `webm`. Audio limited to Vorbis or Opus. Video limited to VP8, VP9, or AV1. Subtitles limited to WebVTT (`D_WEBVTT/SUBTITLES`). No attachments or arbitrary binary payloads permitted.

```c
// Example EBML VINT decoding routine
uint64_t read_ebml_vint(const uint8_t *buffer, size_t max_len, size_t *vint_len) {
    if (max_len == 0) return 0;
    uint8_t first_byte = buffer[0];
    int width = 1;
    uint8_t mask = 0x80;
    while (width <= 8 && !(first_byte & mask)) {
        mask >>= 1;
        width++;
    }
    if (width > 8 || (size_t)width > max_len) return 0; // Error / overflow
    *vint_len = width;
    uint64_t val = first_byte & (~mask);
    for (int i = 1; i < width; i++) {
        val = (val << 8) | buffer[i];
    }
    return val;
}
```

#### 1.1.2 MP4 (ISO/IEC 14496-12 ISOBMFF) & QuickTime MOV
* **Structural Paradigm:** Object-oriented 8-byte (or 16-byte extended 64-bit) atom/box tree (`[Length (4B)][FourCC (4B)][Optional Extended Length (8B)][Payload]`).
* **Hierarchy:**
  * `ftyp` (File Type Box): Major brand (`mp42`, `isom`, `qt  `), minor version, compatible brands.
  * `moov` (Movie Box): Metadata index tree.
    * `mvhd` (Movie Header Box): Global time scale and duration.
    * `trak` (Track Box): Discrete stream pipeline.
      * `tkhd` (Track Header Box): Geometry (width, height), rotation transform matrix ($3\times3$ fixed-point affine matrix), volume.
      * `mdia` (Media Box) -> `mdhd` (Media Header), `hdlr` (Handler: `vide`, `soun`, `sbtl`).
      * `minf` (Media Information) -> `stbl` (Sample Table Box - core index):
        * `stsd` (Sample Description): Codec configuration (`avc1`, `hvc1`, `mp4a`, `tx3g`). Contains decoder configuration records (`avcC`, `hvcC`, `esds`).
        * `stts` (Time-to-Sample): Delta-encoded decoding timestamps (DTS).
        * `ctts` (Composition Time-to-Sample): Presentation time (PTS) offsets ($PTS = DTS + ctts\_offset$). Version 1 allows signed negative offsets (critical for open-GOP B-frame ordering).
        * `stss` (Sync Sample): Table of keyframe/IDR sample numbers.
        * `stsc` (Sample-to-Chunk): Run-length encoded table mapping samples to discrete contiguous chunks.
        * `stsz` / `stz2` (Sample Sizes): Exact byte length of every individual frame.
        * `stco` / `co64` (Chunk Offsets): Absolute 32-bit or 64-bit byte offsets of chunks within the file.
  * `mdat` (Media Data Box): Contiguous raw interleaved payloads.
* **Fragmented MP4 (fMP4):**
  * Essential for Dash/HLS streaming and live recording resilience.
  * Replaces monolithic `stbl` with periodic movie fragments: `moof` (Movie Fragment Box: `mfhd`, `traf`, `tfhd`, `trun`) followed by immediate `mdat`.
  * `trun` (Track Fragment Run Box): Carries per-sample durations, sizes, and flags incrementally.
* **QuickTime MOV Divergence:**
  * Supports edit lists (`elst` inside `edts`) to dictate non-linear playback segments, audio delay padding, or track start time offsetting.
  * Supports legacy atoms (`wide` spacer atom before `mdat`, `rdrf` reference movies, resources fork remnants).
  * Big-endian metadata handling and Mac OS Roman string encodings.

---

### 1.2 Broadcast & Optical Disc Containers

#### 1.2.1 MPEG Transport Stream (MPEG-TS, `.ts`, `.m2ts`)
* **Packet Architecture:** Fixed-length packets. Standard MPEG-TS = 188 bytes; BDAV/M2TS (Blu-ray) = 192 bytes (prepends a 4-byte `TP_extra_header` containing a 2-bit copy permission indicator and 30-bit Arrival Time Stamp - ATS running at 27 MHz).
* **Packet Header (4 bytes):**
  ```
  [Sync Byte (0x47): 8b]
  [Transport Error Indicator (TEI): 1b]
  [Payload Unit Start Indicator (PUSI): 1b]
  [Transport Priority: 1b]
  [PID (Packet Identifier): 13b]
  [Transport Scrambling Control: 2b]
  [Adaptation Field Control: 2b] (01=Payload only, 10=Adaptation only, 11=Both)
  [Continuity Counter: 4b] (Sequence 0..15 for drop detection)
  ```
* **Adaptation Field:**
  * Carries the **Program Clock Reference (PCR)**: 33-bit PCR Base (90 kHz clock) + 6-bit reserved + 9-bit PCR Extension (27 MHz sub-clock).
  * Provides random access flags (`random_access_flag` indicates sequence header/keyframe arrival) and splicing points.
* **Program Specific Information (PSI) Demuxing State Machine:**
  1. Parse **PAT (Program Association Table)** locked strictly on `PID 0x0000`. Maps Program Numbers to **PMT (Program Map Table)** PIDs.
  2. Parse **PMT**: Identifies elementary stream PIDs and their `stream_type` descriptors:
     * `0x01` / `0x02`: MPEG-1 / MPEG-2 Video
     * `0x1B`: AVC / H.264 Video
     * `0x24`: HEVC / H.265 Video
     * `0x81` / `0x86`: ATSC AC-3 / SCTE-35 splice markers
     * `0x82` / `0x83`: Blu-ray DTS / TrueHD
  3. Parse **PES (Packetized Elementary Stream)**: PUSI bit set indicates start of a new PES packet (`0x000001` prefix + `stream_id`). Contains 33-bit **PTS** (Presentation Time Stamp) and optional **DTS** (Decoding Time Stamp) calculated against the 90 kHz timebase.

#### 1.2.2 DVD Video Object (VOB / MPEG-PS)
* **Structural Paradigm:** MPEG-2 Program Stream (ISO/IEC 13818-1) packed in 2048-byte DVD physical sectors.
* **Pack Header:** Begins with `0x000001BA`. Contains the 42-bit System Clock Reference (SCR).
* **DVD Private Stream 1 (`0x000001BD`):**
  * Multiplexes non-MPEG elementary streams.
  * First payload byte indicates substream ID:
    * `0x20` to `0x3F`: SPU (Subpicture Units / DVD Bitmap Subtitles)
    * `0x80` to `0x87`: AC-3 Audio
    * `0x88` to `0x8F`: DTS Audio
    * `0xA0` to `0xA7`: LPCM Audio
* **Navigation Packs (NV_PCK):** Placed at the start of every Video Object Unit (VOBU - ~0.5s playback). Contains two 980-byte metadata sectors:
  1. `PCI` (Presentation Control Information): Seamless angle, highlight/menu interactivity.
  2. `DSI` (Data Search Information): Exact sector addresses of previous/future VOBUs for trick-play (fast-forward/rewind) and seamless branching.

#### 1.2.3 Optical Disc Structures (ISO & BDMV)
* **ISO 9660 & UDF (Universal Disk Format):**
  * DVD-Video enforces UDF 1.02 bridge format.
  * Blu-ray enforces UDF 2.50 or 2.60 (utilizing Virtual Allocation Tables - VAT and metadata partitions).
  * Direct demuxing requires an in-memory UDF file system parser that traverses root ICB hierarchies, reads directory descriptors, and extracts file extents directly without operating system loopback disk mounting.
* **BDMV Topology:**
  * `BDMV/PLAYLIST/*.mpls`: Binary playlist describing playback sequences. Contains `PlayItem` entries mapping In-Time and Out-Time timestamps to specific `.m2ts` streams, handling seamless connection conditions (connection condition 1, 5, or 6 with clean audio PTS continuity across clip boundaries).
  * `BDMV/CLIPINF/*.clpi`: Clip information files containing `ProgramInfo`, stream attributes, and the **EP_map** (Entry Point Map: a multi-tier index mapping PTS timestamps to packet numbers within the associated `.m2ts` file).

---

### 1.3 Legacy Containers

#### 1.3.1 Audio Video Interleave (AVI)
* **Structural Paradigm:** Resource Interchange File Format (RIFF) composed of FourCC chunk identifiers and 32-bit little-endian sizes.
* **Hierarchy:**
  ```
  RIFF: 'AVI '
    ├── LIST: 'hdrl'
    │     ├── 'avih': Main AVI Header (microseconds per frame, max bytes/sec, flags, total frames)
    │     └── LIST: 'strl' (Repeated per stream)
    │           ├── 'strh': Stream header (fccType: 'vids', 'auds', 'mids', 'txts', fccHandler, scale, rate)
    │           └── 'strf': Stream format (BITMAPINFOHEADER for video, WAVEFORMATEX for audio)
    ├── LIST: 'movi'
    │     └── Chunks: [StreamID 2B][Type 2B][Length 4B][Payload] (e.g., '00dc'=compressed video, '01wb'=audio)
    └── 'idx1': Legacy index chunk (maps chunk IDs, flags, offsets, and lengths)
  ```
* **Demuxing Complexities:**
  * **No Native DTS:** AVI has no concept of presentation timestamps distinct from decoding timestamps. B-frame streams require "Packed Bitstream" hacks (DivX/XviD), where multiple frames are packed into a single chunk, followed by empty `00dc` dummy frames (N-VOPs) to maintain the frame clock counter.
  * **2GB Boundary & OpenDML 1.02:** Standard RIFF sizes are 32-bit unsigned, capping file size at 2 GB or 4 GB. OpenDML solves this via chained `AVIX` RIFF lists and a hierarchical index structure (`indx` super-indexes pointing to `ix00` sub-indexes).
  * **Audio VBR Desynchronization:** AVI assumes constant frame size. Variable Bitrate (VBR) MP3 in AVI requires tracking bytes-per-sample spoofing in `strh`, causing audio drift in standard DirectShow splitters unless sample-accurate byte counting is implemented.

#### 1.3.2 Advanced Systems Format (ASF / WMV / WMA)
* **Structural Paradigm:** Chunk-based format using 128-bit GUIDs (16 bytes) and 64-bit integer lengths.
* **Core Objects:**
  * `Header Object` (`75B22630-668E-11CF-A6D9-00AA0062CE6C`):
    * `File Properties Object`: Max packet size, play duration, preroll delay buffer (critical for avoiding startup starvation).
    * `Stream Properties Object`: Defines stream type (`Audio Media`, `Video Media`), error concealment strategies, and codec configuration data (`BITMAPINFOHEADER` embedded for `WMV3`/`WVC1`).
  * `Data Object` (`75B22636-668E-11CF-A6D9-00AA0062CE6C`): Fixed-size transport packets.
  * `Simple Index Object`: 32-bit seek points at fixed time intervals for the primary video stream.
* **Packet Architecture:**
  * Packets have variable fields defined by an Error Correction Flag and Property Flags.
  * Payloads support fragmentation across packets and multiple payload multiplexing within a single packet, requiring internal payload reassembly queues.

#### 1.3.3 Flash Video (FLV)
* **Header:** 9 bytes (`'FLV'`, Version 1, Audio/Video flags, DataOffset 9).
* **Tag Architecture:** Interleaved sequence: `[PreviousTagSize (4B)][FLV Tag Header (11B)][Payload][PreviousTagSize (4B)]`.
  * `Tag Type`: `0x08` (Audio), `0x09` (Video), `0x12` (Script Data: `onMetaData` AMF0/AMF1 packets).
  * `Timestamp`: 24-bit integer + 8-bit `TimestampExtended` high-byte forming a 32-bit millisecond PTS.
* **Modern Codec Injections:**
  * H.264/HEVC inside FLV: FrameType (4 bits) + CodecID (4 bits). If CodecID == 7 (AVC), byte 1 contains `AVCPacketType`:
    * `0x00`: `AVCDecoderConfigurationRecord` (SPS/PPS parameter sets).
    * `0x01`: Raw NAL units with explicit CTS offset (`CompositionTime` 3 bytes).
    * `0x02`: AVC end of sequence.

#### 1.3.4 Ogg Multimedia (OGG / OGV / OGA)
* **Framing & Page Structure:**
  * Physical bitstream broken into Ogg Pages, each beginning with `'OggS'` (`0x4F676753`).
  * `Header Type Flag`: `0x01` = Continued packet, `0x02` = First page of logical stream (BOS), `0x04` = Last page of logical stream (EOS).
  * `Granule Position` (8 bytes): Codec-specific timestamp position.
  * `Bitstream Serial Number` (4 bytes): Uniquely identifies elementary stream within grouped/chained bitstreams.
  * `Segment Table`: Number of segments $N$ (1 byte) followed by $N$ lacing values. Packets $>255$ bytes span multiple lacing values; a value $<255$ terminates a packet.
* **Granulepos Mapping:**
  * **Vorbis:** Direct sample count. $PTS = GranulePos / SampleRate$.
  * **Theora:** Granulepos is split into two fields: high bits represent the frame index of the last keyframe; low bits represent the frame count since that keyframe ($KeyFrameIndex \ll GranuleShift + FrameDelta$).
  * **Opus:** 48 kHz sample counter. Must subtract `pre-skip` (specified in `OpusHead`) from the first decoded samples to achieve true zero-aligned presentation.

#### 1.3.5 RealMedia (RM / RMVB)
* **Header Chunk:** Identifies `'.RMF'`, file version, number of headers.
* **Data Chunk (`DATA`):** Multiplexed packets. Packets carry stream number, timestamp (ms), flags, and fragmented payloads.
* **Audio Interleaving Descrambling:**
  * RealAudio codecs (Cook, RA-Sipr) utilize proprietary interleaving patterns across frames (interleave blocks of $N$ frames $\times$ $M$ slices) to survive packet loss on dial-up networks.
  * The demuxer must implement the descrambling matrix buffers to reassemble frames in sequential order prior to passing them to the decoder.

#### 1.3.6 3GP (3GPP / 3GPP2)
* **Standard:** ISO/IEC 14496-12 ISOBMFF derivative tailored for mobile devices (`3gp4`, `3gp5`, `3gp6`).
* **Elementary Streams:** Video commonly encoded in H.263 or MPEG-4 Part 2; audio exclusively encoded in Adaptive Multi-Rate (AMR-NB or AMR-WB).
* **Framing:** Frame-by-frame sample tables with small chunk sizes to support low-memory mobile decoding.

---

### 1.4 Dedicated Audio Containers

| Container | Sync Signature / Magic | Indexing & Seek Mechanics | Extradata / Codec Headers | Metadata Standards |
| :--- | :--- | :--- | :--- | :--- |
| **MKA** | EBML ID `0x1A45DFA3` | Full EBML `Cues` mapping timecodes to cluster byte positions | `CodecPrivate` atom (stores FLAC headers, ALAC cookies, Vorbis setup packets) | Matroska Tagging, Chapters, Embedded Cover/Font Attachments |
| **MP3** | Frame Sync `0xFFE` / `0xFFF` (11-12 bits) | Sequential scanning unless Xing/LAME VBR header (`TOC` 100-byte table) or VBRI header is present | None (frame headers contain bitrates, sample rates, channel modes) | ID3v1 (trailing 128B), ID3v2.2/2.3/2.4 (prepended tag blocks, unsynchronization support) |
| **AAC / M4A** | ADTS: `0xFFF` (12 bits) syncword; M4A: `ftyp:M4A ` | ADTS: sequential parsing; M4A: `stbl` sample tables (`stts`, `stsz`, `stco`) | ADTS: 7/9-byte frame header; M4A: `esds` atom containing `AudioSpecificConfig` | iTunes metadata style (`moov.udta.meta.ilst`) |
| **FLAC** | `'fLaC'` (`0x664C6143`) | Optional `SEEKTABLE` metadata block mapping sample numbers to byte offsets | `STREAMINFO` block (min/max block size, min/max frame size, sample rate, channels, bit depth) | `VORBIS_COMMENT` metadata block, `PICTURE` block |
| **Opus (Ogg)**| `'OggS'` + Packet Header `'OpusHead'` | Ogg page `granulepos` mapping (48 kHz timebase) | `OpusHead` (channels, pre-skip count, original sample rate, output gain, channel mapping table) | `OpusTags` packet (Vorbis Comment format) |
| **WAV** | RIFF: `'WAVE'` | Chunks parsing; sample-accurate seek by linear byte offset calculation | `fmt ` chunk: `WAVEFORMATEX` or `WAVEFORMATEXTENSIBLE` (channel mask GUIDs) | `INFO` chunk list, `ID3 ` chunk, BWF `bext` (Broadcast Wave metadata) |
| **AIFF** | FORM: `'AIFF'` or `'AIFC'` | Big-endian byte offset math derived from `COMM` chunk sample sizes | `COMM` chunk (sample rate encoded as 80-bit IEEE 754 extended precision float) | `NAME`, `AUTH`, `(c) `, `ANNO` chunks |
| **APE** | `'MAC '` (`0x4D414320`) | Frame descriptors and seek table mapping seek points to bit offsets | APE Header (version, compression level, peak level, total frames) | APEv1 and APEv2 tags (stored at file footer) |
| **WavPack** | `'wvpk'` (`0x7776706B`) | Indexing metadata sub-blocks; optional external `.wvc` correction file | WavPack block header (flags, sample count, CRC) | APEv2 tags |
| **ALAC** | `ftyp:M4A ` + `stsd.alac` | `stbl` sample tables (`stts`, `stsz`, `stco`/`co64`) | 36-byte ALAC magic cookie embedded in `stsd` sample description | iTunes metadata atom tree |

---

## 2. Video Codecs Deep Dive & Bitstream Architectures

Modern and legacy video codecs span discrete paradigms of bitstream organization, entropy coding, block transformations, motion vector prediction, and colorimetry.

```
       TYPICAL HYBRID VIDEO DECODING PIPELINE (AVC/HEVC/AV1)
                                 
 Bitstream ──► [Entropy Decoding] ──► [Inverse Quant/Transform] ──► [Spatial/Temporal Prediction]
 (NAL/OBU)     (CABAC / DAv1d)              (IDCT / ADST)                       │
                                                                                ▼
 Video Out ◄── [Tone Mapping] ◄── [In-Loop Filters] ◄────────────────────── [Reconstruction]
 (P010/NV12)   (HDR10/DoVi)       (Deblock / SAO / CDEF / Film Grain)
```

### 2.1 Modern Video Codecs

#### 2.1.1 AV1 (AOMedia Video 1)
* **Bitstream Structure:** Open Bitstream Unit (OBU) syntax. Each OBU begins with a 1-2 byte header specifying `obu_type`, `obu_extension_flag`, and `obu_has_size_field`:
  * `OBU_SEQUENCE_HEADER`: Profile (Main: 8/10-bit 4:2:0; High: 8/10-bit 4:4:4; Professional: 12-bit), level, color configuration (bit depth, primaries, transfer characteristics, matrix coefficients, full-range flag).
  * `OBU_FRAME_HEADER` & `OBU_FRAME`: Geometry, reference frames, loop filters, quantization parameters.
  * `OBU_TILE_GROUP`: Slice payloads organized in parallel-decodable tile grids.
  * `OBU_METADATA`: HDR metadata (ITUT-T T.35), film grain parameters.
* **Transform & Block Mechanics:**
  * Superblocks up to $128 \times 128$ down to $4 \times 4$.
  * Recursive partitioning (1:1, 1:2, 2:1, 1:4, 4:1, and T-shapes).
  * Transforms: Discrete Cosine Transform (DCT), Asymmetric Discrete Sine Transform (ADST), and Identity transforms.
* **In-Loop Restoration Filters:**
  1. *Deblocking Filter:* Targets block boundary grid artifacts.
  2. *CDEF (Constrained Directional Enhancement Filter):* Operates on $8 \times 8$ blocks along identified edge directions to eliminate ringing without blurring edges.
  3. *Loop Restoration:* Wiener filter or Self-Guided filter applied across tile surfaces.
  4. *Film Grain Synthesis (FGS):* Grain is stripped during encoding and parameterized into autoregressive models. The decoder generates synthetic noise on the reconstructed surface, saving massive bitrates while requiring specialized GPU shader compute passes.
* **Hardware Acceleration:** D3D11VA / D3D12 Video Decode requires Intel Gen12 (Tiger Lake+), NVIDIA GeForce RTX 30-series (Ampere+), or AMD Radeon RX 6000-series (RDNA2+).

#### 2.1.2 HEVC / H.265 (ISO/IEC 23008-2)
* **NAL Unit Syntax:** 2-byte header. `[forbidden_zero_bit (1b)][nal_unit_type (6b)][nuh_layer_id (6b)][nuh_temporal_id_plus1 (3b)]`.
  * `NAL_UNIT_VPS` (32): Video Parameter Set (overall multi-layer capability).
  * `NAL_UNIT_SPS` (33): Sequence Parameter Set (Profile/Tier/Level, chroma subsampling, bit depth, conformance window).
  * `NAL_UNIT_PPS` (34): Picture Parameter Set (Tile columns/rows, CABAC initialization).
  * `NAL_UNIT_SEI` (39/40): Supplemental Enhancement Information (mastering display color volume, content light level).
  * `NAL_UNIT_CODED_SLICE_TRAIL_R` (1), `NAL_UNIT_CODED_SLICE_IDR_W_RADL` (19), `NAL_UNIT_CODED_SLICE_CRA` (21).
* **Coding Structure:**
  * Coding Tree Units (CTUs) up to $64 \times 64$, partitioned into quadtree Coding Units (CUs), Prediction Units (PUs), and Transform Units (TUs).
  * Sample Adaptive Offset (SAO): Applied post-deblocking to correct banding and edge distortion via band offsets and edge offsets.
  * High Tier support for high-bitrate broadcast contributions.

#### 2.1.3 AVC / H.264 (ISO/IEC 14496-10)
* **NAL Unit Syntax:** 1-byte header. `[forbidden_zero_bit (1b)][nal_ref_idc (2b)][nal_unit_type (5b)]`.
  * `Type 7`: SPS, `Type 8`: PPS, `Type 5`: IDR Slice, `Type 1`: Non-IDR Slice.
* **Entropy Engines:** Context-Adaptive Variable-Length Coding (CAVLC) vs. Context-Adaptive Binary Arithmetic Coding (CABAC).
* **Interlaced Tools:** Macroblock-Adaptive Frame/Field (MBAFF) and Picture-Adaptive Frame/Field (PAFF) for handling legacy broadcast material.

#### 2.1.4 VP9
* **Framing:** Encapsulated in WebM or IVF (Interleaved Video Format). IVF provides a 32-byte header followed by 12-byte per-frame headers (frame size and 64-bit timestamp).
* **Profiles:**
  * Profile 0: 8-bit, 4:2:0.
  * Profile 1: 8-bit, 4:2:2 / 4:4:4.
  * Profile 2: 10-bit and 12-bit, 4:2:0 (predominant for YouTube HDR).
  * Profile 3: 10-bit and 12-bit, 4:2:2 / 4:4:4.
* **Superframes:** VP9 packs multiple frames (e.g., invisible reference frames + displayable frames) into a single chunk using a trailing byte descriptor with bit-pattern `110ssfff` ($ss$ = size width, $fff$ = frame count).

---

### 2.2 Legacy Video Codecs

#### 2.2.1 MPEG-4 Visual (Part 2 - DivX, XviD)
* **Architecture:** Macroblock-based ($16 \times 16$). Simple Profile (SP) and Advanced Simple Profile (ASP).
* **Non-Standard Artifacts & Hacks:**
  * **Packed Bitstreams:** Enforces multiple VOPs (Video Object Planes) in one chunk to handle B-frames within AVI.
  * **Quarter-Pel Motion Compensation (QPel):** High interpolation complexity causing stutter on early DirectShow hardware decoders.
  * **Global Motion Compensation (GMC):** XviD supports 3-warp-point GMC, whereas hardware implementations (and standard DivX decoders) only support 1-point (translational) GMC, resulting in extreme frame corruption if decoded via standard ASP profiles.

#### 2.2.2 MPEG-2 (ISO/IEC 13818-2) & MPEG-1
* **Structural Elements:**
  * Sequence Header (`0x000001B3`): Horizontal/Vertical size, Aspect Ratio Information (ARI), frame rate code, bitrate, intra/non-intra quantization matrices.
  * Sequence Extension (`0x000001B5`): Signals MPEG-2 profile/level (Main Profile @ Main Level `MP@ML`, 4:2:2 Profile @ High Level `422P@HL`), progressive sequence flag, chroma format (4:2:0, 4:2:2, 4:4:4).
  * GOP Header (`0x000001B8`): Timecode, drop frame flag, closed/broken GOP flags.
* **Interlacing & Telecine:**
  * Field pictures vs. Frame pictures.
  * `repeat_first_field` and `top_field_first` flags implement 3:2 pulldown (Telecine). Decoders must support Inverse Telecine (IVTC) to recover original 23.976 fps progressive cadences.

#### 2.2.3 VC-1 (SMPTE 421M) & WMV3
* **Profiles:** Simple, Main, and Advanced Profile (`WVC1`).
* **Bitstream Differences:** Simple and Main profiles encapsulate video in ASF format using proprietary sequence headers embedded in `BITMAPINFOHEADER`. Advanced Profile uses elementary stream start codes (`0x0000010F` sequence header) matching MPEG PES conventions.
* **Transforms:** Employs overlapped block transforms to reduce blocking artifacts at low bitrates.

#### 2.2.4 RealVideo (RV30, RV40)
* **Architecture:** Early precursor to H.264 developed by RealNetworks.
* **Bitstream Properties:** RV40 uses modified $4 \times 4$ integer transforms, multiple reference frames, and custom $16 \times 16$ macroblock subdivision. Encapsulated with proprietary 16-byte slice headers carrying non-monotonic timestamps requiring slice reassembly buffers.

#### 2.2.5 Theora
* **Lineage:** Developed by Xiph.Org derived from On2 Technologies' VP3.
* **Architecture:** $8 \times 8$ DCT blocks grouped into $16 \times 16$ macroblocks and $32 \times 32$ superblocks. Always encapsulated within Ogg framing.

#### 2.2.6 DV (Digital Video)
* **Standards:** IEC 61834 and SMPTE 314M.
* **Framing:** Fixed DIF (Digital Interface) block structure (12,000 DIF blocks per frame for 525/60 NTSC, 14,400 DIF blocks per frame for 625/50 PAL). Uncompressed audio and timecodes embedded directly into header DIF blocks. Intra-frame DCT compression; 4:1:1 chroma subsampling (NTSC) or 4:2:0 (PAL).

---

### 2.3 Ancient & Retro Codecs

* **Cinepak (`cvid`):** Developed in 1991 by SuperMac Technologies. Employs vector quantization. Video frames are partitioned into $4 \times 4$ pixel patches. The bitstream transmits codebooks containing vector values; subsequent strip data streams use 8-bit or 1-bit indexes referencing codebook entries. Supports 24-bit RGB and 8-bit dithered paletted outputs.
* **Intel Indeo Video (Indeo 3/4/5 - `IV32`, `IV41`, `IV50`):** Indeo 3 uses vector quantization. Indeo 4 and 5 utilize multi-resolution discrete wavelet transforms (DWT) with directional filter banks and transparency bitmasks. Indeo 5 was notorious for requiring 32-bit legacy DirectShow/VFW codec registry entries, failing natively on 64-bit Windows pipelines unless handled by FFmpeg-based translation layers.
* **Sorenson Video (SVQ1, SVQ3):** Proprietary QuickTime video codecs. SVQ1 utilizes vector quantization. SVQ3 is a proprietary modification of early draft H.264 (AVC) with modified table lookups and customized watermark/header encryption.
* **Motion JPEG (MJPEG):** Every frame is an independent, baseline discrete cosine transform (DCT) JPEG image. Decoders must scan for SOI (`0xFFD8`) and EOI (`0xFFD9`) markers, parse DQT (quantization tables), DHT (Huffman tables), and SOF0 (`0xFFC0`) baseline markers per frame.
* **ITU-T H.263:** Predecessor to MPEG-4 Part 2. Sub-QCIF ($128 \times 96$) to CIF ($352 \times 288$). Utilizes 22 Annexes (e.g., Annex D: Unrestricted Motion Vectors; Annex I: Advanced Intra Coding; Annex J: Deblocking Filter).

---

### 2.4 High Bit-Depth, Color Spaces & HDR Signal Pipelines

#### 2.4.1 Pixel Formats & Memory Alignment
* **8-bit YUV 4:2:0:** Planar (`YUV420P` / `I420`), Bi-Planar NV12 (Y plane followed by interleaved UV samples).
* **10-bit / 12-bit YUV:**
  * Planar formats (`YUV420P10LE`, `YUV420P12LE`): 16-bit little-endian words per sample with the upper 6 or 4 bits zeroed or padded.
  * DirectX GPU Surface `P010`: Bi-Planar format matching NV12, but each sample is a 16-bit word with the actual 10-bit data stored in the **most significant bits** (left-shifted by 6: `sample << 6`). Crucial for native DXGI/D3D11 hardware decoder zero-copy surfaces.

#### 2.4.2 Colorimetry Standards & EOTFs

| Characteristic | Rec.601 (SD) | Rec.709 (HD) | Rec.2020 (UHD/HDR) |
| :--- | :--- | :--- | :--- |
| **White Point** | D65 ($0.3127, 0.3290$) | D65 ($0.3127, 0.3290$) | D65 ($0.3127, 0.3290$) |
| **Red Primary ($x, y$)** | $(0.640, 0.330)$ (SMPTE-C) / $(0.630, 0.340)$ (EBU) | $(0.640, 0.330)$ | $(0.708, 0.292)$ |
| **Green Primary ($x, y$)** | $(0.290, 0.600)$ / $(0.310, 0.595)$ | $(0.300, 0.600)$ | $(0.170, 0.797)$ |
| **Blue Primary ($x, y$)** | $(0.150, 0.060)$ / $(0.155, 0.070)$ | $(0.150, 0.060)$ | $(0.131, 0.046)$ |
| **Luminance Coefficients** | $Y = 0.299R + 0.587G + 0.114B$ | $Y = 0.2126R + 0.7152G + 0.0722B$ | $Y = 0.2627R + 0.6780G + 0.0593B$ |
| **Transfer Function (EOTF)** | Rec.601 Gamma ($\approx 2.2$) | Rec.709 Gamma ($\approx 2.4$) | ST 2084 (PQ) / ARIB STD-B67 (HLG) |

#### 2.4.3 Mathematical Transformations

* **YUV to RGB Matrix Transformation (Rec.709, Limited Range):**
  Given $Y \in [16, 235]$ and $Cb, Cr \in [16, 240]$ for an 8-bit domain:
  $$Y' = \frac{Y - 16}{219}, \quad Cb' = \frac{Cb - 128}{224}, \quad Cr' = \frac{Cr - 128}{224}$$
  $$\begin{bmatrix} R \\ G \\ B \end{bmatrix} = \begin{bmatrix} 1.0 & 0.0 & 1.5748 \\ 1.0 & -0.1873 & -0.4681 \\ 1.0 & 1.8556 & 0.0 \end{bmatrix} \begin{bmatrix} Y' \\ Cb' \\ Cr' \end{bmatrix}$$

* **SMPTE ST 2084 (PQ - Perceptual Quantizer) EOTF:**
  Maps non-linear signal values $N \in [0, 1]$ to absolute display luminance $L \in [0, 10000]\text{ cd/m}^2$:
  $$L = 10000 \times \left( \frac{\max(N^{1/m_2} - c_1, 0)}{c_2 - c_3 N^{1/m_2}} \right)^{1/m_1}$$
  Constants:
  $$m_1 = \frac{2610}{16384} = 0.1593017578125$$
  $$m_2 = \frac{2523}{4096} \times 128 = 78.84375$$
  $$c_1 = \frac{3424}{4096} = 0.8359375, \quad c_2 = \frac{2413}{4096} \times 32 = 18.8515625, \quad c_3 = \frac{2392}{4096} \times 32 = 18.6875$$

* **Hybrid Log-Gamma (HLG - ARIB STD-B67 / BT.2100):**
  Scene-referred EOTF combining a standard gamma curve for lower half luminance with a logarithmic curve for highlights:
  $$E' = \begin{cases} \sqrt{3} E^{0.5}, & 0 \le E \le \frac{1}{12} \\ a \ln(12E - b) + c, & \frac{1}{12} < E \le 1 \end{cases}$$
  Constants: $a = 0.17883277$, $b = 1 - 4a = 0.28466892$, $c = 0.5 - a \ln(4a) = 0.55991073$.

#### 2.4.4 Dolby Vision Profiles & Demuxing Topologies
1. **Profile 5 (Single Layer Proprietary):**
   * Proprietary **IPT-C2** color representation (ICtCp color space variation) with non-linear reshape processing.
   * If decoded without dynamic Dolby Vision RPU metadata interpretation and color reshaping, the output exhibits severe green/magenta color inversion.
2. **Profile 7 (UHD Blu-ray Dual Layer):**
   * **Base Layer (BL):** Standard HDR10 4K 10-bit bitstream.
   * **Enhancement Layer (EL):** 1080p stream containing either Full Enhancement Layer (FEL - carries 12-bit residual difference data) or Minimal Enhancement Layer (MEL - carries only RPU metadata).
   * Playback engines must either seamlessly recombine BL + EL in the decoding pipeline or cleanly discard the EL and fall back to HDR10 base presentation.
3. **Profile 8 (Single Layer Compatible):**
   * Cross-platform standard: Profile 8.1 uses an HDR10-compatible base layer with embedded Dolby Vision RPU metadata in SEI NAL units. Allows seamless tone-mapping fallback to HDR10 if the hardware display pipeline lacks Dolby Vision certification.

---

## 3. Audio Codecs & Channel Topologies

Audio bitstreams require continuous frame boundary synchronization, subband or frequency-domain transform decoding, channel mapping negotiation, and clock-rate adaptation.

### 3.1 Lossless Audio Bitstreams

```
                     LOSSLESS AUDIO BITSTREAM SCHEMATICS
                     
 FLAC:     [ 'fLaC' ] [ STREAMINFO ] [ ...Blocks... ] [ Subframe LPC / Rice ]
 TrueHD:   [ 0xF8726FBA ] [ Access Unit Header ] [ Substream 0 (2.0) ] [ Substream 1 (5.1) ] [ Atmos JOC ]
 DTS-HD:   [ Core DTS Frame (Lossy 1.5Mbps) ] + [ DTS-HD Extension Substream (Lossless Residuals) ]
```

#### 3.1.1 Free Lossless Audio Codec (FLAC)
* **Frame Sync:** 14-bit sync code `0x3FFE` (`11111111111110`).
* **Frame Header:** Variable or fixed block sizes ($16 \dots 65535$ samples), sample rates ($1 \dots 655350$ Hz), bit depth ($4 \dots 32$ bits), channel assignments (independent, left-side, right-side, mid-side).
* **Subframe Architecture:**
  1. *Constant:* Repeated single value.
  2. *Verbatim:* Uncompressed raw PCM data.
  3. *Fixed Linear Prediction:* Polynomial predictor orders 0 through 4.
  4. *Linear Predictive Coding (LPC):* FIR predictor up to order 32 with quantized coefficients. Residuals are entropy-coded using Golomb-Rice coding.

#### 3.1.2 Apple Lossless Audio Codec (ALAC)
* **Encapsulation:** ISOBMFF (`mp4a` atom containing the 36-byte `alac` magic cookie).
* **Compression:** Employs adaptive Golomb-Rice entropy coding combined with 29-order linear prediction. Supports bit depths of 16, 20, 24, and 32 bits, up to 8 discrete channels.

#### 3.1.3 Dolby TrueHD (MLP - Meridian Lossless Packing)
* **Sync Word:** `0xF8726FBA` (Major Sync) occurring periodically (every 128 frames) to allow random access seeking.
* **Substream Topology:**
  * Substream 0: Decodes mandatory 2-channel stereo downmix.
  * Substream 1: Decodes 5.1-channel surround extension.
  * Substream 2: Decodes 7.1-channel surround extension.
  * Substream 3: Carries dynamic Dolby Atmos metadata (spatial audio objects).
* **Throughput:** Maximum bitrates up to 18.09 Mbps; supports up to 16 channels at 96 kHz / 24-bit or 6 channels at 192 kHz / 24-bit.

#### 3.1.4 DTS-HD Master Audio (DTS-HD MA)
* **Core + Extension Architecture:**
  * **Core:** Standard legacy lossy DTS frame (`0x7FFE8001`) decodable by any standard DTS decoder at 768 kbps or 1509 kbps.
  * **Extension Substream:** Synced via `0x64582025`. Carries the lossless `XLL` (Extended Lossless) asset, which computes the arithmetic difference between the lossy core reconstruction and the original uncompressed studio master.
* **Fallback Behavior:** If an audio device or renderer cannot initialize a 24-bit 192 kHz multichannel pipeline, the demuxer can strip the extension and pass only the 1.5 Mbps lossy core with zero playback interruption.

#### 3.1.5 Monkey's Audio (APE)
* **Sync Word:** `'MAC '` (`0x4D414320`).
* **Algorithmic Complexity:** Highly asymmetric compression utilizing neural-network-like adaptive linear predictive filters (orders up to 256). Five compression profiles: Fast, Normal, High, Extra High, Insane. Seeking requires traversing a frame-seek table mapping exact sample indexes to compressed bitstream positions.

#### 3.1.6 WavPack
* **Sync Word:** `'wvpk'`.
* **Hybrid Mode:** Unique architectural feature allowing encoding into a lossy file (`.wv`) and an accompanying correction file (`.wvc`). Combined, they provide bit-exact lossless restoration; standalone, the `.wv` file acts as a high-quality lossy bitstream. Supports 32-bit floating-point PCM audio natively.

---

### 3.2 Lossy Audio Codecs

#### 3.2.1 Advanced Audio Coding (AAC)
* **Framing Formats:**
  * **ADTS (Audio Data Transport Stream):** Self-synchronizing 7-byte (or 9-byte with CRC) header starting with `0xFFF`. Contains sampling frequency index, channel configuration, and frame length.
  * **ADIF (Audio Data Interchange Format):** Single header at file start; cannot be streamed or split.
  * **LATM / LOAS:** Broadcast multiplexing layer (`0x2B7` sync) common in DVB broadcasts.
* **Profiles:**
  * **AAC-LC (Low Complexity):** Modified Discrete Cosine Transform (MDCT) with 1024 or 128-point windows, Temporal Noise Shaping (TNS).
  * **HE-AAC v1 (v2):** Uses **Spectral Band Replication (SBR)** to reconstruct high frequencies from half-sample-rate baseband; v2 adds **Parametric Stereo (PS)** to synthesize stereo sound from a mono core.
  * *Critical Demuxing Edge Case:* Implicit vs. explicit SBR signaling. If an AAC stream signals 24 kHz in `AudioSpecificConfig` with implicit SBR, a non-compliant decoder will output at 24 kHz instead of upsampling to 48 kHz, doubling pitch and playing at half speed.

#### 3.2.2 Dolby Digital (AC-3) & Dolby Digital Plus (E-AC-3)
* **AC-3 (ATSC A/52):**
  * Sync word: `0x0B77`. Frame size is constant relative to sample rate and bitrate (up to 640 kbps). 5.1 channels max.
* **E-AC-3 (Enhanced AC-3):**
  * Data rates up to 6.144 Mbps.
  * Substream structure supports up to 15.1 channels.
  * **Joint Object Coding (JOC):** Encapsulates spatial audio objects and positional metadata for **Dolby Atmos** over commercial streaming pipelines (Netflix, Apple TV+).

#### 3.2.3 Opus
* **Structural Synthesis:** Merges Skype’s **SILK** speech codec (linear prediction) with Xiph.Org’s **CELT** music codec (MDCT).
* **Frame Sizes:** 2.5 ms, 5 ms, 10 ms, 20 ms, 40 ms, 60 ms. Dynamic bitrates from 6 kbps to 510 kbps.
* **Native Clock:** Always decodes internally to 48,000 Hz regardless of source sampling rate. Bitstreams specify `pre-skip` samples (typically 312 samples) in the container header to discard encoder filter warmup transients.

#### 3.2.4 MP3 (MPEG-1 Audio Layer III)
* **Frame Sync:** 11 bits set: `0xFFE` / `0xFFF`.
* **Bitstream Mechanics:**
  * 32 polyphase filterbanks followed by MDCT (18 or 6-point short windows).
  * **Bit Reservoir:** Allows frames with high entropy complexity to borrow unallocated bits from prior frames. Demuxers cannot chop MP3 frames arbitrarily without tracking bit reservoir pointers (`main_data_begin` byte offsets).

#### 3.2.5 Vorbis & WMA
* **Vorbis:** Window-based MDCT audio codec. Always requires three setup packets (`identification`, `comment`, `setup` codebooks) from container extradata before any audio frame can be decoded.
* **Windows Media Audio (WMA):** WMA Standard, WMA Pro (surround up to 7.1 at 24/96), and WMA Lossless. Packets are parsed via ASF packet multiplexing properties.

---

### 3.3 Channel Topologies, Downmixing & Spatial Audio

```
             SURROUND (5.1) TO STEREO (2.0) DOWNMIX TOPOLOGY
             
   Left   (L) ───────────(+)─────────────────────────────► Left Out (Lo)
                          ▲
   Center (C) ──[ * 0.7071 ] 
                          ▼
   Right  (R) ───────────(+)─────────────────────────────► Right Out (Ro)
                          ▲
  Surround Left  (SL) ───[ * 0.7071 ] (or phase shifted +90°)
  Surround Right (SR) ────────────────[ * 0.7071 ] (or phase shifted -90°)
```

#### 3.3.1 Standard Channel Masks (Windows WAVEFORMATEXTENSIBLE)
* `0x00000001`: Speaker Front Left (`SPEAKER_FRONT_LEFT`)
* `0x00000002`: Speaker Front Right (`SPEAKER_FRONT_RIGHT`)
* `0x00000004`: Speaker Front Center (`SPEAKER_FRONT_CENTER`)
* `0x00000008`: Speaker Low Frequency Effects (`SPEAKER_LOW_FREQUENCY`)
* `0x00000010`: Speaker Back Left (`SPEAKER_BACK_LEFT`)
* `0x00000020`: Speaker Back Right (`SPEAKER_BACK_RIGHT`)
* `0x00000200`: Speaker Side Left (`SPEAKER_SIDE_LEFT`)
* `0x00000400`: Speaker Side Right (`SPEAKER_SIDE_RIGHT`)

#### 3.3.2 Downmixing Mathematical Models
When rendering multichannel streams (5.1, 7.1) over stereo headphones or 2.0 speakers, downmixing matrices must normalize signal gains to prevent numerical clipping:

* **ITU-R BS.775 Standard Stereo Downmix:**
  $$L_{out} = L + \frac{\sqrt{2}}{2} C + \frac{\sqrt{2}}{2} SL$$
  $$R_{out} = R + \frac{\sqrt{2}}{2} C + \frac{\sqrt{2}}{2} SR$$
  To prevent clipping, apply normalization factor $S$:
  $$S = \frac{1}{1 + \frac{\sqrt{2}}{2} + \frac{\sqrt{2}}{2}} \approx \frac{1}{2.4142} \approx 0.4142$$
  *LFE Channel Handling:* In professional media playback, Low Frequency Effects (LFE) channels are **discarded** by default during stereo downmixing to prevent acoustic mud and voice distortion, unless the user explicitly enables LFE mixing with a $-10\text{ dB}$ padding factor.

* **Dolby Pro Logic II Matrix Downmix (Surround-Encoded Stereo):**
  Synthesizes a stereo signal ($L_t, R_t$) capable of being decoded back to 5.0 by a matrix receiver:
  $$L_t = L + 0.7071 C - 0.866 SL - 0.5 SR$$
  $$R_t = R + 0.7071 C + 0.5 SL + 0.866 SR$$

#### 3.3.3 Hardware Bitstreaming vs. Software Spatialization
* **WASAPI Exclusive Bitstreaming (IEC 61937):**
  * Compressed frames of AC-3, E-AC-3, TrueHD, or DTS are packed into optical/HDMI SPDIF bursts (`IEC 60958` subframes).
  * Software player bypasses all OS mixer stages, disabling volume controls, equalizers, and DSP processing, handing bit-exact decoding to the AV Receiver (AVR).
* **Software Spatial Decoding:**
  * Decodes Atmos/DTS:X beds and objects into raw PCM positional buffers.
  * Delivers spatial coordinates to HRTF (Head-Related Transfer Function) spatial audio engines (such as Windows Sonic or Dolby Atmos for Headphones via `ISpatialAudioClient`).

---

## 4. Subtitle Formats & Rendering Pipeline

Subtitle architectures split into two distinct engineering challenges: **Vector/Script Engines** that require runtime font layout and composition (SSA/ASS) and **Bitmap Stream Engines** that execute run-length decoding of graphical overlays (PGS, VobSub).

```
                      SUBTITLE RENDERING PIPELINES
                      
 SCRIPT/VECTOR PIPELINE:
 MKV Extradata ──► [libass Context] ──► [Fontconfig/DirectWrite] ──► [FreeType Glyph Cache] ──► RGBA Overlay
 + ASS Events       (Parse Styles)       (Resolve Embedded Fonts)      (Rasterize Curves)

 BITMAP PIPELINE:
 M2TS / VOB    ──► [RLE Decoder]    ──► [CLUT Palette Map]       ──► Hardware Surface Blit
 (PGS / VobSub)    (Decompress Bits)    (Apply YUV->RGB Palette)
```

### 4.1 Complex Styled Subtitles (ASS / SSA)

#### 4.1.1 Advanced SubStation Alpha (v4+) Script Structure
An ASS file consists of structured text blocks:
* `[Script Info]`: Defines coordinate system resolution (`PlayResX`, `PlayResY`), collision logic (`WrapStyle`), and aspect ratio compensation (`ScaledBorderAndShadow`).
* `[V4+ Styles]`: Table defining font, size, colors (Primary, Secondary, Outline, Back/Shadow), metrics, margins, and alignments. Colors are formatted in hexadecimal ABGR: `&HAABBGGRR`.
* `[Events]`: Stream events containing layer number, start/end timestamps, style names, margins, and raw text payloads with inline override tags.

#### 4.1.2 Override Tags & Vector Syntax
ASS rendering engines must parse complex inline tags:
* **Positioning & Transforms:** `\pos(X,Y)`, `\move(X1,Y1,X2,Y2[,T1,T2])`, `\an<alignment>` (numpad-style alignment 1..9).
* **Color & Alpha:** `\1c&HBBGGRR&` (primary color), `\3c` (border color), `\alpha&HAA&`, `\1a` through `\4a`.
* **Rotations & Scaling:** `\frz<degrees>`, `\frx`, `\fry`, `\fscx<percent>`, `\fscy<percent>`.
* **Animations:** `\t([T1,T2,][accel,]<tags>)` - applies smooth linear or accelerated interpolation of styling values across frame presentation times.
* **Clipping:** `\clip(X1,Y1,X2,Y2)` or vector-path clipping `\clip(m <drawing commands>)`.
* **Vector Drawing Commands:** Activated by `\p<scale>` (e.g., `\p1` = $1:1$ scale; `\p0` turns off drawing mode):
  * `m <x> <y>`: Move to point.
  * `l <x> <y>`: Line to point.
  * `b <x1> <y1> <x2> <y2> <x3> <y3>`: Cubic Bézier curve.
  * `e`: Close path.

#### 4.1.3 Font Management, libass & Windows OS Integration
* **Font Dependencies:** Anime and community subtitle releases embed proprietary or customized OpenType/TrueType fonts directly inside the Matroska container (`Attachments` element, MIME types `font/ttf`, `font/otf`, `application/x-truetype-font`).
* **libass Integration Architecture:**
  1. Extract embedded fonts from MKV attachments.
  2. Register font memory blocks directly via `ass_add_font(ass_renderer, font_name, font_data, font_size)`.
  3. Resolve system-installed fonts when fonts are not embedded.
  4. *Windows Platform Engine Pitfall:* Historically, `libass` relied on Fontconfig, which scans the disk and builds large cache files on Windows, creating multi-second startup delays. Modern UniversalMediaPlayer implementations utilize **DirectWrite** font providers for `libass` (`ass_set_font_provider(ass_renderer, ASS_FONTPROVIDER_DIRECTWRITE)`), enabling sub-millisecond native access to the Windows font catalog.

---

### 4.2 Timed Text Subtitles

* **SubRip (SRT):**
  * Structure: Sequential integer counter, timecode range (`00:01:20,000 --> 00:01:23,450`), text lines, terminated by double newline (`\r\n\r\n`).
  * Tolerant Parsing: Must parse non-standard styling tags (`<i>`, `<b>`, `<u>`, `<font color="#RRGGBB">`) and recover from missing sequential numbering or comma vs. period millisecond separators.
* **WebVTT (Web Video Text Tracks):**
  * Begins with signature string `WEBVTT`.
  * Supports CSS block styling (`STYLE`), cue identifiers, vertical text (`vertical:rl`), line positioning (`line:75%`), and intra-cue timestamp markers for karaoke word-highlighting.
* **SAMI (Synchronized Accessible Media Interchange):**
  * Microsoft HTML-derived XML dialect. Contains `<STYLE>` definitions with CSS and `<SYNC Start=milliseconds>` tags.
  * Requires robust HTML DOM sanitization and stripping of unknown formatting elements.
* **MicroDVD:**
  * Frame-based syntax: `{1200}{1350}Subtitle line text`.
  * Requires absolute coupling with the video stream's exact frame rate (e.g., 23.976 vs. 25.0 fps) to determine wall-clock presentation timestamps. If frame rate changes dynamically, timestamps drift.

---

### 4.3 Bitmap & Overlay Subtitles

#### 4.3.1 Presentation Graphic Stream (PGS - Blu-ray / `.sup`)
* **Transport:** Segmented binary packets tagged with Presentation Time Stamps:
  * `PCS` (Presentation Composition Segment): Composition state (Normal, Acquisition Point, Epoch Start), screen geometry, video cropping.
  * `WDS` (Window Definition Segment): Allocates target rectangular rendering sub-regions on screen.
  * `PDS` (Palette Definition Segment): Maps 8-bit palette entries (0..255) to $Y, Cb, Cr, \alpha$ values.
  * `ODS` (Object Definition Segment): Carries raw 2-bit or 8-bit bitmap run-length encoded (RLE) graphical payloads.
  * `END` (End of Display Set): Marks conclusion of the composition update.
* **Memory & Rendering Complexity:** The decoder must maintain an **Epoch** buffer state machine. Rendering requires decoding RLE runs into a hardware surface and performing a color lookup table (CLUT) color-space conversion to display RGB before blending over the video plane.

#### 4.3.2 VobSub (DVD Subpictures, `.idx` / `.sub`)
* **Format:**
  * `.sub`: Raw DVD subpicture packets extracted from VOB Private Stream 1 (`0x20`..`0x3F`).
  * `.idx`: Text descriptor file specifying screen size ($720 \times 480$), offset coordinates, timestamps, and the global 16-color YUV palette.
* **Run-Length Syntax:** 2-bit pixels per line representing: Background, Pattern, Emphasis 1, Emphasis 2. Variable-length nibble codes specify run lengths from 1 to 255 pixels per scanline.

---

## 5. Comparative Compatibility Matrix: libmpv vs. DirectShow / MPC-BE

The two predominant open-source multimedia paradigms on Windows represent fundamentally different software engineering architectures:

* **libmpv / FFmpeg Architecture:** Monolithic, cross-platform engine written in C. Highly optimized, integrated demuxing/decoding pipelines (`libavformat`, `libavcodec`), tightly coupled with GPU shader renderers (`libplacebo` / `vo_gpu_next`), and using unified clock scheduling.
* **DirectShow / MPC-BE Architecture:** Component Object Model (COM) filter-graph architecture. Extensible modularity where discrete filters (Source/Splitter $\to$ Transform/Decoder $\to$ Video/Audio Renderer) negotiate pin connections, media types (`AM_MEDIA_TYPE`), and memory allocators (`IMemAllocator`). Commonly configured with **LAV Filters** (libavformat/libavcodec wrapped in DirectShow COM objects) or MPC-BE’s native C++ splitters/decoders, paired with renderers like **madVR** or **MPC Video Renderer (MPCR)**.

```
 DIRECTSHOW FILTER GRAPH TOPOLOGY
 [ Source / Splitter ] ──► (Video Pin: MEDIATYPE_Video) ──► [ Video Decoder ] ──► (Sub-Type: NV12) ──► [ Video Renderer ]
   (LAV Splitter /          (LAV Video /                     (madVR / MPCR /
    MPC Source)              MPC Video Decoder)               EVR-CP)
                       ──► (Audio Pin: MEDIATYPE_Audio) ──► [ Audio Decoder ] ──► (Sub-Type: PCM)  ──► [ Audio Renderer ]
                                                            (LAV Audio)                              (WASAPI / Sanear)

 LIBMPV ARCHITECTURE
 [ File/Stream ] ──► [ libavformat Demuxer ] ──► [ Packet Ring Buffer ]
                                                        │
                      ┌─────────────────────────────────┴─────────────────────────────────┐
                      ▼                                                                   ▼
       [ libavcodec Video Decode ]                                         [ libavcodec Audio Decode ]
         (DXVA2 / D3D11VA / D3D12)                                                   │
                      │                                                              ▼
                      ▼                                                   [ libswresample Engine ]
          [ libplacebo / GPU Shaders ]                                               │
         (vo_gpu_next: Vulkan/D3D11)                                                 ▼
                      │                                                   [ WASAPI Audio Output ]
                      ▼
         [ Direct Composition / Swapchain ]
```

### 5.1 Format-by-Format Compatibility Evaluation

The following technical matrix provides an exhaustive, format-by-format engineering evaluation across demuxing, decoding, hardware offload, and real-world playback stability.

| Media Standard / Format | Identifiers / FourCC | libmpv Engine Implementation | DirectShow / MPC-BE Ecosystem | Engineering Verdict & Technical Divergence |
| :--- | :--- | :--- | :--- | :--- |
| **Matroska (MKV)** | `.mkv`, `V_MPEGH/ISO/HEVC`, `A_OPUS`, etc. | FFmpeg `matroskadec.c`. Native support for all EBML features, multiple tracks, lacing, attachments, and ordered chapters. | **LAV Splitter**: Excellent. **MPC-BE Matroska Splitter**: Excellent. DirectShow pin negotiation for dynamic track switching. | **Parity.** libmpv provides slightly faster segment-linking (ordered chapters) due to monolithic file pointer caching. |
| **MP4 / MOV** | `.mp4`, `.mov`, `ftyp:isom`, `moov`, `mdat` | FFmpeg `mov.c`. Full support for fragmented MP4, negative `ctts` offsets, edit lists (`elst`), and custom metadata. | **LAV Splitter**: Full ISOBMFF support. **MPC-BE MP4 Splitter**: Robust. Edit list processing occasionally causes 1-frame audio blips in legacy DirectShow graphs. | **Parity / libmpv edge.** libmpv handles complex QuickTime edit lists and zero-copy hardware surface binding with lower jitter. |
| **MPEG-TS / M2TS** | `.ts`, `.m2ts`, Sync `0x47`, ATS 192-byte | FFmpeg `mpegts.c`. Dynamic PAT/PMT tracking, PCR jitter tolerance, ATS stripping for BDAV. | **LAV Splitter**: Full support for TS/M2TS. Supports Blu-ray disc navigation via `libbluray`. | **Parity.** Both support raw transport parsing. MPC-BE handles commercial TV-tuner stream format shifts (resolution change mid-stream) slightly faster. |
| **DVD / VOB** | `.vob`, `.ifo`, MPEG-PS `0x000001BA` | FFmpeg `mpegvideodec.c`. Direct ISO file playback via `dvdnav://` or raw VOB concatenation. Menus supported via `libdvdnav`. | Native DirectShow `DVD Navigator` filter. Complete support for DVD menus, subpictures, audio routing, and seamless branching. | **DirectShow edge.** DirectShow's native COM DVD Navigator provides superior menu and interaction compatibility compared to libdvdnav. |
| **Blu-ray BDMV** | `index.bdmv`, `MovieObject.bdmv`, MPLS | Embedded `libbluray` wrapper. Plays main title playlists (`bd://`), handles seamless branching. Java menus (`BD-J`) require external JRE. | **MPC-BE / LAV**: Native playlist parsers (`.mpls`). Fast title selection menu. DirectShow graph builder handles chapter skipping smoothly. | **Parity.** Neither engine executes complex BD-J menus out-of-the-box without an active Java environment. |
| **AVI** | `.avi`, RIFF: `'AVI '`, `OpenDML` | FFmpeg `avidec.c`. Automatic index reconstruction if `idx1` is corrupted. Full OpenDML multi-gigabyte chunk support. | **LAV Splitter / MPC AVI Splitter**: High compatibility. Handles packed bitstreams and non-standard audio interleaving. | **Parity.** Both engines parse even severely malformed AVI files reliably. |
| **WMV / ASF** | `.wmv`, `.asf`, GUID headers | FFmpeg `asfdec.c`. Software decoding for `WMV1`/`WMV2`/`WMV3`/`WVC1`. Hardware decode via D3D11VA. | Native Microsoft DirectShow `WM ASF Reader` or LAV Splitter + DMO (Direct Media Object) decoders. Native D3D11 acceleration. | **Parity.** DirectShow utilizes native OS DMO components; libmpv relies on FFmpeg's reverse-engineered bitstream decoders. |
| **Flash Video (FLV)** | `.flv`, FLV Tags (`0x08`, `0x09`) | FFmpeg `flvdec.c`. Seamless decoding of AVC/HEVC/AAC inside FLV. | **LAV Splitter / MPC-BE FLV Splitter**: Full support. | **Parity.** Both handle standard and extended-timestamp FLVs without issue. |
| **Ogg / OGV / OGA** | `.ogg`, `.ogv`, `'OggS'` | FFmpeg `oggdec.c`. Full logical bitstream chaining, grouped streams, Vorbis/Theora/Opus decoding. | **LAV Splitter**: Good Ogg demuxing. Occasionally struggles with dynamically chained streams (e.g., internet radio stream metadata updates). | **libmpv edge.** Monolithic clock synchronization avoids audio dropouts during chained Ogg transitions. |
| **RealMedia (RM/RMVB)**| `.rm`, `.rmvb`, `'.RMF'` | FFmpeg `rmdec.c`. Supports RV30, RV40, Cook, RA-Sipr with internal slice descrambling. | **LAV Splitter**: Requires LAV Video and LAV Audio. MPC-BE maintains a dedicated RealMedia Splitter filter. | **Parity.** Excellent software-based playback on both modern pipelines. |
| **AV1 Video** | FourCC: `AV01`, OBU stream | FFmpeg `libdav1d` (software) + `av1_d3d11va` / `av1_d3d12va` (hardware decode). Exceptional multithreaded performance. | **LAV Video Decoder**: Wraps `dav1d` for software decode; supports D3D11/DXVA2 hardware offload. | **Parity.** Both leverage the industry-standard `dav1d` decoding library for software fallback. |
| **HEVC / H.265** | FourCC: `HVC1`, `HEVC` | FFmpeg `hevcdec.c` + D3D11VA / DXVA2 / D3D12. Seamless 10-bit, 12-bit, and 4:2:2 decoding. | **LAV Video**: Complete support via D3D11 native or copy-back modes. Excellent integration with madVR. | **Parity.** Both achieve maximum hardware throughput on NVIDIA, AMD, and Intel GPUs. |
| **AVC / H.264** | FourCC: `AVC1`, `H264` | FFmpeg `h264dec.c` + D3D11VA / DXVA2. High 10-bit (Hi10P) software decode; 8-bit hardware decode. | **LAV Video**: DXVA2 native / D3D11 hardware decoding. Hi10P decoded in software via libavcodec. | **Parity.** Universal compatibility across all profiles and levels. |
| **VP9** | FourCC: `VP90`, WebM / IVF | FFmpeg `vp9dec.c` + D3D11VA hardware decode. Profile 0 and Profile 2 (10-bit HDR) fully supported. | **LAV Video**: D3D11VA hardware decode for Profile 0/2. Reliable YouTube 4K/8K stream rendering. | **Parity.** Hardware decoding performance is identical across modern GPUs. |
| **Legacy MPEG-4 ASP** | FourCC: `XVID`, `DIVX`, `DX50` | FFmpeg `mpeg4videodec.c`. Complete support for GMC (including 3-point warps), QPel, and packed bitstreams. | **LAV Video**: Decodes ASP via libavcodec. Legacy XviD VFW codecs often cause registry conflicts if installed. | **libmpv edge.** Completely isolated from toxic system-wide DirectShow/VFW codec pack overwrites. |
| **VC-1 / WMV3** | FourCC: `WVC1`, `WMV3` | FFmpeg `vc1dec.c` + D3D11VA hardware decoding. | **LAV Video / Microsoft DMO Decoder**: Native hardware acceleration via DXVA2/D3D11VA. | **Parity.** Both engines decode progressive and interlaced VC-1 without frame corruption. |
| **Ancient Codecs** | Cinepak, Indeo (`IV32`-`IV50`), Sorenson | FFmpeg internal decoders (`cinepak.c`, `indeo3.c`, `indeo4.c`, `indeo5.c`, `svq1.c`, `svq3.c`). | Fails in native 64-bit DirectShow unless LAV Filters are explicitly installed. Original 32-bit QuickTime/VFW drivers non-functional on Win64. | **libmpv major edge.** Standalone 64-bit execution of ancient 1990s formats without external codec dependencies. |
| **HDR10 & HDR10+** | BT.2020 PQ, ST 2086, ST 2094-40 | Direct rendering via `vo_gpu_next` / `libplacebo`. Dynamic tone-mapping to SDR or native OS HDR passthrough via DXGI swapchains. | Requires **madVR** or **MPC Video Renderer (MPCR)** for dynamic tone mapping and HDR pass-through metadata negotiation via Windows OS. | **libmpv edge.** Integrated, fully open-source colorimetry engine (`libplacebo`) eliminates need for proprietary renderers. |
| **Dolby Vision** | Profile 5, 7, 8 | `libplacebo` extracts RPU metadata. Generates dynamic tone-mapping curves; converts Profile 5 IPT-C2 to RGB. Profile 7 base-layer fallback. | **MPC-BE + MPCR**: Recent support for parsing Dolby Vision RPU metadata and applying dynamic tone mapping. | **libmpv edge.** More mature mathematical implementation of IPT-C2 and ICtCp color transformations. |
| **Dolby TrueHD / Atmos** | Substreams 0-3, JOC | FFmpeg `mlpdec.c`. Decodes full 7.1 TrueHD to PCM; bitstreams raw TrueHD/Atmos over HDMI via WASAPI. | **LAV Audio**: Bitstreams TrueHD/Atmos directly via WASAPI/DirectSound. Software decodes TrueHD to 7.1 PCM. | **Parity.** Identical bit-exact passthrough over HDMI. |
| **DTS-HD Master Audio** | Core + XLL extension | FFmpeg `dca_core.c` + `dca_xll.c`. Decodes lossless 7.1 24/192 extension; bitstreams raw DTS-HD over HDMI. | **LAV Audio**: Bitstreams DTS-HD; decodes lossless XLL extension in software. | **Parity.** Bit-exact passthrough and bit-exact software decoding parity. |
| **Lossless Audio** | FLAC, ALAC, APE, WavPack | Complete native decoding via FFmpeg audio decoders. Gapless audio presentation via sample-accurate timestamps. | **LAV Audio**: Complete decoding via libavcodec. Gapless playback dependent on DirectShow renderer synchronization. | **libmpv edge.** Monolithic audio pipeline guarantees bit-exact gapless track transitions. |
| **Complex Subtitles** | ASS / SSA (v4+) | Native integration with `libass`. Embedded font extraction, DirectWrite system font integration, vector drawing acceleration. | **DirectShow Graph**: Relies on **VSFilter**, **DirectVobSub**, or **XySubFilter**. XySubFilter provides high-quality bitmap generation to madVR. | **libmpv major edge.** Direct libass integration renders subtitles at the native video frame rate without COM overhead or memory sharing lag. |
| **Bitmap Subtitles** | PGS (`.sup`), VobSub (`.sub`/`.idx`) | FFmpeg `pgssubdec.c`, `vobsub.c`. Direct blit onto GPU textures during presentation. | **LAV Splitter + XySubFilter / MPC-BE Subtitle Consumer**: High performance rendering. | **Parity.** Both render high-resolution 1080p/4K PGS graphic overlays smoothly. |

---

## 6. Broken, Corrupt & Problematic Media Handling

Real-world multimedia software frequently encounters corrupted streams: interrupted network downloads, damaged optical media, non-standard camera muxers, and mismatched aspect ratios. The playback engine must implement aggressive heuristics to maintain presentation continuity without crashing.

```
                  RECOVERY HEURISTICS DECISION TREE
                                  
 Truncated File Detected ──► [ Scan Magic / Moov / Cluster ]
                                      │
              ┌───────────────────────┴───────────────────────┐
              ▼                                               ▼
     [ Missing MP4 'moov' ]                          [ Missing AVI 'idx1' ]
              │                                               │
 Scan backward from EOF ◄── Failed            Iterate 'movi' list sequentially
              │                                               │
 Allocate temporary in-memory 'stbl'          Parse '00dc' (Video) & '01wb' (Audio)
              │                                               │
 Rebuild PTS/DTS from raw NAL samples         Construct dynamic in-memory index table
```

### 6.1 Truncated Files & Missing Header Indexes

#### 6.1.1 Truncated MP4 / MOV (Missing `moov` Atom)
* **Failure Mechanism:** In non-fragmented MP4, the index table (`moov`) is traditionally written at the **end** of the recording process. If recording is interrupted (system crash, battery pull, incomplete HTTP download), the file consists solely of an `ftyp` box and a raw `mdat` box. Standard demuxers error out with `moov atom not found`.
* **Engineering Recovery Heuristic:**
  1. Detect EOF condition with no `moov` atom found.
  2. Scan the file sequentially from byte offset 0, identifying top-level box boundaries.
  3. When `mdat` is encountered, bypass the container abstraction and engage an emergency **bitstream inspection scanner**.
  4. Parse raw byte patterns within `mdat` searching for codec sequence start codes:
     * AVC/HEVC: Search for `0x00000001` or length-prefixed NAL unit headers. Extract SPS/PPS/VPS to establish resolution, frame rate, and profile.
     * AAC: Search for ADTS sync words `0xFFF` to identify audio channels and sample rates.
  5. Construct a synthetic, in-memory `moov` atom tree containing an estimated `stbl` sample table derived from linear time increments ($\Delta t = 1/\text{fps}$).

#### 6.1.2 Broken AVI Index (Missing or Corrupt `idx1` Chunk)
* **Failure Mechanism:** Standard AVI players rely entirely on the trailing `idx1` chunk to map frame numbers to file offsets. If truncated, seeking is disabled, or the file fails to open.
* **Recovery Algorithm:**
  1. If `idx1` offset points beyond physical file size or contains invalid chunk IDs, flag index as broken.
  2. Seek to the start of the `movi` LIST chunk.
  3. Execute a fast sequential parse across all sub-chunks:
     * Read 4-byte chunk ID (`00dc`, `01wb`, etc.) and 4-byte size $S$.
     * Record byte offset, chunk type, flags (keyframe heuristic: for AVC/MPEG-4, inspect intra-slice bits; for audio, all frames are keyframes).
     * Advance pointer by $S + (S \pmod 2)$ (AVI 2-byte word padding).
  4. Construct a dynamic, heap-allocated index array matching OpenDML `AVISTDINDEX` syntax.
  5. Restore full random-access seeking capability transparently to the user.

#### 6.1.3 Truncated Matroska (Missing `Cues`)
* **Behavior:** Matroska does not require `Cues` for linear playback, but seeking without cues requires binary bisection search through `Cluster` elements.
* **Heuristic:**
  * If `Cues` element is absent from `SeekHead`, initiate background worker thread to parse `Cluster` elements (`0x1F43B675`) and their nested `Timestamp` (`0xE7`) elements.
  * Incrementally populate an in-memory sparse index table, allowing instantaneous random access seeking once indexing reaches 100%.

---

### 6.2 Timestamp Drift, Desynchronization & Clock Systems

```
                     A/V SYNCHRONIZATION SERVO LOOP
                     
                          Master Clock (Audio PTS)
                                     │
                                     ▼
 Video Frame PTS ──► [ Clock Comparator ] ──► Error Delta (Δe = V_PTS - A_PTS)
                                                      │
         ┌────────────────────────────────────────────┴────────────────────────────────────────────┐
         ▼                                            ▼                                            ▼
   Δe < -20ms (Video Late)                     -20ms <= Δe <= +20ms                        Δe > +20ms (Video Early)
         │                                            │                                            │
 [ Drop Next B/P Frame ]                       [ Present Frame ]                            [ Hold/Duplicate Frame ]
 (or Skip In-Loop Filter)                       (Direct Blit)                                (Delay Presentation)
```

#### 6.2.1 Clock Selection & Master Clock Synchronization
Demuxed audio and video streams possess independent, non-synchronized hardware timebases. Three master clock regimes are possible:
1. **Audio Master (Default & Recommended):** The human auditory system detects a 5 ms audio click or pitch glitch instantly, but the human eye tolerates a dropped or duplicated video frame up to 40 ms. The video renderer acts as a slave to the audio hardware clock.
2. **Video Master:** Used only when audio is absent or uncompressed hardware video displays (e.g., medical monitors) enforce rigid vertical refresh sync.
3. **External Clock:** Master clock derived from system high-resolution timers (`QueryPerformanceCounter`).

#### 6.2.2 MPEG-TS 33-bit PCR / PTS Rollover Handling
* **Problem:** MPEG-TS timestamps run on a 90 kHz clock using 33-bit unsigned integers. The maximum timestamp value is:
  $$2^{33} - 1 = 8,589,934,591$$
  $$\text{Rollover Interval} = \frac{8,589,934,591}{90,000 \text{ Hz}} = 95,443.717 \text{ seconds} \approx 26.51 \text{ hours}$$
* When an MPEG-TS broadcast runs continuously across this boundary, the PTS abruptly wraps from $8,589,934,591$ to $0$. An uncorrected player calculates a massive negative delta ($\Delta t \approx -26.5\text{ hours}$), halting playback or hanging the rendering pipeline.
* **Recovery Logic:**
  ```c
  int64_t unwrap_pts(int64_t current_pts, int64_t last_pts) {
      int64_t delta = current_pts - last_pts;
      // Detect 33-bit wrap-around (~26.5 hours)
      if (delta < -0x100000000LL) {
          // Wrapped forward across boundary
          delta += 0x200000000LL;
      } else if (delta > 0x100000000LL) {
          // Stream stepped backward across boundary
          delta -= 0x200000000LL;
      }
      return last_pts + delta;
  }
  ```

---

### 6.3 Interleaved Audio/Video Skew & Buffer Underruns

* **Symptom:** In poorly multiplexed AVI, MP4, or FLV files, video chunks and audio chunks are not interleaved symmetrically. For example, all audio packets for the first 10 seconds are placed at the beginning of the file, or audio packets lag 5 seconds behind their associated video frames.
* **Failure State:** A demuxer with small packet ring-buffers ($<8\text{ MB}$) will suffer complete buffer starvation: the video queue overflows waiting for audio frames, or the engine halts reading, causing stutter and playback lockup.
* **Mitigation Architecture:**
  * Implement dynamic, elastic packet ring-buffers.
  * Establish maximum queue thresholds based on duration rather than byte sizes (e.g., minimum 15 seconds of queued demuxed data).
  * If a queue threshold is exceeded due to extreme multiplexing skew, decouple file reading from decoder consumption: allow the demuxer to seek forward to fetch starving stream packets while maintaining cached offsets to return to the deferred stream.

---

### 6.4 Non-Standard Aspect Ratios & Geometry Negotiation

Discrepancies between physical raster dimensions and intended display proportions are a pervasive source of geometric distortion (stretched or squished faces) in digital media playback.

#### 6.4.1 Mathematical Foundations: SAR, DAR, and PAR
* **Storage Aspect Ratio (SAR):** The ratio of horizontal to vertical pixel raster count:
  $$\text{SAR} = \frac{\text{Width}_{\text{storage}}}{\text{Height}_{\text{storage}}}$$
* **Pixel Aspect Ratio (PAR):** The physical geometry of an individual pixel (square vs. rectangular):
  $$\text{PAR} = \frac{\text{Pixel Width}}{\text{Pixel Height}}$$
* **Display Aspect Ratio (DAR):** The final physical geometry intended for on-screen viewing:
  $$\text{DAR} = \text{SAR} \times \text{PAR} = \frac{\text{Width}_{\text{storage}} \times \text{PAR}_x}{\text{Height}_{\text{storage}} \times \text{PAR}_y}$$

#### 6.4.2 The Anamorphic DVD Dilemma
A standard NTSC DVD stores video at a rigid resolution of $720 \times 480$ pixels ($\text{SAR} = 720/480 = 1.5 = 3:2$). However, no physical TV operates at a $3:2$ aspect ratio. The video is anamorphic:
* If flagged as Fullscreen ($4:3$ DAR):
  $$\text{PAR} = \frac{\text{DAR}}{\text{SAR}} = \frac{4/3}{720/480} = \frac{4}{3} \times \frac{2}{3} = \frac{8}{9} \approx 0.8889$$
* If flagged as Widescreen ($16:9$ DAR):
  $$\text{PAR} = \frac{\text{DAR}}{\text{SAR}} = \frac{16/9}{720/480} = \frac{16}{9} \times \frac{2}{3} = \frac{32}{27} \approx 1.1852$$

#### 6.4.3 Conflict Resolution Priority Hierarchy
Real-world media often contains conflicting aspect ratio metadata stamped across multiple layer headers. UniversalMediaPlayer implements a deterministic priority hierarchy:

```
  ┌─────────────────────────────────────────────────────────────────────────┐
  │                   ASPECT RATIO RESOLUTION PRECEDENCE                   │
  └─────────────────────────────────────────────────────────────────────────┘
   PRIORITY 1: Explicit User Aspect Ratio Override (Forces 16:9, 4:3, 2.35:1)
                                      │
   PRIORITY 2: Container Sample Aspect Ratio Atom / Element
               - MP4: 'pasp' (Pixel Aspect Ratio Box) inside 'stsd'
               - MKV: 'DisplayWidth' and 'DisplayHeight' in TrackEntry
                                      │
   PRIORITY 3: Elementary Bitstream Sequence Header VUI
               - H.264 / HEVC: VUI (Video Usability Info) 'aspect_ratio_idc'
               - MPEG-2: Sequence Header / Sequence Display Extension
                                      │
   PRIORITY 4: Container Track Header Default Geometry
               - MP4: 'tkhd' width and height (ignoring matrix rotation)
                                      │
   PRIORITY 5: Raw Raster SAR Fallback (Assumes 1:1 Square Pixels)
```

#### 6.4.4 Dynamic Mid-Stream Resolution Switching
* **Broadcast Scenario:** In ATSC/DVB MPEG-TS television broadcasts, programming transitions dynamically between $1920 \times 1080\text{i}$ (16:9 HDTV) and $704 \times 480\text{i}$ (4:3 commercial break) without opening a new stream or file handle.
* **Renderer Failure State:** DirectShow graphs using legacy video renderers (VMR-9, EVR) frequently crash or display frozen frames because DirectShow pin reconnection (`ReceiveConnection`) requires tearing down the D3D device surface allocation.
* **libmpv & Modern Solution:**
  * The video decoder detects the Sequence Parameter Set change mid-stream.
  * It emits a re-initialization event to the presentation swapchain.
  * Modern GPU pipelines (`libplacebo` / `vo_gpu_next`) reallocate the backing Direct3D11/Vulkan texture surfaces asynchronously without tearing down the renderer or pausing the audio master clock, maintaining uninterrupted playback across dynamic broadcast shifts.

---

## 7. UniversalMediaPlayer Architectural Recommendations

Based on the exhaustive technical analyses conducted across container structures, video/audio decoders, subtitle rendering pipelines, and corruption recovery mechanics, the following structural engineering recommendations are established for UniversalMediaPlayer:

1. **Primary Playback Engine Core: `libmpv` (C API)**
   * *Rationale:* The monolithic architecture of `libmpv` eliminates the fragility, COM registration vulnerabilities, and multi-threaded pin-negotiation lockups inherent in the Windows DirectShow ecosystem.
   * *Capabilities:* Bundles native FFmpeg demuxing/decoding for legacy codecs (Cinepak, Indeo, RealVideo) and modern standards (AV1, HEVC, VP9) out-of-the-box in a unified 64-bit binary without requiring system-wide codec installations.
2. **Video Presentation Pipeline: `libplacebo` / `vo_gpu_next` over Direct3D11 / Vulkan**
   * Delivers native dynamic tone-mapping for HDR10, HDR10+, and Dolby Vision (Profiles 5, 7, and 8).
   * Implements automated, high-precision SAR/DAR correction and shader-based film grain synthesis offloading.
3. **Subtitle Subsystem: `libass` with DirectWrite Provider**
   * Bypasses legacy Fontconfig disk cache bottlenecks on Windows while ensuring frame-accurate, bit-exact rendering of complex ASS/SSA karaoke, vector paths, and embedded fonts.
4. **Audio Subsystem: WASAPI Exclusive Mode with Fallback to Shared Spatial Audio**
   * Provides bit-exact hardware passthrough (IEC 61937) for Dolby Atmos / TrueHD and DTS-HD MA to high-end external AVR receivers.
   * Executes ITU-R BS.775 downmixing with clipping prevention when outputting to standard stereo endpoints.
5. **Resilience & Ingestion Layer:**
   * Enforce aggressive pre-parsing scanners for truncated MP4 (`moov` search) and broken AVI files (`idx1` dynamic reconstruction).
   * Utilize an Audio Master clock synchronization loop with adaptive 33-bit MPEG-TS PTS rollover unwrapping.

---

*This document serves as the formal compatibility and demuxing engineering reference for the design, implementation, and quality assurance testing of the UniversalMediaPlayer architecture.*
