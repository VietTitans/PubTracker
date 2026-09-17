import { useEffect, useState } from "react";
import { userManager } from "./oidc";

export default function AuthCallback() {
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    userManager
      .signinRedirectCallback()
      .then(() => {
        window.location.replace("/");
      })
      .catch((err) => {
        setError(err instanceof Error ? err.message : String(err));
      });
  }, []);

  if (error) {
    return <p style={{ padding: 20 }}>Sign-in failed: {error}</p>;
  }

  return <p style={{ padding: 20 }}>Signing you in...</p>;
}
