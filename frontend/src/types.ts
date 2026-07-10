export interface ApiResponse<T> {
  status: string;
  data: T;
  message?: string;
}

export interface DashboardOverview {
  totalEvents: number;
  eventsByType: Record<string, number>;
  recentSuccessRate: number;
}

export interface LogEventCommand {
  eventType: string;
  payload: string;
  source: string;
  timestamp?: string;
}

export interface EventAcceptedResponse {
  message: string;
}

export interface EventFeedItem {
  eventType: string;
  source: string;
  timestamp: string;
  payload?: string;
}
