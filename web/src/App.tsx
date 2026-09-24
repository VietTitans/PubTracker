import { useEffect, useRef, useState } from "react";
import {
  askChat,
  deleteCurrentUser,
  getCurrentUser,
  getSearchQueriesForUser,
  getSearchQueryById,
  subscribeToSearchQuery,
  unsubscribeFromSearchQuery,
  updateCurrentUser,
  type ChatResponse,
  type SearchQuery,
  type User,
} from "./api/client";
import { useAuth } from "./auth/useAuth";
import { detectSourceLabel } from "./lib/sourceLabel"; 
import "./App.css";

function ExternalLinkIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6" />
      <polyline points="15 3 21 3 21 9" />
      <line x1="10" y1="14" x2="21" y2="3" />
    </svg>
  );
}

function TrashIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <polyline points="3 6 5 6 21 6" />
      <path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6" />
      <path d="M10 11v6" />
      <path d="M14 11v6" />
      <path d="M9 6V4a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2" />
    </svg>
  );
}

function Spinner() {
  return <span className="spinner" aria-hidden="true" />;
}

function ChevronDownIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <polyline points="6 9 12 15 18 9" />
    </svg>
  );
}

function ProfileIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
      <circle cx="12" cy="7" r="4" />
    </svg>
  );
}

function SettingsIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="3" />
      <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z" />
    </svg>
  );
}

function SignOutIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
      <polyline points="16 17 21 12 16 7" />
      <line x1="21" y1="12" x2="9" y2="12" />
    </svg>
  );
}

function ConfirmDialog({
  title,
  message,
  confirmLabel,
  isBusy,
  onConfirm,
  onCancel,
}: {
  title: string;
  message: string;
  confirmLabel: string;
  isBusy: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  useEffect(() => {
    function onKeyDown(e: KeyboardEvent) {
      if (e.key === "Escape") {
        onCancel();
      }
    }
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [onCancel]);

  return (
    <div className="modal-overlay" onClick={onCancel}>
      <div
        className="modal-card"
        role="alertdialog"
        aria-modal="true"
        aria-labelledby="confirm-dialog-title"
        onClick={(e) => e.stopPropagation()}
      >
        <h3 id="confirm-dialog-title">{title}</h3>
        <p className="modal-message">{message}</p>
        <div className="modal-actions">
          <button className="btn btn-ghost" onClick={onCancel} disabled={isBusy}>
            Cancel
          </button>
          <button className="btn btn-danger" onClick={onConfirm} disabled={isBusy} autoFocus>
            {isBusy ? <Spinner /> : confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}

function DetailDialog({
  target,
  detail,
  isLoading,
  error,
  onClose,
  onRequestUnsubscribe,
}: {
  target: SearchQuery;
  detail: SearchQuery | null;
  isLoading: boolean;
  error: string | null;
  onClose: () => void;
  onRequestUnsubscribe: () => void;
}) {
  useEffect(() => {
    function onKeyDown(e: KeyboardEvent) {
      if (e.key === "Escape") {
        onClose();
      }
    }
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [onClose]);

  const source = detectSourceLabel(target.targetUrl);
  const lastFetch = detail?.lastFetchedAt
    ? new Date(detail.lastFetchedAt).toLocaleString(undefined, { hour12: false })
    : "Never";
  const keywords = detail?.tags ?? [];

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div
        className="modal-card"
        role="dialog"
        aria-modal="true"
        aria-labelledby="detail-title"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="detail-header">
          <span className={`badge badge-${source.toLowerCase()}`}>{source}</span>
          <h3 id="detail-title">Search details</h3>
        </div>

        {error && (
          <p className="alert" role="alert">
            {error}
          </p>
        )}

        <dl className="detail-list">
          <div className="detail-tags-row">
            <dt>Keywords</dt>
            <dd>
              {isLoading ? (
                <Spinner />
              ) : keywords.length > 0 ? (
                <div className="tag-group">
                  {keywords.map((keyword) => (
                    <span key={keyword} className="tag">
                      {keyword}
                    </span>
                  ))}
                </div>
              ) : (
                "—"
              )}
            </dd>
          </div>
          <div>
            <dt>Records registered</dt>
            <dd>{isLoading ? <Spinner /> : (detail?.sourceRecordCount ?? "—")}</dd>
          </div>
          <div>
            <dt>Last fetch</dt>
            <dd>{isLoading ? <Spinner /> : lastFetch}</dd>
          </div>
        </dl>

        <a href={target.targetUrl} target="_blank" rel="noreferrer" className="detail-url">
          Click here to follow the URL
        </a>

        <div className="modal-actions">
          <button className="btn btn-danger-outline" onClick={onRequestUnsubscribe}>
            Unsubscribe
          </button>
          <button className="btn btn-ghost" onClick={onClose} autoFocus>
            Close
          </button>
        </div>
      </div>
    </div>
  );
}

function UserMenu({
  user,
  onOpenProfile,
  onOpenSettings,
  onSignOut,
}: {
  user: User;
  onOpenProfile: () => void;
  onOpenSettings: () => void;
  onSignOut: () => void;
}) {
  const [isOpen, setIsOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function onPointerDown(e: MouseEvent) {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setIsOpen(false);
      }
    }
    function onKeyDown(e: KeyboardEvent) {
      if (e.key === "Escape") {
        setIsOpen(false);
      }
    }
    document.addEventListener("mousedown", onPointerDown);
    window.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("mousedown", onPointerDown);
      window.removeEventListener("keydown", onKeyDown);
    };
  }, []);

  const initial = user.name.trim()[0]?.toUpperCase() ?? "?";

  return (
    <div className="user-menu" ref={menuRef}>
      <button
        className="user-menu-trigger"
        onClick={() => setIsOpen((open) => !open)}
        aria-haspopup="menu"
        aria-expanded={isOpen}
      >
        <span className="avatar" aria-hidden="true">
          {initial}
        </span>
        <span className="user-chip-text">
          <span className="user-chip-name">{user.name}</span>
        </span>
        <ChevronDownIcon />
      </button>

      {isOpen && (
        <div className="user-menu-dropdown" role="menu">
          <button
            role="menuitem"
            className="user-menu-item"
            onClick={() => {
              setIsOpen(false);
              onOpenProfile();
            }}
          >
            <ProfileIcon />
            Profile
          </button>
          <button
            role="menuitem"
            className="user-menu-item"
            onClick={() => {
              setIsOpen(false);
              onOpenSettings();
            }}
          >
            <SettingsIcon />
            Settings
          </button>
          <div className="user-menu-divider" />
          <button
            role="menuitem"
            className="user-menu-item"
            onClick={() => {
              setIsOpen(false);
              onSignOut();
            }}
          >
            <SignOutIcon />
            Sign out
          </button>
        </div>
      )}
    </div>
  );
}

function ProfileDialog({
  user,
  onClose,
  onSaved,
}: {
  user: User;
  onClose: () => void;
  onSaved: (user: User) => void;
}) {
  const [name, setName] = useState(user.name);
  const [username, setUsername] = useState(user.username);
  const [email, setEmail] = useState(user.email);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    function onKeyDown(e: KeyboardEvent) {
      if (e.key === "Escape") {
        onClose();
      }
    }
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [onClose]);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setIsSaving(true);
    setError(null);
    try {
      const updated = await updateCurrentUser({ name, username, email });
      onSaved(updated);
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div
        className="modal-card"
        role="dialog"
        aria-modal="true"
        aria-labelledby="profile-title"
        onClick={(e) => e.stopPropagation()}
      >
        <h3 id="profile-title">Profile</h3>

        {error && (
          <p className="alert" role="alert">
            {error}
          </p>
        )}

        <form onSubmit={handleSubmit}>
          <div className="form-field">
            <label className="field-label" htmlFor="profile-name">
              Name
            </label>
            <input
              id="profile-name"
              className="text-input"
              value={name}
              onChange={(e) => setName(e.target.value)}
              required
            />
          </div>

          <div className="form-field">
            <label className="field-label" htmlFor="profile-username">
              Username
            </label>
            <input
              id="profile-username"
              className="text-input"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              required
            />
          </div>

          <div className="form-field">
            <label className="field-label" htmlFor="profile-email">
              Email
            </label>
            <input
              id="profile-email"
              type="email"
              className="text-input"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
            />
          </div>

          <div className="modal-actions">
            <button type="button" className="btn btn-ghost" onClick={onClose} disabled={isSaving}>
              Cancel
            </button>
            <button type="submit" className="btn btn-primary" disabled={isSaving}>
              {isSaving ? <Spinner /> : "Save"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

function SettingsDialog({
  onClose,
  onRequestDeleteAccount,
}: {
  onClose: () => void;
  onRequestDeleteAccount: () => void;
}) {
  useEffect(() => {
    function onKeyDown(e: KeyboardEvent) {
      if (e.key === "Escape") {
        onClose();
      }
    }
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [onClose]);

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div
        className="modal-card"
        role="dialog"
        aria-modal="true"
        aria-labelledby="settings-title"
        onClick={(e) => e.stopPropagation()}
      >
        <h3 id="settings-title">Settings</h3>

        <div className="danger-zone">
          <p className="danger-zone-title">Delete account</p>
          <p className="modal-message">
            Permanently remove your account and all subscriptions. This can't be undone.
          </p>
          <button className="btn btn-danger-outline" onClick={onRequestDeleteAccount}>
            Delete account
          </button>
        </div>

        <div className="modal-actions">
          <button className="btn btn-ghost" onClick={onClose} autoFocus>
            Close
          </button>
        </div>
      </div>
    </div>
  );
}

function ChatBubbleIcon() {
  return (
    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M21 11.5a8.38 8.38 0 0 1-.9 3.8 8.5 8.5 0 0 1-7.6 4.7 8.38 8.38 0 0 1-3.8-.9L3 21l1.9-5.7a8.38 8.38 0 0 1-.9-3.8 8.5 8.5 0 0 1 4.7-7.6 8.38 8.38 0 0 1 3.8-.9h.5a8.48 8.48 0 0 1 8 8v.5z" />
    </svg>
  );
}

function CloseIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <line x1="18" y1="6" x2="6" y2="18" />
      <line x1="6" y1="6" x2="18" y2="18" />
    </svg>
  );
}

interface ChatMessage {
  id: string;
  role: "user" | "assistant";
  text: string;
  citations?: ChatResponse["citations"];
}

// citations holds every record retrieved for the answer (the full top-K), not just the ones
// the model actually referenced - narrow it down to those actually cited inline, in the order
// they're first mentioned, so the numbered list shown to the user only contains records that
// were really used.
function getCitedRecordsInOrder(text: string, citations: ChatResponse["citations"]): ChatResponse["citations"] {
  const byExternalId = new Map(citations.map((c) => [c.externalId, c]));
  const seen = new Set<string>();
  const ordered: ChatResponse["citations"] = [];
  const pattern = /\[([^\]\s]+)\]/g;
  let match: RegExpExecArray | null;

  while ((match = pattern.exec(text)) !== null) {
    const citation = byExternalId.get(match[1]);
    if (citation && !seen.has(citation.externalId)) {
      seen.add(citation.externalId);
      ordered.push(citation);
    }
  }

  return ordered;
}

// The assistant cites records inline as "[externalId]" (e.g. "[pubmed:31241244]") - a stable
// ID, not something meant for display. Swap each one for a linked, numbered reference matching
// its position in citedRecords, and leave any bracket text that isn't a known citation (the
// model citing something outside the list) untouched.
function renderAnswerText(text: string, citedRecords: ChatResponse["citations"]): React.ReactNode[] {
  const indexByExternalId = new Map(citedRecords.map((c, i) => [c.externalId, i + 1]));
  const parts: React.ReactNode[] = [];
  const pattern = /\[([^\]\s]+)\]/g;
  let lastIndex = 0;
  let match: RegExpExecArray | null;
  let key = 0;

  while ((match = pattern.exec(text)) !== null) {
    const refIndex = indexByExternalId.get(match[1]);
    if (refIndex === undefined) {
      continue;
    }

    parts.push(text.slice(lastIndex, match.index));
    const citation = citedRecords[refIndex - 1];
    parts.push(
      citation.sourceUrl ? (
        <a key={key++} href={citation.sourceUrl} target="_blank" rel="noreferrer" title={citation.title}>
          [{refIndex}]
        </a>
      ) : (
        `[${refIndex}]`
      )
    );
    lastIndex = match.index + match[0].length;
  }
  parts.push(text.slice(lastIndex));

  return parts;
}

function FloatingChat() {
  const [isOpen, setIsOpen] = useState(false);
  const [question, setQuestion] = useState("");
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [isAsking, setIsAsking] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const panelRef = useRef<HTMLDivElement>(null);
  const nextId = useRef(0);

  useEffect(() => {
    if (isOpen) {
      messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
    }
  }, [messages, isOpen]);

  useEffect(() => {
    function onKeyDown(e: KeyboardEvent) {
      if (e.key === "Escape") {
        setIsOpen(false);
      }
    }
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, []);

  useEffect(() => {
    if (!isOpen) {
      return;
    }
    function onPointerDown(e: MouseEvent) {
      if (panelRef.current && !panelRef.current.contains(e.target as Node)) {
        setIsOpen(false);
      }
    }
    document.addEventListener("mousedown", onPointerDown);
    return () => document.removeEventListener("mousedown", onPointerDown);
  }, [isOpen]);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const trimmed = question.trim();
    if (!trimmed || isAsking) {
      return;
    }

    // Cap how much history we send so a long-running conversation doesn't grow the request
    // (and the model's context) without bound.
    const history = messages.slice(-20).map((m) => ({ role: m.role, text: m.text }));

    setMessages((prev) => [...prev, { id: `${nextId.current++}`, role: "user", text: trimmed }]);
    setQuestion("");
    setIsAsking(true);
    setError(null);
    try {
      const result = await askChat(trimmed, history);
      setMessages((prev) => [
        ...prev,
        { id: `${nextId.current++}`, role: "assistant", text: result.answer, citations: result.citations },
      ]);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setIsAsking(false);
    }
  }

  if (!isOpen) {
    return (
      <button className="chat-fab" onClick={() => setIsOpen(true)} aria-label="Open chat">
        <ChatBubbleIcon />
      </button>
    );
  }

  return (
    <div className="chat-panel" ref={panelRef} role="dialog" aria-modal="false" aria-labelledby="chat-panel-title">
      <div className="chat-panel-header">
        <span id="chat-panel-title">Ask about your tracked literature</span>
        <button className="btn btn-icon chat-panel-close" onClick={() => setIsOpen(false)} aria-label="Close chat">
          <CloseIcon />
        </button>
      </div>

      <div className="chat-messages">
        {messages.length === 0 && (
          <p className="chat-empty-hint">Ask a question about the papers you're tracking.</p>
        )}
        {messages.map((message) => {
          const citedRecords = message.citations ? getCitedRecordsInOrder(message.text, message.citations) : [];
          return (
            <div key={message.id} className={`chat-bubble chat-bubble-${message.role}`}>
              <p>{message.citations ? renderAnswerText(message.text, citedRecords) : message.text}</p>
              {citedRecords.length > 0 && (
                <ul className="chat-citations">
                  {citedRecords.map((citation) => (
                    <li key={citation.externalId}>
                      {citation.sourceUrl ? (
                        <a href={citation.sourceUrl} target="_blank" rel="noreferrer">
                          {citation.title}
                        </a>
                      ) : (
                        citation.title
                      )}
                    </li>
                  ))}
                </ul>
              )}
            </div>
          );
        })}
        {isAsking && (
          <div className="chat-bubble chat-bubble-assistant chat-bubble-pending">
            <Spinner />
          </div>
        )}
        {error && (
          <p className="alert" role="alert">
            {error}
          </p>
        )}
        <div ref={messagesEndRef} />
      </div>

      <form className="chat-input-row" onSubmit={handleSubmit}>
        <input
          className="text-input"
          value={question}
          onChange={(e) => setQuestion(e.target.value)}
          placeholder="Ask a question..."
          aria-label="Question"
          autoFocus
        />
        <button type="submit" disabled={isAsking || !question.trim()} aria-label="Send">
          {isAsking ? <Spinner /> : "➤"}
        </button>
      </form>
    </div>
  );
}

function App() {
  const { isLoading, isAuthenticated, login, logout } = useAuth();
  const [user, setUser] = useState<User | null>(null);
  const [searchQueries, setSearchQueries] = useState<SearchQuery[]>([]);
  const [targetUrl, setTargetUrl] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [unsubscribingId, setUnsubscribingId] = useState<number | null>(null);
  const [confirmTarget, setConfirmTarget] = useState<SearchQuery | null>(null);
  const [undoStack, setUndoStack] = useState<string[]>([]);
  const [isRedoing, setIsRedoing] = useState(false);
  const [detailTarget, setDetailTarget] = useState<SearchQuery | null>(null);
  const [detailData, setDetailData] = useState<SearchQuery | null>(null);
  const [isDetailLoading, setIsDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState<string | null>(null);
  const [isProfileOpen, setIsProfileOpen] = useState(false);
  const [isSettingsOpen, setIsSettingsOpen] = useState(false);
  const [isDeleteAccountConfirmOpen, setIsDeleteAccountConfirmOpen] = useState(false);
  const [isDeletingAccount, setIsDeletingAccount] = useState(false);

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

  // The backend fetches a brand new search query's records shortly after it's created
  // (a few seconds after this returns) rather than during the subscribe request itself, so
  // the list we get back immediately still shows 0 records. How long that actually takes
  // varies a lot by source (PEDro's Playwright scrape can take much longer than a single
  // fixed delay would predict, especially a cold browser launch on a large result set), so
  // keep polling until it's actually done rather than giving up after a guessed duration -
  // a stopped poll would otherwise leave the spinner stuck until a manual page reload.
  function scheduleRecordCountRefresh(userId: number, searchQueryId: number) {
    const pollIntervalMs = 4000;

    function poll() {
      setTimeout(() => {
        getSearchQueriesForUser(userId)
          .then((queries) => {
            setSearchQueries(queries);
            // Matches the condition the list actually renders a spinner for (App.tsx's
            // sourceRecordCount === null check) - not lastFetchedAt, which PEDro's baseline
            // poll (no individual records linked yet) can leave null indefinitely even once
            // sourceRecordCount is populated.
            const stillFetching = queries.some((q) => q.id === searchQueryId && q.sourceRecordCount === null);
            if (stillFetching) {
              poll();
            }
          })
          .catch(() => {
            // Transient failure (e.g. a network blip) - keep trying rather than leaving the
            // spinner stuck forever.
            poll();
          });
      }, pollIntervalMs);
    }

    poll();
  }

  async function handleSubscribe(e: React.FormEvent) {
    e.preventDefault();
    if (!user || !targetUrl.trim()) {
      return;
    }

    setIsSubmitting(true);
    setError(null);
    try {
      const newQuery = await subscribeToSearchQuery(targetUrl.trim());
      setTargetUrl("");
      setSearchQueries(await getSearchQueriesForUser(user.id));
      scheduleRecordCountRefresh(user.id, newQuery.id);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleConfirmUnsubscribe() {
    if (!user || !confirmTarget) {
      return;
    }

    const target = confirmTarget;
    setUnsubscribingId(target.id);
    setError(null);
    try {
      await unsubscribeFromSearchQuery(target.id);
      setSearchQueries(await getSearchQueriesForUser(user.id));
      setConfirmTarget(null);
      setUndoStack((prev) => [...prev, target.targetUrl]);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setUnsubscribingId(null);
    }
  }

  async function handleDeleteAccount() {
    setIsDeletingAccount(true);
    setError(null);
    try {
      await deleteCurrentUser();
      setIsDeleteAccountConfirmOpen(false);
      setIsSettingsOpen(false);
      logout();
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
      setIsDeletingAccount(false);
    }
  }

  async function handleRedo() {
    if (!user || undoStack.length === 0) {
      return;
    }

    const url = undoStack[undoStack.length - 1];
    setIsRedoing(true);
    setError(null);
    try {
      const newQuery = await subscribeToSearchQuery(url);
      setSearchQueries(await getSearchQueriesForUser(user.id));
      scheduleRecordCountRefresh(user.id, newQuery.id);
      setUndoStack((prev) => prev.slice(0, -1));
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setIsRedoing(false);
    }
  }

  async function handleLogin() {
    setError(null);
    try {
      await login();
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    }
  }

  async function handleOpenDetail(sq: SearchQuery) {
    setDetailTarget(sq);
    setDetailData(null);
    setDetailError(null);
    setIsDetailLoading(true);
    try {
      const fresh = await getSearchQueryById(sq.id);
      setDetailData(fresh);
      setSearchQueries((prev) => prev.map((q) => (q.id === fresh.id ? fresh : q)));
    } catch (err) {
      setDetailError(err instanceof Error ? err.message : String(err));
    } finally {
      setIsDetailLoading(false);
    }
  }

  if (isLoading) {
    return (
      <div className="screen-center">
        <Spinner />
      </div>
    );
  }

  if (!isAuthenticated) {
    return (
      <div className="screen-center">
        <div className="auth-card">
          <h1>PubTracker</h1>
          <p className="tagline">
            Paste a search URL from PEDro or PubMed and get a digest whenever new results appear.
          </p>
          {error && (
            <p className="alert" role="alert">
              {error}
            </p>
          )}
          <button className="btn btn-primary btn-block" onClick={handleLogin}>
            Sign in
          </button>
        </div>
      </div>
    );
  }

  return (
    <main className="app">
      <header className="header-row">
        <h1>PubTracker</h1>
        <div className="header-actions">
          {user && (
            <UserMenu
              user={user}
              onOpenProfile={() => setIsProfileOpen(true)}
              onOpenSettings={() => setIsSettingsOpen(true)}
              onSignOut={() => logout()}
            />
          )}
        </div>
      </header>

      {error && (
        <p className="alert" role="alert">
          {error}
        </p>
      )}

      <section className="card">
        <label className="field-label" htmlFor="target-url">
          Add a search
        </label>
        <form onSubmit={handleSubscribe} className="subscribe-form">
          <input
            id="target-url"
            type="url"
            placeholder="Paste a PEDro or PubMed search URL"
            value={targetUrl}
            onChange={(e) => setTargetUrl(e.target.value)}
            required
          />
          <button type="submit" className="btn btn-primary" disabled={isSubmitting}>
            {isSubmitting ? <Spinner /> : "Subscribe"}
          </button>
        </form>
      </section>

      <section>
        <div className="section-header">
          <h2>Your search queries</h2>
          {searchQueries.length > 0 && <span className="count-badge">{searchQueries.length}</span>}
          {undoStack.length > 0 && (
            <button className="btn btn-ghost btn-sm section-header-action" onClick={handleRedo} disabled={isRedoing}>
              {isRedoing ? <Spinner /> : "Redo"}
            </button>
          )}
        </div>

        {searchQueries.length === 0 ? (
          <div className="empty-state">
            <p>No subscriptions yet.</p>
            <p className="empty-state-hint">Paste a search URL above to start tracking new results.</p>
          </div>
        ) : (
          <ul className="search-query-list">
            {searchQueries.map((sq) => {
              const source = detectSourceLabel(sq.targetUrl);
              const tags = sq.tags;
              return (
                <li key={sq.id} onClick={() => handleOpenDetail(sq)} className="clickable">
                  <span className={`badge badge-${source.toLowerCase()}`}>{source}</span>
                  {tags.length > 0 ? (
                    <div className="tag-group tag-group-row">
                      {tags.map((tag) => (
                        <span key={tag} className="tag">
                          {tag}
                        </span>
                      ))}
                    </div>
                  ) : (
                    <span className="query-url-text" title={sq.targetUrl}>
                      {sq.targetUrl}
                    </span>
                  )}
                  <div className="record-count" title="Records registered">
                    <span className="record-count-label">Records</span>
                    {sq.sourceRecordCount === null ? (
                      <Spinner />
                    ) : (
                      <span className="record-count-value">{sq.sourceRecordCount}</span>
                    )}
                  </div>
                  <a
                    href={sq.targetUrl}
                    target="_blank"
                    rel="noreferrer"
                    className="btn btn-icon"
                    onClick={(e) => e.stopPropagation()}
                    aria-label="Open in new tab"
                    title="Open in new tab"
                  >
                    <ExternalLinkIcon />
                  </a>
                  <button
                    className="btn btn-icon btn-danger-hover"
                    onClick={(e) => {
                      e.stopPropagation();
                      setConfirmTarget(sq);
                    }}
                    disabled={unsubscribingId === sq.id}
                    aria-label="Unsubscribe"
                    title="Unsubscribe"
                  >
                    {unsubscribingId === sq.id ? <Spinner /> : <TrashIcon />}
                  </button>
                </li>
              );
            })}
          </ul>
        )}
      </section>

      {confirmTarget && (
        <ConfirmDialog
          title="Unsubscribe from this search?"
          message={confirmTarget.targetUrl}
          confirmLabel="Unsubscribe"
          isBusy={unsubscribingId === confirmTarget.id}
          onConfirm={handleConfirmUnsubscribe}
          onCancel={() => setConfirmTarget(null)}
        />
      )}

      {detailTarget && (
        <DetailDialog
          target={detailTarget}
          detail={detailData}
          isLoading={isDetailLoading}
          error={detailError}
          onClose={() => setDetailTarget(null)}
          onRequestUnsubscribe={() => {
            setConfirmTarget(detailTarget);
            setDetailTarget(null);
          }}
        />
      )}

      {isProfileOpen && user && (
        <ProfileDialog
          user={user}
          onClose={() => setIsProfileOpen(false)}
          onSaved={(updatedUser) => setUser(updatedUser)}
        />
      )}

      {isSettingsOpen && (
        <SettingsDialog
          onClose={() => setIsSettingsOpen(false)}
          onRequestDeleteAccount={() => setIsDeleteAccountConfirmOpen(true)}
        />
      )}

      {isDeleteAccountConfirmOpen && (
        <ConfirmDialog
          title="Delete your account?"
          message="This will permanently remove your account and all subscriptions. This can't be undone."
          confirmLabel="Delete account"
          isBusy={isDeletingAccount}
          onConfirm={handleDeleteAccount}
          onCancel={() => setIsDeleteAccountConfirmOpen(false)}
        />
      )}

      <FloatingChat />
    </main>
  );
}

export default App;
