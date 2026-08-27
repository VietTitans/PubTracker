import { userManager } from "../auth/oidc";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL as string;

export interface User {
  id: number;
  name: string;
  username: string;
  email: string;
}

export interface SearchQuery {
  id: number;
  sourceId: number;
  targetUrl: string;
  subscribers: string[] | null;
  lastDigestSentAt: string | null;
  recordCount: number;
  topic: string | null;
  therapy: string | null;
  problem: string | null;
  bodyPart: string | null;
  publicationYear: string | null;
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const oidcUser = await userManager.getUser();

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      ...(oidcUser?.access_token ? { Authorization: `Bearer ${oidcUser.access_token}` } : {}),
      ...init?.headers,
    },
  });

  if (!response.ok) {
    const body = await response.text();
    throw new Error(`${response.status} ${response.statusText}: ${body}`);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export function getCurrentUser(): Promise<User> {
  return request<User>("/api/Users/me");
}

export function updateCurrentUser(dto: { name: string; username: string; email: string }): Promise<User> {
  return request<User>("/api/Users/me", {
    method: "PUT",
    body: JSON.stringify(dto),
  });
}

export function deleteCurrentUser(): Promise<void> {
  return request<void>("/api/Users/me", {
    method: "DELETE",
  });
}

export function getSearchQueriesForUser(userId: number): Promise<SearchQuery[]> {
  return request<SearchQuery[]>(`/api/Users/${userId}/search-queries`);
}

export function getSearchQueryById(searchQueryId: number): Promise<SearchQuery> {
  return request<SearchQuery>(`/api/SearchQueries/${searchQueryId}`);
}

export function subscribeToSearchQuery(targetUrl: string): Promise<SearchQuery> {
  return request<SearchQuery>("/api/SearchQueries", {
    method: "POST",
    body: JSON.stringify({ targetUrl }),
  });
}

export function unsubscribeFromSearchQuery(searchQueryId: number): Promise<void> {
  return request<void>(`/api/SearchQueries/${searchQueryId}`, {
    method: "DELETE",
  });
}
