import axios from 'axios';

const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_URL || '',
});

apiClient.interceptors.request.use((config) => {
  const token = localStorage.getItem('token');
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

apiClient.interceptors.response.use(
  (res) => res,
  (err) => {
    if (err.response?.status === 401) {
      localStorage.removeItem('token');
      localStorage.removeItem('user');
      window.location.href = '/login';
    }
    return Promise.reject(err);
  }
);

export default apiClient;

// --- Types ---
export interface Flag {
  id: number;
  key: string;
  name: string;
  description?: string;
  type: 'Boolean' | 'String' | 'Number';
  isArchived: boolean;
  createdAt: string;
  createdByUserId: string;
  rowVersion: string;
}

export interface FlagListResponse {
  items: Flag[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface Environment {
  id: number;
  name: string;
  isProtected: boolean;
}

export interface TargetingRule {
  id: number;
  type: 'Percentage' | 'Attribute';
  percentage?: number;
  attributeKey?: string;
  operator?: 'Equals' | 'NotEquals' | 'InList';
  attributeValue?: string;
  order: number;
  serveValue?: string;
}

export interface FlagEnvironmentValue {
  id: number;
  flagId: number;
  environmentId: number;
  environmentName: string;
  isEnabled: boolean;
  value?: string;
  rowVersion: string;
  rules: TargetingRule[];
}

export interface FlagVersion {
  id: number;
  flagId: number;
  environmentId: number;
  version: number;
  changedByUserId: string;
  changedAt: string;
  reason: string;
  snapshot: string;
  isRollback: boolean;
}

export interface ChangeRequest {
  id: number;
  flagId: number;
  flagKey: string;
  flagName: string;
  environmentId: number;
  environmentName: string;
  requestedByUserId: string;
  reviewedByUserId?: string;
  status: 'Pending' | 'Approved' | 'Rejected';
  proposedChanges: string;
  reason: string;
  createdAt: string;
  reviewedAt?: string;
  reviewNote?: string;
}

export interface ApiKey {
  id: number;
  environmentId: number;
  environmentName: string;
  name: string;
  isActive: boolean;
  createdAt: string;
}

// --- Auth ---
export const login = (email: string, password: string) =>
  apiClient.post<{ token: string; userId: string; email: string; role: string }>('/auth/login', { email, password });

// --- Flags ---
export const getFlags = (page = 1, pageSize = 20, search?: string) =>
  apiClient.get<FlagListResponse>('/api/flags', { params: { page, pageSize, search } });

export const getFlag = (id: number) => apiClient.get<Flag>(`/api/flags/${id}`);

export const createFlag = (data: { key: string; name: string; description?: string; type: string }) =>
  apiClient.post<Flag>('/api/flags', data);

export const updateFlag = (id: number, data: { name: string; description?: string; rowVersion: string }) =>
  apiClient.put<Flag>(`/api/flags/${id}`, data);

export const archiveFlag = (id: number) => apiClient.delete(`/api/flags/${id}`);

// --- Environments ---
export const getEnvironments = () => apiClient.get<Environment[]>('/api/environments');

// --- Flag Environment Values ---
export const getFlagEnvValue = (flagId: number, envId: number) =>
  apiClient.get<FlagEnvironmentValue>(`/api/flags/${flagId}/environments/${envId}`);

export const updateFlagEnvValue = (flagId: number, envId: number, data: {
  isEnabled: boolean; value?: string; rules: Partial<TargetingRule>[]; reason: string; rowVersion: string;
}) => apiClient.put<FlagEnvironmentValue | { changeRequestId: number; message: string }>(
  `/api/flags/${flagId}/environments/${envId}`, data);

export const getHistory = (flagId: number, envId: number) =>
  apiClient.get<FlagVersion[]>(`/api/flags/${flagId}/environments/${envId}/history`);

export const rollback = (flagId: number, envId: number, versionId: number) =>
  apiClient.post<FlagEnvironmentValue>(`/api/flags/${flagId}/environments/${envId}/rollback/${versionId}`);

// --- Change Requests ---
export const getChangeRequests = (status?: string) =>
  apiClient.get<ChangeRequest[]>('/api/change-requests', { params: { status } });

export const approveChangeRequest = (id: number, reviewNote?: string) =>
  apiClient.post(`/api/change-requests/${id}/approve`, { reviewNote });

export const rejectChangeRequest = (id: number, reviewNote?: string) =>
  apiClient.post(`/api/change-requests/${id}/reject`, { reviewNote });

// --- API Keys ---
export const getApiKeys = () => apiClient.get<ApiKey[]>('/api/api-keys');

export const createApiKey = (data: { environmentId: number; name: string }) =>
  apiClient.post<{ id: number; plainKey: string; name: string; environmentId: number }>('/api/api-keys', data);

export const revokeApiKey = (id: number) => apiClient.delete(`/api/api-keys/${id}`);
