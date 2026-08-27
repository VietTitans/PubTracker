import { useEffect, useState } from "react";
import type { User } from "oidc-client-ts";
import { userManager } from "./oidc";

export function useAuth() {
  const [oidcUser, setOidcUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    userManager.getUser().then((u) => {
      setOidcUser(u && !u.expired ? u : null);
      setIsLoading(false);
    });

    const onUserLoaded = (u: User) => setOidcUser(u);
    const onUserUnloaded = () => setOidcUser(null);

    userManager.events.addUserLoaded(onUserLoaded);
    userManager.events.addUserUnloaded(onUserUnloaded);
    return () => {
      userManager.events.removeUserLoaded(onUserLoaded);
      userManager.events.removeUserUnloaded(onUserUnloaded);
    };
  }, []);

  return {
    oidcUser,
    isLoading,
    isAuthenticated: oidcUser !== null,
    login: () => userManager.signinRedirect(),
    logout: () => userManager.signoutRedirect(),
  };
}
