import { useEffect, useState } from "react";
import {
  getSearchQueriesForUser,
  getSearchQueryById,
  subscribeToSearchQuery,
  unsubscribeFromSearchQuery,
  type SearchQuery,
} from "../../api/client";
import { errorMessage } from "../../lib/errorMessage";

const PENDING_URL_KEY = "pendingUrl";
const PENDING_URL_TTL_MS = 60 * 60 * 1000;

// A URL typed on the signed-out home page survives the Keycloak round trip here. It lives in
// localStorage, not sessionStorage, so it also survives finishing sign-up in another tab (e.g. an
// email-verification link); the expiry keeps a stale URL from subscribing someone days later.
export function rememberPendingUrl(url: string) {
  localStorage.setItem(PENDING_URL_KEY, JSON.stringify({ url, savedAt: Date.now() }));
}

// Removes the remembered URL from storage; null if there is none or it has expired.
function takePendingUrl(): string | null {
  const raw = localStorage.getItem(PENDING_URL_KEY);
  if (!raw) {
    return null;
  }
  localStorage.removeItem(PENDING_URL_KEY);

  try {
    const { url, savedAt } = JSON.parse(raw) as { url?: unknown; savedAt?: unknown };
    if (typeof url === "string" && typeof savedAt === "number" && Date.now() - savedAt <= PENDING_URL_TTL_MS) {
      return url;
    }
  } catch {
    // Unreadable entry: treat it as absent.
  }
  return null;
}

export interface SubscriptionDetail {
  target: SearchQuery;
  data: SearchQuery | null;
  isLoading: boolean;
  error: string | null;
}

export function useSubscriptions(userId: number | null, onError: (message: string | null) => void) {
  const [searchQueries, setSearchQueries] = useState<SearchQuery[]>([]);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [unsubscribingId, setUnsubscribingId] = useState<number | null>(null);
  const [undoStack, setUndoStack] = useState<string[]>([]);
  const [isRedoing, setIsRedoing] = useState(false);
  const [detail, setDetail] = useState<SubscriptionDetail | null>(null);

  function fail(err: unknown) {
    onError(errorMessage(err));
  }

  useEffect(() => {
    if (userId !== null) {
      void loadSubscriptions(userId);
    }
  }, [userId]);

  async function loadSubscriptions(id: number) {
    try {
      setSearchQueries(await getSearchQueriesForUser(id));

      const pendingUrl = takePendingUrl();
      if (pendingUrl) {
        const newQuery = await subscribeToSearchQuery(pendingUrl);
        setSearchQueries(await getSearchQueriesForUser(id));
        scheduleRecordCountRefresh(id, newQuery.id);
      }
    } catch (err) {
      fail(err);
    }
  }

  // The backend fetches a brand new search query's records shortly after it's created
  // (a few seconds after this returns) rather than during the subscribe request itself, so
  // the list we get back immediately still shows 0 records. How long that actually takes
  // varies a lot by source (PEDro's Playwright scrape can take much longer than a single
  // fixed delay would predict, especially a cold browser launch on a large result set), so
  // keep polling until it's actually done rather than giving up after a guessed duration -
  // a stopped poll would otherwise leave the spinner stuck until a manual page reload.
  function scheduleRecordCountRefresh(id: number, searchQueryId: number) {
    const pollIntervalMs = 4000;

    function poll() {
      setTimeout(() => {
        getSearchQueriesForUser(id)
          .then((queries) => {
            setSearchQueries(queries);
            // Matches the condition the list actually renders a spinner for (the
            // sourceRecordCount === null check in RecordCount); not lastFetchedAt, which PEDro's
            // baseline poll (no individual records linked yet) can leave null indefinitely even
            // once sourceRecordCount is populated. Also stops once a poll has actually failed
            // (lastPollFailedAt set); otherwise a down source (e.g. PubMed) leaves
            // sourceRecordCount null forever and this polls every 4s indefinitely.
            const stillFetching = queries.some(
              (q) => q.id === searchQueryId && q.sourceRecordCount === null && !q.lastPollFailedAt,
            );
            if (stillFetching) {
              poll();
            }
          })
          .catch(() => {
            // Transient failure (e.g. a network blip); keep trying rather than leaving the
            // spinner stuck forever.
            poll();
          });
      }, pollIntervalMs);
    }

    poll();
  }

  // Resolves true when the subscription was created.
  async function subscribe(targetUrl: string): Promise<boolean> {
    if (userId === null) {
      return false;
    }

    setIsSubmitting(true);
    onError(null);
    try {
      const newQuery = await subscribeToSearchQuery(targetUrl);
      setSearchQueries(await getSearchQueriesForUser(userId));
      scheduleRecordCountRefresh(userId, newQuery.id);
      return true;
    } catch (err) {
      fail(err);
      return false;
    } finally {
      setIsSubmitting(false);
    }
  }

  // Resolves true when the subscription was removed.
  async function unsubscribe(target: SearchQuery): Promise<boolean> {
    if (userId === null) {
      return false;
    }

    setUnsubscribingId(target.id);
    onError(null);
    try {
      await unsubscribeFromSearchQuery(target.id);
      setSearchQueries(await getSearchQueriesForUser(userId));
      setUndoStack((prev) => [...prev, target.targetUrl]);
      return true;
    } catch (err) {
      fail(err);
      return false;
    } finally {
      setUnsubscribingId(null);
    }
  }

  async function redoUnsubscribe() {
    if (userId === null || undoStack.length === 0) {
      return;
    }

    const url = undoStack[undoStack.length - 1];
    setIsRedoing(true);
    onError(null);
    try {
      const newQuery = await subscribeToSearchQuery(url);
      setSearchQueries(await getSearchQueriesForUser(userId));
      scheduleRecordCountRefresh(userId, newQuery.id);
      setUndoStack((prev) => prev.slice(0, -1));
    } catch (err) {
      fail(err);
    } finally {
      setIsRedoing(false);
    }
  }

  async function openDetail(target: SearchQuery) {
    setDetail({ target, data: null, isLoading: true, error: null });
    try {
      const fresh = await getSearchQueryById(target.id);
      setDetail((d) => (d ? { ...d, data: fresh } : d));
      setSearchQueries((prev) => prev.map((q) => (q.id === fresh.id ? fresh : q)));
    } catch (err) {
      setDetail((d) => (d ? { ...d, error: errorMessage(err) } : d));
    } finally {
      setDetail((d) => (d ? { ...d, isLoading: false } : d));
    }
  }

  function closeDetail() {
    setDetail(null);
  }

  return {
    searchQueries,
    isSubmitting,
    unsubscribingId,
    isRedoing,
    canRedo: undoStack.length > 0,
    detail,
    subscribe,
    unsubscribe,
    redoUnsubscribe,
    openDetail,
    closeDetail,
  };
}

export type Subscriptions = ReturnType<typeof useSubscriptions>;
