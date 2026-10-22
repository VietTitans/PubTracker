import { useEffect, useState } from "react";
import {
  getDatabaseSuggestions,
  setSuggestionVote,
  suggestDatabase,
  type DatabaseSuggestion,
} from "../api/client";
import SignInPrompt from "./SignInPrompt";

export default function SuggestPage({ isAuthenticated, onLogin }: { isAuthenticated: boolean; onLogin: () => void }) {
  const [suggestions, setSuggestions] = useState<DatabaseSuggestion[]>([]);
  const [name, setName] = useState("");
  const [websiteUrl, setWebsiteUrl] = useState("");
  const [reason, setReason] = useState("");
  const [isSending, setIsSending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (isAuthenticated) {
      getDatabaseSuggestions()
        .then(setSuggestions)
        .catch((err) => setError(err instanceof Error ? err.message : String(err)));
    }
  }, [isAuthenticated]);

  if (!isAuthenticated) {
    return <SignInPrompt what="suggest a database" onLogin={onLogin} />;
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setIsSending(true);
    setError(null);
    try {
      await suggestDatabase({ name, websiteUrl, reason: reason || undefined });
      setName("");
      setWebsiteUrl("");
      setReason("");
      // A repeat of an existing name becomes a vote on it, so reload to show the new order.
      setSuggestions(await getDatabaseSuggestions());
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setIsSending(false);
    }
  }

  async function handleVote(s: DatabaseSuggestion) {
    setError(null);
    try {
      await setSuggestionVote(s.id, !s.hasVoted);
      setSuggestions(await getDatabaseSuggestions());
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    }
  }

  return (
    <>
      <section className="card page-card">
        <h2>Suggest a database</h2>
        <p className="page-hint">Which literature database should PubTracker support next?</p>

        {error && (
          <p className="alert" role="alert">
            {error}
          </p>
        )}

        <form onSubmit={handleSubmit}>
          <div className="form-field">
            <label className="field-label" htmlFor="suggest-name">
              Name
            </label>
            <input
              id="suggest-name"
              className="text-input"
              maxLength={200}
              value={name}
              onChange={(e) => setName(e.target.value)}
              required
            />
          </div>
          <div className="form-field">
            <label className="field-label" htmlFor="suggest-url">
              Website
            </label>
            <input
              id="suggest-url"
              type="url"
              className="text-input"
              placeholder="https://"
              maxLength={2048}
              value={websiteUrl}
              onChange={(e) => setWebsiteUrl(e.target.value)}
              required
            />
          </div>
          <div className="form-field">
            <label className="field-label" htmlFor="suggest-reason">
              Why is it useful? (optional)
            </label>
            <textarea
              id="suggest-reason"
              className="text-input"
              rows={3}
              maxLength={2000}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
            />
          </div>
          <button type="submit" className="btn btn-primary" disabled={isSending}>
            Submit
          </button>
        </form>
      </section>

      <section>
        <div className="section-header">
          <h2>Already requested</h2>
          {suggestions.length > 0 && <span className="count-badge">{suggestions.length}</span>}
        </div>
        {suggestions.length === 0 ? (
          <div className="empty-state">
            <p>No suggestions yet.</p>
            <p className="empty-state-hint">Be the first to suggest one above.</p>
          </div>
        ) : (
          <ul className="search-query-list">
            {suggestions.map((s) => (
              <li key={s.id}>
                <a href={s.websiteUrl} target="_blank" rel="noreferrer" className="suggestion-name">
                  {s.name}
                </a>
                <span className="suggestion-votes">{s.voteCount}</span>
                <button
                  className={`btn btn-sm ${s.hasVoted ? "btn-primary" : "btn-ghost"}`}
                  onClick={() => handleVote(s)}
                  aria-pressed={s.hasVoted}
                >
                  +1
                </button>
              </li>
            ))}
          </ul>
        )}
      </section>
    </>
  );
}
