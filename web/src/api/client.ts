import { userManager } from "../auth/oidc";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL as string;

export interface User {
  id: number;
  name: string;
  username: string;
  email: string;
  digestsStopped: boolean;
}

export interface SearchQuery {
  id: number;
  sourceId: number;
  targetUrl: string;
  subscribers: string[] | null;
  lastDigestSentAt: string | null;
  storedRecordCount: number;
  lastFetchedAt: string | null;
  sourceRecordCount: number | null;
  tags: string[];
  lastPollFailedAt: string | null;
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const oidcUser = await userManager.getUser();

  const url = `${API_BASE_URL}${path}`;
  let response: Response;
  try {
    response = await fetch(url, {
      ...init,
      headers: {
        "Content-Type": "application/json",
        ...(oidcUser?.access_token ? { Authorization: `Bearer ${oidcUser.access_token}` } : {}),
        ...init?.headers,
      },
    });
  } catch {
    throw new Error(
      `Could not reach the API`
    );
  }

  if (response.status === 401) {
    const reason = await response.json().then((b) => b?.reason as string | undefined).catch(() => undefined);
    if (reason === "account_deleted") {
      // A locked-out account's Keycloak SSO session is still alive; a local-only sign-out
      // would let "Sign in" silently round-trip through Keycloak with no prompt and land
      // right back here. End the SSO session too; the notice query param carries the
      // message across the redirect since in-memory state doesn't survive it.
      await userManager.signoutRedirect({
        post_logout_redirect_uri: `${window.location.origin}?notice=account_deleted`,
      });
      throw new Error("This account has been deleted.");
    }

    await userManager.removeUser();
    throw new Error("Your session has ended. Please sign in again.");
  }

  if (!response.ok) {
    const body = await response.text();
    // The API's errors are { message }; show just the message when there is one.
    const message = (() => {
      try {
        return (JSON.parse(body) as { message?: string }).message;
      } catch {
        return undefined;
      }
    })();
    throw new Error(message ?? `${response.status} ${response.statusText}: ${body}`);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

const API_PREFIX = "/api/v1";

export function getCurrentUser(): Promise<User> {
  return request<User>(`${API_PREFIX}/Users/me`);
}

export function updateCurrentUser(dto: { name: string; username: string; email: string }): Promise<User> {
  return request<User>(`${API_PREFIX}/Users/me`, {
    method: "PUT",
    body: JSON.stringify(dto),
  });
}

export function setDigestsStopped(stopped: boolean): Promise<void> {
  return request<void>(`${API_PREFIX}/Users/me/digests-stopped`, {
    method: "PUT",
    body: JSON.stringify({ stopped }),
  });
}

export function deleteCurrentUser(): Promise<void> {
  return request<void>(`${API_PREFIX}/Users/me`, {
    method: "DELETE",
  });
}

interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

const MAX_PAGE_SIZE = 100;

// The UI shows every subscription at once, so walk all pages rather than only the first.
export async function getSearchQueriesForUser(userId: number): Promise<SearchQuery[]> {
  const queries: SearchQuery[] = [];
  for (let page = 1; ; page++) {
    const result = await request<PagedResponse<SearchQuery>>(
      `${API_PREFIX}/Users/${userId}/search-queries?page=${page}&pageSize=${MAX_PAGE_SIZE}`
    );
    queries.push(...result.items);
    // An empty page also ends the walk, in case rows were removed while paging.
    if (result.items.length === 0 || queries.length >= result.totalCount) {
      return queries;
    }
  }
}

export function getSearchQueryById(searchQueryId: number): Promise<SearchQuery> {
  return request<SearchQuery>(`${API_PREFIX}/SearchQueries/${searchQueryId}`);
}

export function subscribeToSearchQuery(targetUrl: string): Promise<SearchQuery> {
  return request<SearchQuery>(`${API_PREFIX}/SearchQueries`, {
    method: "POST",
    body: JSON.stringify({ targetUrl }),
  });
}

export function unsubscribeFromSearchQuery(searchQueryId: number): Promise<void> {
  return request<void>(`${API_PREFIX}/SearchQueries/${searchQueryId}`, {
    method: "DELETE",
  });
}

export interface DatabaseSuggestion {
  id: number;
  name: string;
  websiteUrl: string;
  voteCount: number;
  hasVoted: boolean;
}

export function submitFeedback(dto: { type: string; message: string }): Promise<void> {
  return request<void>(`${API_PREFIX}/Feedback`, {
    method: "POST",
    body: JSON.stringify(dto),
  });
}

// Ordered by votes, so the first page holds what matters; the UI has no paging controls.
export async function getDatabaseSuggestions(): Promise<DatabaseSuggestion[]> {
  const result = await request<PagedResponse<DatabaseSuggestion>>(
    `${API_PREFIX}/DatabaseSuggestions?page=1&pageSize=${MAX_PAGE_SIZE}`
  );
  return result.items;
}

export function suggestDatabase(dto: { name: string; websiteUrl: string; reason?: string }): Promise<DatabaseSuggestion> {
  return request<DatabaseSuggestion>(`${API_PREFIX}/DatabaseSuggestions`, {
    method: "POST",
    body: JSON.stringify(dto),
  });
}

export function setSuggestionVote(suggestionId: number, voted: boolean): Promise<void> {
  return request<void>(`${API_PREFIX}/DatabaseSuggestions/${suggestionId}/vote`, {
    method: voted ? "PUT" : "DELETE",
  });
}
