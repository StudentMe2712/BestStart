/**
 * API Client for TrendScanner backend.
 * Provides typed fetch wrappers with error handling.
 */

import {
  DeleteResponse,
  FilterState,
  ManualScanResponse,
  Source,
  SourceCreate,
  SourceUpdate,
  SystemStatus,
  Trend,
  TrendFeedbackResponse,
  TrendReviewResponse,
} from '../types';

const API_BASE = '/api';

class ApiError extends Error {
  public status: number;
  public details?: unknown;

  constructor(message: string, status: number, details?: unknown) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.details = details;
  }
}

async function request<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
  const url = `${API_BASE}${endpoint}`;
  const headers = {
    'Content-Type': 'application/json',
    Accept: 'application/json',
    ...options.headers,
  };

  try {
    const response = await fetch(url, {
      ...options,
      headers,
    });

    if (!response.ok) {
      let errorData: any = null;
      try {
        errorData = await response.json();
      } catch {
        errorData = { detail: response.statusText };
      }
      const message = errorData?.detail || `API request failed with status ${response.status}`;
      throw new ApiError(message, response.status, errorData);
    }

    // Handle 204 No Content
    if (response.status === 204) {
      return {} as T;
    }

    return await response.json();
  } catch (error) {
    if (error instanceof ApiError) {
      throw error;
    }
    throw new ApiError(
      error instanceof Error ? error.message : 'Network error or backend unreachable',
      0
    );
  }
}

export const apiClient = {
  /**
   * Fetch paginated and filtered trends.
   */
  async getTrends(filters: Partial<FilterState> & { search?: string } = {}): Promise<Trend[]> {
    const params = new URLSearchParams();

    if (filters.skip !== undefined) {
      params.append('skip', filters.skip.toString());
    }
    if (filters.limit !== undefined) {
      params.append('limit', filters.limit.toString());
    }
    if (filters.minScore !== null && filters.minScore !== undefined) {
      params.append('min_score', filters.minScore.toString());
    }
    if (filters.maxScam !== null && filters.maxScam !== undefined) {
      params.append('max_scam', filters.maxScam.toString());
    }
    if (filters.status && filters.status !== 'all') {
      params.append('status', filters.status);
    }
    if (filters.sourceId !== null && filters.sourceId !== undefined) {
      params.append('source_id', filters.sourceId.toString());
    }
    if (filters.tab) {
      params.append('tab', filters.tab);
    }
    if (filters.onlyTrends) {
      params.append('only_trends', 'true');
    }
    const searchVal = filters.searchQuery || filters.search;
    if (searchVal && searchVal.trim()) {
      params.append('search', searchVal.trim());
    }

    const query = params.toString() ? `?${params.toString()}` : '';
    return request<Trend[]>(`/trends${query}`);
  },

  /**
   * Fetch a single trend by ID.
   */
  async getTrendById(id: number): Promise<Trend> {
    return request<Trend>(`/trends/${id}`);
  },

  /**
   * Submit RLHF user feedback (1 = Like, -1 = Dislike, 0 = Neutral).
   */
  async setFeedback(id: number, score: number): Promise<TrendFeedbackResponse> {
    return request<TrendFeedbackResponse>(`/trends/${id}/feedback`, {
      method: 'PATCH',
      body: JSON.stringify({ score }),
    });
  },

  /**
   * Toggle or set liked / favorite status for a trend (Inbox Zero).
   */
  async toggleLikeTrend(id: number, isLiked?: boolean): Promise<TrendFeedbackResponse> {
    return this.setFeedback(id, isLiked ? 1 : 0);
  },

  /**
   * Mark a trend as reviewed or unreviewed.
   */
  async reviewTrend(id: number, isReviewed: boolean = true): Promise<TrendReviewResponse> {
    return request<TrendReviewResponse>(`/trends/${id}/review`, {
      method: 'PUT',
      body: JSON.stringify({ is_reviewed: isReviewed }),
    });
  },

  /**
   * Generate or retrieve deep analytical report for a trend.
   */
  async generateTrendReport(id: number): Promise<{ trend_id: number; detailed_report: string; trend_name?: string | null }> {
    return request<{ trend_id: number; detailed_report: string; trend_name?: string | null }>(`/trends/${id}/report`, {
      method: 'POST',
    });
  },

  /**
   * Delete a trend by ID.
   */
  async deleteTrend(id: number): Promise<DeleteResponse> {
    return request<DeleteResponse>(`/trends/${id}`, {
      method: 'DELETE',
    });
  },

  /**
   * List all configured ingestion sources.
   */
  async getSources(activeOnly: boolean = false): Promise<Source[]> {
    const query = activeOnly ? '?active_only=true' : '';
    return request<Source[]>(`/sources${query}`);
  },

  /**
   * Create a new ingestion source.
   */
  async createSource(sourceData: SourceCreate): Promise<Source> {
    return request<Source>('/sources', {
      method: 'POST',
      body: JSON.stringify(sourceData),
    });
  },

  /**
   * Update an existing source configuration.
   */
  async updateSource(id: number, sourceData: SourceUpdate): Promise<Source> {
    return request<Source>(`/sources/${id}`, {
      method: 'PUT',
      body: JSON.stringify(sourceData),
    });
  },

  /**
   * Delete an ingestion source.
   */
  async deleteSource(id: number): Promise<DeleteResponse> {
    return request<DeleteResponse>(`/sources/${id}`, {
      method: 'DELETE',
    });
  },

  /**
   * Trigger an immediate manual scan.
   */
  async triggerManualScan(): Promise<ManualScanResponse> {
    return request<ManualScanResponse>('/scan/manual', {
      method: 'POST',
    });
  },

  /**
   * Run Deep Web Search and AI Competitor Research saved to Obsidian Vault.
   */
  async runDeepResearch(id: number): Promise<{
    status: string;
    trend_id: number;
    file_name: string;
    file_path: string;
    detailed_report?: string;
    message?: string;
  }> {
    return request<{
      status: string;
      trend_id: number;
      file_name: string;
      file_path: string;
      detailed_report?: string;
      message?: string;
    }>(`/trends/${id}/deep-research`, {
      method: 'POST',
    });
  },

  /**
   * Retrieve system operational health, statistics, and scheduler status.
   */
  async getSystemStatus(): Promise<SystemStatus> {
    return request<SystemStatus>('/system/status');
  },

  /**
   * Pause automated background scanner scheduler.
   */
  async pauseScanner(): Promise<{ status: string; is_paused: boolean; message: string }> {
    return request<{ status: string; is_paused: boolean; message: string }>('/system/pause', {
      method: 'POST',
    });
  },

  /**
   * Resume automated background scanner scheduler.
   */
  async resumeScanner(): Promise<{ status: string; is_paused: boolean; message: string }> {
    return request<{ status: string; is_paused: boolean; message: string }>('/system/resume', {
      method: 'POST',
    });
  },
};
