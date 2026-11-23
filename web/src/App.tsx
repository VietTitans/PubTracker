import { useEffect, useState } from "react";
import { Route, Routes } from "react-router-dom";
import { useAuth } from "./auth/useAuth";
import Spinner from "./components/Spinner";
import AccountControls from "./features/account/AccountControls";
import { useCurrentUser } from "./features/account/useCurrentUser";
import { useSubscriptions } from "./features/subscriptions/useSubscriptions";
import AppShell from "./layout/AppShell";
import { errorMessage } from "./lib/errorMessage";
import FeedbackPage from "./pages/FeedbackPage";
import HomePage from "./pages/HomePage";
import HowItWorksPage from "./pages/HowItWorksPage";
import { PrivacyPage, TermsPage } from "./pages/LegalPages";
import "./App.css";

function App() {
  const { isLoading, isAuthenticated, login, logout } = useAuth();
  const [error, setError] = useState<string | null>(null);
  const { user, setUser } = useCurrentUser(isAuthenticated, setError);
  const subscriptions = useSubscriptions(user?.id ?? null, setError);

  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    if (params.get("notice") === "account_deleted") {
      setError("This account has been deleted.");
      window.history.replaceState({}, "", window.location.pathname);
    }
  }, []);

  async function handleLogin() {
    setError(null);
    try {
      await login();
    } catch (err) {
      setError(errorMessage(err));
    }
  }

  if (isLoading) {
    return (
      <div className="screen-center">
        <Spinner />
      </div>
    );
  }

  return (
    <AppShell
      error={error}
      controls={
        <AccountControls
          user={user}
          onUserChange={setUser}
          onLogin={handleLogin}
          onLogout={logout}
          onError={setError}
        />
      }
    >
      <Routes>
        <Route path="/feedback" element={<FeedbackPage isAuthenticated={isAuthenticated} onLogin={handleLogin} />} />
        <Route path="/how-it-works" element={<HowItWorksPage />} />
        <Route path="/privacy" element={<PrivacyPage />} />
        <Route path="/terms" element={<TermsPage />} />
        <Route
          path="*"
          element={<HomePage isAuthenticated={isAuthenticated} subscriptions={subscriptions} onLogin={handleLogin} />}
        />
      </Routes>
    </AppShell>
  );
}

export default App;
