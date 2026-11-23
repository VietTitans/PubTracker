import type { ReactNode } from "react";
import { Link, NavLink } from "react-router-dom";
import ThemeToggle from "../components/ThemeToggle";

export default function AppShell({
  controls,
  error,
  children,
}: {
  controls: ReactNode;
  error: string | null;
  children: ReactNode;
}) {
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
          <NavLink to="/how-it-works">How it works</NavLink>
          <NavLink to="/feedback">Feedback</NavLink>
          <NavLink to="/suggest">Suggest a database</NavLink>
        </nav>
        <div className="header-actions">
          <ThemeToggle />
          {controls}
        </div>
      </header>

      {error && (
        <p className="alert" role="alert">
          {error}
        </p>
      )}

      {children}

      <footer className="app-footer">
        <Link to="/privacy">Privacy policy</Link>
        <Link to="/terms">Terms</Link>
      </footer>
    </main>
  );
}
