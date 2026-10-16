import { useEffect, useRef, useState } from "react";
import { Link, NavLink, Route, Routes } from "react-router-dom";
import { motion } from "motion/react";
import {
  deleteCurrentUser,
  getCurrentUser,
  getSearchQueriesForUser,
  getSearchQueryById,
  subscribeToSearchQuery,
  unsubscribeFromSearchQuery,
  updateCurrentUser,
  type SearchQuery,
  type User,
} from "./api/client";
import { useAuth } from "./auth/useAuth";
import { detectSourceLabel } from "./lib/sourceLabel";
import HowItWorksPage from "./pages/HowItWorksPage";
import FeedbackPage from "./pages/FeedbackPage";
import SuggestPage from "./pages/SuggestPage";
import "./App.css";

const PENDING_URL_KEY = "pendingUrl";
// Enabled-by-default sources only (PEDro is off by default server-side, see SourceCatalog.cs).
const SUPPORTED_SOURCES = [{ label: "PubMed", cls: "badge-pubmed", url: "https://pubmed.ncbi.nlm.nih.gov/" }];

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

function SunIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="5" />
      <line x1="12" y1="1" x2="12" y2="3" />
      <line x1="12" y1="21" x2="12" y2="23" />
      <line x1="4.22" y1="4.22" x2="5.64" y2="5.64" />
      <line x1="18.36" y1="18.36" x2="19.78" y2="19.78" />
      <line x1="1" y1="12" x2="3" y2="12" />
      <line x1="21" y1="12" x2="23" y2="12" />
      <line x1="4.22" y1="19.78" x2="5.64" y2="18.36" />
      <line x1="18.36" y1="5.64" x2="19.78" y2="4.22" />
    </svg>
  );
}

function MoonIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z" />
    </svg>
  );
}

type Theme = "light" | "dark";

function getInitialTheme(): Theme {
  return document.documentElement.dataset.theme === "dark" ? "dark" : "light";
}

function ThemeToggle() {
  const [theme, setTheme] = useState<Theme>(getInitialTheme);

  useEffect(() => {
    document.documentElement.dataset.theme = theme;
    localStorage.setItem("theme", theme);
  }, [theme]);

  const label = theme === "dark" ? "Switch to day mode" : "Switch to night mode";

  return (
    <button
      type="button"
      className="btn btn-icon"
      onClick={() => setTheme((t) => (t === "dark" ? "light" : "dark"))}
      aria-label={label}
      title={label}
    >
      {theme === "dark" ? <SunIcon /> : <MoonIcon />}
    </button>
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

function DetailPanel({
  target,
  detail,
  isLoading,
  error,
}: {
  target: SearchQuery;
  detail: SearchQuery | null;
  isLoading: boolean;
  error: string | null;
}) {
  const lastFetch = detail?.lastFetchedAt
    ? new Date(detail.lastFetchedAt).toLocaleString(undefined, { hour12: false })
    : "Never";
  const lastPollFailedAt = detail?.lastPollFailedAt
    ? new Date(detail.lastPollFailedAt).toLocaleString(undefined, { hour12: false })
    : null;
  const keywords = detail?.tags ?? [];

  return (
    <div className="query-detail">
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
          <dt>Articles found</dt>
          <dd>{isLoading ? <Spinner /> : (detail?.sourceRecordCount ?? "—")}</dd>
        </div>
        <div>
          <dt>Last fetch</dt>
          <dd>{isLoading ? <Spinner /> : lastFetch}</dd>
        </div>
        {!isLoading && lastPollFailedAt && (
          <div>
            <dt>Status</dt>
            <dd className="detail-status-warning">
              Last check failed ({lastPollFailedAt}) - will retry on the next scheduled check
            </dd>
          </div>
        )}
      </dl>

      <a href={target.targetUrl} target="_blank" rel="noreferrer" className="detail-url">
        Click here to follow the URL
      </a>
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
        <p className="modal-subtitle">Manage your account.</p>

        <div className="danger-zone">
          <p className="danger-zone-title">Delete account</p>
          <p className="modal-message">
            You have 14 days to recover your account by signing in again. After that, your account and all subscriptions will be permanently deleted.
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

function formatRelative(iso: string): string {
  const minutes = Math.round((new Date(iso).getTime() - Date.now()) / 60000);
  const rtf = new Intl.RelativeTimeFormat(undefined, { numeric: "auto" });
  if (Math.abs(minutes) < 60) return rtf.format(minutes, "minute");
  if (Math.abs(minutes) < 60 * 24) return rtf.format(Math.round(minutes / 60), "hour");
  return rtf.format(Math.round(minutes / (60 * 24)), "day");
}

function getStatus(sq: SearchQuery, source: string): { kind: "ok" | "setup" | "failed"; text: string } {
  if (sq.lastPollFailedAt) {
    return { kind: "failed", text: `Couldn't reach ${source}, will retry` };
  }
  if (sq.sourceRecordCount === null) {
    return { kind: "setup", text: "Setting up" };
  }
  return { kind: "ok", text: sq.lastFetchedAt ? `Checked ${formatRelative(sq.lastFetchedAt)}` : "Not checked yet" };
}

function getUrlProblem(url: string, existing: SearchQuery[]): string | null {
  if (!url) {
    return null;
  }
  try {
    new URL(url);
  } catch {
    return "Paste the full link, starting with https://";
  }
  if (existing.some((q) => q.targetUrl === url)) {
    return "You're already subscribed to this search.";
  }
  return null;
}

function App() {
  const { isLoading, isAuthenticated, login, logout } = useAuth();
  const [user, setUser] = useState<User | null>(null);
  const [searchQueries, setSearchQueries] = useState<SearchQuery[]>([]);
  const [targetUrl, setTargetUrl] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [unsubscribingId, setUnsubscribingId] = useState<number | null>(null);
  const [showUndoToast, setShowUndoToast] = useState(false);
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
    const params = new URLSearchParams(window.location.search);
    if (params.get("notice") === "account_deleted") {
      setError("This account has been deleted.");
      window.history.replaceState({}, "", window.location.pathname);
    }
  }, []);

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

      // A URL typed on the signed-out home page survives the Keycloak round trip here.
      const pendingUrl = sessionStorage.getItem(PENDING_URL_KEY);
      if (pendingUrl) {
        sessionStorage.removeItem(PENDING_URL_KEY);
        const newQuery = await subscribeToSearchQuery(pendingUrl);
        setSearchQueries(await getSearchQueriesForUser(currentUser.id));
        scheduleRecordCountRefresh(currentUser.id, newQuery.id);
      }
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
            // sourceRecordCount === null check); not lastFetchedAt, which PEDro's baseline
            // poll (no individual records linked yet) can leave null indefinitely even once
            // sourceRecordCount is populated. Also stops once a poll has actually failed
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

  useEffect(() => {
    if (!showUndoToast) {
      return;
    }
    const timer = window.setTimeout(() => setShowUndoToast(false), 8000);
    return () => window.clearTimeout(timer);
  }, [showUndoToast]);

  async function handleUnsubscribe(target: SearchQuery) {
    if (!user) {
      return;
    }

    setUnsubscribingId(target.id);
    setError(null);
    try {
      await unsubscribeFromSearchQuery(target.id);
      setSearchQueries(await getSearchQueriesForUser(user.id));
      if (detailTarget?.id === target.id) {
        setDetailTarget(null);
      }
      setUndoStack((prev) => [...prev, target.targetUrl]);
      setShowUndoToast(true);
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
      setShowUndoToast(false);
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
    if (detailTarget?.id === sq.id) {
      setDetailTarget(null);
      return;
    }
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

  function handleSignedOutSearch(e: React.FormEvent) {
    e.preventDefault();
    if (!targetUrl.trim()) {
      return;
    }
    sessionStorage.setItem(PENDING_URL_KEY, targetUrl.trim());
    void handleLogin();
  }

  const trimmedUrl = targetUrl.trim();
  const urlProblem = getUrlProblem(trimmedUrl, searchQueries);

  const searchBox = (
    <div>
    <form noValidate onSubmit={isAuthenticated ? handleSubscribe : handleSignedOutSearch} className="search-box">
      <input
        id="target-url"
        type="url"
        placeholder="Paste a search URL"
        aria-label="Search URL"
        aria-invalid={urlProblem !== null}
        aria-describedby="target-url-problem"
        value={targetUrl}
        onChange={(e) => setTargetUrl(e.target.value)}
      />
      <button type="submit" className="btn btn-primary" disabled={isSubmitting || !trimmedUrl || urlProblem !== null}>
        {isSubmitting ? <Spinner /> : "Subscribe"}
      </button>
    </form>
    <p id="target-url-problem" className="url-problem" role="status">
      {urlProblem}
    </p>
    </div>
  );

  const supportedDatabases = (
    <p className="supported">
      <span>Supported:</span>
      {SUPPORTED_SOURCES.map((src) => (
        <a
          key={src.label}
          className={`badge ${src.cls}`}
          href={src.url}
          target="_blank"
          rel="noopener noreferrer"
          title={`Open ${src.label} to build a search`}
        >
          {src.label}
          <svg viewBox="0 0 24 24" width="12" height="12" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <path d="M14 3h7v7M21 3l-9 9M19 14v5a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V7a2 2 0 0 1 2-2h5" />
          </svg>
        </a>
      ))}
      <span>
        More coming. <Link to="/suggest">Suggest one</Link>.
      </span>
    </p>
  );

  const signedOutHome = (
    <section className="hero">
      <h2>Track new research. Get a digest.</h2>
      {searchBox}
      <ol className="steps">
        <li>
          <strong>1</strong>Paste a search URL
        </li>
        <li>
          <strong>2</strong>We track the search results
        </li>
        <li>
          <strong>3</strong>New results land in your inbox
        </li>
      </ol>
{supportedDatabases}
    </section>
  );

  return (
  <>
    <header className="header-row">
      <h1>
        <Link to="/">PubTracker</Link>
      </h1>
      <nav className="nav-links">
        <NavLink to="/" end>
          Home
        </NavLink>
        <NavLink to="/how-it-works">How it works</NavLink>
        <NavLink to="/suggest">Suggest a database</NavLink>
        <NavLink to="/feedback">Feedback</NavLink>
      </nav>
      <div className="header-actions">
        <ThemeToggle />
        {user ? (
          <UserMenu
            user={user}
            onOpenProfile={() => setIsProfileOpen(true)}
            onOpenSettings={() => setIsSettingsOpen(true)}
            onSignOut={() => logout()}
          />
        ) : (
          <button className="btn btn-primary btn-sm" onClick={handleLogin}>
            Sign in
          </button>
        )}
      </div>
    </header>
    <motion.main
      className="app"
      initial={{ opacity: 0, y: 12 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.5, ease: "easeOut" }}
    >

      {error && (
        <p className="alert" role="alert">
          {error}
        </p>
      )}

      <Routes>
        <Route path="/how-it-works" element={<HowItWorksPage />} />
        <Route path="/feedback" element={<FeedbackPage isAuthenticated={isAuthenticated} onLogin={handleLogin} />} />
        <Route path="/suggest" element={<SuggestPage isAuthenticated={isAuthenticated} onLogin={handleLogin} />} />
        <Route
          path="*"
          element={
            !isAuthenticated ? (
              signedOutHome
            ) : (
              <>
      <section className="hero-band">
        <h2>Track new research</h2>
        <p>Get a digest when something new is published.</p>
      </section>
      <div className="hero-band-search">{searchBox}</div>
      {supportedDatabases}

      <section>
        <div className="section-header">
          <h2>Your searches</h2>
          {searchQueries.length > 0 && <span className="count-badge">{searchQueries.length}</span>}
          {undoStack.length > 0 && (
            <button className="btn btn-ghost btn-sm section-header-action" onClick={handleRedo} disabled={isRedoing}>
              {isRedoing ? <Spinner /> : "Undo"}
            </button>
          )}
        </div>

        {searchQueries.length === 0 ? (
          <div className="empty-state">
            <p>No searches yet.</p>
            <p className="empty-state-hint">Paste a search URL above to start tracking new articles.</p>
          </div>
        ) : (
          <ul className="search-query-list">
            {searchQueries.map((sq) => {
              const source = detectSourceLabel(sq.targetUrl);
              const tags = sq.tags;
              const status = getStatus(sq, source);
              const isExpanded = detailTarget?.id === sq.id;
              return (
                <li key={sq.id} className={isExpanded ? "expanded" : undefined}>
                  <div className="query-row clickable" onClick={() => handleOpenDetail(sq)}>
                    <span className={`badge badge-${source.toLowerCase()}`}>{source}</span>
                    <div className="query-main">
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
                      <div className="query-meta">
                        <span className={`status-chip status-${status.kind}`}>{status.text}</span>
                        <span>
                          {sq.lastDigestSentAt
                            ? `Last digest ${new Date(sq.lastDigestSentAt).toLocaleDateString(undefined, { day: "numeric", month: "short" })}`
                            : "No digest sent yet"}
                        </span>
                      </div>
                    </div>
                    <div className="record-count" title="Articles found">
                      <span className="record-count-label">Articles</span>
                      <span className="record-count-row">
                        {sq.sourceRecordCount === null && !sq.lastPollFailedAt ? (
                          <Spinner />
                        ) : (
                          <span className="record-count-value">{sq.sourceRecordCount ?? "—"}</span>
                        )}
                      </span>
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
                        void handleUnsubscribe(sq);
                      }}
                      disabled={unsubscribingId === sq.id}
                      aria-label="Unsubscribe"
                      title="Unsubscribe"
                    >
                      {unsubscribingId === sq.id ? <Spinner /> : <TrashIcon />}
                    </button>
                    <button
                      className="btn btn-icon expand-toggle"
                      onClick={(e) => {
                        e.stopPropagation();
                        void handleOpenDetail(sq);
                      }}
                      aria-expanded={isExpanded}
                      aria-label={isExpanded ? "Hide details" : "Show details"}
                      title={isExpanded ? "Hide details" : "Show details"}
                    >
                      <ChevronDownIcon />
                    </button>
                  </div>
                  {isExpanded && (
                    <DetailPanel target={sq} detail={detailData} isLoading={isDetailLoading} error={detailError} />
                  )}
                </li>
              );
            })}
          </ul>
        )}
      </section>
              </>
            )
          }
        />
      </Routes>

      {showUndoToast && (
        <div className="toast" role="status">
          <span>Unsubscribed.</span>
          <button className="btn btn-ghost btn-sm" onClick={handleRedo} disabled={isRedoing}>
            {isRedoing ? <Spinner /> : "Undo"}
          </button>
        </div>
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
          message="Are you sure you want to delete your account?"
          confirmLabel="Delete account"
          isBusy={isDeletingAccount}
          onConfirm={handleDeleteAccount}
          onCancel={() => setIsDeleteAccountConfirmOpen(false)}
        />
      )}
    </motion.main>
    </>
  );
}

export default App;
