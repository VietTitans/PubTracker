import { useEffect, useState } from "react";
import {
  getCurrentUser,
  getSearchQueriesForUser,
  subscribeToSearchQuery,
  unsubscribeFromSearchQuery,
  type SearchQuery,
  type User,
} from "./api/client";
import { useAuth } from "./auth/useAuth";
import "./App.css";

function App() {
  const { isLoading, isAuthenticated, login, logout } = useAuth();
  const [user, setUser] = useState<User | null>(null);
  const [searchQueries, setSearchQueries] = useState<SearchQuery[]>([]);
  const [targetUrl, setTargetUrl] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    if (isAuthenticated) {
      void loadCurrentUserAndSubscriptions();
    }
  }, [isAuthenticated]);

  async function loadCurrentUserAndSubscriptions() {
    try {
      const currentUser = await getCurrentUser();
      setUser(currentUser);
      setSearchQueries(await getSearchQueriesForUser(currentUser.id));
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    }
  }

  async function handleSubscribe(e: React.FormEvent) {
    e.preventDefault();
    if (!user || !targetUrl.trim()) {
      return;
    }

    setIsSubmitting(true);
    setError(null);
    try {
      await subscribeToSearchQuery(targetUrl.trim());
      setTargetUrl("");
      setSearchQueries(await getSearchQueriesForUser(user.id));
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleUnsubscribe(searchQueryId: number) {
    if (!user) {
      return;
    }

    setError(null);
    try {
      await unsubscribeFromSearchQuery(searchQueryId);
      setSearchQueries(await getSearchQueriesForUser(user.id));
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    }
  }

  if (isLoading) {
    return (
      <main className="app">
        <p>Loading...</p>
      </main>
    );
  }

  if (!isAuthenticated) {
    return (
      <main className="app">
        <h1>PubTracker</h1>
        <button onClick={() => login()}>Sign in</button>
      </main>
    );
  }

  return (
    <main className="app">
      <div className="header-row">
        <h1>PubTracker</h1>
        <button onClick={() => logout()}>Sign out</button>
      </div>

      {user && (
        <p className="user-line">
          Signed in as {user.name} ({user.email})
        </p>
      )}

      {error && <p className="error">{error}</p>}

      <form onSubmit={handleSubscribe} className="subscribe-form">
        <input
          type="url"
          placeholder="Paste a PEDro or PubMed search URL"
          value={targetUrl}
          onChange={(e) => setTargetUrl(e.target.value)}
          required
        />
        <button type="submit" disabled={isSubmitting}>
          {isSubmitting ? "Subscribing..." : "Subscribe"}
        </button>
      </form>

      <h2>Your search queries</h2>
      {searchQueries.length === 0 ? (
        <p>No subscriptions yet.</p>
      ) : (
        <ul className="search-query-list">
          {searchQueries.map((sq) => (
            <li key={sq.id}>
              <a href={sq.targetUrl} target="_blank" rel="noreferrer">
                {sq.targetUrl}
              </a>
              <button onClick={() => handleUnsubscribe(sq.id)}>Unsubscribe</button>
            </li>
          ))}
        </ul>
      )}
    </main>
  );
}

export default App;
