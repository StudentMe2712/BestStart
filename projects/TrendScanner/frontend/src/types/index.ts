/**
 * TypeScript interfaces and type definitions for TrendScanner Global Radar Frontend.
 */

export interface Source {
  id: number;
  name: string;
  url: string;
  source_type: 'rss' | 'reddit' | 'telegram_html' | 'auto_discovered' | string;
  is_active: boolean;
  last_scanned?: string | null;
}

export interface SourceCreate {
  name: string;
  url: string;
  source_type: string;
  is_active?: boolean;
}

export interface SourceUpdate {
  name?: string;
  url?: string;
  source_type?: string;
  is_active?: boolean;
}

export interface Trend {
  id: number;
  source_id: number;
  original_text: string;
  content_hash?: string | null;
  is_trend: boolean;
  trend_name?: string | null;
  ai_score?: number | null;
  scam_probability?: number | null;
  ai_summary?: string | null;
  source_url?: string | null;
  is_reviewed: boolean;
  parsed_date?: string | null;
  source_name?: string | null;
  source_type?: string | null;
  ai_status?: string | null;
  mention_count?: number;
  detailed_report?: string | null;
  is_liked?: boolean;
  user_feedback?: number;
  is_new?: boolean;
}

export interface TrendLikeResponse {
  trend_id: number;
  is_liked: boolean;
  updated: boolean;
}

export interface TrendFeedbackUpdate {
  score: number;
}

export interface TrendFeedbackResponse {
  trend_id: number;
  user_feedback: number;
  is_liked: boolean;
  updated: boolean;
}

export interface TrendReportResponse {
  trend_id: number;
  detailed_report: string;
  trend_name?: string | null;
}

export interface SystemStats {
  total_count: number;
  reviewed_count: number;
  new_count: number;
  confirmed_trends_count: number;
  liked_count?: number;
  disliked_count?: number;
  inbox_count?: number;
  database_count?: number;
  pending_ai_count: number;
  avg_score: number;
  avg_scam_probability: number;
}

export interface SchedulerJobInfo {
  id: string;
  name: string;
  next_run_time?: string | null;
}

export interface SchedulerInfo {
  running: boolean;
  interval_minutes: number;
  next_run_time?: string | null;
  jobs?: SchedulerJobInfo[];
}

export interface LastScanSummary {
  status: string;
  scanned_sources: number;
  new_trends_found: number;
  finished_at?: string;
  errors?: string[];
}

export interface SystemStatus {
  status: string;
  scheduler?: SchedulerInfo;
  active_sources_count: number;
  pending_ai_count: number;
  stats: SystemStats;
  groq_model: string;
  last_scan?: LastScanSummary | null;
  last_scan_time?: string | null;
  next_scan_time?: string | null;
}

export interface FilterState {
  tab: 'inbox' | 'liked' | 'database' | 'all';
  status: 'all' | 'new' | 'reviewed';
  minScore: number | null;
  maxScam: number | null;
  sourceId: number | null;
  onlyTrends: boolean;
  searchQuery: string;
  skip: number;
  limit: number;
}

export interface ManualScanResponse {
  status: string;
  scanned_sources: number;
  new_trends_found: number;
  processed_ai?: number;
  pending_ai_count?: number;
  errors: string[];
}

export interface TrendReviewResponse {
  trend_id: number;
  is_reviewed: boolean;
  updated: boolean;
}

export interface DeleteResponse {
  deleted: boolean;
  trend_id?: number;
  source_id?: number;
}
