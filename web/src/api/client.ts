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
  lastFetchedAt: string | null;
  sourceRecordCount: number | null;
  tags: string[];
  lastPollFailedAt: string | null;
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
    throw new Error(`${response.status} ${response.statusText}: ${body}`);
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

export function deleteCurrentUser(): Promise<void> {
  return request<void>(`${API_PREFIX}/Users/me`, {
    method: "DELETE",
  });
}

export function getSearchQueriesForUser(userId: number): Promise<SearchQuery[]> {
  return request<SearchQuery[]>(`${API_PREFIX}/Users/${userId}/search-queries`);
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
