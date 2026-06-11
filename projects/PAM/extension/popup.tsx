/**
 * Popup UI shown when clicking the extension icon.
 * Displays stats and a simple status indicator.
 */

import { useEffect, useState } from "react"

interface Stats {
  total: number
  success: number
  failed: number
  queueSize: number
}

export default function Popup() {
  const [stats, setStats] = useState<Stats>({
    total: 0,
    success: 0,
    failed: 0,
    queueSize: 0
  })

  useEffect(() => {
    const fetchStats = () => {
      chrome.runtime.sendMessage({ type: "GET_STATS" }, (res) => {
        if (res) setStats(res)
      })
    }
    fetchStats()
    const interval = setInterval(fetchStats, 2000)
    return () => clearInterval(interval)
  }, [])

  const resetStats = () => {
    chrome.runtime.sendMessage({ type: "RESET_STATS" }, () => {
      setStats({ total: 0, success: 0, failed: 0, queueSize: 0 })
    })
  }

  return (
    <div
      style={{
        width: 260,
        padding: 16,
        fontFamily: "ui-monospace, SFMono-Regular, monospace",
        background: "#0a0a0a",
        color: "#ededeb"
      }}>
      <div
        style={{
          fontSize: 11,
          letterSpacing: "0.18em",
          textTransform: "uppercase",
          color: "#76766f",
          marginBottom: 8
        }}>
        <span
          style={{
            display: "inline-block",
            width: 8,
            height: 8,
            background: "#a3e635",
            marginRight: 8
          }}
        />
        PAM // active
      </div>
      <div style={{ fontSize: 16, fontWeight: 600, marginBottom: 16 }}>
        Personal AI Memory
      </div>

      <div style={{ display: "grid", gap: 8, fontSize: 13 }}>
        <Row label="Сохранено" value={stats.success} accent="#a3e635" />
        <Row label="В очереди" value={stats.queueSize} />
        <Row label="Ошибок" value={stats.failed} accent={stats.failed > 0 ? "#f4524a" : undefined} />
      </div>

      <button
        onClick={resetStats}
        style={{
          marginTop: 16,
          width: "100%",
          padding: "8px 12px",
          background: "transparent",
          border: "1px solid #262624",
          color: "#a3a39a",
          fontFamily: "inherit",
          fontSize: 11,
          letterSpacing: "0.14em",
          textTransform: "uppercase",
          cursor: "pointer",
          borderRadius: 2
        }}>
        Сбросить счётчики
      </button>

      <div
        style={{
          marginTop: 16,
          paddingTop: 12,
          borderTop: "1px solid #262624",
          fontSize: 11,
          color: "#76766f",
          lineHeight: 1.5
        }}>
        Backend:{" "}
        <a
          href="http://localhost:8000/docs"
          target="_blank"
          rel="noreferrer"
          style={{ color: "#5ee9d0" }}>
          localhost:8000
        </a>
        <br />
        Web UI:{" "}
        <a
          href="http://localhost:3000"
          target="_blank"
          rel="noreferrer"
          style={{ color: "#5ee9d0" }}>
          localhost:3000
        </a>
      </div>
    </div>
  )
}

function Row({
  label,
  value,
  accent
}: {
  label: string
  value: number
  accent?: string
}) {
  return (
    <div style={{ display: "flex", justifyContent: "space-between" }}>
      <span style={{ color: "#a3a39a" }}>{label}</span>
      <span style={{ fontWeight: 600, color: accent || "#ededeb" }}>
        {value}
      </span>
    </div>
  )
}
