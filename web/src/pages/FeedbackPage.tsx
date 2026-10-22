import { useState } from "react";
import { submitFeedback } from "../api/client";
import SignInPrompt from "./SignInPrompt";

const TYPES = [
  { value: "bug", label: "Bug" },
  { value: "idea", label: "Idea" },
  { value: "praise", label: "Praise" },
];

export default function FeedbackPage({ isAuthenticated, onLogin }: { isAuthenticated: boolean; onLogin: () => void }) {
  const [type, setType] = useState("idea");
  const [message, setMessage] = useState("");
  const [isSending, setIsSending] = useState(false);
  const [isSent, setIsSent] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!isAuthenticated) {
    return <SignInPrompt what="leave feedback" onLogin={onLogin} />;
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setIsSending(true);
    setError(null);
    try {
      await submitFeedback({ type, message });
      setMessage("");
      setIsSent(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setIsSending(false);
    }
  }

  return (
    <section className="card page-card">
      <h2>Feedback</h2>
      <p className="page-hint">Found a bug, have an idea, or something works well? Tell us.</p>

      {error && (
        <p className="alert" role="alert">
          {error}
        </p>
      )}
      {isSent && <p className="notice">Thanks, your feedback was sent.</p>}

      <form onSubmit={handleSubmit}>
        <div className="form-field">
          <label className="field-label" htmlFor="feedback-type">
            Type
          </label>
          <select id="feedback-type" className="text-input" value={type} onChange={(e) => setType(e.target.value)}>
            {TYPES.map((t) => (
              <option key={t.value} value={t.value}>
                {t.label}
              </option>
            ))}
          </select>
        </div>

        <div className="form-field">
          <label className="field-label" htmlFor="feedback-message">
            Message
          </label>
          <textarea
            id="feedback-message"
            className="text-input"
            rows={6}
            maxLength={2000}
            value={message}
            onChange={(e) => {
              setMessage(e.target.value);
              setIsSent(false);
            }}
            required
          />
        </div>

        <button type="submit" className="btn btn-primary" disabled={isSending || !message.trim()}>
          Send
        </button>
      </form>
    </section>
  );
}
