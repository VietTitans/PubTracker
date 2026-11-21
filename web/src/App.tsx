import { useEffect, useRef, useState } from "react";
import { Link, NavLink, Route, Routes } from "react-router-dom";
import {
  deleteCurrentUser,
  getCurrentUser,
  setDigestsStopped,
  updateCurrentUser,
  type SearchQuery,
  type User,
} from "./api/client";
import { useAuth } from "./auth/useAuth";
import Icon from "./components/Icon";
import Modal from "./components/Modal";
import SourceBadge from "./components/SourceBadge";
import Spinner from "./components/Spinner";
import TagList from "./components/TagList";
import SubscriptionRow from "./features/subscriptions/SubscriptionRow";
import { rememberPendingUrl, useSubscriptions } from "./features/subscriptions/useSubscriptions";
import { PrivacyPage, TermsPage } from "./pages/LegalPages";
import FeedbackPage from "./pages/FeedbackPage";
import SuggestPage from "./pages/SuggestPage";
import "./App.css";

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
      {theme === "dark" ? <Icon name="sun" /> : <Icon name="moon" />}
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
  return (
    <Modal titleId="confirm-dialog-title" role="alertdialog" onClose={onCancel}>
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
    </Modal>
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
  const lastFetch = detail?.lastFetchedAt
    ? new Date(detail.lastFetchedAt).toLocaleString(undefined, { hour12: false })
    : "Never";
  const lastPollFailedAt = detail?.lastPollFailedAt
    ? new Date(detail.lastPollFailedAt).toLocaleString(undefined, { hour12: false })
    : null;
  const keywords = detail?.tags ?? [];

  return (
    <Modal titleId="detail-title" onClose={onClose}>
      <div className="detail-header">
        <SourceBadge url={target.targetUrl} />
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
              <TagList tags={keywords} />
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

      <div className="modal-actions">
        <button className="btn btn-danger-outline" onClick={onRequestUnsubscribe}>
          Unsubscribe
        </button>
        <button className="btn btn-ghost" onClick={onClose} autoFocus>
          Close
        </button>
      </div>
    </Modal>
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

  const initial = user.username.trim()[0]?.toUpperCase() ?? "?";

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
          <span className="user-chip-name">{user.username}</span>
        </span>
        <Icon name="chevronDown" size={14} />
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
            <Icon name="profile" />
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
            <Icon name="settings" />
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
            <Icon name="signOut" />
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
  const [username, setUsername] = useState(user.username);
  const [email, setEmail] = useState(user.email);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setIsSaving(true);
    setError(null);
    try {
      const updated = await updateCurrentUser({ username, email });
      onSaved(updated);
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <Modal titleId="profile-title" onClose={onClose}>
      <h3 id="profile-title">Profile</h3>

      {error && (
        <p className="alert" role="alert">
          {error}
        </p>
      )}

      <form onSubmit={handleSubmit}>
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
    </Modal>
  );
}

function SettingsDialog({
  digestsStopped,
  onToggleDigests,
  onClose,
  onRequestDeleteAccount,
}: {
  digestsStopped: boolean;
  onToggleDigests: () => void;
  onClose: () => void;
  onRequestDeleteAccount: () => void;
}) {
  return (
    <Modal titleId="settings-title" onClose={onClose}>
      <h3 id="settings-title">Settings</h3>
      <p className="modal-subtitle">Manage your account.</p>

      <p className="modal-message">
        {digestsStopped
          ? "Digest emails are off. Your searches are kept."
          : "Digest emails are on."}
      </p>
      <button className="btn btn-ghost" onClick={onToggleDigests}>
        {digestsStopped ? "Turn Digests back on" : "Stop all Digests"}
      </button>

      <div className="danger-zone">
        <p className="danger-zone-title">Delete account</p>
        <p className="modal-message">
          You have 30 days to recover your account by signing in again. After that, your account and all subscriptions will be permanently deleted.
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
    </Modal>
  );
}

function App() {
  const { isLoading, isAuthenticated, login, logout } = useAuth();
  const [user, setUser] = useState<User | null>(null);
  const [targetUrl, setTargetUrl] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [confirmTarget, setConfirmTarget] = useState<SearchQuery | null>(null);
  const [isProfileOpen, setIsProfileOpen] = useState(false);
  const [isSettingsOpen, setIsSettingsOpen] = useState(false);
  const [isDeleteAccountConfirmOpen, setIsDeleteAccountConfirmOpen] = useState(false);
  const [isDeletingAccount, setIsDeletingAccount] = useState(false);
  const {
    searchQueries,
    isSubmitting,
    unsubscribingId,
    isRedoing,
    canRedo,
    detail,
    subscribe,
    unsubscribe,
    redoUnsubscribe,
    openDetail,
    closeDetail,
  } = useSubscriptions(user?.id ?? null, setError);

  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    if (params.get("notice") === "account_deleted") {
      setError("This account has been deleted.");
      window.history.replaceState({}, "", window.location.pathname);
    }
  }, []);

  useEffect(() => {
    if (isAuthenticated) {
      void loadCurrentUser();
    }
  }, [isAuthenticated]);

  async function loadCurrentUser() {
    try {
      setUser(await getCurrentUser());
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    }
  }

  async function handleSubscribe(e: React.FormEvent) {
    e.preventDefault();
    if (!user || !targetUrl.trim()) {
      return;
    }

    if (await subscribe(targetUrl.trim())) {
      setTargetUrl("");
    }
  }

  async function handleConfirmUnsubscribe() {
    if (!confirmTarget) {
      return;
    }

    if (await unsubscribe(confirmTarget)) {
      setConfirmTarget(null);
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

  async function handleLogin() {
    setError(null);
    try {
      await login();
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
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
    rememberPendingUrl(targetUrl.trim());
    void handleLogin();
  }

  const searchBox = (
    <form onSubmit={isAuthenticated ? handleSubscribe : handleSignedOutSearch} className="search-box">
      <input
        id="target-url"
        type="url"
        placeholder="Paste a search URL"
        aria-label="Search URL"
        value={targetUrl}
        onChange={(e) => setTargetUrl(e.target.value)}
        required
      />
      <button type="submit" className="btn btn-primary" disabled={isSubmitting}>
        {isSubmitting ? <Spinner /> : "Subscribe"}
      </button>
    </form>
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
          <strong>2</strong>We check it on a schedule
        </li>
        <li>
          <strong>3</strong>New results land in your inbox
        </li>
      </ol>
      <p className="supported">
        <span className="badge badge-pubmed">PubMed</span>
        <span>
          More databases coming. <Link to="/suggest">Suggest one</Link>.
        </span>
      </p>
    </section>
  );

  return (
    <main className="app">
      <header className="header-row">
        <h1>
          <Link to="/">PubTracker</Link>
        </h1>
        <nav className="nav-links">
          <NavLink to="/" end>
            Home
          </NavLink>
          <NavLink to="/feedback">Feedback</NavLink>
          <NavLink to="/suggest">Suggest a database</NavLink>
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

      {error && (
        <p className="alert" role="alert">
          {error}
        </p>
      )}

      <Routes>
        <Route path="/feedback" element={<FeedbackPage isAuthenticated={isAuthenticated} onLogin={handleLogin} />} />
        <Route path="/privacy" element={<PrivacyPage />} />
        <Route path="/terms" element={<TermsPage />} />
        <Route path="/suggest" element={<SuggestPage isAuthenticated={isAuthenticated} onLogin={handleLogin} />} />
        <Route
          path="*"
          element={
            !isAuthenticated ? (
              signedOutHome
            ) : (
              <>
      <section className="hero hero-compact">{searchBox}</section>

      <section>
        <div className="section-header">
          <h2>Your search queries</h2>
          {searchQueries.length > 0 && <span className="count-badge">{searchQueries.length}</span>}
          {canRedo && (
            <button className="btn btn-ghost btn-sm section-header-action" onClick={redoUnsubscribe} disabled={isRedoing}>
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
            {searchQueries.map((sq) => (
              <SubscriptionRow
                key={sq.id}
                searchQuery={sq}
                isUnsubscribing={unsubscribingId === sq.id}
                onOpen={() => openDetail(sq)}
                onUnsubscribe={() => setConfirmTarget(sq)}
              />
            ))}
          </ul>
        )}
      </section>
              </>
            )
          }
        />
      </Routes>

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

      {detail && (
        <DetailDialog
          target={detail.target}
          detail={detail.data}
          isLoading={detail.isLoading}
          error={detail.error}
          onClose={closeDetail}
          onRequestUnsubscribe={() => {
            setConfirmTarget(detail.target);
            closeDetail();
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
          digestsStopped={user?.digestsStopped ?? false}
          onToggleDigests={async () => {
            if (!user) return;
            try {
              const stopped = !user.digestsStopped;
              await setDigestsStopped(stopped);
              setUser((u) => (u ? { ...u, digestsStopped: stopped } : u));
            } catch (err) {
              setError(err instanceof Error ? err.message : String(err));
            }
          }}
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
      <footer className="app-footer">
        <Link to="/privacy">Privacy policy</Link>
        <Link to="/terms">Terms</Link>
      </footer>
    </main>
  );
}

export default App;
