import { act, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { getSearchQueriesForUser, subscribeToSearchQuery, type SearchQuery } from "../../api/client";
import { useSubscriptions } from "./useSubscriptions";

vi.mock("../../api/client", () => ({
  getSearchQueriesForUser: vi.fn(),
  getSearchQueryById: vi.fn(),
  subscribeToSearchQuery: vi.fn(),
  unsubscribeFromSearchQuery: vi.fn(),
}));

const POLL_INTERVAL_MS = 4000;
const USER_ID = 1;
const NEW_QUERY_ID = 7;

const getQueries = vi.mocked(getSearchQueriesForUser);
const subscribeTo = vi.mocked(subscribeToSearchQuery);

function searchQuery(overrides: Partial<SearchQuery> = {}): SearchQuery {
  return {
    id: NEW_QUERY_ID,
    sourceId: 1,
    targetUrl: "https://pubmed.ncbi.nlm.nih.gov/?term=test",
    subscribers: null,
    lastDigestSentAt: null,
    storedRecordCount: 0,
    lastFetchedAt: null,
    sourceRecordCount: null,
    tags: [],
    lastPollFailedAt: null,
    ...overrides,
  };
}

async function advance(ms: number) {
  await act(async () => {
    await vi.advanceTimersByTimeAsync(ms);
  });
}

// Mounts the hook for a signed-in user with no Subscriptions, then subscribes to a new Search Query.
// Resolves once subscribe() has returned, i.e. with the first poll timer armed.
async function subscribeToNewQuery() {
  const onError = vi.fn();
  const hook = renderHook(() => useSubscriptions(USER_ID, onError));
  await act(async () => {});

  subscribeTo.mockResolvedValueOnce(searchQuery());
  await act(async () => {
    await hook.result.current.subscribe("https://pubmed.ncbi.nlm.nih.gov/?term=test");
  });

  return { hook, onError };
}

describe("useSubscriptions record-count polling", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    sessionStorage.clear();
    getQueries.mockReset();
    subscribeTo.mockReset();
    getQueries.mockResolvedValueOnce([]); // initial load
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("keeps polling while the new query has no record count, then stops once it has one", async () => {
    getQueries.mockResolvedValueOnce([searchQuery()]); // list right after subscribing
    const { hook } = await subscribeToNewQuery();
    getQueries.mockClear();

    getQueries.mockResolvedValueOnce([searchQuery()]); // poll 1: still no count
    await advance(POLL_INTERVAL_MS);
    expect(getQueries).toHaveBeenCalledTimes(1);

    getQueries.mockResolvedValueOnce([searchQuery({ sourceRecordCount: 12 })]); // poll 2: counted
    await advance(POLL_INTERVAL_MS);
    expect(getQueries).toHaveBeenCalledTimes(2);
    expect(hook.result.current.searchQueries[0].sourceRecordCount).toBe(12);

    await advance(POLL_INTERVAL_MS * 5);
    expect(getQueries).toHaveBeenCalledTimes(2);
  });

  it("stops polling once the query's poll has failed, even with no record count", async () => {
    getQueries.mockResolvedValueOnce([searchQuery()]);
    await subscribeToNewQuery();
    getQueries.mockClear();

    getQueries.mockResolvedValueOnce([searchQuery({ lastPollFailedAt: "2026-11-21T10:00:00Z" })]);
    await advance(POLL_INTERVAL_MS);
    expect(getQueries).toHaveBeenCalledTimes(1);

    await advance(POLL_INTERVAL_MS * 5);
    expect(getQueries).toHaveBeenCalledTimes(1);
  });

  it("keeps polling after a transient fetch failure", async () => {
    getQueries.mockResolvedValueOnce([searchQuery()]);
    const { hook, onError } = await subscribeToNewQuery();
    getQueries.mockClear();

    getQueries.mockRejectedValueOnce(new Error("network blip"));
    await advance(POLL_INTERVAL_MS);
    expect(getQueries).toHaveBeenCalledTimes(1);

    getQueries.mockResolvedValueOnce([searchQuery({ sourceRecordCount: 3 })]);
    await advance(POLL_INTERVAL_MS);
    expect(getQueries).toHaveBeenCalledTimes(2);
    expect(hook.result.current.searchQueries[0].sourceRecordCount).toBe(3);
    expect(onError).not.toHaveBeenCalledWith("network blip");
  });

  it("only watches the query that was just subscribed to", async () => {
    getQueries.mockResolvedValueOnce([searchQuery()]);
    await subscribeToNewQuery();
    getQueries.mockClear();

    // Another query still has no count, but the one we subscribed to is done.
    getQueries.mockResolvedValueOnce([
      searchQuery({ id: 99, sourceRecordCount: null }),
      searchQuery({ sourceRecordCount: 4 }),
    ]);
    await advance(POLL_INTERVAL_MS);
    await advance(POLL_INTERVAL_MS * 5);
    expect(getQueries).toHaveBeenCalledTimes(1);
  });

  it("subscribes to a URL remembered before sign-in and polls it like any new query", async () => {
    sessionStorage.setItem("pendingUrl", "https://pubmed.ncbi.nlm.nih.gov/?term=pending");
    subscribeTo.mockResolvedValueOnce(searchQuery());
    getQueries.mockReset();
    getQueries.mockResolvedValueOnce([]); // initial load
    getQueries.mockResolvedValueOnce([searchQuery()]); // after the handoff subscribe

    const hook = renderHook(() => useSubscriptions(USER_ID, vi.fn()));
    await act(async () => {});

    expect(subscribeTo).toHaveBeenCalledWith("https://pubmed.ncbi.nlm.nih.gov/?term=pending");
    expect(sessionStorage.getItem("pendingUrl")).toBeNull();
    expect(hook.result.current.searchQueries).toHaveLength(1);

    getQueries.mockClear();
    getQueries.mockResolvedValueOnce([searchQuery({ sourceRecordCount: 8 })]);
    await advance(POLL_INTERVAL_MS);
    expect(getQueries).toHaveBeenCalledTimes(1);
    expect(hook.result.current.searchQueries[0].sourceRecordCount).toBe(8);
  });
});
